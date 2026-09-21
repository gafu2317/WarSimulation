"""Export every GrandStudy asset collection as an independent Unity FBX."""
import json
import os
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/Blender/GrandStudy.blend"
MODEL_ROOT = ROOT / "Assets/Models/GrandStudy/Models"
REPORT_ROOT = ROOT / "docs/Art/GrandStudy"


def source_objects(collection):
    return [obj for obj in collection.all_objects if obj.type in {"MESH", "CURVE", "FONT"}]


def duplicate_for_export(obj, export_collection):
    duplicate = obj.copy()
    duplicate.data = obj.data.copy()
    duplicate.animation_data_clear()
    duplicate.parent = None
    duplicate.matrix_world = obj.matrix_world.copy()
    export_collection.objects.link(duplicate)
    bpy.ops.object.select_all(action="DESELECT")
    duplicate.select_set(True)
    bpy.context.view_layer.objects.active = duplicate
    if duplicate.type in {"CURVE", "FONT"}:
        bpy.ops.object.convert(target="MESH")
    return duplicate


def bounds(objects):
    points = []
    for obj in objects:
        points.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    if not points:
        raise RuntimeError(f"Collection has no exportable geometry: {objects}")
    return [min(point[index] for point in points) for index in range(3)], [max(point[index] for point in points) for index in range(3)]


def triangles(objects):
    return sum(len(poly.vertices) - 2 for obj in objects for poly in obj.data.polygons)


def export_collection(collection, output_path):
    sources = source_objects(collection)
    if not sources:
        raise RuntimeError(f"Collection has no exportable objects: {collection.name}")
    export_collection = bpy.data.collections.new("__GRAND_STUDY_FBX_EXPORT__")
    bpy.context.scene.collection.children.link(export_collection)
    duplicates = [duplicate_for_export(obj, export_collection) for obj in sources]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in duplicates:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = duplicates[0]
    bpy.ops.export_scene.fbx(
        filepath=str(output_path),
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
    minimum, maximum = bounds(duplicates)
    triangle_count = triangles(duplicates)
    materials = sorted({slot.material.name for obj in sources for slot in obj.material_slots if slot.material})
    for duplicate in duplicates:
        bpy.data.objects.remove(duplicate, do_unlink=True)
    bpy.data.collections.remove(export_collection)
    return {
        "name": collection.name,
        "asset_type": collection.get("category", "Objects"),
        "source": "ArtSource/Blender/GrandStudy.blend",
        "fbx": str(output_path.relative_to(ROOT)),
        "parts": len(sources),
        "triangles": triangle_count,
        "minimum": minimum,
        "maximum": maximum,
        "materials": materials,
    }


def instance_record(obj):
    collection = obj.instance_collection
    return {
        "name": obj.name,
        "asset": collection.name,
        "location": [float(obj.location.x), float(obj.location.y), float(obj.location.z)],
        "rotation_z": float(obj.rotation_euler.z),
        "scale": [float(value) for value in obj.scale],
    }


def export_all():
    MODEL_ROOT.mkdir(parents=True, exist_ok=True)
    REPORT_ROOT.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    collections = sorted((collection for collection in bpy.data.collections if collection.get("category")), key=lambda item: item.name)
    records = []
    for collection in collections:
        output_path = MODEL_ROOT / f"{collection.name}.fbx"
        record = export_collection(collection, output_path)
        records.append(record)
        print(f"EXPORTED {collection.name} parts={record['parts']} triangles={record['triangles']}", flush=True)

    room = bpy.data.collections.get("Room assembly")
    if room is None:
        raise RuntimeError("Room assembly collection is missing")
    room_instances = [obj for obj in room.objects if obj.instance_type == "COLLECTION" and obj.instance_collection]
    source_scene = bpy.data.scenes["01 Furnished Study"]
    camera = source_scene.camera
    room_data = {
        "instances": [instance_record(obj) for obj in room_instances],
        "camera": {
            "location": [float(value) for value in camera.location],
            "target": [float(value) for value in (camera.location + camera.rotation_euler.to_quaternion() @ Vector((0, 0, -1)))],
            "lens": float(camera.data.lens),
            "sensor_width": float(camera.data.sensor_width),
            "sensor_height": float(camera.data.sensor_height),
            "sensor_fit": camera.data.sensor_fit,
        },
    }
    manifest = {
        "blender": bpy.app.version_string,
        "source": "ArtSource/Blender/GrandStudy.blend",
        "models": records,
        "room": room_data,
        "coordinate_mapping": "Unity (x,y,z) = Blender (x,z,-y); Unity rotationY = Blender rotationZ",
    }
    path = REPORT_ROOT / "unity_export_manifest.json"
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"WROTE {path} models={len(records)} room_instances={len(room_instances)}", flush=True)


if __name__ == "__main__":
    export_all()
