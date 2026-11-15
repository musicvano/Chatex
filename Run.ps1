#!/usr/bin/env pwsh
#Requires -Version 7.0

# Chatex developer commands. Run without arguments (or with 'help') to list them.
# Extra arguments after 'test' and 'migration' are passed on, see the examples in the help.

param(
    [ValidateSet('help', 'setup', 'db', 'db-stop', 'migration', 'api', 'web', 'build', 'test', 'format', 'check', 'clean')]
    [string] $Command = 'help'
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$Web = Join-Path $Root 'Source/Chatex.Web'
$Solution = 'Chatex.slnx'
$DataProject = 'Source/Chatex.Data'
$ApiProject = 'Source/Chatex.Api'

$Commands = [ordered]@{
    'help'      = 'Show this list.'
    'setup'     = 'First run: restore tools and packages, start the database and apply the migrations.'
    'db'        = 'Start PostgreSQL in Docker (compose.yaml) and apply the migrations.'
    'db-stop'   = 'Stop the PostgreSQL container; the data stays in its volume.'
    'migration' = 'Add an EF Core migration: migration <Name>.'
    'api'       = 'Run the API on http://localhost:5214 (API reference at /scalar).'
    'web'       = 'Run the Angular dev server on http://localhost:4200 (start the API first).'
    'build'     = 'Build the solution and the web app.'
    'test'      = 'Run the backend and frontend tests; with arguments, run dotnet test with them instead.'
    'format'    = 'Format C# (dotnet format) and the web app (Prettier).'
    'check'     = 'Run everything CI runs: format checks, Release build, migration check, tests, audit, lint, build.'
    'clean'     = 'Delete build output (bin, obj, dist and the Angular cache).'
}

function Show-Help {
    Write-Host 'Chatex developer commands' -ForegroundColor Cyan
    Write-Host ''
    Write-Host '  ./Run.ps1 <command> [arguments]'
    Write-Host ''
    foreach ($entry in $Commands.GetEnumerator()) {
        Write-Host ('  {0,-10} ' -f $entry.Key) -ForegroundColor Green -NoNewline
        Write-Host $entry.Value
    }
    Write-Host ''
    Write-Host 'Examples:' -ForegroundColor Cyan
    Write-Host '  ./Run.ps1 setup'
    Write-Host '  ./Run.ps1 migration AddMessageReactions'
    Write-Host '  ./Run.ps1 test --project Tests/Chatex.Core.Tests --filter-class "*ChatServiceTests"'
    Write-Host '  ./Run.ps1 check'
    Write-Host ''
    Write-Host 'Needs the .NET 10 SDK, Node.js 24 and Docker. See Docs/GettingStarted.md.'
}

# Runs a program in a folder (the repository root by default) and stops the script if it fails.
function Invoke-Step {
    param(
        [Parameter(Mandatory)] [string] $FilePath,
        [string[]] $ArgumentList = @(),
        [string] $WorkingDirectory = $Root
    )

    Write-Host "> $FilePath $($ArgumentList -join ' ')" -ForegroundColor DarkGray
    Push-Location $WorkingDirectory
    try {
        & $FilePath @ArgumentList
        if ($LASTEXITCODE -ne 0) {
            throw "'$FilePath $($ArgumentList -join ' ')' failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

function Start-Database {
    Invoke-Step docker 'compose', 'up', '--detach', '--wait'
    Invoke-Step dotnet 'ef', 'database', 'update', '--project', $DataProject
}

switch ($Command) {
    'help' {
        Show-Help
    }
    'setup' {
        Invoke-Step dotnet 'tool', 'restore'
        Invoke-Step dotnet 'restore', $Solution
        Invoke-Step npm 'ci' -WorkingDirectory $Web
        Start-Database
        Write-Host 'Ready: ./Run.ps1 api, then ./Run.ps1 web in a second terminal.' -ForegroundColor Cyan
    }
    'db' {
        Start-Database
    }
    'db-stop' {
        Invoke-Step docker 'compose', 'stop'
    }
    'migration' {
        if ($args.Count -ne 1) {
            throw 'Usage: ./Run.ps1 migration <Name>'
        }
        Invoke-Step dotnet 'ef', 'migrations', 'add', $args[0], '--project', $DataProject, '--output-dir', 'Migrations'
    }
    'api' {
        Invoke-Step dotnet 'run', '--project', $ApiProject, '--launch-profile', 'http'
    }
    'web' {
        Invoke-Step npm 'start' -WorkingDirectory $Web
    }
    'build' {
        Invoke-Step dotnet 'build', $Solution
        Invoke-Step npm 'run', 'build' -WorkingDirectory $Web
    }
    'test' {
        if ($args.Count -gt 0) {
            Invoke-Step dotnet (@('test') + $args)
        }
        else {
            Invoke-Step dotnet 'test', '--solution', $Solution
            Invoke-Step npm 'test', '--', '--watch=false' -WorkingDirectory $Web
        }
    }
    'format' {
        Invoke-Step dotnet 'format', $Solution
        Invoke-Step npm 'run', 'format' -WorkingDirectory $Web
    }
    'check' {
        # The same steps as .github/workflows/ci.yml.
        Invoke-Step dotnet 'restore', $Solution
        Invoke-Step dotnet 'tool', 'restore'
        Invoke-Step dotnet 'format', $Solution, '--verify-no-changes', '--no-restore'
        Invoke-Step dotnet 'build', $Solution, '-c', 'Release', '--no-restore'
        Invoke-Step dotnet 'ef', 'migrations', 'has-pending-model-changes', '--project', $DataProject, '--configuration', 'Release', '--no-build'
        Invoke-Step dotnet 'test', '--solution', $Solution, '-c', 'Release', '--no-build'
        Invoke-Step npm 'ci' -WorkingDirectory $Web
        Invoke-Step npm 'audit', '--omit=dev', '--audit-level=high' -WorkingDirectory $Web
        Invoke-Step npm 'run', 'format:check' -WorkingDirectory $Web
        Invoke-Step npm 'run', 'lint' -WorkingDirectory $Web
        Invoke-Step npm 'test', '--', '--watch=false' -WorkingDirectory $Web
        Invoke-Step npm 'run', 'build' -WorkingDirectory $Web
        Write-Host 'All checks passed.' -ForegroundColor Green
    }
    'clean' {
        $folders = @(
            Get-ChildItem -Path (Join-Path $Root 'Source'), (Join-Path $Root 'Tests') -Directory |
                ForEach-Object { Join-Path $_.FullName 'bin'; Join-Path $_.FullName 'obj' }
            Join-Path $Web 'dist'
            Join-Path $Web '.angular/cache'
        ) | Where-Object { Test-Path $_ }
        foreach ($folder in $folders) {
            Write-Host "Deleting $folder" -ForegroundColor DarkGray
            Remove-Item -Recurse -Force $folder
        }
    }
}
