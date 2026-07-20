import trimesh
import numpy as np
import sys
import json

def load_and_inspect_cad(file_path):
    try:
        mesh = trimesh.load(file_path)
        print(f"--- CAD Mesh Info ---")
        print(f"Vertices: {len(mesh.vertices)}")
        print(f"Faces: {len(mesh.faces)}")
        return mesh
    except Exception as e:
        print(f"Error loading CAD file: {e}")
        sys.exit(1)

def generate_raster_path(mesh, step_size=30.0):
    print(f"\n--- Generating Path (Resolution: {step_size}mm) ---")
    
    # Get the outer bounds of the mesh [[min_x, min_y, min_z], [max_x, max_y, max_z]]
    bounds = mesh.bounds
    min_x, min_y, _ = bounds[0]
    max_x, max_y, max_z = bounds[1]

    # Generate grid lines for X and Y
    x_coords = np.arange(min_x + (step_size/2), max_x, step_size)
    y_coords = np.arange(min_y + (step_size/2), max_y, step_size)

    ray_origins = []
    ray_directions = []

    # Create a zig-zag pattern to minimize robot travel time
    for i, x in enumerate(x_coords):
        # Reverse Y direction every other column
        current_y_coords = y_coords[::-1] if i % 2 == 1 else y_coords
        for y in current_y_coords:
            # Position rays slightly above the highest point of the CAD model pointing down
            ray_origins.append([x, y, max_z + 10.0])
            ray_directions.append([0.0, 0.0, -1.0])

    # Cast the rays onto the mesh
    locations, index_ray, index_tri = mesh.ray.intersects_location(
        ray_origins=ray_origins,
        ray_directions=ray_directions
    )

    waypoints = []
    # Loop through hits, match them with the surface normal at that exact triangle face
    for loc, tri_idx in zip(locations, index_tri):
        normal = mesh.face_normals[tri_idx]
        waypoints.append({
            "position": loc.tolist(),
            "normal": normal.tolist()
        })

    print(f"Generated {len(waypoints)} target waypoints on the surface.")
    
    # Save the planned path to a JSON file
    output_path = "/root/cad_path_planner/planned_path.json"
    with open(output_path, 'w') as f:
        json.dump(waypoints, f, indent=4)
    print(f"Path saved to: {output_path}")
    
    return waypoints

if __name__ == "__main__":
    cad_file_path = "/root/cad_path_planner/part.STL" 
    mesh = load_and_inspect_cad(cad_file_path)
    
    # Step size of 30mm for a fast initial test on your 640mm part
    generate_raster_path(mesh, step_size=30.0)