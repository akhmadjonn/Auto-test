#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "=== Generating TypeScript types ==="
cd "$SCRIPT_DIR/../frontend"
npx openapi-typescript "$SCRIPT_DIR/openapi.yaml" -o src/types/api.ts
echo "Done: frontend/src/types/api.ts"
