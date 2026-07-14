# BAE System Bringup

## Prerequisites
- Docker
- nvidia-container-toolkit
- SSH key added to your GitHub account with access to the UoS-EEE-FENDER organisation

## Quick Start
```bash
cd docker
./run.sh        # first run builds and creates the container
./run.sh        # subsequent runs attach to the existing container
```