import bpy
import math
import os
import random
from mathutils import Vector


PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE_PATH = os.path.join(PROJECT_ROOT, "ArtSource", "Blender", "NaturalRockVariants.blend")
MODEL_ROOT = os.path.join(PROJECT_ROOT, "Assets", "Models", "Environment", "NaturalRockVariants")
PREVIEW_PATH = os.path.join(
    PROJECT_ROOT,
    "docs",
    "Art",
    "EnvironmentModels",
    "NaturalRockVariants_Preview.png",
)
TOP_PREVIEW_PATH = os.path.join(
    PROJECT_ROOT,
    "docs",
    "Art",
    "EnvironmentModels",
    "NaturalRockVariants_TopPreview.png",
)


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)
    root = bpy.data.collections.new("NaturalRockVariants")
    bpy.context.scene.collection.children.link(root)
    return root


def make_material(name, color, roughness):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    shader = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = 0.0

    coordinates = nodes.new("ShaderNodeTexCoord")
    broad_noise = nodes.new("ShaderNodeTexNoise")
    broad_noise.noise_dimensions = "3D"
    broad_noise.inputs["Scale"].default_value = 3.2
    broad_noise.inputs["Detail"].default_value = 5.0
    broad_noise.inputs["Roughness"].default_value = 0.72
    color_ramp = nodes.new("ShaderNodeValToRGB")
    color_ramp.color_ramp.elements[0].position = 0.24
    color_ramp.color_ramp.elements[0].color = tuple(min(1.0, channel * 0.5) for channel in color) + (1.0,)
    color_ramp.color_ramp.elements[1].position = 0.78
    color_ramp.color_ramp.elements[1].color = tuple(min(1.0, channel * 1.42 + 0.025) for channel in color) + (1.0,)

    detail_noise = nodes.new("ShaderNodeTexNoise")
    detail_noise.noise_dimensions = "3D"
    detail_noise.inputs["Scale"].default_value = 22.0
    detail_noise.inputs["Detail"].default_value = 4.0
    detail_noise.inputs["Roughness"].default_value = 0.78
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.38
    bump.inputs["Distance"].default_value = 0.11

    links.new(coordinates.outputs["Generated"], broad_noise.inputs["Vector"])
    links.new(broad_noise.outputs["Fac"], color_ramp.inputs["Fac"])
    links.new(color_ramp.outputs["Color"], shader.inputs["Base Color"])
    links.new(coordinates.outputs["Generated"], detail_noise.inputs["Vector"])
    links.new(detail_noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    return material


def link_only(obj, collection):
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    collection.objects.link(obj)


def smooth_shade(obj):
    for polygon in obj.data.polygons:
        polygon.use_smooth = True


def make_noise_terms(seed, count=12):
    rng = random.Random(seed)
    terms = []
    for index in range(count):
        frequency = 1.15 + (index % 4) * 0.88 + rng.uniform(-0.18, 0.18)
        direction = Vector(
            (
                rng.uniform(-1.0, 1.0),
                rng.uniform(-1.0, 1.0),
                rng.uniform(-1.0, 1.0),
            )
        ).normalized()
        terms.append((direction, frequency, rng.uniform(0.0, math.tau), 1.0 / (1.0 + index * 0.34)))
    return terms


def layered_noise(point, terms):
    total = 0.0
    weight = 0.0
    for direction, frequency, phase, amplitude in terms:
        total += math.sin(point.dot(direction) * frequency * math.tau + phase) * amplitude
        weight += amplitude
    return total / weight


def add_rock(
    name,
    collection,
    material,
    scale,
    seed,
    location=(0.0, 0.0, 0.0),
    rotation=(0.0, 0.0, 0.0),
    roughness=0.16,
    flatten_bottom=0.58,
    flatten_top=0.0,
    top_slope=(0.0, 0.0),
    lean=(0.0, 0.0),
    taper_top=0.0,
    side_planes=0.0,
    strata=0.0,
    cracks=0.06,
    subdivisions=4,
):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1.0)
    obj = bpy.context.object
    obj.name = name
    terms = make_noise_terms(seed)
    fine_terms = make_noise_terms(seed + 491, count=8)
    crack_rng = random.Random(seed + 983)
    crack_terms = []
    for _ in range(3):
        normal = Vector(
            (
                crack_rng.uniform(-1.0, 1.0),
                crack_rng.uniform(-1.0, 1.0),
                crack_rng.uniform(-0.45, 0.75),
            )
        ).normalized()
        crack_terms.append(
            (
                normal,
                crack_rng.uniform(-0.42, 0.42),
                crack_rng.uniform(0.045, 0.09),
                crack_rng.uniform(0.65, 1.0),
            )
        )
    sx, sy, sz = scale

    for vertex in obj.data.vertices:
        unit = vertex.co.normalized()
        low = layered_noise(unit * 0.72, terms)
        fine = layered_noise(unit * 1.85, fine_terms)
        radial = 1.0 + roughness * low + roughness * 0.28 * fine
        for normal, offset, width, depth in crack_terms:
            distance = abs(unit.dot(normal) - offset)
            radial -= cracks * depth * math.exp(-((distance / width) ** 2))

        x = unit.x * sx * radial
        y = unit.y * sy * radial
        z = unit.z * sz * radial

        upper = max(0.0, unit.z)
        taper = max(0.38, 1.0 - taper_top * upper * upper)
        x *= taper
        y *= taper
        x += lean[0] * (unit.z + 1.0) * 0.5
        y += lean[1] * (unit.z + 1.0) * 0.5

        if side_planes > 0.0:
            x_limit = sx * (0.68 + 0.05 * low)
            y_limit = sy * (0.68 - 0.04 * low)
            if abs(x) > x_limit:
                x = math.copysign(x_limit + (abs(x) - x_limit) * (1.0 - side_planes), x)
            if abs(y) > y_limit:
                y = math.copysign(y_limit + (abs(y) - y_limit) * (1.0 - side_planes), y)

        if flatten_top > 0.0:
            top = sz * 0.62 + top_slope[0] * x + top_slope[1] * y
            if z > top:
                z = top + (z - top) * (1.0 - flatten_top)

        bottom = -sz * flatten_bottom
        if z < bottom:
            z = bottom + (z - bottom) * 0.08

        if strata > 0.0:
            band = math.sin((z / max(sz, 0.001) + 0.2 * low) * math.tau * 4.2)
            x *= 1.0 + strata * band
            y *= 1.0 + strata * band

        vertex.co = (x, y, z)

    obj.location = location
    obj.rotation_euler = rotation
    obj.data.materials.append(material)
    obj.data.update()
    smooth_shade(obj)
    link_only(obj, collection)
    return obj


