import bpy
import math
import os
import random
from mathutils import Vector


PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE_PATH = os.path.join(PROJECT_ROOT, "ArtSource", "Blender", "NaturalTreeVariants.blend")
MODEL_ROOT = os.path.join(PROJECT_ROOT, "Assets", "Models", "Environment", "NaturalTreeVariants")
PREVIEW_PATH = os.path.join(PROJECT_ROOT, "docs", "Art", "EnvironmentModels", "NaturalTreeVariants_Preview.png")
TOP_PREVIEW_PATH = os.path.join(
    PROJECT_ROOT,
    "docs",
    "Art",
    "EnvironmentModels",
    "NaturalTreeVariants_TopPreview.png",
)


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)
    root = bpy.data.collections.new("NaturalTreeVariants")
    bpy.context.scene.collection.children.link(root)
    return root


def make_material(name, color, roughness):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    shader = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    return material


def smooth_shade(obj):
    for polygon in obj.data.polygons:
        polygon.use_smooth = True


def link_only(obj, collection):
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    collection.objects.link(obj)


def add_tapered_tube(name, points, radii, collection, material, sides=14):
    point_vectors = [Vector(point) for point in points]
    vertices = []
    for index, point in enumerate(point_vectors):
        if index == 0:
            tangent = (point_vectors[1] - point).normalized()
        elif index == len(point_vectors) - 1:
            tangent = (point - point_vectors[index - 1]).normalized()
        else:
            tangent = (point_vectors[index + 1] - point_vectors[index - 1]).normalized()
        reference = Vector((0.0, 0.0, 1.0))
        if abs(tangent.dot(reference)) > 0.92:
            reference = Vector((0.0, 1.0, 0.0))
        side = tangent.cross(reference).normalized()
        up = side.cross(tangent).normalized()
        for segment in range(sides):
            angle = math.tau * segment / sides
            offset = math.cos(angle) * side + math.sin(angle) * up
            vertices.append(tuple(point + offset * radii[index]))
    faces = []
    for ring in range(len(point_vectors) - 1):
        for segment in range(sides):
            next_segment = (segment + 1) % sides
            lower = ring * sides + segment
            lower_next = ring * sides + next_segment
            upper = (ring + 1) * sides + segment
            upper_next = (ring + 1) * sides + next_segment
            faces.append((lower, lower_next, upper_next, upper))
    faces.append(tuple(range(sides - 1, -1, -1)))
    top_start = (len(point_vectors) - 1) * sides
    faces.append(tuple(top_start + segment for segment in range(sides)))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    smooth_shade(obj)
    return obj


def add_branch(name, start, end, radius_start, radius_end, collection, material):
    start_vector = Vector(start)
    end_vector = Vector(end)
    direction = end_vector - start_vector
    bpy.ops.mesh.primitive_cone_add(
        vertices=12,
        radius1=radius_start,
        radius2=radius_end,
        depth=direction.length,
        location=(start_vector + end_vector) * 0.5,
    )
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    obj.data.materials.append(material)
    smooth_shade(obj)
    link_only(obj, collection)
    return obj


def add_foliage_cluster(name, location, scale, rotation, collection, material, seed):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    rng = random.Random(seed)
    phase = rng.uniform(0.0, math.tau)
    for vertex in obj.data.vertices:
        direction = vertex.co.normalized()
        deformation = 1.0 + 0.055 * (
            math.sin(direction.x * 3.4 + phase)
            + math.sin(direction.y * 4.1 - phase * 0.6)
            + math.sin(direction.z * 3.0 + phase * 0.35)
        ) / 3.0
        vertex.co = direction * deformation
    obj.scale = scale
    obj.rotation_euler = rotation
    obj.data.materials.append(material)
    smooth_shade(obj)
    link_only(obj, collection)
    return obj


def merge_wood_parts(parts, material):
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    trunk = parts[0]
    bpy.context.view_layer.objects.active = trunk
    bpy.ops.object.join()
    trunk.name = "Trunk"
    trunk.data.name = "TrunkMesh"
    trunk.data.materials.clear()
    trunk.data.materials.append(material)
    for polygon in trunk.data.polygons:
        polygon.material_index = 0

    remesh = trunk.modifiers.new("Merge wood intersections", "REMESH")
    remesh.mode = "VOXEL"
    remesh.voxel_size = 0.035
    remesh.adaptivity = 0.0
    remesh.use_remove_disconnected = False
    remesh.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    smooth_shade(trunk)
    return trunk


