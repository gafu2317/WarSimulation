import bpy
import bmesh
import json
import math
import os
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "ArtSource", "Blender", "KingdomBuildings.blend")
MODELS = os.path.join(ROOT, "Assets", "Models", "Kingdom", "Buildings")
REVIEW = os.path.join(ROOT, "docs", "Art", "KingdomBuildings")
PALETTE = {
    "Stone": "BEB6A1",
    "StoneTrim": "DDD3BA",
    "StoneShade": "A29986",
    "Plaster": "EADBBE",
    "Roof": "527A85",
    "RoofEdge": "3B5964",
    "Timber": "6C4B37",
    "Door": "946642",
    "Window": "344952",
    "Brass": "C0A05D",
    "Banner": "AD6050",
}
MATERIALS = {}
PARTS = None


def linear(channel):
    return channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4


def material(name, color):
    rgb = tuple(linear(int(color[i:i + 2], 16) / 255) for i in (0, 2, 4))
    result = bpy.data.materials.new(name)
    result.diffuse_color = (*rgb, 1)
    result.use_nodes = True
    shader = result.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*rgb, 1)
    shader.inputs["Roughness"].default_value = 0.82
    return result


def finish(obj, name, surface, bevel=0):
    obj.name = name
    obj.data.materials.append(MATERIALS[surface])
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        modifier = obj.modifiers.new("Soft edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    PARTS.objects.link(obj)
    return obj


def box(name, center, size, surface, bevel=0.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.dimensions = size
    return finish(obj, name, surface, bevel)


def cylinder(name, center, radius, depth, surface, bevel=0.025, sides=16):
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=depth, location=center)
    return finish(bpy.context.object, name, surface, bevel)


def mesh(name, vertices, faces, surface, bevel=0):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    return finish(obj, name, surface, bevel)


def extrude(name, profile, front, back, surface, bevel=0.015):
    count = len(profile)
    vertices = [(x, y, z) for y in (front, back) for x, z in profile]
    faces = [tuple(range(count - 1, -1, -1)), tuple(range(count, count * 2))]
    faces += [(i, (i + 1) % count, (i + 1) % count + count, i + count) for i in range(count)]
    return mesh(name, vertices, faces, surface, bevel)


def beam(name, start, end, width, surface):
    direction = Vector(end) - Vector(start)
    obj = box(name, (Vector(start) + Vector(end)) / 2, (width, width, direction.length), surface, 0.015)
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    return obj


def arch_profile(x, bottom, width, height):
    radius = width / 2
    spring = bottom + height - radius
    return [(x - radius, bottom), (x + radius, bottom)] + [
        (x + math.cos(math.pi * i / 12) * radius, spring + math.sin(math.pi * i / 12) * radius)
        for i in range(13)
    ]


def arch_trim(name, x, y, bottom, width, height, border, surface):
    radius = width / 2
    spring = bottom + height - radius
    for side in (-1, 1):
        box(name + " jamb", (x + side * (radius + border / 2), y, (bottom + spring) / 2),
            (border, border, spring - bottom), surface, 0.015)
    for i in range(8):
        a = math.pi * i / 8
        b = math.pi * (i + 1) / 8
        profile = [(x + math.cos(angle) * r, spring + math.sin(angle) * r)
                   for r, angle in ((radius, a), (radius, b), (radius + border, b), (radius + border, a))]
        extrude(name + " arch stone", profile, y - border / 2, y + border / 2, surface, 0.009)


def doorway(x, y, bottom, width, height):
    extrude("Oak door", arch_profile(x, bottom, width, height), y - 0.075, y + 0.03, "Door")
    arch_trim("Door surround", x, y - 0.04, bottom, width, height, 0.16, "StoneTrim")
    box("Door seam", (x, y - 0.101, bottom + (height - width / 2) / 2),
        (0.025, 0.025, height - width / 2), "Timber", 0)
    for z in (bottom + 0.28, bottom + 0.82):
        box("Door strap", (x, y - 0.105, z), (width * 0.84, 0.045, 0.075), "Timber", 0.01)
    for side in (-1, 1):
        obj = cylinder("Door handle", (x + side * width * 0.14, y - 0.15, bottom + height * 0.44),
                       0.046, 0.04, "Brass", 0.005, 8)
        obj.rotation_euler.x = math.pi / 2


def window(x, y, bottom, width, height, trim="StoneTrim"):
    extrude("Dark window", arch_profile(x, bottom, width, height), y - 0.035, y + 0.015, "Window", 0.005)
    arch_trim("Window frame", x, y - 0.06, bottom, width, height, 0.085, trim)
    box("Window sill", (x, y - 0.09, bottom), (width + 0.24, 0.22, 0.11), trim, 0.015)
    box("Window mullion", (x, y - 0.087, bottom + height * 0.46), (0.055, 0.05, height * 0.85), trim, 0.008)


def hip_roof(name, x, y, width, depth, base, height):
    a, b = width / 2, depth / 2
    ridge = depth * 0.22
    vertices = [(x - a, y - b, base), (x + a, y - b, base),
                (x + a, y + b, base), (x - a, y + b, base),
                (x, y - ridge, base + height), (x, y + ridge, base + height)]
    mesh(name, vertices, [(0, 1, 4), (1, 2, 5, 4), (2, 3, 5), (3, 0, 4, 5), (3, 2, 1, 0)], "Roof", 0.025)
    box("Roof eaves", (x, y, base - 0.055), (width + 0.06, depth + 0.06, 0.15), "RoofEdge", 0.035)
    beam("Roof ridge", (x, y - ridge - 0.04, base + height), (x, y + ridge + 0.04, base + height), 0.12, "RoofEdge")


def tower(x, y):
    cylinder("Tower footing", (x, y, 0.2), 0.95, 0.4, "StoneShade", 0.045)
    cylinder("Tower shaft", (x, y, 2.6), 0.82, 4.8, "Stone", 0.035)
    cylinder("Tower lower trim", (x, y, 0.52), 0.87, 0.17, "StoneTrim")
    cylinder("Tower crown", (x, y, 4.89), 0.91, 0.28, "StoneTrim")
    cylinder("Tower roof rim", (x, y, 5.06), 1.03, 0.15, "RoofEdge")
    bpy.ops.mesh.primitive_cone_add(vertices=16, radius1=1.04, radius2=0.085, depth=1.82,
                                   location=(x, y, 6.015))
    finish(bpy.context.object, "Tower roof", "Roof", 0.018)
    window(x, y - 0.815, 3.63, 0.31, 0.79)
    cylinder("Tower finial", (x, y, 7.0), 0.085, 0.2, "Brass", 0.012, 8)


def castle():
    box("Keep foundation", (0, 0, 0.18), (4.15, 3.8, 0.36), "StoneShade", 0.065)
    box("Keep walls", (0, 0, 2.31), (3.8, 3.42, 4.22), "Stone", 0.07)
    box("Keep foundation trim", (0, 0, 0.52), (3.98, 3.6, 0.2), "StoneTrim")
    box("Keep cornice", (0, 0, 4.32), (4.04, 3.66, 0.23), "StoneTrim", 0.04)
    box("Keep belt course", (0, 0, 2.72), (3.94, 3.56, 0.14), "StoneTrim")
    hip_roof("Keep roof", 0, 0, 4.4, 4.08, 4.5, 1.7)
    for x in (-2.05, 2.05):
        tower(x, 1.08)
    doorway(0, -1.75, 0.35, 1.12, 1.84)
    box("Entry step", (0, -2.03, 0.105), (1.72, 0.68, 0.21), "StoneTrim", 0.035)
    box("Entry upper step", (0, -1.92, 0.25), (1.47, 0.46, 0.19), "StoneTrim", 0.025)
    for x in (-1.14, 0, 1.14):
        window(x, -1.73, 3.12, 0.41, 0.85)
    for side in (-1, 1):
        box("Keep corner stone", (side * 1.84, -1.69, 1.45), (0.22, 0.17, 1.52), "StoneTrim")
        for z in (0.93, 2.22):
            box("Masonry accent", (side * 1.08, -1.729, z), (0.39, 0.048, 0.18), "StoneShade", 0.015)
    before = set(PARTS.objects)
    for x in (-0.87, 0.87):
        window(x, -1.918, 3.12, 0.42, 0.85)
    for obj in set(PARTS.objects) - before:
        obj.location = Vector((-obj.location.y, obj.location.x, obj.location.z))
        obj.rotation_euler.z += math.pi / 2
    cylinder("Flagstaff", (-2.05, 1.08, 7.45), 0.03, 0.84, "Timber", 0, 8)
    mesh("Castle pennant", [(-2.02, 1.08, 7.8), (-1.21, 1.08, 7.68), (-1.4, 1.08, 7.43),
                            (-2.02, 1.08, 7.48), (-2.02, 1.105, 7.8), (-1.21, 1.105, 7.68),
                            (-1.4, 1.105, 7.43), (-2.02, 1.105, 7.48)],
         [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)], "Banner")


