#!/bin/bash
set -e

# Create Docker Swarm secrets for AutoTest platform
# Run this ONCE on a fresh server before first deployment
#
# Usage:
#   1. Copy secrets.json.example → secrets.json
#   2. Fill in real values
#   3. Run this script
#
# Or create secrets individually for infrastructure services

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
INFRA_DIR="$(dirname "$SCRIPT_DIR")"

echo "========================================="
echo "  AutoTest — Docker Secrets Setup"
echo "========================================="
echo ""

# --- App secrets (single JSON file) ---
SECRETS_FILE="${INFRA_DIR}/secrets.json"

if [ -f "$SECRETS_FILE" ]; then
    echo ">>> Creating api-secrets from ${SECRETS_FILE}..."
    docker secret create api-secrets "$SECRETS_FILE" 2>/dev/null && echo "    [ok] api-secrets created" || echo "    [skip] api-secrets already exists"
else
    echo ">>> secrets.json not found!"
    echo "    1. cp ${INFRA_DIR}/secrets.json.example ${SECRETS_FILE}"
    echo "    2. Edit ${SECRETS_FILE} with real values"
    echo "    3. Re-run this script"
    echo ""
fi

# --- Infrastructure secrets (individual files for PG/Redis/MinIO native _FILE support) ---
echo ""
echo ">>> Creating infrastructure secrets..."
echo "    Enter values for each (input is hidden):"
echo ""

create_secret() {
    local name=$1
    local description=$2

    # Check if already exists
    if docker secret inspect "$name" > /dev/null 2>&1; then
        echo "    [skip] $name already exists"
        return
    fi

    echo -n "  $description ($name): "
    read -s value
    echo ""

    if [ -n "$value" ]; then
        echo "$value" | docker secret create "$name" -
        echo "    [ok] $name created"
    else
        echo "    [skip] $name — empty, skipped"
    fi
}

create_secret "db-password"     "PostgreSQL password"
create_secret "redis-password"  "Redis password"
create_secret "minio-access-key" "MinIO access key"
create_secret "minio-secret-key" "MinIO secret key"

echo ""
echo "========================================="
echo "  Done! To deploy:"
echo "    cd ${INFRA_DIR}"
echo "    ./scripts/deploy.sh --env prod"
echo "========================================="
echo ""
echo "  To update api-secrets later:"
echo "    docker secret rm api-secrets"
echo "    docker secret create api-secrets secrets.json"
echo "    docker service update --force autotest_api"