def trunk_point(spec, fraction):
    return (
        spec["lean"] * fraction,
        spec["curve_y"] * fraction,
        spec["trunk_height"] * fraction,
    )


def create_tree(spec, root, materials):
    collection = bpy.data.collections.new(f"NaturalTree_{spec['index']:02d}_{spec['label']}")
    root.children.link(collection)
    height = spec["trunk_height"]
    base_radius = spec["trunk_radius"]
    trunk_points = (
        (0.0, 0.0, 0.0),
        trunk_point(spec, 0.34),
        trunk_point(spec, 0.68),
        trunk_point(spec, 1.0),
    )
    wood_parts = [add_tapered_tube(
        "Trunk",
        trunk_points,
        (base_radius, base_radius * 0.78, base_radius * 0.5, base_radius * 0.26),
        collection,
        materials["bark"],
    )]
    for index, branch in enumerate(spec["branches"]):
        side, start_fraction, length, forward, rise = branch
        start = Vector(trunk_point(spec, start_fraction))
        end = start + Vector((side * length * 0.82, forward, rise + 0.08))
        wood_parts.append(add_branch(
            f"Branch_{index:02d}",
            start,
            end,
            base_radius * (0.34 if index == 0 else 0.29),
            base_radius * 0.09,
            collection,
            materials["bark"],
        ))
    root_rng = random.Random(3000 + spec["index"])
    for index in range(3):
        angle = spec["root_phase"] + math.tau * index / 3 + root_rng.uniform(-0.12, 0.12)
        reach = root_rng.uniform(0.28, 0.36)
        wood_parts.append(add_branch(
            f"Root_{index:02d}",
            (0.0, 0.0, 0.1),
            (math.cos(angle) * reach, math.sin(angle) * reach, 0.025),
            base_radius * 0.42,
            0.02,
            collection,
            materials["bark_dark"],
        ))
    merge_wood_parts(wood_parts, materials["bark"])
    offsets = (
        (0.0, 0.0, 0.14),
        (-0.34, 0.03, 0.09),
        (0.34, -0.03, 0.08),
        (-0.18, -0.23, -0.04),
        (0.18, 0.23, -0.03),
        (-0.42, 0.14, -0.17),
        (0.42, -0.13, -0.16),
        (0.0, -0.18, -0.27),
        (0.03, 0.18, 0.33),
    )
    crown_rng = random.Random(7000 + spec["index"])
    center = Vector(spec["crown_center"])
    crown_radius = Vector(spec["crown_radius"])
    for index in range(spec["cluster_count"]):
        offset = Vector(offsets[index])
        offset.x += spec["asymmetry"] * (0.35 + index * 0.04)
        offset += Vector((
            crown_rng.uniform(-0.035, 0.035),
            crown_rng.uniform(-0.035, 0.035),
            crown_rng.uniform(-0.035, 0.035),
        ))
        location = center + Vector((
            offset.x * crown_radius.x,
            offset.y * crown_radius.y,
            offset.z * crown_radius.z,
        ))
        size = crown_rng.uniform(0.92, 1.08)
        scale = (
            crown_radius.x * 0.6 * size,
            crown_radius.y * 0.72 * crown_rng.uniform(0.94, 1.06),
            crown_radius.z * 0.68 * crown_rng.uniform(0.94, 1.06),
        )
        rotation = (
            crown_rng.uniform(-0.14, 0.14),
            crown_rng.uniform(-0.14, 0.14),
            crown_rng.uniform(-0.28, 0.28),
        )
        material = materials["leaf_light"] if index == 8 else materials["leaf"]
        add_foliage_cluster(
            f"Foliage_{index:02d}",
            location,
            scale,
            rotation,
            collection,
            material,
            9000 + spec["index"] * 20 + index,
        )
    return collection


