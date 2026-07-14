#!/usr/bin/env python3
"""
robot_base.py

Base class for all robot task nodes. Provides:
  - Robot I/O (joint state subscription, joint position publisher)
  - Cartesian pose subscription
  - Joint-space motion (move_to)
  - Zivid camera settings loading
  - Pose list loading from a config yaml
  - Common service utilities

Subclass this and implement run().
"""

import time
import yaml
import os

import rclpy
from rclpy.node import Node

from sensor_msgs.msg import JointState
from iiwa_msgs.msg import JointPosition, JointQuantity, CartesianPose
from rcl_interfaces.srv import SetParameters
from rclpy.parameter import Parameter
from ament_index_python.packages import get_package_share_directory


# ---------------------------------------------------------------------------
# Motion tuning — adjust per robot/environment
# ---------------------------------------------------------------------------
POSITION_TOLERANCE  = 0.0001   # rad — how close is "at pose"
SETTLE_TIME         = 3    # s   — wait after reaching pose before capture
MOVE_TIMEOUT        = 20.0   # s   — abort if pose not reached within this time
COMMAND_RATE        = 0.05   # s   — spin interval while sending move commands
ROBOT_READY_TIMEOUT = 30.0   # s   — max wait for first joint state on startup


class RobotBase(Node):

    def __init__(self, node_name: str,
                 poses_yaml: str = 'capture_poses.yaml',
                 scenario: str = 'hand_eye'):

        super().__init__(node_name)

        # Store for use by load_poses(); poses are empty until load_poses() is called.
        self._poses_yaml = poses_yaml
        self.scenario    = scenario
        self.poses: list = []

        # ------------------------------------------------------------------
        # Robot state
        # ------------------------------------------------------------------
        self.current_joints: list[float] = [0.0] * 7
        self.joint_received: bool        = False
        self.cartesian_pose              = None

        # ------------------------------------------------------------------
        # Robot I/O
        # ------------------------------------------------------------------
        self.create_subscription(
            JointState,
            '/iiwa/joint_states',
            self._joint_cb,
            10
        )
        self.create_subscription(
            CartesianPose,
            '/iiwa/state/CartesianPose',
            self._cartesian_cb,
            10
        )
        self.joint_pub = self.create_publisher(
            JointPosition,
            '/iiwa/command/JointPosition',
            1
        )

    # ==========================================================================
    # Pose loading
    # ==========================================================================

    def load_poses(self, poses_yaml: str = None, scenario: str = None):
        """
        Load poses from a scenarios-format YAML file.

        Must be called explicitly by subclasses after declare_parameter() so
        that the scenario can be driven by a ROS parameter rather than being
        baked in at construction time.

        Falls back to the values passed to __init__ if either argument is omitted.
        """
        poses_yaml = poses_yaml or self._poses_yaml
        scenario   = scenario   or self.scenario

        config_path = os.path.join(
            get_package_share_directory('robot_tasks'),
            'config', poses_yaml
        )

        with open(config_path, 'r') as f:
            data = yaml.safe_load(f)

        if 'scenarios' not in data:
            raise ValueError(
                f"{poses_yaml} must use 'scenarios' format"
            )

        if scenario not in data['scenarios']:
            raise ValueError(
                f"Scenario '{scenario}' not found. "
                f"Available: {list(data['scenarios'].keys())}"
            )

        self.scenario = scenario
        self.poses    = data['scenarios'][scenario]['poses']

        self.get_logger().info(
            f"Loaded {len(self.poses)} poses from {poses_yaml} "
            f"(scenario: {self.scenario})"
        )

    # ==========================================================================
    # Robot callbacks
    # ==========================================================================

    def _joint_cb(self, msg: JointState):
        self.current_joints = list(msg.position)
        self.joint_received = True

    def _cartesian_cb(self, msg: CartesianPose):
        self.cartesian_pose = msg.pose_stamped.pose

    # ==========================================================================
    # Robot motion
    # ==========================================================================

    def _send_joint_command(self, joints: list[float]):
        msg = JointPosition()
        msg.header.stamp    = self.get_clock().now().to_msg()
        msg.header.frame_id = 'iiwa_link_0'

        jq = JointQuantity()
        jq.a1, jq.a2, jq.a3 = joints[0], joints[1], joints[2]
        jq.a4, jq.a5, jq.a6 = joints[3], joints[4], joints[5]
        jq.a7 = joints[6]

        msg.position = jq
        self.joint_pub.publish(msg)

    def _at_target(self, target: list[float]) -> bool:
        return all(
            abs(c - t) < POSITION_TOLERANCE
            for c, t in zip(self.current_joints, target)
        )

    def move_to(self, target: list[float], label: str = "") -> bool:
        """
        Command robot to target joint positions (radians).
        Repeatedly sends the command and spins until the robot reaches the
        target or MOVE_TIMEOUT is exceeded.

        Returns True if reached, False on timeout.
        """
        tag = f" [{label}]" if label else ""
        self.get_logger().info(
            f"Moving to{tag}: {[f'{j:.3f}' for j in target]}"
        )

        deadline = time.time() + MOVE_TIMEOUT

        while time.time() < deadline:
            self._send_joint_command(target)
            rclpy.spin_once(self, timeout_sec=COMMAND_RATE)

            if self._at_target(target):
                self.get_logger().info("  ✓ Pose reached")

                self.get_logger().info(
                    f"Target : {[f'{j:.6f}' for j in target]}"
                )

                self.get_logger().info(
                    f"Current: {[f'{j:.6f}' for j in self.current_joints]}"
                )

                return True

        self.get_logger().error(f"  ✗ Move timeout{tag}")
        return False

    def _wait_for_cartesian_pose(self, timeout: float = 5.0):
        """
        Block until a fresh cartesian pose message arrives.
        Resets to None first to guarantee the pose is from after this call,
        not a stale one from the previous pose.
        Raises RuntimeError on timeout.
        """
        self.cartesian_pose = None
        deadline = time.time() + timeout

        while self.cartesian_pose is None:
            rclpy.spin_once(self, timeout_sec=0.1)
            if time.time() > deadline:
                raise RuntimeError("Timed out waiting for cartesian pose")

    # ==========================================================================
    # Camera settings
    # ==========================================================================

    def _camera_settings_path(self) -> str:
        return os.path.join(
            get_package_share_directory('robot_tasks'),
            'config', 'camera_settings.yaml'
        )

    def load_camera_settings(self, yaml_file_path: str = None):
        """
        Push camera settings YAML to the Zivid node via ROS parameters.
        Defaults to robot_tasks/config/camera_settings.yaml if no path given.

        Note: infield correction ignores these settings (uses its own automatic
        settings). Only needed before hand-eye or pointcloud captures.
        """
        path = yaml_file_path or self._camera_settings_path()
        self.get_logger().info(f"Loading camera settings: {path}")

        with open(path, 'r') as f:
            yaml_string = f.read()

        param = Parameter(
            name="settings_yaml",
            value=yaml_string
        ).to_parameter_msg()

        client = self.create_client(SetParameters, '/zivid_camera/set_parameters')

        while not client.wait_for_service(timeout_sec=2.0):
            self.get_logger().info("Waiting for Zivid parameter service...")

        future = client.call_async(SetParameters.Request(parameters=[param]))
        rclpy.spin_until_future_complete(self, future)

        if not future.result():
            raise RuntimeError("Failed to apply camera settings")

        self.get_logger().info("  ✓ Camera settings applied")

    # ==========================================================================
    # Service utilities
    # ==========================================================================

    def _wait_for_services(self, clients: list, label: str = ""):
        """Block until all listed service clients are available."""
        tag = f" ({label})" if label else ""
        self.get_logger().info(f"Waiting for services{tag}...")

        for client in clients:
            while not client.wait_for_service(timeout_sec=2.0):
                self.get_logger().info(f"  Waiting for {client.srv_name}...")

        self.get_logger().info(f"  ✓ Services ready{tag}")

    # ==========================================================================
    # Startup
    # ==========================================================================

    def wait_for_robot(self):
        """
        Block until the robot publishes joint states.
        Call this at the start of run() in every subclass.
        Raises RuntimeError if no joint states arrive within ROBOT_READY_TIMEOUT.
        """
        self.get_logger().info("Waiting for /iiwa/joint_states...")
        deadline = time.time() + ROBOT_READY_TIMEOUT

        while not self.joint_received:
            rclpy.spin_once(self, timeout_sec=0.5)
            if time.time() > deadline:
                raise RuntimeError(
                    "No joint states received — is the robot running?"
                )

        self.get_logger().info("Robot connected\n")