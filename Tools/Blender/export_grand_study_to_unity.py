"""Export every GrandStudy asset collection as an independent Unity FBX."""
import json
import os
import re
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/Blender/GrandStudy.blend"
MODEL_ROOT = ROOT / "Assets/Models/GrandStudy/Models"
TEXTURE_ROOT = ROOT / "Assets/Models/GrandStudy/Textures"
REPORT_ROOT = ROOT / "docs/Art/GrandStudy"
PROCEDURAL_MATERIALS = {
    "Walnut",
    "Dark wood",
    "Leather",
    "Back leather",
    "Red velvet",
    "Curtain blue",
    "Brass",
    "Brocade",
    "Piping",
}


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
    if any(slot.material and slot.material.name in PROCEDURAL_MATERIALS for slot in duplicate.material_slots):
        ensure_uv(duplicate)
    return duplicate


def ensure_uv(obj):
    mesh = obj.data
    if mesh.uv_layers:
        return
    uv_layer = mesh.uv_layers.new(name="GrandStudy UV")
    coordinates = [vertex.co for vertex in mesh.vertices]
    minimum = Vector((min(co.x for co in coordinates), min(co.y for co in coordinates), min(co.z for co in coordinates)))
    maximum = Vector((max(co.x for co in coordinates), max(co.y for co in coordinates), max(co.z for co in coordinates)))
    size = maximum - minimum
    projection_axis = min(range(3), key=lambda index: size[index])
    first, second = ((1, 2), (0, 2), (0, 1))[projection_axis]
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            coordinate = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            u = (coordinate[first] - minimum[first]) / max(size[first], 1e-6)
            v = (coordinate[second] - minimum[second]) / max(size[second], 1e-6)
            uv_layer.data[loop_index].uv = (u, v)


def texture_stem(material_name):
    return re.sub(r"[^a-z0-9]+", "_", material_name.lower()).strip("_")


def bake_input(material, input_name, image_name, color_space):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    bpy.ops.mesh.primitive_plane_add(size=2)
    plane = bpy.context.object
    if material.name == "Brocade":
        for vertex in plane.data.vertices:
            vertex.co = (vertex.co.x, 0, vertex.co.y)
    temporary = material.copy()
    temporary.name = f"__BAKE_{material.name}_{input_name}__"
    plane.data.materials.append(temporary)
    nodes = temporary.node_tree.nodes
    links = temporary.node_tree.links
    principled = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    material_output = next(node for node in nodes if node.type == "OUTPUT_MATERIAL" and node.is_active_output)
    source_input = principled.inputs[input_name]
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Strength"].default_value = 1
    if source_input.is_linked:
        source_link = source_input.links[0]
        links.new(source_link.from_socket, emission.inputs["Color"])
    elif input_name == "Base Color":
        emission.inputs["Color"].default_value = source_input.default_value
    else:
        value = float(source_input.default_value)
        emission.inputs["Color"].default_value = (value, value, value, 1)
    for link in list(material_output.inputs["Surface"].links):
        links.remove(link)
    links.new(emission.outputs["Emission"], material_output.inputs["Surface"])

    image = bpy.data.images.new(image_name, width=512, height=512, alpha=True)
    image.colorspace_settings.name = color_space
    image_node = nodes.new("ShaderNodeTexImage")
    image_node.image = image
    nodes.active = image_node
    image_node.select = True
    bpy.ops.object.select_all(action="DESELECT")
    plane.select_set(True)
    bpy.context.view_layer.objects.active = plane
    bpy.ops.object.bake(type="EMIT", margin=4, use_clear=True)
    bpy.data.objects.remove(plane, do_unlink=True)
    bpy.data.materials.remove(temporary)
    return image