def create_variants(root, materials):
    presets = (
        ("Round", 1.76, 0.245, 0.0, 0.015, (0.0, 0.0, 2.25), (1.0, 0.9, 0.78), 8, 0.0, 0.1, ((-1, 0.56, 0.54, 0.05, 0.55), (1, 0.67, 0.48, -0.03, 0.48))),
        ("Wide", 1.72, 0.25, 0.02, -0.01, (0.02, 0.0, 2.22), (1.12, 0.96, 0.7), 8, 0.0, 0.45, ((-1, 0.53, 0.62, 0.04, 0.52), (1, 0.61, 0.61, -0.02, 0.5))),
        ("Tall", 1.88, 0.235, -0.015, 0.02, (-0.02, 0.0, 2.42), (0.9, 0.82, 0.9), 8, 0.0, 0.8, ((-1, 0.58, 0.5, 0.08, 0.62), (1, 0.7, 0.45, -0.03, 0.56))),
        ("LeftLean", 1.78, 0.25, -0.09, 0.015, (-0.13, 0.01, 2.28), (1.02, 0.9, 0.78), 8, -0.025, 1.1, ((-1, 0.54, 0.58, 0.04, 0.52), (1, 0.66, 0.46, -0.04, 0.5))),
        ("RightLean", 1.8, 0.24, 0.09, -0.01, (0.14, 0.0, 2.3), (1.03, 0.9, 0.77), 8, 0.025, 1.4, ((1, 0.55, 0.58, -0.02, 0.54), (-1, 0.68, 0.46, 0.06, 0.5))),
        ("Dense", 1.82, 0.255, 0.025, 0.02, (0.02, 0.0, 2.34), (1.05, 0.94, 0.84), 9, 0.0, 1.75, ((-1, 0.53, 0.55, 0.05, 0.58), (1, 0.62, 0.56, -0.05, 0.55), (-1, 0.73, 0.38, 0.12, 0.42))),
        ("Open", 1.83, 0.24, -0.02, -0.015, (-0.01, 0.0, 2.34), (1.08, 0.95, 0.78), 7, 0.0, 2.05, ((-1, 0.5, 0.64, 0.08, 0.57), (1, 0.63, 0.62, -0.04, 0.53), (1, 0.74, 0.36, 0.1, 0.4))),
        ("Compact", 1.58, 0.225, 0.035, 0.01, (0.04, 0.0, 2.02), (0.88, 0.8, 0.72), 8, 0.0, 2.4, ((-1, 0.57, 0.48, 0.04, 0.48), (1, 0.68, 0.42, -0.03, 0.44))),
        ("Mature", 1.92, 0.27, -0.025, 0.02, (-0.03, 0.0, 2.42), (1.18, 1.02, 0.84), 9, 0.0, 2.75, ((-1, 0.5, 0.68, 0.08, 0.62), (1, 0.58, 0.68, -0.06, 0.6), (-1, 0.7, 0.46, 0.14, 0.48))),
        ("Asymmetric", 1.8, 0.25, 0.05, -0.02, (0.08, 0.0, 2.3), (1.06, 0.93, 0.8), 8, 0.075, 3.05, ((1, 0.52, 0.66, -0.06, 0.56), (-1, 0.65, 0.5, 0.08, 0.54), (1, 0.74, 0.38, 0.1, 0.42))),
    )
    collections = []
    for index, preset in enumerate(presets, start=1):
        label, trunk_height, trunk_radius, lean, curve_y, crown_center, crown_radius, cluster_count, asymmetry, root_phase, branches = preset
        spec = {
            "index": index,
            "label": label,
            "trunk_height": trunk_height,
            "trunk_radius": trunk_radius,
            "lean": lean,
            "curve_y": curve_y,
            "crown_center": crown_center,
            "crown_radius": crown_radius,
            "cluster_count": cluster_count,
            "asymmetry": asymmetry,
            "root_phase": root_phase,
            "branches": branches,
        }
        collections.append(create_tree(spec, root, materials))
    return collections


