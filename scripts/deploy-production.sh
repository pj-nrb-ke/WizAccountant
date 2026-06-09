#!/usr/bin/env bash
# deploy-production.sh  (G1)
# Run on the VPS to pull latest image, apply DB migrations, restart API.
# Usage: ./scripts/deploy-production.sh [optional-image-tag]
set -euo pipefail

COMPOSE_FILE="$(dirname "$0")/../infra/docker/docker-compose.prod.yml"
IMAGE_TAG="${1:-latest}"
HEALTH_URL="http://127.0.0.1:8088/health"

echo "=== WizAccountant Production Deploy ==="
echo "Image tag : $IMAGE_TAG"
echo "Compose   : $COMPOSE_FILE"

# 1. Pull latest image
docker compose -f "$COMPOSE_FILE" pull

# 2. Stop old container gracefully (30s drain)
docker compose -f "$COMPOSE_FILE" stop --timeout 30 api || true

# 3. Start new container (EF MigrateAsync runs at startup — no separate step needed)
IMAGE_TAG="$IMAGE_TAG" docker compose -f "$COMPOSE_FILE" up -d --force-recreate api

# 4. Wait for health
echo "Waiting for health check…"
for i in $(seq 1 24); do
    if curl -sf "$HEALTH_URL" > /dev/null 2>&1; then
        echo "✅ API healthy at $HEALTH_URL"
        break
    fi
    echo "  attempt $i/24 — waiting 5s…"
    sleep 5
    if [[ $i -eq 24 ]]; then
        echo "❌ Health check timed out after 120s"
        docker compose -f "$COMPOSE_FILE" logs --tail=50 api
        exit 1
    fi
done

# 5. Print version
curl -s "$HEALTH_URL" | python3 -m json.tool 2>/dev/null || true

echo ""
echo "=== Deploy complete ✅ ==="