def add_angular_block(name, collection, material, scale, seed, location=(0.0, 0.0, 0.0), rotation=(0.0, 0.0, 0.0)):
    return add_rock(
        name,
        collection,
        material,
        scale,
        seed,
        location=location,
        rotation=rotation,
        roughness=0.19,
        flatten_bottom=0.56,
        flatten_top=0.76,
        top_slope=(0.12, -0.07),
        side_planes=0.72,
        cracks=0.1,
    )


def collection_bounds(collection):
    objects = [obj for obj in collection.all_objects if obj.type == "MESH"]
    bpy.context.view_layer.update()
    points = [obj.matrix_world @ vertex.co for obj in objects for vertex in obj.data.vertices]
    minimum = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    return minimum, maximum


def center_on_ground(collection):
    minimum, maximum = collection_bounds(collection)
    offset = Vector((-(minimum.x + maximum.x) * 0.5, -(minimum.y + maximum.y) * 0.5, -minimum.z))
    for obj in collection.all_objects:
        if obj.parent is None:
            obj.location += offset


def new_variant(root, index, label):
    collection = bpy.data.collections.new(f"NaturalRock_{index:02d}_{label}")
    root.children.link(collection)
    return collection


def create_variants(root, materials):
    variants = []

    collection = new_variant(root, 1, "TallMonolith")
    add_rock(
        "Rock_Main",
        collection,
        materials["granite_dark"],
        (0.82, 0.68, 1.85),
        101,
        roughness=0.27,
        flatten_bottom=0.59,
        flatten_top=0.34,
        top_slope=(0.24, -0.06),
        lean=(0.18, -0.04),
        taper_top=0.5,
        side_planes=0.38,
        cracks=0.11,
    )
    add_rock("Rock_Base", collection, materials["granite"], (0.9, 0.7, 0.43), 102, location=(-0.28, 0.04, -0.78), roughness=0.21, cracks=0.08)
    center_on_ground(collection)
    variants.append(collection)

    collection = new_variant(root, 2, "BroadAngular")
    add_angular_block("Rock_Main", collection, materials["granite"], (1.52, 1.08, 1.06), 201, rotation=(0.02, -0.04, 0.12))
    center_on_ground(collection)
    variants.append(collection)

    collection = new_variant(root, 3, "RiverBoulder")
    add_rock(
        "Rock_Main",
        collection,
        materials["river_stone"],
        (1.48, 1.08, 0.72),
        301,
        rotation=(0.02, 0.08, -0.08),
        roughness=0.11,
        flatten_bottom=0.62,
        flatten_top=0.18,
        top_slope=(-0.04, 0.02),
        cracks=0.025,
    )
    center_on_ground(collection)
    variants.append(collection)

    collection = new_variant(root, 4, "FracturedBoulder")
    add_angular_block("Rock_Main", collection, materials["basalt"], (1.25, 1.0, 1.12), 401, location=(-0.2, 0.05, 0.0), rotation=(0.05, -0.05, -0.12))
    add_rock("Rock_Fragment", collection, materials["basalt_light"], (0.58, 0.48, 0.46), 402, location=(1.0, -0.28, -0.52), rotation=(0.12, 0.2, 0.38), roughness=0.22, side_planes=0.45, cracks=0.09)
    center_on_ground(collection)
    variants.append(collection)

    collection = new_variant(root, 5, "FlatSlab")
    add_rock(
        "Rock_Main",
        collection,
        materials["slate"],
        (1.72, 1.14, 0.48),
        501,
        rotation=(0.03, 0.07, 0.1),
        roughness=0.17,
        flatten_bottom=0.62,
        flatten_top=0.62,
        top_slope=(0.03, -0.02),
        side_planes=0.34,
        strata=0.025,
        cracks=0.07,
    )
    center_on_ground(collection)
    variants.append(collection)

    collection = new_variant(root, 6, "LayeredOutcrop")
    add_rock(
        "Rock_Main",
        collection,
        materials["warm_stone"],
        (1.58, 0.92, 0.82),
        601,
        rotation=(0.02, 0.05, -0.04),
        roughness=0.22,
        flatten_top=0.3,
        top_slope=(0.04, -0.03),
        side_planes=0.28,
        strata=0.075,
        cracks=0.09,
    )
    add_rock("Rock_Front", collection, materials["warm_stone_dark"], (0.78, 0.58, 0.4), 602, location=(0.62, -0.55, -0.42), rotation=(-0.04, 0.05, 0.1), roughness=0.2, cracks=0.07)
    center_on_ground(collection)
    variants.append(collection)

    collection = new_variant(root, 7, "LeaningShard")
    add_rock(
        "Rock_Main",
        collection,
        materials["slate_dark"],
        (0.86, 0.7, 1.58),
        701,
        rotation=(0.0, 0.0, -0.08),
        roughness=0.27,
        flatten_bottom=0.58,
        flatten_top=0.32,
        top_slope=(-0.3, 0.04),
        lean=(-0.32, 0.03),
        taper_top=0.62,
        side_planes=0.5,
        strata=0.018,
        cracks=0.11,
    )
    center_on_ground(collection)
    variants.append(collection)

    collection = new_variant(root, 8, "TwinBoulder")
    add_rock("Rock_Left", collection, materials["granite"], (1.0, 0.82, 0.84), 801, location=(-0.65, 0.04, -0.02), rotation=(0.02, 0.06, -0.14), roughness=0.2, flatten_top=0.34, side_planes=0.25, cracks=0.07)
    add_rock("Rock_Right", collection, materials["granite_dark"], (0.88, 0.75, 0.68), 802, location=(0.72, -0.12, -0.2), rotation=(-0.04, 0.08, 0.22), roughness=0.21, flatten_top=0.26, side_planes=0.2, cracks=0.075)
    center_on_ground(collection)
    variants.append(collection)

    collection = new_variant(root, 9, "LowOutcrop")
    add_rock("Rock_Main", collection, materials["basalt_light"], (1.48, 1.0, 0.62), 901, location=(-0.15, 0.05, -0.12), rotation=(0.02, -0.03, 0.06), roughness=0.21, flatten_top=0.3, cracks=0.08)
    add_rock("Rock_Side_A", collection, materials["basalt"], (0.62, 0.48, 0.38), 902, location=(1.12, -0.18, -0.34), rotation=(0.1, 0.06, 0.32), roughness=0.22, cracks=0.08)
    add_rock("Rock_Side_B", collection, materials["basalt"], (0.48, 0.42, 0.31), 903, location=(-1.15, 0.16, -0.39), rotation=(-0.06, 0.12, -0.24), roughness=0.21, cracks=0.07)
    center_on_ground(collection)
    variants.append(collection)

    collection = new_variant(root, 10, "LongRidge")
    add_rock(
        "Rock_Main",
        collection,
        materials["warm_stone_dark"],
        (1.75, 0.7, 0.86),
        1001,
        rotation=(0.02, 0.05, -0.04),
        roughness=0.24,
        flatten_bottom=0.6,
        flatten_top=0.42,
        top_slope=(-0.08, 0.02),
        side_planes=0.42,
        strata=0.035,
        cracks=0.095,
    )
    center_on_ground(collection)
    variants.append(collection)

    return variants


