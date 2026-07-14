#!/bin/bash
set -e

source /setup_env.sh

echo "================================================"
echo " BAE System Container"
echo " IFACE : ${IFACE:-not detected}"
echo " RMW   : $RMW_IMPLEMENTATION"
echo "================================================"

exec "$@"