def house():
    box("House foundation", (0, 0, 0.17), (3.2, 2.98, 0.34), "StoneShade", 0.06)
    extrude("Plaster house", [(-1.48, 0.25), (1.48, 0.25), (1.48, 2.75), (0, 4.3), (-1.48, 2.75)],
            -1.36, 1.36, "Plaster", 0.035)
    for side in (-1, 1):
        for y in (-1.39, 1.39):
            box("Corner timber", (side * 1.43, y, 1.51), (0.16, 0.15, 2.52), "Timber", 0.018)
    box("Front timber sill", (0, -1.41, 0.42), (3.0, 0.14, 0.18), "Timber")
    box("Gable beam", (0, -1.405, 2.74), (3.0, 0.14, 0.16), "Timber")
    beam("Gable center timber", (0, -1.42, 2.8), (0, -1.42, 4.22), 0.13, "Timber")
    for side in (-1, 1):
        beam("Gable rake", (side * 1.52, -1.43, 2.72), (0, -1.43, 4.33), 0.14, "Timber")
        x = side * 1.74
        profile = [(0, 4.43), (x, 2.7), (x, 2.53), (0, 4.26)]
        extrude("Slate roof", profile, -1.65, 1.65, "Roof", 0.025)
        beam("Roof bargeboard", (x, -1.69, 2.6), (0, -1.69, 4.35), 0.14, "RoofEdge")
        beam("Rear bargeboard", (x, 1.69, 2.6), (0, 1.69, 4.35), 0.14, "RoofEdge")
        box("Roof eave", (x, 0, 2.6), (0.12, 3.43, 0.16), "RoofEdge")
    beam("Roof ridge", (0, -1.76, 4.43), (0, 1.76, 4.43), 0.15, "RoofEdge")
    doorway(-0.58, -1.43, 0.34, 0.71, 1.63)
    window(0.74, -1.425, 1.1, 0.61, 0.88, "Timber")
    box("Doorstep", (-0.58, -1.65, 0.13), (1.08, 0.49, 0.26), "StoneTrim", 0.035)
    for side in (-1, 1):
        box("Window shutter", (0.74 + side * 0.43, -1.48, 1.49), (0.2, 0.1, 0.8), "Door", 0.018)
    box("Chimney", (0.87, 0.68, 3.86), (0.48, 0.52, 1.48), "Stone", 0.035)
    box("Chimney cap", (0.87, 0.68, 4.64), (0.63, 0.66, 0.17), "StoneTrim", 0.03)
    box("Chimney opening", (0.87, 0.68, 4.732), (0.38, 0.4, 0.02), "Window", 0)
    for y in (-0.57, 0.66):
        box("Side window surround", (1.503, y, 1.64), (0.12, 0.78, 1.01), "Timber")
        box("Side window glass", (1.57, y, 1.66), (0.024, 0.59, 0.8), "Window", 0)
        box("Side window mullion", (1.589, y, 1.66), (0.045, 0.055, 0.84), "Timber", 0.007)


