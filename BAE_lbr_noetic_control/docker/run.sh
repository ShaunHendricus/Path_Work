#!/bin/bash
set -e

CONTAINER_NAME="kuka_deployer"

xhost +local:docker 2>/dev/null || true

if docker ps -a --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "Attaching to existing container..."
    docker start "${CONTAINER_NAME}"
    docker exec -it "${CONTAINER_NAME}" bash --login
else
    echo "Starting SSH agent..."
    eval $(ssh-agent -s)
    ssh-add

    echo "Building image..."
    DOCKER_BUILDKIT=1 docker compose build --ssh default

    echo "Creating container..."
    docker compose run --name "${CONTAINER_NAME}" kuka_deployer bash
fi