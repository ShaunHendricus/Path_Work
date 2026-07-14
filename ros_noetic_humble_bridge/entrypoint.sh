#!/bin/bash
set -e

source /setup_env.sh

ros2 daemon stop 2>/dev/null || true
ros2 daemon start 2>/dev/null || true

echo "================================================"
echo " ROS Bridge Container"
echo " ROS_MASTER_URI : $ROS_MASTER_URI"
echo " ROS_IP         : $ROS_IP"
echo " IFACE          : $IFACE"
echo " RMW            : $RMW_IMPLEMENTATION"
echo "================================================"

exec "$@"