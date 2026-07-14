# syntax=docker/dockerfile:1.4
FROM osrf/ros:humble-desktop-full

# ── Environment ────────────────────────────────────────────────────────────────
# ENV ROSDEP_SKIP_KEYS="ament_cmake_clang_tidy ament_cmake_clang_format urdf_launch"
ENV ROSDEP_SKIP_KEYS="ament_cmake_clang_tidy ament_cmake_clang_format urdf_launch ament_python"
ENV RMW_IMPLEMENTATION=rmw_cyclonedds_cpp

# ── System & ROS binary packages ───────────────
RUN apt-get update && apt-get install -y \
    python3-colcon-common-extensions \
    python3-vcstool \
    python3-rosdep \
    ros-dev-tools \
    iproute2 \
    python-is-python3 \
    git \
    wget \
    curl \
    gnupg2 \
    lsb-release \
    software-properties-common \
    g++ \
    ocl-icd-opencl-dev \
    clinfo \
    ros-humble-moveit \
    ros-humble-moveit-ros-move-group \
    ros-humble-moveit-ros-planning-interface \
    ros-humble-moveit-ros-visualization \
    ros-humble-moveit-setup-assistant \
    ros-humble-moveit-planners-ompl \
    ros-humble-moveit-planners-chomp \
    ros-humble-moveit-servo \
    ros-humble-moveit-kinematics \
    ros-humble-moveit-simple-controller-manager \
    ros-humble-moveit-configs-utils \
    ros-humble-rmw-cyclonedds-cpp \
    ros-humble-ros2-control \
    ros-humble-ros2-controllers \
    ros-humble-controller-manager \
    ros-humble-joint-state-broadcaster \
    ros-humble-joint-trajectory-controller \
    ros-humble-realtime-tools \
    ros-humble-control-msgs \
    ros-humble-topic-tools \
    ros-humble-joint-state-publisher \
    ros-humble-joint-state-publisher-gui \
    ros-humble-urdf-launch \
    ros-humble-gz-ros2-control \
    && rm -rf /var/lib/apt/lists/*

# ── Zivid SDK ──────────────────────────────────────────────────────────────────
RUN wget -q \
    https://downloads.zivid.com/sdk/releases/2.17.0+5fc9f05e-1/u22/zivid_2.17.0+5fc9f05e-1_amd64.deb \
    https://downloads.zivid.com/sdk/releases/2.17.0+5fc9f05e-1/u22/amd64/zivid-studio_2.17.0+5fc9f05e-1_amd64.deb \
    https://downloads.zivid.com/sdk/releases/2.17.0+5fc9f05e-1/u22/amd64/zivid-tools_2.17.0+5fc9f05e-1_amd64.deb \
    && apt-get update \
    && dpkg -i zivid_2.17.0+5fc9f05e-1_amd64.deb || true \
    && dpkg -i zivid-studio_2.17.0+5fc9f05e-1_amd64.deb || true \
    && dpkg -i zivid-tools_2.17.0+5fc9f05e-1_amd64.deb || true \
    && apt-get install -f -y \
    && rm -f zivid_2.17.0+5fc9f05e-1_amd64.deb \
              zivid-studio_2.17.0+5fc9f05e-1_amd64.deb \
              zivid-tools_2.17.0+5fc9f05e-1_amd64.deb \
    && rm -rf /var/lib/apt/lists/*

# ── rosdep init  ────────────────────────────────────────────────
RUN rosdep update

# ── LBR / vcs stack ────────────────────────
RUN mkdir -p /root/ros2_ws/src
WORKDIR /root/ros2_ws
RUN /bin/bash -c "source /opt/ros/humble/setup.bash && \
    vcs import src --input \
    https://raw.githubusercontent.com/lbr-stack/lbr_fri_ros2_stack/humble/lbr_fri_ros2_stack/repos-fri-1.15.yaml"

# ── Build stable packages (lbr stack + any vcs-imported packages) ─────────────
RUN /bin/bash -c "source /opt/ros/humble/setup.bash && \
    rosdep install -i --from-path src --rosdistro humble -y \
    --skip-keys=\"${ROSDEP_SKIP_KEYS}\" && \
    colcon build --symlink-install \
    --packages-skip \
        zivid_camera \
        zivid_description \
        zivid_interfaces \
        zivid_rviz_plugin \
        zivid_samples \
        bae_system_description \
        camera_mount_description \
        robot_tasks \
        iiwa_msgs"

# ── Private repos ────────────────────────────────────
WORKDIR /root/ros2_ws/src
RUN --mount=type=ssh \
    mkdir -p /root/.ssh && \
    ssh-keyscan github.com >> /root/.ssh/known_hosts && \
    git clone git@github.com:UoS-EEE-FENDER/zivid-ros.git && \
    git clone git@github.com:UoS-EEE-FENDER/BAE_system_description.git && \
    git clone git@github.com:UoS-EEE-FENDER/camera_mount_description.git && \
    git clone git@github.com:UoS-EEE-FENDER/iiwa_msgs.git && \
    git clone git@github.com:UoS-EEE-FENDER/BAE_robot_tasks.git

# ── Build your packages on top of the already-built stable layer ───────────────
WORKDIR /root/ros2_ws
RUN /bin/bash -c "source /opt/ros/humble/setup.bash && \
    source install/setup.bash && \
    rosdep install -i --from-path src --rosdistro humble -y \
    --skip-keys=\"${ROSDEP_SKIP_KEYS}\" && \
    colcon build --symlink-install \
    --packages-select \
        zivid_camera \
        zivid_description \
        zivid_interfaces \
        zivid_rviz_plugin \
        zivid_samples \
        bae_system_description \
        camera_mount_description \
        robot_tasks \
        iiwa_msgs"

# ── Shell environment ──────────────────────────────────────────────────────────
COPY setup_env.sh /setup_env.sh
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /setup_env.sh /entrypoint.sh && \
    echo "source /setup_env.sh" >> /root/.bashrc

ENTRYPOINT ["/entrypoint.sh"]
CMD ["bash"]