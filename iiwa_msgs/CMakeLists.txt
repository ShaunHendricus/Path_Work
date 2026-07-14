cmake_minimum_required(VERSION 3.8)
project(iiwa_msgs)

find_package(ament_cmake REQUIRED)
find_package(rosidl_default_generators REQUIRED)
find_package(action_msgs REQUIRED)
find_package(std_msgs REQUIRED)
find_package(geometry_msgs REQUIRED)

rosidl_generate_interfaces(${PROJECT_NAME}
  "msg/CartesianControlModeLimits.msg"
  "msg/CartesianEulerPose.msg"
  "msg/CartesianImpedanceControlMode.msg"
  "msg/CartesianPlane.msg"
  "msg/CartesianPose.msg"
  "msg/CartesianQuantity.msg"
  "msg/CartesianVelocity.msg"
  "msg/CartesianWrench.msg"
  "msg/ControlMode.msg"
  "msg/DesiredForceControlMode.msg"
  "msg/DOF.msg"
  "msg/JointDamping.msg"
  "msg/JointImpedanceControlMode.msg"
  "msg/JointPosition.msg"
  "msg/JointPositionVelocity.msg"
  "msg/JointQuantity.msg"
  "msg/JointStiffness.msg"
  "msg/JointTorque.msg"
  "msg/JointVelocity.msg"
  "msg/RedundancyInformation.msg"
  "msg/SinePatternControlMode.msg"
  "msg/Spline.msg"
  "msg/SplineSegment.msg"

  "srv/ConfigureControlMode.srv"
  "srv/SetEndpointFrame.srv"
  "srv/SetPTPCartesianSpeedLimits.srv"
  "srv/SetPTPJointSpeedLimits.srv"
  "srv/SetSmartServoJointSpeedLimits.srv"
  "srv/SetSmartServoLinSpeedLimits.srv"
  "srv/SetSpeedOverride.srv"
  "srv/SetWorkpiece.srv"
  "srv/TimeToDestination.srv"

  "action/MoveAlongSpline.action"
  "action/MoveToCartesianPose.action"
  "action/MoveToJointPosition.action"

  DEPENDENCIES builtin_interfaces action_msgs std_msgs geometry_msgs
)

install(
  FILES my_bridge_mapping.yaml
  DESTINATION share/${PROJECT_NAME}
)

ament_package()