import bpy
import glob
import os
from mathutils import Vector


PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MODEL_ROOT = os.path.join(PROJECT_ROOT, "Assets", "Models", "Environment")


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def world_bounds(objects):
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    minimum = tuple(min(point[axis] for point in points) for axis in range(3))
    maximum = tuple(max(point[axis] for point in points) for axis in range(3))
    return minimum, maximum


def validate_model(path):
    clear_scene()
    bpy.ops.import_scene.fbx(filepath=path)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    assert meshes, f"No mesh: {path}"
    names = [obj.name for obj in meshes]
    filename = os.path.basename(path)
    if "Tree" in filename:
        assert any(name.startswith("Trunk") for name in names), f"Missing Trunk: {filename}"
        assert any(name.startswith("Foliage") for name in names), f"Missing Foliage: {filename}"
    if "MagicStone" in filename:
        assert any(name.startswith("Pedestal") for name in names), f"Missing Pedestal: {filename}"
        core_prefix = "Core" if filename.startswith(("Artificial", "Refined", "ReferenceInspired")) else "Crystal"
        assert any(name.startswith(core_prefix) for name in names), f"Missing {core_prefix}: {filename}"
    minimum, maximum = world_bounds(meshes)
    assert minimum[2] <= 0.0 < maximum[2], f"Ground plane does not meet model: {filename}"
    dimensions = tuple(maximum[axis] - minimum[axis] for axis in range(3))
    print(f"PASS {filename}: meshes={len(meshes)} size={dimensions[0]:.2f}x{dimensions[1]:.2f}x{dimensions[2]:.2f}")


paths = sorted(glob.glob(os.path.join(MODEL_ROOT, "*", "*.fbx")))
assert len(paths) == 32, f"Expected 32 FBX files, found {len(paths)}"
for model_path in paths:
    validate_model(model_path)
print("PASS all environment models")
