#!/usr/bin/env bash
# Activates a Chatex release on the production server.
#
# Install once as root (see Docs/Deployment.md):
#   install -o root -g root -m 755 deploy/activate-release.sh /usr/local/bin/chatex-activate
# The deploy user may run only this command through sudo. It must stay root-owned and outside
# anything the deploy user can write, otherwise sudo on it would grant root.
#
# Usage: chatex-activate <git sha>
#   - If /opt/chatex/incoming/<sha> exists (uploaded by the deploy workflow), it becomes
#     /opt/chatex/releases/<sha>.
#   - Otherwise /opt/chatex/releases/<sha> must already exist: this is how you roll back.
# Then it runs the database migrations, switches /opt/chatex/current, restarts the API and checks
# /health. If the API does not come up, it switches back to the previous release.
set -euo pipefail

readonly ROOT=/opt/chatex
readonly INCOMING="$ROOT/incoming"
readonly RELEASES="$ROOT/releases"
readonly CURRENT="$ROOT/current"
readonly ENV_FILE=/etc/chatex/chatex.env
readonly SERVICE=chatex-api
readonly HEALTH_URL=http://127.0.0.1:5000/health
readonly KEEP_RELEASES=5

log() { printf '[chatex-activate] %s\n' "$*"; }
fail() { log "error: $*" >&2; exit 1; }

[[ $EUID -eq 0 ]] || fail "must run as root"
[[ $# -eq 1 ]] || fail "usage: chatex-activate <git sha>"
readonly SHA="$1"
[[ $SHA =~ ^[0-9a-f]{7,40}$ ]] || fail "not a git sha: $SHA"

readonly SRC="$INCOMING/$SHA"
readonly DST="$RELEASES/$SHA"
PREVIOUS="$(readlink -f "$CURRENT" 2>/dev/null || true)"
readonly PREVIOUS

# 1. Move the upload into releases/ and make it read-only for the service user.
if [[ -d $SRC ]]; then
  if [[ $PREVIOUS == "$DST" ]]; then
    # A re-run of the same commit: keep the running files, just restart below.
    rm -rf "$SRC"
    log "$SHA is already installed and running"
  else
    rm -rf "$DST"
    mv "$SRC" "$DST"
    log "installed $DST"
  fi
elif [[ -d $DST ]]; then
  log "no upload for $SHA, re-activating the existing release"
else
  fail "neither $SRC nor $DST exists"
fi
chown -R root:chatex "$DST"
chmod -R u=rwX,go=rX "$DST"
chmod 755 "$DST/efbundle"
[[ -f $DST/api/Chatex.Api.dll ]] || fail "$DST/api/Chatex.Api.dll is missing"
[[ -f $DST/web/index.html ]] || fail "$DST/web/index.html is missing"

# 2. Migrate. Migrations run while the old release still serves traffic, so they must stay
#    compatible with it. Rolling back does not undo migrations.
# The env file uses systemd syntax, so read the value instead of sourcing it (';' would split).
CONNECTION="$(grep -m1 '^ConnectionStrings__Chatex=' "$ENV_FILE" | cut -d= -f2- || true)"
CONNECTION="${CONNECTION#\"}"
CONNECTION="${CONNECTION%\"}"
[[ -n $CONNECTION ]] || fail "ConnectionStrings__Chatex is not set in $ENV_FILE"
log "applying migrations"
"$DST/efbundle" --connection "$CONNECTION"

# 3. Switch the symlink atomically and restart.
switch_to() {
  ln -sfn "$1" "$CURRENT.tmp"
  mv -T "$CURRENT.tmp" "$CURRENT"
  systemctl restart "$SERVICE"
}

healthy() {
  local _
  for _ in $(seq 1 30); do
    if curl --fail --silent --max-time 2 "$HEALTH_URL" > /dev/null; then
      return 0
    fi
    sleep 1
  done
  return 1
}

log "switching to $SHA"
switch_to "$DST"
if ! healthy; then
  if [[ -n $PREVIOUS && -d $PREVIOUS && $PREVIOUS != "$DST" ]]; then
    log "health check failed, rolling back to $(basename "$PREVIOUS")"
    switch_to "$PREVIOUS"
  fi
  journalctl -u "$SERVICE" -n 50 --no-pager >&2 || true
  fail "$SHA did not pass the health check"
fi
log "$SHA is live"

# 4. Clean up: leftover uploads and all but the newest releases (never the running one).
find "$INCOMING" -mindepth 1 -maxdepth 1 -type d -mmin +60 -exec rm -rf {} +
# Release folder names are hex SHAs, so parsing ls output is safe here.
# shellcheck disable=SC2012
ls -1dt "$RELEASES"/*/ | tail -n +$((KEEP_RELEASES + 1)) | while read -r old; do
  old="${old%/}"
  if [[ $old != "$DST" ]]; then
    rm -rf "$old"
    log "removed old release $(basename "$old")"
  fi
done
