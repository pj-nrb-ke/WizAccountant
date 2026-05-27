#!/usr/bin/env bash
# Deploy WizAccountant.Api on VPS (AscendBooks). Run on server as root.
set -eu

APP_ROOT=/opt/wizaccountant
DOCKER_DIR=$APP_ROOT/infra/docker
BRANCH=main
APP_URL=https://app.ascendbooks.biz

echo "==> WizAccountant deploy ($BRANCH)"

if [ ! -d "$APP_ROOT/.git" ]; then
  echo "Clone repo first: git clone <repo-url> $APP_ROOT"
  exit 1
fi

cd "$APP_ROOT"
git fetch origin
git stash push -m "deploy-stash-$(date +%s)" 2>/dev/null || true
git checkout "$BRANCH"
git pull origin "$BRANCH"

echo "==> Docker build"
cd "$DOCKER_DIR"
COMPOSE="docker compose -p wizaccountant -f docker-compose.prod.yml"
$COMPOSE up -d --build

echo "==> Caddy"
SNIP=$DOCKER_DIR/caddy-wizaccountant.snippet
if [ -f "$SNIP" ] && ! grep -q 'app.ascendbooks.biz' /etc/caddy/Caddyfile 2>/dev/null; then
  echo "" >> /etc/caddy/Caddyfile
  cat "$SNIP" >> /etc/caddy/Caddyfile
fi
if command -v caddy >/dev/null 2>&1; then
  caddy validate --config /etc/caddy/Caddyfile
  systemctl reload caddy
fi

echo "==> Smoke"
sleep 3
curl -sf "http://127.0.0.1:8088/health" | head -c 200
echo ""
curl -sf "$APP_URL/health" | head -c 120 || true
echo ""

echo "Done. App: $APP_URL"