def bake_material_textures():
    TEXTURE_ROOT.mkdir(parents=True, exist_ok=True)
    records = {}
    original_scene = bpy.context.window.scene
    bake_scene = bpy.data.scenes.new("__GRAND_STUDY_MATERIAL_BAKE__")
    bpy.context.window.scene = bake_scene
    try:
        for name in sorted(PROCEDURAL_MATERIALS):
            material = bpy.data.materials.get(name)
            if material is None or not material.use_nodes:
                continue
            principled = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
            if principled is None:
                continue
            stem = texture_stem(name)
            base_path = TEXTURE_ROOT / f"grandstudy_{stem}_base.png"
            mask_path = TEXTURE_ROOT / f"grandstudy_{stem}_mask.png"
            base_image = bake_input(material, "Base Color", f"{stem}_base", "sRGB")
            base_image.filepath_raw = str(base_path)
            base_image.file_format = "PNG"
            base_image.save()

            roughness_image = bake_input(material, "Roughness", f"{stem}_roughness", "Non-Color")
            roughness_pixels = list(roughness_image.pixels)
            metallic = float(principled.inputs["Metallic"].default_value)
            mask_image = bpy.data.images.new(f"{stem}_mask", width=512, height=512, alpha=True)
            mask_image.colorspace_settings.name = "Non-Color"
            mask_pixels = []
            for index in range(0, len(roughness_pixels), 4):
                smoothness = 1.0 - roughness_pixels[index]
                mask_pixels.extend((metallic, 0.0, 0.0, smoothness))
            mask_image.pixels = mask_pixels
            mask_image.filepath_raw = str(mask_path)
            mask_image.file_format = "PNG"
            mask_image.save()
            records[name] = {
                "base_texture": str(base_path.relative_to(ROOT)),
                "mask_texture": str(mask_path.relative_to(ROOT)),
            }
            bpy.data.images.remove(base_image)
            bpy.data.images.remove(roughness_image)
            bpy.data.images.remove(mask_image)
            print(f"BAKED {name}", flush=True)
    finally:
        bpy.context.window.scene = original_scene
        bpy.data.scenes.remove(bake_scene)
    return records


def bounds(objects):
    points = []
    for obj in objects:
        points.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    if not points:
        raise RuntimeError(f"Collection has no exportable geometry: {objects}")
    return [min(point[index] for point in points) for index in range(3)], [max(point[index] for point in points) for index in range(3)]


def triangles(objects):
    return sum(len(poly.vertices) - 2 for obj in objects for poly in obj.data.polygons)


def material_record(material, baked_textures):
    base_color = list(material.diffuse_color)
    metallic = 0.0
    roughness = 0.5
    alpha = base_color[3]
    if material.use_nodes:
        principled = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
        if principled:
            base_color = list(principled.inputs["Base Color"].default_value)
            metallic = float(principled.inputs["Metallic"].default_value)
            roughness = float(principled.inputs["Roughness"].default_value)
            alpha = float(principled.inputs["Alpha"].default_value)
    return {
        "name": material.name,
        "base_color": [float(value) for value in base_color],
        "metallic": metallic,
        "roughness": roughness,
        "alpha": alpha,
        **baked_textures.get(material.name, {}),
    }


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
    TEXTURE_ROOT.mkdir(parents=True, exist_ok=True)
    REPORT_ROOT.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    script_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    manifest_only = script_args[:1] == ["--manifest-only"]
    textures_only = script_args[:1] == ["--textures-only"]
    selected = set(script_args[1:]) if script_args[:1] == ["--assets"] else set()
    collections = sorted(
        (collection for collection in bpy.data.collections if collection.get("category") and not manifest_only and not textures_only and (not selected or collection.name in selected)),
        key=lambda item: item.name,
    )
    manifest_path = REPORT_ROOT / "unity_export_manifest.json"
    records_by_name = {}
    baked_textures = {}
    if (selected or manifest_only or textures_only) and manifest_path.exists():
        existing = json.loads(manifest_path.read_text())
        records_by_name = {record["name"]: record for record in existing["models"]}
        baked_textures = {
            material["name"]: {key: material[key] for key in ("base_texture", "mask_texture") if key in material}
            for material in existing.get("materials", [])
        }
    if not manifest_only and not selected:
        baked_textures = bake_material_textures()
    for collection in collections:
        output_path = MODEL_ROOT / f"{collection.name}.fbx"
        record = export_collection(collection, output_path)
        records_by_name[record["name"]] = record
        print(f"EXPORTED {collection.name} parts={record['parts']} triangles={record['triangles']}", flush=True)
    records = sorted(records_by_name.values(), key=lambda record: record["name"])
    if len(records) != 73:
        raise RuntimeError(f"Export manifest must contain 73 models, got {len(records)}")

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
        "materials": [material_record(material, baked_textures) for material in sorted(bpy.data.materials, key=lambda item: item.name)],
        "models": records,
        "room": room_data,
        "coordinate_mapping": "Unity (x,y,z) = Blender (x,z,-y); Unity rotationY = Blender rotationZ",
    }
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"WROTE {manifest_path} models={len(records)} room_instances={len(room_instances)}", flush=True)


if __name__ == "__main__":
    export_all()
