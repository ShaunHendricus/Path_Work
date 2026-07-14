# syntax=docker/dockerfile:1.4
FROM osrf/ros:noetic-desktop-full

# ── Environment ────────────────────────────────────────────────────────────────
ENV DEBIAN_FRONTEND=noninteractive
ENV CATKIN_WS=/root/catkin_ws

# ── System deps ────────────────────────────────────────────────────────────────
RUN apt-get update && apt-get install -y --no-install-recommends \
    git \
    python3-pip \
    python-is-python3 \
    python3-rosdep \
    build-essential \
    iproute2 \
    ros-noetic-moveit \
    ros-noetic-ros-controllers \
    ros-noetic-ros-control \
    ros-noetic-force-torque-sensor-controller \
    && rm -rf /var/lib/apt/lists/*

# ── rosdep ────────────────────────────────────────────────────────────────
RUN rosdep update

# ── Clone repos ────────────────────────────────────────────────────────────────
RUN mkdir -p $CATKIN_WS/src
WORKDIR $CATKIN_WS/src
RUN --mount=type=ssh \
    mkdir -p /root/.ssh && \
    ssh-keyscan github.com >> /root/.ssh/known_hosts && \
    git clone git@github.com:UoS-EEE-FENDER/iiwa_stack.git /root/catkin_ws/src/iiwa_stack
    
# ── Dependencies & build ───────────────────────────────────────────────────────
WORKDIR ${CATKIN_WS}
RUN /bin/bash -c "source /opt/ros/noetic/setup.bash && \
    rosdep install --from-paths src --ignore-src -y && \
    catkin_make"

# ── Shell environment ──────────────────────────────────────────────────────────
COPY setup_env.sh /setup_env.sh
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /setup_env.sh /entrypoint.sh && \
    echo "source /setup_env.sh" >> /root/.bashrc

ENTRYPOINT ["/entrypoint.sh"]
CMD ["bash"]