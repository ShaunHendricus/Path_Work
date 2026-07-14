#!/bin/bash

source /opt/ros/humble/setup.bash
source /ros2_ws/install/setup.bash
source /ros1_bridge_ws/install/setup.bash

export ROS_MASTER_URI=http://172.31.1.137:11311
export ROS_IP=172.31.1.137
export ROS_HOSTNAME=172.31.1.137
export RMW_IMPLEMENTATION=rmw_cyclonedds_cpp

IFACE=$(ip -o addr show | awk '$4 ~ /172\.31\.1\.137/ {print $2}')

cat > /tmp/cyclonedds.xml << EOF
<?xml version="1.0" encoding="UTF-8"?>
<CycloneDDS xmlns="https://cdds.io/config"
            xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
            xsi:schemaLocation="https://cdds.io/config https://raw.githubusercontent.com/eclipse-cyclonedds/cyclonedds/master/etc/cyclonedds.xsd">
    <Domain id="any">
        <General>
            <Interfaces>
                <NetworkInterface name="${IFACE}" multicast="false"/>
            </Interfaces>
        </General>
    </Domain>
</CycloneDDS>
EOF

export CYCLONEDDS_URI=file:///tmp/cyclonedds.xml