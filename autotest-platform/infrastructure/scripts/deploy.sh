#!/bin/bash
set -e

# Usage: ./deploy.sh [--env test|prod] [--tag TAG]
# Examples:
#   ./deploy.sh --env test                    # Deploy to test with :latest
#   ./deploy.sh --env prod --tag abc1234      # Deploy to prod with specific tag
#   ./deploy.sh                               # Deploy to prod with :latest (default)

ENV="prod"
TAG="latest"
REGISTRY="${REGISTRY:-autotest}"

while [[ $# -gt 0 ]]; do
    case $1 in
        --env) ENV="$2"; shift 2;;
        --tag) TAG="$2"; shift 2;;
        --registry) REGISTRY="$2"; shift 2;;
        *) echo "Unknown option: $1"; exit 1;;
    esac
done

STACK_NAME="autotest"
if [ "$ENV" = "test" ]; then
    STACK_NAME="autotest-test"
fi

echo "========================================="
echo "  AutoTest Platform Deployment"
echo "========================================="
echo "  Environment: $ENV"
echo "  Registry:    $REGISTRY"
echo "  Tag:         $TAG"
echo "  Stack:       $STACK_NAME"
echo "========================================="
echo ""

# Build images
echo ">>> Building backend image..."
docker build -t "${REGISTRY}-api:${TAG}" -f backend/Dockerfile backend/

echo ">>> Building frontend image..."
docker build -t "${REGISTRY}-frontend:${TAG}" -f frontend/Dockerfile frontend/

# Push if registry is remote
if [[ "$REGISTRY" == *"/"* ]] || [[ "$REGISTRY" == *"."* ]]; then
    echo ">>> Pushing images to registry..."
    docker push "${REGISTRY}-api:${TAG}"
    docker push "${REGISTRY}-frontend:${TAG}"
fi

# Deploy stack
echo ">>> Deploying stack..."
export REGISTRY TAG

if [ "$ENV" = "test" ]; then
    docker stack deploy \
        -c infrastructure/docker-compose.yml \
        -c infrastructure/docker-compose.test.yml \
        "$STACK_NAME" --with-registry-auth
else
    docker stack deploy \
        -c infrastructure/docker-compose.yml \
        "$STACK_NAME" --with-registry-auth
fi

# Wait and show status
echo ""
echo ">>> Waiting for services to start..."
sleep 5

echo ""
echo ">>> Stack services:"
docker stack services "$STACK_NAME"

echo ""
echo "========================================="
echo "  Deployment complete!"
echo "========================================="

if [ "$ENV" = "test" ]; then
    echo "  API:      https://api-test.avtolider.uz"
    echo "  Frontend: https://test.avtolider.uz"
    echo "  Health:   https://api-test.avtolider.uz/health"
    echo "  Swagger:  https://api-test.avtolider.uz/swagger"
else
    echo "  API:      https://api.avtolider.uz"
    echo "  Frontend: https://avtolider.uz"
    echo "  Health:   https://api.avtolider.uz/health"
fi
