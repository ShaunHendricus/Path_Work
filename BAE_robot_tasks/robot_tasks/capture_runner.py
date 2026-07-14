#!/usr/bin/env python3
"""
capture_runner.py

Moves the robot through a sequence of poses and captures a Zivid point cloud
at each one, saving the frame as a .zdf file and the robot pose as JSON.

Usage:
  ros2 run robot_tasks capture_runner
"""

import time
import json
import os

import rclpy
from zivid_interfaces.srv import CaptureAndSave

from robot_tasks.robot_base import RobotBase, SETTLE_TIME


SAVE_DIR = os.path.expanduser("~/capture_data")


class CaptureRunner(RobotBase):

    def __init__(self):
        super().__init__('capture_runner', poses_yaml='capture_poses.yaml', scenario='capture')

        self.capture_client = self.create_client(
            CaptureAndSave,
            '/capture_and_save'
        )

        os.makedirs(SAVE_DIR, exist_ok=True)
        self.get_logger().info(f"Saving captures to: {SAVE_DIR}")

        self.load_poses()
    
    # ==========================================================================
    # Capture + save
    # ==========================================================================

    def _trigger_capture(self, index: int) -> bool:
        """
        Call CaptureAndSave service. Saves the frame as a .zdf file.
        Returns True on success.
        """
        file_path = os.path.join(SAVE_DIR, f"capture_{index:03d}.zdf")

        future = self.capture_client.call_async(
            CaptureAndSave.Request(file_path=file_path)
        )
        rclpy.spin_until_future_complete(self, future)
        result = future.result()

        if result.success:
            self.get_logger().info(f"  ✓ Saved frame to {file_path}")
        else:
            self.get_logger().warning(f"  ✗ Capture failed: {result.message}")

        return result.success

    def _save_robot_pose(self, index: int):
        """Save current cartesian pose to JSON alongside the frame."""
        pose = self.cartesian_pose
        pose_data = {
            "position": {
                "x": pose.position.x,
                "y": pose.position.y,
                "z": pose.position.z,
            },
            "orientation": {
                "x": pose.orientation.x,
                "y": pose.orientation.y,
                "z": pose.orientation.z,
                "w": pose.orientation.w,
            }
        }
        path = os.path.join(SAVE_DIR, f"pose_{index:03d}.json")
        with open(path, 'w') as f:
            json.dump(pose_data, f, indent=2)
        self.get_logger().info(f"  ✓ Saved pose to {path}")

    # ==========================================================================
    # Entry point
    # ==========================================================================

    def run(self):
        self._wait_for_services([self.capture_client], label="capture")
        self.load_camera_settings()
        self.wait_for_robot()

        successful = 0
        for i, pose in enumerate(self.poses):
            self.get_logger().info(f"\n--- Pose {i+1}/{len(self.poses)} ---")

            if not self.move_to(pose, label=f"pose {i+1}"):
                continue

            time.sleep(SETTLE_TIME)
            self._wait_for_cartesian_pose()

            if not self._trigger_capture(i):
                continue

            self._save_robot_pose(i)
            successful += 1

        self.get_logger().info(
            f"\nDone — {successful}/{len(self.poses)} captures saved to {SAVE_DIR}"
        )


def main():
    rclpy.init()
    node = CaptureRunner()
    try:
        node.run()
    except KeyboardInterrupt:
        node.get_logger().info("Interrupted")
    except Exception as e:
        node.get_logger().error(f"Capture failed: {e}")
        raise
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()