from setuptools import setup
import os
from glob import glob

package_name = 'robot_tasks'

setup(
    name=package_name,
    version='0.1.0',
    packages=[package_name],
    data_files=[
        ('share/ament_index/resource_index/packages',
            ['resource/' + package_name]),
        ('share/' + package_name, ['package.xml']),
        (os.path.join('share', package_name, 'config'),
            glob('config/*.yaml')),
    ],
    install_requires=['setuptools'],
    zip_safe=True,
    entry_points={
        'console_scripts': [
            'task_runner = robot_tasks.task_runner:main',    
            'capture_runner = robot_tasks.capture_runner:main',
            'path_planning = robot_tasks.path_planner:main',
        ],
    },
)
