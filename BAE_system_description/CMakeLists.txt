cmake_minimum_required(VERSION 3.8)
project(bae_system_description)

if(CMAKE_COMPILER_IS_GNUCXX OR CMAKE_CXX_COMPILER_ID MATCHES "Clang")
  add_compile_options(-Wall -Wextra -Wpedantic)
endif()

find_package(ament_cmake REQUIRED)
find_package(xacro REQUIRED)

# Directories (data/config files) → share
install(
  DIRECTORY urdf launch meshes rviz
  DESTINATION share/${PROJECT_NAME}
)

# Executable scripts → lib  (this is what ros2 run and Node() look up)
install(PROGRAMS
  scripts/pointcloud_downsampler
  DESTINATION lib/${PROJECT_NAME}
)

if(BUILD_TESTING)
  find_package(ament_lint_auto REQUIRED)
  set(ament_cmake_copyright_FOUND TRUE)
  set(ament_cmake_cpplint_FOUND TRUE)
  ament_lint_auto_find_test_dependencies()
endif()

ament_package()