def setup_preview(root, collections, materials):
    preview = bpy.data.collections.new("Preview")
    root.children.link(preview)
    bpy.ops.mesh.primitive_plane_add(size=22, location=(0.0, 0.0, -0.02))
    ground = bpy.context.object
    ground.name = "PreviewGround"
    ground.data.materials.append(materials["ground"])
    link_only(ground, preview)
    positions = [(-5.2 + column * 2.6, 1.2 - row * 3.55, 0.0) for row in range(2) for column in range(5)]
    for collection, offset in zip(collections, positions):
        for obj in collection.all_objects:
            obj.location += Vector(offset)
    world = bpy.context.scene.world or bpy.data.worlds.new("NaturalTreeWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.035, 0.052, 0.065, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.52
    bpy.ops.object.light_add(type="AREA", location=(-4.0, -5.5, 10.0))
    key = bpy.context.object
    key.data.energy = 1150
    key.data.size = 5.5
    key.rotation_euler = (Vector((0.0, 0.0, 1.7)) - key.location).to_track_quat("-Z", "Y").to_euler()
    link_only(key, preview)
    bpy.ops.object.light_add(type="AREA", location=(6.0, 2.0, 6.5))
    fill = bpy.context.object
    fill.data.energy = 650
    fill.data.size = 4.0
    fill.rotation_euler = (Vector((0.0, 0.0, 1.8)) - fill.location).to_track_quat("-Z", "Y").to_euler()
    link_only(fill, preview)
    bpy.ops.object.camera_add(location=(0.0, -20.5, 8.9))
    camera = bpy.context.object
    camera.data.lens = 52
    camera.rotation_euler = (Vector((0.0, 0.0, 1.45)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    link_only(camera, preview)
    scene = bpy.context.scene
    scene.camera = camera
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    os.makedirs(os.path.dirname(PREVIEW_PATH), exist_ok=True)
    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)
    for collection, offset in zip(collections, positions):
        for obj in collection.all_objects:
            obj.location -= Vector(offset)


def render_top_preview(collections):
    preview = bpy.data.collections["Preview"]
    ground = bpy.data.objects["PreviewGround"]
    ground.hide_render = True
    positions = [(-5.2 + column * 2.6, 1.65 - row * 3.3, 0.0) for row in range(2) for column in range(5)]
    for collection, offset in zip(collections, positions):
        for obj in collection.all_objects:
            obj.location += Vector(offset)

    bpy.ops.object.camera_add(location=(0.0, 0.0, 22.0))
    camera = bpy.context.object
    camera.name = "TopPreviewCamera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 13.5
    camera.rotation_euler = (Vector((0.0, 0.0, 0.0)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    link_only(camera, preview)

    scene = bpy.context.scene
    scene.camera = camera
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 800
    scene.render.filepath = TOP_PREVIEW_PATH
    bpy.ops.render.render(write_still=True)

    for collection, offset in zip(collections, positions):
        for obj in collection.all_objects:
            obj.location -= Vector(offset)
    ground.hide_render = False


def export_models(collections):
    os.makedirs(MODEL_ROOT, exist_ok=True)
    for collection in collections:
        bpy.ops.object.select_all(action="DESELECT")
        objects = list(collection.all_objects)
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(MODEL_ROOT, f"{collection.name}.fbx"),
            use_selection=True,
            object_types={"MESH"},
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_ALL",
            axis_forward="-Z",
            axis_up="Y",
            add_leaf_bones=False,
            bake_anim=False,
            path_mode="AUTO",
        )


def main():
    os.makedirs(os.path.dirname(SOURCE_PATH), exist_ok=True)
    root = reset_scene()
    materials = {
        "bark": make_material("Natural Tree Bark", (0.25, 0.11, 0.045), 0.94),
        "bark_dark": make_material("Natural Tree Bark Dark", (0.15, 0.055, 0.02), 0.97),
        "leaf": make_material("Natural Tree Leaf", (0.12, 0.38, 0.14), 0.88),
        "leaf_light": make_material("Natural Tree Leaf Light", (0.15, 0.42, 0.17), 0.86),
        "ground": make_material("Natural Tree Preview Ground", (0.075, 0.105, 0.095), 1.0),
    }
    collections = create_variants(root, materials)
    export_models(collections)
    setup_preview(root, collections, materials)
    render_top_preview(collections)
    bpy.data.collections["Preview"].hide_render = True
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE_PATH)


if __name__ == "__main__":
    main()