def export_models(collections):
    os.makedirs(MODEL_ROOT, exist_ok=True)
    for collection in collections:
        bpy.ops.object.select_all(action="DESELECT")
        objects = [obj for obj in collection.all_objects if obj.type == "MESH"]
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(MODEL_ROOT, f"{collection.name}.fbx"),
            use_selection=True,
            object_types={"MESH"},
            use_mesh_modifiers=True,
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_ALL",
            axis_forward="-Z",
            axis_up="Y",
            add_leaf_bones=False,
            bake_anim=False,
            path_mode="AUTO",
        )


def setup_preview(root, collections):
    preview = bpy.data.collections.new("Preview")
    root.children.link(preview)
    positions = [(-6.2 + column * 3.1, 0.0, 3.65 - row * 3.65) for row in range(2) for column in range(5)]
    for collection, offset in zip(collections, positions):
        for obj in collection.all_objects:
            if obj.parent is None:
                obj.location += Vector(offset)

    world = bpy.context.scene.world or bpy.data.worlds.new("NaturalRockWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.018, 0.024, 0.032, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.42

    bpy.ops.object.light_add(type="AREA", location=(-4.0, -7.0, 10.0))
    key = bpy.context.object
    key.name = "PreviewKey"
    key.data.energy = 1200
    key.data.size = 5.0
    key.rotation_euler = (Vector((0.0, 0.0, 3.1)) - key.location).to_track_quat("-Z", "Y").to_euler()
    link_only(key, preview)

    bpy.ops.object.light_add(type="AREA", location=(7.0, -2.0, 6.5))
    fill = bpy.context.object
    fill.name = "PreviewFill"
    fill.data.energy = 850
    fill.data.size = 4.5
    fill.rotation_euler = (Vector((0.0, 0.0, 3.0)) - fill.location).to_track_quat("-Z", "Y").to_euler()
    link_only(fill, preview)

    bpy.ops.object.light_add(type="AREA", location=(0.0, 3.0, 8.0))
    rim = bpy.context.object
    rim.name = "PreviewRim"
    rim.data.energy = 900
    rim.data.size = 3.5
    rim.rotation_euler = (Vector((0.0, 0.0, 3.0)) - rim.location).to_track_quat("-Z", "Y").to_euler()
    link_only(rim, preview)

    bpy.ops.object.camera_add(location=(0.0, -24.0, 4.2))
    camera = bpy.context.object
    camera.name = "PreviewCamera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 15.2
    camera.rotation_euler = (Vector((0.0, 0.0, 3.25)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    link_only(camera, preview)

    scene = bpy.context.scene
    scene.camera = camera
    try:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    os.makedirs(os.path.dirname(PREVIEW_PATH), exist_ok=True)
    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)

    for collection, offset in zip(collections, positions):
        for obj in collection.all_objects:
            if obj.parent is None:
                obj.location -= Vector(offset)


def render_top_preview(collections):
    preview = bpy.data.collections["Preview"]
    positions = [(-6.2 + column * 3.1, 1.85 - row * 3.7, 0.0) for row in range(2) for column in range(5)]
    for collection, offset in zip(collections, positions):
        for obj in collection.all_objects:
            if obj.parent is None:
                obj.location += Vector(offset)

    bpy.ops.object.camera_add(location=(0.0, 0.0, 22.0))
    camera = bpy.context.object
    camera.name = "TopPreviewCamera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 15.2
    camera.rotation_euler = (Vector((0.0, 0.0, 0.0)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    link_only(camera, preview)

    scene = bpy.context.scene
    scene.camera = camera
    scene.render.filepath = TOP_PREVIEW_PATH
    bpy.ops.render.render(write_still=True)

    for collection, offset in zip(collections, positions):
        for obj in collection.all_objects:
            if obj.parent is None:
                obj.location -= Vector(offset)


def main():
    os.makedirs(os.path.dirname(SOURCE_PATH), exist_ok=True)
    root = reset_scene()
    materials = {
        "granite": make_material("Rock Granite", (0.31, 0.33, 0.34), 0.94),
        "granite_dark": make_material("Rock Granite Dark", (0.22, 0.235, 0.245), 0.96),
        "river_stone": make_material("Rock River Stone", (0.36, 0.39, 0.40), 0.9),
        "basalt": make_material("Rock Basalt", (0.15, 0.17, 0.18), 0.97),
        "basalt_light": make_material("Rock Basalt Light", (0.24, 0.255, 0.26), 0.96),
        "slate": make_material("Rock Slate", (0.29, 0.32, 0.34), 0.95),
        "slate_dark": make_material("Rock Slate Dark", (0.19, 0.22, 0.24), 0.97),
        "warm_stone": make_material("Rock Warm Stone", (0.37, 0.34, 0.30), 0.95),
        "warm_stone_dark": make_material("Rock Warm Stone Dark", (0.26, 0.245, 0.22), 0.97),
    }
    collections = create_variants(root, materials)
    export_models(collections)
    setup_preview(root, collections)
    render_top_preview(collections)
    bpy.data.collections["Preview"].hide_render = True
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE_PATH)


if __name__ == "__main__":
    main()
