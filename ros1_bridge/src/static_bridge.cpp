// Copyright 2015 Open Source Robotics Foundation, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#include <string>

// include ROS 1
#ifdef __clang__
# pragma clang diagnostic push
# pragma clang diagnostic ignored "-Wunused-parameter"
#endif
#include "ros/ros.h"
#ifdef __clang__
# pragma clang diagnostic pop
#endif

// include ROS 2
#include "rclcpp/rclcpp.hpp"

#include "ros1_bridge/bridge.hpp"


int main(int argc, char * argv[])
{
  // ROS 1 node
  ros::init(argc, argv, "ros_bridge");
  ros::NodeHandle ros1_node;

  // ROS 2 node
  rclcpp::init(argc, argv);
  auto ros2_node = rclcpp::Node::make_shared("ros_bridge");

  // bridge one example topic
  // std::string topic_name = "chatter";
  // std::string ros1_type_name = "std_msgs/String";
  // std::string ros2_type_name = "std_msgs/msg/String";

  std::vector<ros1_bridge::BridgeHandles> all_bridges;
  size_t queue_size = 10;

  //  Bridge: Joint States (ROS 1 -> ROS 2)
  all_bridges.push_back(ros1_bridge::create_bidirectional_bridge(
    ros1_node, ros2_node, 
    "sensor_msgs/JointState", 
    "sensor_msgs/msg/JointState", 
    "iiwa/joint_states", queue_size));

  //  Bridge: Cartesian Pose State Feedback (ROS 1 -> ROS 2)
  all_bridges.push_back(ros1_bridge::create_bidirectional_bridge(
    ros1_node, ros2_node, 
    "iiwa_msgs/CartesianPose", 
    "iiwa_msgs/msg/CartesianPose", 
    "iiwa/state/CartesianPose", queue_size));

  // Bridge: Command Cartesian Pose (ROS 2 -> ROS 1)
  all_bridges.push_back(ros1_bridge::create_bidirectional_bridge(
    ros1_node, ros2_node, 
    "geometry_msgs/PoseStamped", 
    "geometry_msgs/msg/PoseStamped", 
    "iiwa/command/CartesianPose", queue_size));

  // Bridge: Command Joint Position (ROS 2 -> ROS 1)
  all_bridges.push_back(ros1_bridge::create_bidirectional_bridge(
    ros1_node, ros2_node, 
    "iiwa_msgs/JointPosition", 
    "iiwa_msgs/msg/JointPosition", 
    "iiwa/command/JointPosition", queue_size));
    
  // auto handles = ros1_bridge::create_bidirectional_bridge(
  //   ros1_node, ros2_node, ros1_type_name, ros2_type_name, topic_name, queue_size);

  // ROS 1 asynchronous spinner
  ros::AsyncSpinner async_spinner(1);
  async_spinner.start();

  // ROS 2 spinning loop
  rclcpp::executors::SingleThreadedExecutor executor;
  while (ros1_node.ok() && rclcpp::ok()) {
    executor.spin_node_once(ros2_node, std::chrono::milliseconds(1000));
  }

  return 0;
}
