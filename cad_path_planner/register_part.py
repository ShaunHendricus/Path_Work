import open3d as o3d
import numpy as np
import json
import os

def register_cad_to_scan(cad_mesh_path, scan_pcd_path):
    print("--- Starting Part Registration ---")
    
    # 1. Load CAD mesh and sample it to create a source point cloud
    mesh = o3d.io.read_triangle_mesh(cad_mesh_path)
    cad_pcd = mesh.sample_points_uniformly(number_of_points=50000)
    print(f"Sampled {len(cad_pcd.points)} points from CAD model.")
    
    # 2. Load the Zivid Scan Point Cloud
    if os.path.exists(scan_pcd_path):
        scan_pcd = o3d.io.read_point_cloud(scan_pcd_path)
        print(f"Loaded physical scan with {len(scan_pcd.points)} points.")
    else:
        print(f"Scan file '{scan_pcd_path}' not found.")
        print("Generating a dummy shifted scan cloud for offline script validation...")
        # Create an artificial shifted cloud to test the math offline
        scan_pcd = o3d.geometry.PointCloud(cad_pcd)
        R = scan_pcd.get_rotation_matrix_from_xyz((0.05, 0.05, 0.0))
        scan_pcd.rotate(R, center=(0, 0, 0))
        scan_pcd.translate((10.0, 20.0, 5.0)) # Offset by 10mm, 20mm, 5mm

    # 3. Set ICP Parameters
    # threshold is the max search distance for matching points (in mm)
    threshold = 50.0 
    trans_init = np.identity(4) # Initial guess matrix

    print("Running Point-to-Point ICP registration...")
    registration_result = o3d.pipelines.registration.registration_icp(
        cad_pcd, scan_pcd, threshold, trans_init,
        o3d.pipelines.registration.TransformationEstimationPointToPoint()
    )

    transformation_matrix = registration_result.transformation
    
    print("\n--- Alignment Convergence Results ---")
    print(f"Fitness (overlapping ratio): {registration_result.fitness:.4f}")
    print(f"Inlier RMSE (Root Mean Squared Error): {registration_result.inlier_rmse:.4f} mm")
    print("\nCalculated 4x4 Transformation Matrix (CAD -> Camera):")
    print(transformation_matrix)
    
    # Save matrix for the ROS 2 task runner node
    matrix_output_path = "/root/cad_path_planner/registration_matrix.json"
    with open(matrix_output_path, 'w') as f:
        json.dump(transformation_matrix.tolist(), f, indent=4)
    print(f"\nMatrix successfully saved to: {matrix_output_path}")
    
    return transformation_matrix

if __name__ == "__main__":
    cad_path = "/root/cad_path_planner/part.STL"
    
    # This path will contain your actual Zivid capture later on
    scan_path = "/root/cad_path_planner/zivid_scan.pcd" 
    
    register_cad_to_scan(cad_path, scan_path)