def wall(length=6):
    box("Wall core", (0, 0, 1.52), (length, 0.96, 2.08), "Stone", 0)
    box("Wall plinth", (0, 0, 0.16), (length, 1.18, 0.32), "StoneShade", 0)
    box("Wall foot trim", (0, 0, 0.4), (length, 1.07, 0.16), "StoneTrim", 0)
    box("Wall coping", (0, 0, 2.66), (length, 1.14, 0.2), "StoneTrim", 0)
    for i in range(int(length)):
        x = -length / 2 + i + 0.5
        box("Wall merlon", (x, 0, 3.005), (0.57, 1.04, 0.53), "Stone", 0.028)
        box("Merlon cap", (x, 0, 3.27), (0.65, 1.11, 0.12), "StoneTrim", 0.02)
    for side in (-1, 1):
        for x, z in ((-length * 0.3, 0.91), (length * 0.27, 1.43), (-length * 0.08, 2.12)):
            box("Wall stone accent", (x, side * 0.486, z), (0.48, 0.035, 0.22), "StoneShade", 0.012)


def corner():
    before = set(PARTS.objects)
    wall(3)
    for obj in set(PARTS.objects) - before:
        obj.location.x += 1.5
    before = set(PARTS.objects)
    wall(3)
    for obj in set(PARTS.objects) - before:
        x, y, z = obj.location
        obj.location = (-y, x + 1.5, z)
        obj.rotation_euler.z = math.pi / 2
    box("Corner pier", (0, 0, 1.39), (1.26, 1.26, 2.78), "Stone", 0.025)
    box("Corner footing", (0, 0, 0.18), (1.38, 1.38, 0.36), "StoneShade", 0.025)
    box("Corner crown", (0, 0, 2.8), (1.43, 1.43, 0.18), "StoneTrim", 0.025)
    for x in (-0.47, 0.47):
        for y in (-0.47, 0.47):
            box("Corner merlon", (x, y, 3.13), (0.48, 0.48, 0.5), "Stone", 0.025)


