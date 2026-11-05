using Chatex.Business.Common;
using Chatex.Business.Users;
using Chatex.DataAccess.Abstractions;
using Chatex.DataAccess.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Chatex.Business.Auth;

public interface IAuthService
{
    /// <summary>Creates the account and its profile, and signs the new user in.</summary>
    Task<Result<AuthResult>> RegisterAsync(RegisterCommand command, ClientInfo client, CancellationToken cancellationToken = default);

    Task<Result<AuthResult>> LoginAsync(LoginCommand command, ClientInfo client, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a refresh token for a new token pair. Each refresh token works once; presenting a used one
    /// signs the user out everywhere, because it means the token was stolen.
    /// </summary>
    Task<Result<AuthResult>> RefreshAsync(RefreshCommand command, ClientInfo client, CancellationToken cancellationToken = default);

    /// <summary>Ends one session. Succeeds even if the token is unknown or already revoked.</summary>
    Task<Result> LogoutAsync(RefreshCommand command, CancellationToken cancellationToken = default);

    /// <summary>Ends every session of the user.</summary>
    Task<Result> LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default);
}

internal sealed class AuthService(
    UserManager<User> userManager,
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    ITokenService tokens,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider,
    IValidator<RegisterCommand> registerValidator,
    IValidator<LoginCommand> loginValidator) : IAuthService
{
    private const int MaxUserAgentLength = 512;
    private const int MaxIpLength = 45;

    public async Task<Result<AuthResult>> RegisterAsync(
        RegisterCommand command, ClientInfo client, CancellationToken cancellationToken = default)
    {
        if (await registerValidator.ValidateToErrorAsync(command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var user = new User { UserName = command.UserName.ToLowerInvariant(), Email = command.Email.Trim() };

        return await unitOfWork.ExecuteInTransactionAsync<Result<AuthResult>>(async ct =>
        {
            var created = await userManager.CreateAsync(user, command.Password);
            if (!created.Succeeded)
            {
                return ToError(created.Errors);
            }

            users.AddProfile(new UserProfile { UserId = user.Id, DisplayName = command.DisplayName.Trim() });
            return await IssueTokensAsync(user, client, ct);
        }, cancellationToken);
    }

    public async Task<Result<AuthResult>> LoginAsync(
        LoginCommand command, ClientInfo client, CancellationToken cancellationToken = default)
    {
        if (await loginValidator.ValidateToErrorAsync(command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var login = command.Login.Trim();
        var user = login.Contains('@')
            ? await userManager.FindByEmailAsync(login)
            : await userManager.FindByNameAsync(login);
        if (user is null)
        {
            return AuthErrors.InvalidCredentials;
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return AuthErrors.LockedOut;
        }

        if (!await userManager.CheckPasswordAsync(user, command.Password))
        {
            await userManager.AccessFailedAsync(user);
            return await userManager.IsLockedOutAsync(user) ? AuthErrors.LockedOut : AuthErrors.InvalidCredentials;
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await IssueTokensAsync(user, client, cancellationToken);
    }

    public async Task<Result<AuthResult>> RefreshAsync(
        RefreshCommand command, ClientInfo client, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return AuthErrors.InvalidRefreshToken;
        }

        var now = timeProvider.GetUtcNow();
        var current = await refreshTokens.GetByHashAsync(tokens.HashRefreshToken(command.RefreshToken), cancellationToken);
        if (current is null || current.ExpiresAt <= now)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        if (current.RevokedAt is not null)
        {
            // A used token came back: someone else holds a copy. End every session of this user.
            await refreshTokens.RevokeAllForUserAsync(current.UserId, now, cancellationToken);
            return AuthErrors.InvalidRefreshToken;
        }

        current.RevokedAt = now;
        return await IssueTokensAsync(current.User, client, cancellationToken, replaces: current);
    }

    public async Task<Result> LogoutAsync(RefreshCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Result.Success;
        }

        var token = await refreshTokens.GetByHashAsync(tokens.HashRefreshToken(command.RefreshToken), cancellationToken);
        if (token is { RevokedAt: null })
        {
            token.RevokedAt = timeProvider.GetUtcNow();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success;
    }

    public async Task<Result> LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await refreshTokens.RevokeAllForUserAsync(userId, timeProvider.GetUtcNow(), cancellationToken);
        return Result.Success;
    }

    /// <summary>Stores a new refresh token (linking it from <paramref name="replaces"/>), saves, and returns both tokens.</summary>
    private async Task<AuthResult> IssueTokensAsync(
        User user, ClientInfo client, CancellationToken cancellationToken, RefreshToken? replaces = null)
    {
        var now = timeProvider.GetUtcNow();
        var access = tokens.CreateAccessToken(user);
        var refresh = tokens.CreateRefreshToken();
        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refresh.Hash,
            ExpiresAt = now + jwtOptions.Value.RefreshTokenLifetime,
            UserAgent = Truncate(client.UserAgent, MaxUserAgentLength),
            CreatedByIp = Truncate(client.IpAddress, MaxIpLength),
        };
        refreshTokens.Add(entity);
        if (replaces is not null)
        {
            replaces.ReplacedByTokenId = entity.Id;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Loads the profile into the tracked user for the response.
        var withProfile = await users.GetWithProfileAsync(user.Id, cancellationToken) ?? user;
        return new AuthResult(access.Token, access.ExpiresAt, refresh.Token, entity.ExpiresAt, withProfile.ToMyProfileDto());
    }

    private static Error ToError(IEnumerable<IdentityError> errors)
    {
        var list = errors.ToList();
        if (list.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return AuthErrors.UserNameTaken;
        }

        if (list.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            return AuthErrors.EmailTaken;
        }

        return IdentityErrors.ToValidationError(list);
    }

    private static string? Truncate(string? value, int maxLength) =>
        value?.Length > maxLength ? value[..maxLength] : value;
}
