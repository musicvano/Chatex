# Deployment

[← Docs index](Index.md)

Chatex runs on one Ubuntu 26.04 droplet on DigitalOcean. Everything runs on that one machine:

- **Nginx** serves the Angular app and proxies `/api`, `/hubs` and `/health` to the API.
- **The API** runs as the systemd service `chatex-api` on `127.0.0.1:5000`, under the `chatex` user.
- **PostgreSQL** holds the database.

GitHub Actions builds, tests and deploys. This is a learning setup: you do everything as `root` over SSH.

You need:

- **A domain name.** The refresh cookie is `Secure`, so the app needs HTTPS. Point a DNS A record at the droplet.
- **A droplet with 1–2 GB RAM.** The server only runs the app; builds happen elsewhere.
- **Only one copy of the API.** Online status is kept in memory.

The server files are in [`deploy/`](../deploy): the systemd unit, the Nginx site, the settings template and the activation script.

## Set up the server

Create the droplet with your SSH key. Then log in as `root` and get a copy of this repository:

```bash
git clone <your repository url> chatex && cd chatex
```

1. Install the packages and open the firewall for SSH, HTTP and HTTPS:

```bash
apt update && apt install -y aspnetcore-runtime-10.0 postgresql nginx certbot python3-certbot-nginx ufw rsync curl
```

```bash
ufw allow OpenSSH && ufw allow 'Nginx Full' && ufw enable
```

2. Create the `chatex` user, which the API runs as, and the app folders:

```bash
useradd --system --home-dir /var/lib/chatex --shell /usr/sbin/nologin chatex
```

```bash
mkdir -p /opt/chatex/incoming /opt/chatex/releases /var/lib/chatex/uploads /etc/chatex && chown -R chatex:chatex /var/lib/chatex
```

3. Create the database. Choose a password:

```bash
sudo -u postgres psql -c "CREATE ROLE chatex LOGIN PASSWORD 'CHANGE_ME'" -c "CREATE DATABASE chatex OWNER chatex"
```

4. Copy the settings template. Then set the database password, your domain as the JWT issuer and audience, and a signing key. You can generate a key with `openssl rand -base64 48`.

```bash
cp deploy/chatex.env.example /etc/chatex/chatex.env && chmod 600 /etc/chatex/chatex.env && nano /etc/chatex/chatex.env
```

5. Install the activation script and the service:

```bash
cp deploy/activate-release.sh /usr/local/bin/chatex-activate && chmod 755 /usr/local/bin/chatex-activate
```

```bash
cp deploy/systemd/chatex-api.service /etc/systemd/system/ && systemctl daemon-reload && systemctl enable chatex-api
```

6. Set up Nginx and HTTPS. First replace `chat.example.com` in `deploy/nginx/chatex.conf` with your domain.

```bash
cp deploy/nginx/chatex.conf /etc/nginx/sites-available/chatex && ln -s /etc/nginx/sites-available/chatex /etc/nginx/sites-enabled/ && rm -f /etc/nginx/sites-enabled/default
```

```bash
nginx -t && systemctl reload nginx && certbot --nginx -d chat.example.com
```

The service starts on the first deploy.

## How a deploy works

A release is a folder with four things:

- `api/` — the published API
- `efbundle` — an executable that applies the database migrations
- `web/` — the Angular build
- `REVISION` — the commit hash

The release is uploaded to `/opt/chatex/incoming/<commit>`. Then [`chatex-activate <commit>`](../deploy/activate-release.sh) does the rest:

1. moves the release to `/opt/chatex/releases/<commit>`
2. applies the migrations
3. points the `/opt/chatex/current` symlink at the new release
4. restarts the API and checks `/health`
5. switches back to the previous release if the API doesn't come up
6. deletes all but the newest five releases

## Deploy with GitHub Actions

[`ci.yml`](../.github/workflows/ci.yml) builds and tests every push to `dev` and every pull request. [`deploy.yml`](../.github/workflows/deploy.yml) runs on pushes to `main`: it runs CI, builds a release, uploads it over SSH and runs `chatex-activate`.

1. Create a key for GitHub on your PC:

```bash
ssh-keygen -t ed25519 -N "" -f chatex-deploy
```

2. On the server, allow that key to log in. Paste the contents of `chatex-deploy.pub` into this command:

```bash
echo '<contents of chatex-deploy.pub>' >> /root/.ssh/authorized_keys
```

3. On GitHub, open **Settings → Environments**, create an environment named `production` and add these values to it:

| Name | Kind | Value |
|---|---|---|
| `SSH_HOST` | secret | the droplet's IP address |
| `SSH_USER` | secret | `root` |
| `SSH_PRIVATE_KEY` | secret | the contents of `chatex-deploy` (the private key) |
| `SSH_KNOWN_HOSTS` | secret | the output of `ssh-keyscan -t ed25519 <droplet ip>` |
| `PUBLIC_URL` | variable | `https://chat.example.com` |

Push to `main`, or open **Actions → Deploy → Run workflow**.

## Deploy by hand

You can also deploy without GitHub Actions. Build the release on your PC, from the repository root. The `release/` folder is git-ignored; delete it first if it already exists.

```bash
dotnet publish Chatex.Api -c Release -r linux-x64 --self-contained false -o release/api
```

```bash
dotnet tool restore && dotnet ef migrations bundle --project Chatex.DataAccess -c Release -r linux-x64 -o release/efbundle --force
```

```bash
npm --prefix Chatex.Web ci && npm --prefix Chatex.Web run build && cp -r Chatex.Web/dist/chatex/browser release/web
```

Then upload and activate it. The release name must be a commit hash, so use the output of `git rev-parse --short HEAD`:

```bash
scp -r release root@chat.example.com:/opt/chatex/incoming/<commit>
```

```bash
ssh root@chat.example.com chatex-activate <commit>
```

## Useful commands

Follow the API logs:

```bash
journalctl -u chatex-api -f
```

Show the active release:

```bash
readlink /opt/chatex/current
```

Roll back to an older release (list them with `ls -t /opt/chatex/releases`). Rolling back doesn't undo database migrations.

```bash
chatex-activate <commit>
```

Check disk usage:

```bash
df -h / && du -sh /var/lib/postgresql /var/lib/chatex/uploads /opt/chatex/releases
```

Back up the database to a file:

```bash
sudo -u postgres pg_dump -Fc chatex > chatex.dump
```