def gate():
    radius, spring = 1.05, 1.42
    for side in (-1, 1):
        x = side * (3 + radius) / 2
        width = 3 - radius
        box("Gate pier", (x, 0, 1.52), (width, 0.96, 2.08), "Stone", 0)
        box("Gate footing", (x, 0, 0.16), (width, 1.18, 0.32), "StoneShade", 0)
        box("Gate foot trim", (x, 0, 0.4), (width, 1.07, 0.16), "StoneTrim", 0)
    profile = [(-radius, 2.56), (radius, 2.56)] + [
        (radius * math.cos(math.pi * i / 12), spring + radius * math.sin(math.pi * i / 12))
        for i in range(13)
    ]
    extrude("Gate arch lintel", profile, -0.48, 0.48, "Stone", 0)
    for y in (-0.53, 0.53):
        arch_trim("Gate arch", 0, y, 0, radius * 2, spring + radius, 0.18, "StoneTrim")
    box("Gate coping", (0, 0, 2.66), (6, 1.14, 0.2), "StoneTrim", 0)
    for i in range(6):
        x = i - 2.5
        box("Gate merlon", (x, 0, 3.005), (0.57, 1.04, 0.53), "Stone", 0.028)
        box("Gate cap", (x, 0, 3.27), (0.65, 1.11, 0.12), "StoneTrim", 0.02)
    for side in (-1, 1):
        box("Gate buttress", (side * 1.47, 0, 1.36), (0.34, 1.22, 2.72), "StoneTrim", 0.02)
        extrude("Gate banner", [(side * 2.23 - 0.19, 2.15), (side * 2.23 + 0.19, 2.15),
                                (side * 2.23 + 0.19, 1.4), (side * 2.23, 1.24), (side * 2.23 - 0.19, 1.4)],
                -0.53, -0.5, "Banner", 0.005)


