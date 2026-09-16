import json
import os

import bpy
from mathutils import Vector


PROJECT_ROOT = "/Users/fukutomi/Unity/WarSimulation"
SOURCE = os.path.join(PROJECT_ROOT, "ArtSource/Blender/FantasySchoolLevels.blend")
MODEL_ROOT = os.path.join(PROJECT_ROOT, "Assets/Models/Kingdom/City/Models")
REPORT_ROOT = os.path.join(PROJECT_ROOT, "docs/Art/SchoolLevelUnity")
SCHOOLS = ("WarriorAcademy", "ArcaneAcademy", "SpiritAcademy")


def mesh_objects(collection):
    return [obj for obj in collection.all_objects if obj.type == "MESH"]


def world_matrix_without_asset_root(obj):
    matrix = obj.matrix_basis.copy()
    parent = obj.parent
    while parent is not None and parent.get("asset_root") is not True:
        matrix = parent.matrix_basis @ matrix
        parent = parent.parent
    return matrix


def collection_bounds(objects):
    points = []
    for obj in objects:
        matrix = world_matrix_without_asset_root(obj)
        points.extend(matrix @ Vector(corner) for corner in obj.bound_box)
    minimum = [min(point[index] for point in points) for index in range(3)]
    maximum = [max(point[index] for point in points) for index in range(3)]
    return minimum, maximum


def triangle_count(objects):
    return sum(len(poly.vertices) - 2 for obj in objects for poly in obj.data.polygons)


def duplicate_for_export(obj, export_collection):
    duplicate = obj.copy()
    duplicate.data = obj.data.copy()
    duplicate.animation_data_clear()
    duplicate.parent = None
    duplicate.matrix_world = world_matrix_without_asset_root(obj)
    export_collection.objects.link(duplicate)
    return duplicate


def export_collection(collection, output_path):
    source_objects = mesh_objects(collection)
    if not source_objects:
        raise RuntimeError(f"Collection has no mesh objects: {collection.name}")
    export_collection = bpy.data.collections.new("__SCHOOL_FBX_EXPORT__")
    bpy.context.scene.collection.children.link(export_collection)
    duplicates = [duplicate_for_export(obj, export_collection) for obj in source_objects]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in duplicates:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = duplicates[0]
    bpy.ops.export_scene.fbx(
        filepath=output_path,
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True,
        bake_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="AUTO",
        use_custom_props=True,
    )
    for duplicate in duplicates:
        bpy.data.objects.remove(duplicate, do_unlink=True)
    bpy.data.collections.remove(export_collection)
    return source_objects


def export_all():
    os.makedirs(MODEL_ROOT, exist_ok=True)
    os.makedirs(REPORT_ROOT, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=SOURCE)
    records = []
    for school in SCHOOLS:
        for level in range(1, 6):
            name = f"{school}_Lv{level}"
            collection = bpy.data.collections.get(name)
            if collection is None:
                raise RuntimeError(f"Missing Blender collection: {name}")
            output_path = os.path.join(MODEL_ROOT, f"{name}.fbx")
            objects = export_collection(collection, output_path)
            minimum, maximum = collection_bounds(objects)
            materials = sorted(
                {
                    slot.material.name
                    for obj in objects
                    for slot in obj.material_slots
                    if slot.material is not None
                }
            )
            records.append(
                {
                    "name": name,
                    "asset_type": "building",
                    "source": "ArtSource/Blender/FantasySchoolLevels.blend",
                    "fbx": os.path.relpath(output_path, PROJECT_ROOT),
                    "parts": len(objects),
                    "triangles": triangle_count(objects),
                    "minimum": minimum,
                    "maximum": maximum,
                    "materials": materials,
                }
            )
            print(f"Exported {name}: {len(objects)} parts -> {output_path}", flush=True)
    manifest = {"blender": bpy.app.version_string, "plot_size_m": 26, "models": records}
    manifest_path = os.path.join(REPORT_ROOT, "export_manifest.json")
    with open(manifest_path, "w", encoding="utf-8") as handle:
        json.dump(manifest, handle, ensure_ascii=False, indent=2)
    print(f"Wrote {manifest_path}", flush=True)


if __name__ == "__main__":
    export_all()
