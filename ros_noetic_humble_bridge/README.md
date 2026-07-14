# ROS Noetic ↔ ROS2 Humble Bridge

A Docker-based bridge connecting ROS1 Noetic and ROS2 Humble, including support for custom messages.

---

## Architecture

```
┌─────────────────────┐     ┌─────────────────────┐     ┌─────────────────────┐
│   Noetic Container  │────▶│   Bridge Container  │────▶│   Humble Container  │
│   (ROS1 driver)     │     │   (ros1_bridge)     │     │   (your ROS2 code)  │
│   osrf/ros:noetic   │     │   jammy + both ROS  │     │   osrf/ros:humble   │
└─────────────────────┘     └─────────────────────┘     └─────────────────────┘

  All containers: network_mode: host
  Fixed IP:       172.31.1.137
  RMW:            CycloneDDS
```

---

## How It Works

- Based on `ros:humble-ros-base-jammy` (Ubuntu 22.04)
- ROS1 libraries installed via `ros-desktop-dev` (Jammy package) — no separate Noetic install needed
- `ros1_bridge` built from source using the [smith-doug action_bridge_humble fork](https://github.com/smith-doug/ros1_bridge)
- Custom messages cloned from [iiwa_msgs](https://github.com/UoS-EEE-FENDER/iiwa_msgs), built for both ROS2 and ROS1 [iiwa_stack](https://github.com/UoS-EEE-FENDER/iiwa_stack), then the bridge is compiled with both sourced so it detects the custom message pairs automatically
- CycloneDDS interface auto-detected at runtime from the fixed IP
- ROS2 daemon restarted on container start to avoid stale discovery cache

---

## Prerequisites

- Docker and Docker Compose installed
- `roscore` running in your Noetic container
- Fixed IP `172.31.1.137` assigned to your network interface
- All three containers using `network_mode: host`

---

## Project Structure

```
ros_noetic_humble_bridge/
├── Dockerfile            # Bridge image definition
├── docker-compose.yml    # Container configuration
├── entrypoint.sh         # Container startup script
├── setup_env.sh          # Environment setup (sourced by entrypoint and interactive shells)
└── run.sh                # Build / run / attach script
```

---

## Usage

### Build and Run

```bash
# First run — builds image and creates container
bash run.sh

# Subsequent runs — attaches to existing container
bash run.sh
```

### Running the Bridge

```bash
# Dynamic bridge — auto-detects and bridges all matching topics
ros2 run ros1_bridge dynamic_bridge --bridge-all-topics

# Check which message pairs are known to the bridge
ros2 run ros1_bridge dynamic_bridge --print-pairs
```

---

## Testing

### ROS1 → ROS2

In the **Noetic container**:
```bash
source /root/catkin_ws/devel/setup.bash
rostopic pub /test_chatter std_msgs/String "data: 'hello from ROS1'" -r 1
```

In the **Humble container**:
```bash
ros2 topic echo /test_chatter std_msgs/msg/String
```

### ROS2 → ROS1

In the **Humble container**:
```bash
ros2 topic pub /test_ros2 std_msgs/msg/String "data: 'hello from ROS2'" -r 1
```

In the **Noetic container** (start subscriber first — see Known Issues):
```bash
rostopic echo /test_ros2
```

### Custom Messages

In the **Noetic container**:
```bash
source /root/catkin_ws/devel/setup.bash
rostopic pub /test_custom custom_msgs/JointPosition "header:
  seq: 0
  stamp: {secs: 0, nsecs: 0}
  frame_id: ''
position:
  a1: 1.0
  a2: 2.0
  a3: 3.0
  a4: 4.0
  a5: 5.0
  a6: 6.0
  a7: 7.0" -r 1
```

In the **Humble container**:
```bash
ros2 topic echo /test_custom custom_msgs/msg/JointPosition
```

---

## Custom Messages

Message definitions live in [UoS-EEE-FENDER/iiwa_msgs](https://github.com/UoS-EEE-FENDER/iiwa_msgs):


### Currently Bridged Messages

All of them i think



### Field Name Mapping

ROS2 enforces `snake_case` field names. If your ROS1 messages use `camelCase`, add a mapping to `bridge_mapping.yaml`:

```yaml
-
  ros1_package_name: 'custom_msgs'
  ros1_message_name: 'CartesianPose'
  ros2_package_name: 'custom_msgs'
  ros2_message_name: 'CartesianPose'
  fields_1_to_2:
    poseStamped: pose_stamped
  fields_2_to_1:
    pose_stamped: poseStamped
```

---

## Networking

All containers use `network_mode: host`. The fixed IP `172.31.1.137` is used for:

| Variable | Purpose |
|---|---|
| `ROS_MASTER_URI` | Points to `roscore` in the Noetic container |
| `ROS_IP` / `ROS_HOSTNAME` | Identifies this machine on the ROS network |
| CycloneDDS config | Interface auto-detected from this IP at startup |

CycloneDDS config is generated dynamically at container startup — the correct network interface is looked up from the fixed IP, so the config works regardless of interface name (`wlp0s20f3`, `eth0`, etc.).

---

## Known Issues

### ROS2 daemon stale cache
Topics may not be visible immediately after starting the bridge. The entrypoint handles this automatically, but if needed:
```bash
ros2 daemon stop && ros2 daemon start
```

### `docker exec` sessions lose environment
Always attach via `bash run.sh` — it uses `bash --login` which sources `setup_env.sh` through `.bashrc`. Running `docker exec ... bash` directly will be missing the ROS environment.

### ROS1 CLI tools not available in bridge container
`rostopic`, `rosnode` etc. do not work in the bridge container due to Python path conflicts between ROS1 and ROS2. Use these tools from your Noetic container instead.

### Dynamic bridge requires subscribers on both sides
For the ROS2 → ROS1 direction, start `rostopic echo` in the Noetic container **before** publishing from ROS2. The bridge only creates a 2-to-1 bridge when it detects a ROS1 subscriber.

### CycloneDDS multicast disabled
The network interface does not support multicast, so it is disabled in the generated CycloneDDS config. Peer discovery works via unicast on the host network.

### Just git clone breaks the rosout 
Need to checkout specific hash commit as the main repo has something broken for humble
---