def make_asset(name, recipe):
    global PARTS
    PARTS = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(PARTS)
    recipe()
    root = bpy.data.objects.new(name, None)
    PARTS.objects.link(root)
    for obj in PARTS.objects:
        if obj != root:
            obj.parent = root
    return PARTS, root


def export_asset(collection):
    bpy.ops.object.select_all(action="DESELECT")
    copies = []
    for obj in list(collection.objects):
        if obj.type != "MESH":
            continue
        duplicate = obj.copy()
        duplicate.data = obj.data.copy()
        duplicate.parent = None
        duplicate.matrix_world = obj.matrix_world.copy()
        bpy.context.scene.collection.objects.link(duplicate)
        duplicate.select_set(True)
        copies.append(duplicate)
    bpy.context.view_layer.objects.active = copies[0]
    bpy.ops.object.join()
    exported = bpy.context.object
    exported.name = collection.name + "_Mesh"
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    triangulate = exported.modifiers.new("Export triangles", "TRIANGULATE")
    bpy.ops.object.modifier_apply(modifier=triangulate.name)
    exported.data.calc_loop_triangles()
    bounds = [exported.matrix_world @ Vector(v) for v in exported.bound_box]
    report = {
        "name": collection.name,
        "vertices": len(exported.data.vertices),
        "triangles": len(exported.data.loop_triangles),
        "minimum": [min(v[i] for v in bounds) for i in range(3)],
        "maximum": [max(v[i] for v in bounds) for i in range(3)],
        "materials": [m.name for m in exported.data.materials],
    }
    bpy.ops.export_scene.fbx(filepath=os.path.join(MODELS, collection.name + ".fbx"), use_selection=True,
                             object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True, mesh_smooth_type="FACE",
                             bake_space_transform=True, add_leaf_bones=False, bake_anim=False, path_mode="AUTO")
    data = exported.data
    bpy.data.objects.remove(exported, do_unlink=True)
    bpy.data.meshes.remove(data)
    return report


def camera_at(scene, position, target, scale):
    camera = bpy.data.objects.new("Preview Camera", bpy.data.cameras.new("Preview Camera"))
    scene.collection.objects.link(camera)
    camera.location = position
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = scale
    scene.camera = camera


def fit_camera(scene, objects):
    bpy.context.view_layer.update()
    camera = scene.camera
    inverse = camera.matrix_world.inverted()
    points = [inverse @ obj.matrix_world @ Vector(corner)
              for obj in objects if obj.type == "MESH" for corner in obj.bound_box]
    left, right = min(p.x for p in points), max(p.x for p in points)
    bottom, top = min(p.y for p in points), max(p.y for p in points)
    camera.location += camera.rotation_euler.to_quaternion() @ Vector(((left + right) / 2, (bottom + top) / 2, 0))
    camera.data.ortho_scale = max(right - left, (top - bottom) * scene.render.resolution_x / scene.render.resolution_y) * 1.12


