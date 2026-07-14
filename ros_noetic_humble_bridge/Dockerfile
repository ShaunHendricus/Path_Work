FROM ros:humble-ros-base-jammy
SHELL ["/bin/bash", "-o", "pipefail", "-o", "errexit", "-c"]
ENV DEBIAN_FRONTEND=noninteractive

# ── 1. Enable universe repo ───────────────────────────────────────────────────
RUN apt-get -y update && \
    apt-get -y install software-properties-common && \
    add-apt-repository universe && \
    apt-get -y update

# ── 2. Upgrade to full ROS2 desktop + tools ───────────────────────────────────
RUN apt-get -y install \
    ros-humble-desktop \
    ros-humble-rmw-cyclonedds-cpp \
    python3-colcon-common-extensions \
    iproute2 \
    xterm x11-apps

# ── 3. Temporarily remove ROS2 apt repo ──────────────────────────────────────
RUN mv /etc/apt/sources.list.d/ros2.sources /root/ && \
    apt-get update

# ── 4. Fix catkin conflict in dpkg ────────────────────────────────────────────
RUN sed -i -e 's|^Conflicts: catkin|#Conflicts: catkin|' /var/lib/dpkg/status && \
    apt-get install -f

# ── 5. Force install conflicting Python packages ──────────────────────────────
RUN apt-get download python3-catkin-pkg python3-rospkg python3-rosdistro && \
    dpkg --force-overwrite -i python3-catkin-pkg*.deb && \
    dpkg --force-overwrite -i python3-rospkg*.deb && \
    dpkg --force-overwrite -i python3-rosdistro*.deb && \
    apt-get install -f

# ── 6. Install ROS1 (ros-desktop-dev) ────────────────────────────────────────
RUN apt-get -y install ros-desktop-dev

# ── 7. Restore ROS2 apt repo ──────────────────────────────────────────────────
RUN mv /root/ros2.sources /etc/apt/sources.list.d/ && \
    apt-get -y update

RUN --mount=type=ssh \
    mkdir -p /root/.ssh && \
    ssh-keyscan github.com >> /root/.ssh/known_hosts && \
    git clone git@github.com:UoS-EEE-FENDER/iiwa_stack.git /iiwa_stack && \
    git clone git@github.com:UoS-EEE-FENDER/iiwa_msgs.git /ros2_ws/src/iiwa_msgs

# ── 9. Build iiwa_msgs for ROS1 ───────────────────────
RUN mkdir -p /catkin_ws/src && \
    cp -r /iiwa_stack/iiwa_msgs /catkin_ws/src/iiwa_msgs && \
    cd /catkin_ws && \
    unset ROS_DISTRO && \
    unset AMENT_PREFIX_PATH && \
    unset COLCON_PREFIX_PATH && \
    unset CMAKE_PREFIX_PATH && \
    unset ROS_PACKAGE_PATH && \
    unset PYTHONPATH && \
    /bin/bash -c "source /usr/share/ros-desktop-dev/setup.bash 2>/dev/null || true && \
        catkin_make install"

# ── 10. Build custom_msgs for ROS2 ───────────────────────────────────────────
RUN source /opt/ros/humble/setup.bash && \
    cd /ros2_ws && \
    colcon build --cmake-args -DCMAKE_BUILD_TYPE=Release

# ── 11. Build ros1_bridge with custom messages ────────────────────────────────
RUN --mount=type=ssh \
    source /opt/ros/humble/setup.bash && \
    source /catkin_ws/install/setup.bash && \
    source /ros2_ws/install/setup.bash && \
    mkdir -p /ros1_bridge_ws/src && \
    cd /ros1_bridge_ws/src && \
    git clone git@github.com:UoS-EEE-FENDER/ros1_bridge.git && \
    cd ros1_bridge && \
    git checkout b9f1739fd84fc877a8ec6e5c416b65aa2d782f89 && \
    cd /ros1_bridge_ws && \
    rm -rf build install log && \
    export ROS1_BRIDGE_MAPPING_RULES=/bridge_mapping.yaml && \
    colcon build --symlink-install --cmake-force-configure --base-paths src

# ── 12. Setup env and entrypoint ─────────────────────────────────────────────
COPY setup_env.sh /setup_env.sh
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /setup_env.sh /entrypoint.sh && \
    echo "source /setup_env.sh" >> /root/.bashrc

ENTRYPOINT ["/entrypoint.sh"]
CMD ["bash"]
