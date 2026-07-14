#!/usr/bin/env python3
"""
task_runner.py

Automated calibration tasks for the iiwa + Zivid camera system.

Modes:
  infield   -- Infield correction only (automatic camera settings)
  hand_eye  -- Hand-eye calibration only (uses camera_settings.yaml)
  both      -- Single robot pass performing both (recommended)

Usage:
  ros2 run robot_tasks task_runner --ros-args -p mode:=infield
  ros2 run robot_tasks task_runner --ros-args -p mode:=hand_eye
  ros2 run robot_tasks task_runner --ros-args -p mode:=both
"""

import time
import rclpy

from zivid_interfaces.msg import HandEyeCalibrationObjects
from std_srvs.srv import Trigger
from zivid_interfaces.srv import (
    InfieldCorrectionCapture,
    InfieldCorrectionCompute,
    HandEyeCalibrationStart,
    HandEyeCalibrationCapture,
    HandEyeCalibrationCalibrate,
)

from robot_tasks.robot_base import RobotBase, SETTLE_TIME


VALID_MODES = ("infield", "hand_eye", "capture")


class TaskRunner(RobotBase):
    def __init__(self):
        super().__init__('task_runner')          # no scenario arg

        self.declare_parameter("mode", "hand_eye")
        mode = self.get_parameter("mode").get_parameter_value().string_value

        if mode not in VALID_MODES:
            raise ValueError(f"Unknown mode '{mode}'. Valid: {VALID_MODES}")

        self.mode = mode
        self.load_poses('capture_poses.yaml', scenario=mode)
        
        # ------------------------------------------------------------------
        # Infield correction services
        # ------------------------------------------------------------------
        self.infield_start             = self.create_client(Trigger,                    'infield_correction/start')
        self.infield_capture           = self.create_client(InfieldCorrectionCapture,   'infield_correction/capture')
        self.infield_compute           = self.create_client(InfieldCorrectionCompute,   'infield_correction/compute')
        self.infield_compute_and_write = self.create_client(InfieldCorrectionCompute,   'infield_correction/compute_and_write')

        # ------------------------------------------------------------------
        # Hand-eye calibration services
        # ------------------------------------------------------------------
        self.he_start     = self.create_client(HandEyeCalibrationStart,    'hand_eye_calibration/start')
        self.he_capture   = self.create_client(HandEyeCalibrationCapture,  'hand_eye_calibration/capture')
        self.he_calibrate = self.create_client(HandEyeCalibrationCalibrate,'hand_eye_calibration/calibrate')

    # ==========================================================================
    # Infield steps
    # ==========================================================================

    def _infield_start(self):
        self.get_logger().info("Starting infield correction session")
        future = self.infield_start.call_async(Trigger.Request())
        rclpy.spin_until_future_complete(self, future)
        result = future.result()
        if not result.success:
            raise RuntimeError(f"Infield start failed: {result.message}")

    def _infield_capture(self) -> bool:
        future = self.infield_capture.call_async(InfieldCorrectionCapture.Request())
        rclpy.spin_until_future_complete(self, future)
        result = future.result()
        if result.success:
            self.get_logger().info(
                f"  ✓ Infield capture #{result.number_of_captures}"
                f" | status: {result.status}"
            )
        else:
            self.get_logger().warning(f"  ✗ Infield capture failed: {result.message}")
        return result.success

    def _infield_compute(self):
        """Intermediate compute for live trueness feedback. Non-destructive."""
        future = self.infield_compute.call_async(InfieldCorrectionCompute.Request())
        rclpy.spin_until_future_complete(self, future)
        result = future.result()
        if result.success:
            self.get_logger().info(
                f"  Trueness error: {result.average_trueness_error * 100:.2f}%"
            )

    def _infield_compute_and_write(self):
        """Final compute + write correction to camera. Raises on failure."""
        self.get_logger().info("Writing infield correction to camera...")
        future = self.infield_compute_and_write.call_async(
            InfieldCorrectionCompute.Request()
        )
        rclpy.spin_until_future_complete(self, future)
        if not future.result().success:
            raise RuntimeError("Infield compute_and_write failed")
        
        result = future.result()
        self.get_logger().info(
                    f'Infield correction compute results:'
                )
        self.get_logger().info(f'  Success: {result.success}')
        self.get_logger().info(f'  Number of captures: {result.number_of_captures}')
        self.get_logger().info('  Trueness errors:')
        for i, error in enumerate(result.trueness_errors):
            self.get_logger().info(f'  - Capture {i + 1}: {error * 100.0} %')
        self.get_logger().info(
            f'  Average trueness error: {result.average_trueness_error * 100.0} %'
        )
        self.get_logger().info(
            f'  Maximum trueness error: {result.maximum_trueness_error * 100.0} %'
        )
        self.get_logger().info(
            f'  Dimension accuracy: {result.dimension_accuracy * 100.0} %'
        )
        self.get_logger().info(f'  Z min: {result.z_min} m')
        self.get_logger().info(f'  Z max: {result.z_max} m')
        message_str = f'"""{result.message}"""' if result.message else ''
        self.get_logger().info(f'  Message: {message_str}')
        self.get_logger().info("  ✓ Infield correction written to camera")

    # ==========================================================================
    # Hand-eye steps
    # ==========================================================================

    def _he_start(self):
        self.get_logger().info("Starting hand-eye calibration session")
        req = HandEyeCalibrationStart.Request()
        req.calibration_objects.type = HandEyeCalibrationObjects.CALIBRATION_BOARD
        future = self.he_start.call_async(req)
        rclpy.spin_until_future_complete(self, future)
        result = future.result()
        if not result.success:
            raise RuntimeError(f"Hand-eye start failed: {result.message}")

    def _he_capture(self, robot_pose) -> bool:
        req = HandEyeCalibrationCapture.Request()
        req.robot_pose = robot_pose
        future = self.he_capture.call_async(req)
        rclpy.spin_until_future_complete(self, future)
        result = future.result()
        if result.success:
            self.get_logger().info("  ✓ Hand-eye capture OK")
        else:
            self.get_logger().warning(f"  ✗ Hand-eye capture failed: {result.message}")
        return result.success

    def _he_calibrate(self):
        """Run final hand-eye solve. Raises on failure."""
        self.get_logger().info("Running hand-eye calibration solve...")
        req = HandEyeCalibrationCalibrate.Request()
        req.configuration = HandEyeCalibrationCalibrate.Request.EYE_IN_HAND
        future = self.he_calibrate.call_async(req)
        rclpy.spin_until_future_complete(self, future)
        if not future.result().success:
            raise RuntimeError("Hand-eye calibration solve failed")
        
        T = future.result().transform
        print("Translation:", T.translation)
        print("Rotation:", T.rotation)

        self.get_logger().info("  ✓ Hand-eye calibration complete")

    # ==========================================================================
    # Task runners
    # ==========================================================================

    def run_infield(self):
        """
        Infield correction pass.
        Note: infield capture uses automatic camera settings — camera_settings.yaml
        is not loaded here.
        """
        self._wait_for_services(
            [self.infield_start, self.infield_capture,
             self.infield_compute, self.infield_compute_and_write],
            label="infield"
        )
        self._infield_start()

        captures = 0
        for i, pose in enumerate(self.poses):
            self.get_logger().info(f"\n--- Pose {i+1}/{len(self.poses)} ---")
            if not self.move_to(pose, label=f"pose {i+1}"):
                continue
            time.sleep(SETTLE_TIME)
            if self._infield_capture():
                captures += 1
                self._infield_compute()  # live trueness feedback

        self.get_logger().info(f"\nInfield: {captures} captures")
        self._infield_compute_and_write()

    def run_hand_eye(self):
        """Hand-eye calibration pass."""
        self._wait_for_services(
            [self.he_start, self.he_capture, self.he_calibrate],
            label="hand_eye"
        )
        self.load_camera_settings()
        self._he_start()

        captures = 0
        for i, pose in enumerate(self.poses):
            self.get_logger().info(f"\n--- Pose {i+1}/{len(self.poses)} ---")
            if not self.move_to(pose, label=f"pose {i+1}"):
                continue
            time.sleep(SETTLE_TIME)
            self._wait_for_cartesian_pose()
            if self._he_capture(self.cartesian_pose):
                captures += 1

        self.get_logger().info(f"\nHand-eye: {captures} captures")
        self._he_calibrate()

    # ==========================================================================
    # Entry point
    # ==========================================================================

    def run(self):
        self.wait_for_robot()

        if self.mode == "infield":
            self.run_infield()
        elif self.mode == "hand_eye":
            self.run_hand_eye()


def main():
    rclpy.init()
    node = TaskRunner()
    try:
        node.run()
    except KeyboardInterrupt:
        node.get_logger().info("Interrupted")
    except Exception as e:
        node.get_logger().error(f"Task failed: {e}")
        raise
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()