def presentation(scene):
    global PARTS
    PARTS = bpy.data.collections.new(scene.name + " Presentation")
    scene.collection.children.link(PARTS)
    box("Preview floor", (0, 0, -0.16), (200, 200, 0.3), "Ground", 0)
    world = bpy.data.worlds.new(scene.name + " World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.5, 0.57, 0.64, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.45
    scene.world = world
    for name, position, energy, size in (("Key", (-9, -12, 18), 2400, 9), ("Fill", (10, -2, 12), 1400, 10),
                                        ("Rim", (0, 10, 15), 2000, 8)):
        light = bpy.data.objects.new(name, bpy.data.lights.new(name, "AREA"))
        PARTS.objects.link(light)
        light.location = position
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.rotation_euler = (Vector((0, 0, 1)) - light.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 1200
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"


def render(scene, filename):
    scene.render.filepath = os.path.join(REVIEW, filename)
    bpy.ops.render.render(write_still=True, scene=scene.name)


def main():
    for path in (os.path.dirname(SOURCE), MODELS, REVIEW):
        os.makedirs(path, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = "Buildings"
    scene.unit_settings.system = "METRIC"
    for name, color in PALETTE.items():
        MATERIALS[name] = material("Kingdom_" + name, color)
    MATERIALS["Ground"] = material("Preview_Ground", "CDD0C6")
    assets = [make_asset(name, recipe) for name, recipe in (
        ("Kingdom_Castle", castle), ("Kingdom_House", house), ("Kingdom_Wall_Straight", wall),
        ("Kingdom_Wall_Corner", corner), ("Kingdom_Wall_Gate", gate))]
    bpy.context.view_layer.update()
    report = [export_asset(collection) for collection, _ in assets]
    with open(os.path.join(REVIEW, "model_manifest.json"), "w") as handle:
        json.dump({"blender": bpy.app.version_string, "palette_srgb": PALETTE, "models": report,
                   "wall_sockets_blender": {"straight": [[-3, 0, 0], [3, 0, 0]],
                                            "gate": [[-3, 0, 0], [3, 0, 0]],
                                            "corner": [[3, 0, 0], [0, 3, 0]]}}, handle, indent=2)
    assets[0][1].location = (-3.25, 0.8, 0)
    assets[1][1].location = (3.5, -0.6, 0)
    walls_scene = bpy.data.scenes.new("Wall Modules")
    for collection, root in assets[2:]:
        scene.collection.children.unlink(collection)
        walls_scene.collection.children.link(collection)
    for (_, root), offset in zip(assets[2:], ((-6.8, 0, 0), (0, 0, 0), (7.2, 0, 0))):
        root.location = offset
    presentation(scene)
    camera_at(scene, (12, -23, 15), (0, 0, 3.15), 14.5)
    fit_camera(scene, [obj for collection, _ in assets[:2] for obj in collection.objects])
    render(scene, "Buildings_Preview.png")
    bpy.context.window.scene = walls_scene
    presentation(walls_scene)
    camera_at(walls_scene, (10, -25, 20), (0.5, 0.6, 1.1), 23.5)
    walls_scene.render.resolution_y = 850
    render(walls_scene, "WallModules_Preview.png")
    assembly = bpy.data.scenes.new("Wall Connection Example")
    bpy.context.window.scene = assembly
    for index, (source_index, position, angle) in enumerate((
        (4, (0, -6, 0), 0), (2, (-6, -6, 0), 0), (2, (6, -6, 0), 0),
        (3, (-12, -6, 0), 0), (3, (12, -6, 0), math.pi / 2),
        (2, (-12, 0, 0), math.pi / 2), (2, (12, 0, 0), math.pi / 2),
    )):
        collection, root = assets[source_index]
        instance_collection = bpy.data.collections.get(collection.name + " Instance")
        if instance_collection is None:
            instance_collection = bpy.data.collections.new(collection.name + " Instance")
            for obj in collection.objects:
                if obj.type == "MESH":
                    duplicate = obj.copy()
                    duplicate.parent = None
                    duplicate.matrix_world = obj.matrix_local.copy()
                    instance_collection.objects.link(duplicate)
        instance = bpy.data.objects.new("Connected " + str(index), None)
        instance.instance_type = "COLLECTION"
        instance.instance_collection = instance_collection
        instance.location = position
        instance.rotation_euler.z = angle
        assembly.collection.objects.link(instance)
    presentation(assembly)
    camera_at(assembly, (14, -29, 23), (0, -2, 0.8), 32)
    assembly.render.resolution_y = 1050
    render(assembly, "WallConnection_Preview.png")
    bpy.context.window.scene = scene
    bpy.ops.object.select_all(action="DESELECT")
    for area in bpy.context.screen.areas:
        if area.type == "VIEW_3D":
            area.spaces.active.region_3d.view_perspective = "CAMERA"
            area.spaces.active.shading.type = "MATERIAL"
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE)
    print("KINGDOM_MODELS", json.dumps(report))


if __name__ == "__main__":
    main()
