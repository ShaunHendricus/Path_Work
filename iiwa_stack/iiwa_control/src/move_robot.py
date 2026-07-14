#!/usr/bin/env python
import rospy
import sys
import tty
import termios
from iiwa_msgs.msg import JointPosition, JointQuantity
from sensor_msgs.msg import JointState

# Global variables to store state
current_joints = [0.0] * 7
selected_joint = 0  # 0-6 corresponds to A1-A7

def get_key():
    fd = sys.stdin.fileno()
    old_settings = termios.tcgetattr(fd)
    try:
        tty.setraw(fd)
        ch = sys.stdin.read(1)
        if ch == '\x03':  # Ctrl+C
            raise KeyboardInterrupt
    finally:
        termios.tcsetattr(fd, termios.TCSADRAIN, old_settings)
    return ch

def joint_state_callback(msg):
    """Updates the current joint positions from the robot feedback."""
    global current_joints
    # Map standard ROS JointState (names/position list) to our list
    # Assuming the order is standard (A1 to A7)
    current_joints = list(msg.position)

def main():
    global selected_joint, current_joints
    
    # 1. Initialize ROS Node
    rospy.init_node('laptop_manual_jogger', anonymous=True)
    
    # 2. Create Publisher and Subscriber
    # We listen to what the robot IS doing
    sub = rospy.Subscriber('/iiwa/joint_states', JointState, joint_state_callback)
    
    # We tell the robot what TO DO
    pub = rospy.Publisher('/iiwa/command/JointPosition', JointPosition, queue_size=1)
    
    rate = rospy.Rate(10) # 10hz
    
    print("--- Laptop Manual Jogger ---")
    print("Controls:")
    print("  [1-7]: Select Joint A1-A7")
    print("  [w]:   Increase Angle (+)")
    print("  [s]:   Decrease Angle (-)")
    print("  [q]:   Quit")
    print("----------------------------")

    # Wait for first joint state
    print("Waiting for robot connection...")
    rospy.wait_for_message('/iiwa/joint_states', JointState)
    print("Robot connected! Starting control loop.")

    step_size = 0.05 # Radians (~2.8 degrees)

    while not rospy.is_shutdown():
        # Read key (blocking check for simplicity in this example)
        key = get_key()
        
        if key == 'q':
            break
        
        # Change Selection
        elif key in ['1','2','3','4','5','6','7']:
            selected_joint = int(key) - 1
            print(f"Selected Joint: A{selected_joint + 1}")
            continue
            
        # Modify Position
        target_joints = list(current_joints) # Copy current state
        
        if key == 'w':
            target_joints[selected_joint] += step_size
            print(f"Moving A{selected_joint+1} +")
        elif key == 's':
            target_joints[selected_joint] -= step_size
            print(f"Moving A{selected_joint+1} -")
        else:
            continue # Ignore other keys

        # 3. Build and Publish the Command
        cmd_msg = JointPosition()
        
        # Header MUST be stamped or some listeners ignore it
        cmd_msg.header.stamp = rospy.Time.now()
        cmd_msg.header.frame_id = "iiwa_link_0"
        
        # Populate the iiwa_msgs JointQuantity
        jq = JointQuantity()
        jq.a1 = target_joints[0]
        jq.a2 = target_joints[1]
        jq.a3 = target_joints[2]
        jq.a4 = target_joints[3]
        jq.a5 = target_joints[4]
        jq.a6 = target_joints[5]
        jq.a7 = target_joints[6]
        
        cmd_msg.position = jq
        
        pub.publish(cmd_msg)
        rate.sleep()

try:
    while not rospy.is_shutdown():
        key = get_key()
        ...
except KeyboardInterrupt:
    print("\nExiting cleanly...")