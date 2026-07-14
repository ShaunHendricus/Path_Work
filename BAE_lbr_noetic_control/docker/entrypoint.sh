#!/bin/bash
set -e

source /setup_env.sh

echo "================================================"
echo " Kuka Deployer Container"
echo " ROS_MASTER_URI : $ROS_MASTER_URI"
echo " ROS_IP         : $ROS_IP"
echo " IFACE          : ${IFACE:-not detected}"
echo "================================================"

exec "$@"