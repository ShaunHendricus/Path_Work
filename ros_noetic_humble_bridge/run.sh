#!/bin/bash
set -e

CONTAINER_NAME="ros_bridge"

xhost +local:docker 2>/dev/null || true

if docker ps -a --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "Attaching to existing container..."
    docker start "${CONTAINER_NAME}" 2>/dev/null || true
    docker exec -it "${CONTAINER_NAME}" bash --login
else
    echo "Building image..."
    DOCKER_BUILDKIT=1 docker compose build --ssh default
    
    echo "Creating and entering container..."
    docker compose run \
        --name "${CONTAINER_NAME}" \
        ros_bridge bash
fi