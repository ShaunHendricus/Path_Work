#!/bin/bash

source /root/catkin_ws/devel/setup.bash

export ROS_MASTER_URI=http://172.31.1.137:11311
export ROS_IP=172.31.1.137
export ROS_HOSTNAME=172.31.1.137

# Detect interface from fixed robot network IP
IFACE=$(ip -o addr show | awk '$4 ~ /172\.31\.1\.137/ {print $2}')

if [ -z "$IFACE" ]; then
    echo "WARNING: Could not detect network interface for 172.31.1.137" >&2
    echo "         Check that the robot network is connected" >&2
else
    echo "INFO: Using interface: ${IFACE}"
fi

export IFACE