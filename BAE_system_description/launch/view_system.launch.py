import os
from launch import LaunchDescription
from launch_ros.actions import Node
from launch.actions import DeclareLaunchArgument, OpaqueFunction
from launch.substitutions import LaunchConfiguration
from ament_index_python.packages import get_package_share_directory

def generate_launch_description():
    pkg_share = get_package_share_directory('bae_system_description')
    urdf_xacro = os.path.join(pkg_share, 'urdf', 'bae_system.urdf.xacro')
    rviz_config = os.path.join(pkg_share, 'rviz', 'view_system.rviz')

    robot_description = os.popen(
        f"xacro {urdf_xacro} robot_name:=lbr mode:=true"
    ).read()

    use_gui_arg = DeclareLaunchArgument(
        'use_gui',
        default_value='false'
    )

    # Voxel leaf size argument (metres) — tune this to balance density vs. performance
    voxel_leaf_size_arg = DeclareLaunchArgument(
        'voxel_leaf_size',
        default_value='0.005',           # 2 cm — good starting point; increase if still heavy
        description='Voxel grid leaf size in metres for point cloud downsampling'
    )

    # Topic arguments — adjust to match your actual sensor topic
    input_cloud_arg = DeclareLaunchArgument(
        'input_cloud_topic',
        default_value='/points/xyzrgba',
        description='Raw point cloud topic published by the sensor'
    )

    output_cloud_arg = DeclareLaunchArgument(
        'output_cloud_topic',
        default_value='/points_downsampled_world',
        description='Downsampled point cloud topic consumed by RViz'
    )

    def create_nodes(context):
        use_gui        = LaunchConfiguration('use_gui').perform(context).lower() == 'true'
        leaf_size      = LaunchConfiguration('voxel_leaf_size').perform(context)
        input_topic    = LaunchConfiguration('input_cloud_topic').perform(context)
        output_topic   = LaunchConfiguration('output_cloud_topic').perform(context)

        nodes = []

        nodes.append(Node(
            package='bae_system_description',
            executable='pointcloud_downsampler',
            name='voxel_grid_downsampler',
            parameters=[{
                'leaf_size':    float(leaf_size),
                'input_topic':  input_topic,
                'output_topic': output_topic,
                'target_frame': 'world',
            }],
            output='screen'
        ))


        # ── RVIZ ──────────────────────────────────────────────────────────────
        nodes.append(Node(
            package='rviz2',
            executable='rviz2',
            arguments=['-d', rviz_config],
            output='screen'
        ))

        # ── WORLD → TABLE (STATIC TF) ─────────────────────────────────────────
        nodes.append(Node(
            package='tf2_ros',
            executable='static_transform_publisher',
            arguments=["0.698", "0.0", "0.508", "0", "0", "0", "world", "table_link"]
        ))

        # ── WORLD → ROBOT BASE (STATIC TF) ───────────────────────────────────
        nodes.append(Node(
            package='tf2_ros',
            executable='static_transform_publisher',
            arguments=["0.420", "0.0", "1.016", "0", "0", "0", "world", "lbr_link_0"]
        ))

        if use_gui:
            nodes.append(Node(
                package='robot_state_publisher',
                executable='robot_state_publisher',
                parameters=[{'robot_description': robot_description}]
            ))
            nodes.append(Node(
                package='joint_state_publisher_gui',
                executable='joint_state_publisher_gui'
            ))
        else:
            nodes.append(Node(
                package='robot_state_publisher',
                executable='robot_state_publisher',
                parameters=[{'robot_description': robot_description}],
                remappings=[('joint_states', '/iiwa/joint_states')]
            ))

        return nodes

    return LaunchDescription([
        use_gui_arg,
        voxel_leaf_size_arg,
        input_cloud_arg,
        output_cloud_arg,
        OpaqueFunction(function=create_nodes)
    ])