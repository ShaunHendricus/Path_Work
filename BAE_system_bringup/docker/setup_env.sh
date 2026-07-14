# setup_env.sh
#!/bin/bash

source /opt/ros/humble/setup.bash
source /root/ros2_ws/install/setup.bash

export RMW_IMPLEMENTATION=rmw_cyclonedds_cpp

# Detect network interface by fixed robot network IP
IFACE=$(ip -o addr show | awk '$4 ~ /172\.31\.1\.137/ {print $2}')

if [ -z "$IFACE" ]; then
    echo "WARNING: Could not detect network interface for 172.31.1.137" >&2
    echo "         CycloneDDS will use default interface discovery" >&2
else
    echo "INFO: CycloneDDS using interface: ${IFACE}"
fi

cat > /tmp/cyclonedds.xml << EOF
<?xml version="1.0" encoding="UTF-8"?>
<CycloneDDS xmlns="https://cdds.io/config"
            xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
            xsi:schemaLocation="https://cdds.io/config
            https://raw.githubusercontent.com/eclipse-cyclonedds/cyclonedds/master/etc/cyclonedds.xsd">
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