import math
from pathlib import Path

import bpy
from mathutils import Vector


try:
    ROOT = Path(__file__).resolve().parents[2]
except NameError:
    ROOT = Path("/Users/fukutomi/Unity/WarSimulation")
SOURCE = ROOT / "ArtSource/Blender/FantasySchoolLevels.blend"
OUTPUT = ROOT / "ArtSource/Blender/SchoolLevelsPresentation.blend"
RENDER = ROOT / "docs/Art/SchoolLevelUnity/school_levels_presentation.png"

ROWS = [
    ("WarriorAcademy", "WARRIOR ACADEMY", (0.48, 0.12, 0.10, 1.0)),
    ("ArcaneAcademy", "ARCANE ACADEMY", (0.12, 0.27, 0.43, 1.0)),
    ("SpiritAcademy", "SPIRIT ACADEMY", (0.15, 0.34, 0.23, 1.0)),
]


def material(name, color, roughness=0.7, metallic=0.0):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    shader = next((node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
    if shader is None:
        shader = mat.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    if "Emission Color" in shader.inputs:
        shader.inputs["Emission Color"].default_value = (0, 0, 0, 1)
        shader.inputs["Emission Strength"].default_value = 0
    return mat


def cube(name, location, scale, mat, bevel=0.0, collection=None):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    if bevel:
        modifier = obj.modifiers.new("Soft presentation corners", "BEVEL")
        modifier.width = bevel
        modifier.segments = 5
    if collection and obj.name not in collection.objects:
        for linked in list(obj.users_collection):
            linked.objects.unlink(obj)
        collection.objects.link(obj)
    return obj


def text(label, body, location, size, mat, camera, collection):
    bpy.ops.object.text_add(location=location)
    obj = bpy.context.object
    obj.name = label
    obj.data.body = body
    obj.data.align_x = "CENTER"
    obj.data.align_y = "CENTER"
    obj.data.size = size
    obj.data.extrude = 0.015
    obj.data.bevel_depth = 0.008
    obj.data.materials.append(mat)
    obj.rotation_euler = (camera.location - obj.location).to_track_quat("Z", "Y").to_euler()
    for linked in list(obj.users_collection):
        linked.objects.unlink(obj)
    collection.objects.link(obj)
    return obj


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def area_light(name, location, energy, size, color, target, collection):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    data.color = color
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.location = location
    look_at(obj, target)
    return obj


def main():
    # Opening a .blend from Blender's console replaces the current context, so run this
    # script once more after the source file becomes active.
    if Path(bpy.data.filepath).resolve() != SOURCE.resolve():
        bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
        return
    scene = bpy.context.scene
    scene.name = "School Levels Presentation"
    # Workbench keeps this review sheet readable on machines where headless Eevee
    # lighting is unavailable; the source materials remain editable in the .blend.
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.display.shading.curvature_ridge_factor = 1.4
    scene.display.shading.curvature_valley_factor = 1.1
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 1100
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(RENDER)
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    # Keep Blender's current view transform so the script works across Blender 4 and 5.

    world = scene.world or bpy.data.worlds.new("School Levels World")
    scene.world = world
    world.use_nodes = True
    background = next((node for node in world.node_tree.nodes if node.type == "BACKGROUND"), None)
    if background is None:
        background = world.node_tree.nodes.new("ShaderNodeBackground")
    world_output = next((node for node in world.node_tree.nodes if node.type == "OUTPUT_WORLD"), None)
    if world_output is None:
        world_output = world.node_tree.nodes.new("ShaderNodeOutputWorld")
    if not any(link.to_node == world_output for link in world.node_tree.links):
        world.node_tree.links.new(background.outputs[0], world_output.inputs[0])
    background.inputs["Color"].default_value = (0.012, 0.015, 0.02, 1)
    background.inputs["Strength"].default_value = 0.24

    presentation = bpy.data.collections.get("PRESENTATION") or bpy.data.collections.new("PRESENTATION")
    if presentation.name not in scene.collection.children:
        scene.collection.children.link(presentation)

    panel = material("Presentation Panel", (0.075, 0.085, 0.105, 1), 0.72)
    panel_edge = material("Presentation Edge", (0.18, 0.20, 0.23, 1), 0.58, 0.12)
    floor = material("Presentation Floor", (0.025, 0.03, 0.04, 1), 0.8)
    white = material("Presentation White", (0.84, 0.86, 0.88, 1), 0.45)
    muted = material("Presentation Muted", (0.42, 0.46, 0.51, 1), 0.58)
    row_mats = [material("Row Accent " + kind, accent, 0.52, 0.05) for kind, _, accent in ROWS]

    # A single dark floor keeps all fifteen models readable while retaining their original bases.
    cube("Presentation Floor", (80, 38, -0.78), (190, 132, 0.35), floor, 1.2, presentation)
    for row, (_, _, _) in enumerate(ROWS):
        y = row * 38
        cube("Row Panel %d" % (row + 1), (80, y, -0.48), (181, 30.5, 0.28), panel, 1.2, presentation)
        cube("Row Edge %d" % (row + 1), (80, y - 15.15, -0.28), (181, 0.4, 0.35), row_mats[row], 0.12, presentation)
        cube("Row Edge Back %d" % (row + 1), (80, y + 15.15, -0.28), (181, 0.4, 0.35), row_mats[row], 0.12, presentation)

    # Keep a little extra margin so no model is clipped at the image edges.
    camera_data = bpy.data.cameras.new("School Presentation Camera")
    camera = bpy.data.objects.new("School Presentation Camera", camera_data)
    scene.collection.objects.link(camera)
    camera.location = (82, -212, 126)
    camera_data.lens = 47
    camera_data.sensor_width = 36
    look_at(camera, (80, 38, 9.5))
    scene.camera = camera

    target = (80, 35, 8)
    area_light("Key Light", (30, -80, 145), 1550, 75, (1.0, 0.91, 0.78), target, presentation)
    area_light("Fill Light", (170, 15, 85), 1050, 60, (0.62, 0.76, 1.0), target, presentation)
    area_light("Rim Light", (-55, 105, 125), 1300, 55, (0.75, 0.88, 1.0), target, presentation)

    # Keep the source collections untouched; only the new presentation helpers are linked here.
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT))
    scene.render.filepath = str(RENDER)
    bpy.ops.render.render(write_still=True)
    print("PRESENTATION_RENDERED", RENDER)


if __name__ == "__main__":
    main()
