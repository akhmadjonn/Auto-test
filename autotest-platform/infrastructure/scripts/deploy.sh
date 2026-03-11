#!/bin/bash
set -e

REGISTRY="${REGISTRY:?REGISTRY environment variable is required}"
TAG="${TAG:-latest}"
STACK_NAME="autotest"

echo "=== AutoTest Platform Deployment ==="
echo "Registry: $REGISTRY"
echo "Tag:      $TAG"
echo "Stack:    $STACK_NAME"
echo ""

# Build backend image
echo ">>> Building backend image..."
docker build -t "$REGISTRY/autotest-api:$TAG" -f backend/Dockerfile backend/

# Build frontend image
echo ">>> Building frontend image..."
docker build -t "$REGISTRY/autotest-frontend:$TAG" -f frontend/Dockerfile frontend/

# Push images to registry
echo ">>> Pushing images..."
docker push "$REGISTRY/autotest-api:$TAG"
docker push "$REGISTRY/autotest-frontend:$TAG"

# Deploy stack
echo ">>> Deploying stack..."
docker stack deploy -c infrastructure/docker-compose.yml "$STACK_NAME" --with-registry-auth

# Print service status
echo ""
echo ">>> Stack services:"
docker stack services "$STACK_NAME"

echo ""
echo "=== Deployment complete ==="
