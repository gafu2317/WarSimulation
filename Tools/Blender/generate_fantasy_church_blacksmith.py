import bpy
import importlib.util
import json
import math
import os
import sys
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "ArtSource", "Blender", "FantasyChurchAndBlacksmith.blend")
REVIEW = os.path.join(ROOT, "docs", "Art", "FantasyChurchAndBlacksmith")
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location(
    "fantasy_style", os.path.join(os.path.dirname(__file__), "generate_realistic_fantasy_buildings.py")
)
r = importlib.util.module_from_spec(spec)
spec.loader.exec_module(r)
g = r.g
g.REVIEW = REVIEW


def gable_roof(name, width, depth, base, height, material="Roof"):
    g.extrude(name, [(-width / 2, base), (0, base + height), (width / 2, base)],
              -depth / 2, depth / 2, material, 0.025)
    g.beam(name + " ridge", (0, -depth / 2 - 0.08, base + height + 0.02),
           (0, depth / 2 + 0.08, base + height + 0.02), 0.14, "RoofEdge")
    for y in (-depth / 2 - 0.06, depth / 2 + 0.06):
        for side in (-1, 1):
            g.beam(name + " bargeboard", (side * width / 2, y, base),
                   (0, y, base + height), 0.13, "RoofEdge")


def cone_roof(name, center, radius, base, height):
    bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=radius, radius2=0.12, depth=height,
                                   location=(center[0], center[1], base + height / 2))
    g.finish(bpy.context.object, name, "Roof", 0.018)
    g.cylinder(name + " rim", (center[0], center[1], base + 0.04), radius + 0.08, 0.16, "RoofEdge", 0.01, 24)


def cross(name, x, y, bottom, height, width):
    g.box(name + " upright", (x, y, bottom + height / 2), (0.13, 0.13, height), "Brass", 0.018)
    g.box(name + " arm", (x, y, bottom + height * 0.63), (width, 0.13, 0.13), "Brass", 0.018)


def rose_window(x, y, z, radius):
    pane = g.cylinder("Church rose window glass", (x, y, z), radius * 0.83, 0.08, "StainedGlass", 0.008, 32)
    pane.rotation_euler.x = math.pi / 2
    bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=0.13, major_segments=32, minor_segments=8,
                                    location=(x, y - 0.03, z), rotation=(math.pi / 2, 0, 0))
    g.finish(bpy.context.object, "Church rose window surround", "StoneTrim", 0.008)
    for angle in [math.tau * index / 12 for index in range(12)]:
        end = (x + math.cos(angle) * radius * 0.86, y - 0.085, z + math.sin(angle) * radius * 0.86)
        g.beam("Church rose window tracery", (x, y - 0.085, z), end, 0.055, "StoneTrim")
    g.cylinder("Church rose window boss", (x, y - 0.11, z), 0.24, 0.12, "Brass", 0.008, 16).rotation_euler.x = math.pi / 2


def church():
    g.box("Church foundation", (0, 0.5, 0.3), (19.2, 28.0, 0.6), "StoneShade", 0.025)
    g.box("Church nave", (0, 1.0, 4.75), (11.8, 22.0, 8.9), "Stone", 0.02)
    r.shifted(lambda: gable_roof("Church nave slate roof", 13.0, 23.0, 9.2, 5.1), (0, 1.0, 0))
    for side in (-1, 1):
        g.box("Church aisle", (side * 7.1, 1.0, 3.2), (3.0, 21.5, 5.8), "Stone", 0.018)
        r.shifted(lambda: gable_roof("Church aisle roof", 3.55, 22.2, 6.1, 2.0), (side * 7.1, 1.0, 0))
        for y in (-6.5, -2.0, 2.5, 7.0):
            g.box("Church stepped buttress", (side * 8.82, y, 2.55), (0.72, 1.05, 4.9), "StoneShade", 0.018)
            g.box("Church buttress cap", (side * 8.82, y - 0.03, 5.05), (0.88, 1.18, 0.22), "StoneTrim", 0.012)
            r.shifted(lambda y=y: r.lancet(y, -8.62, 2.0, 1.05, 2.75), angle=side * math.pi / 2)

    g.box("Church transept", (0, 3.0, 5.0), (18.0, 7.6, 9.4), "Stone", 0.02)
    r.shifted(lambda: gable_roof("Church transept roof", 8.4, 19.0, 9.7, 4.2), (0, 3.0, 0), math.pi / 2)
    for side in (-1, 1):
        r.shifted(lambda: r.lancet(0, -9.03, 3.0, 1.65, 4.1), (0, 3.0, 0), side * math.pi / 2)
        g.box("Transept corner buttress", (side * 9.28, 0.25, 3.1), (0.78, 0.9, 5.8), "StoneShade", 0.018)
        g.box("Transept corner buttress", (side * 9.28, 5.75, 3.1), (0.78, 0.9, 5.8), "StoneShade", 0.018)

    g.cylinder("Church apse", (0, 11.1, 4.55), 5.45, 8.5, "Stone", 0.018, 32)
    cone_roof("Church apse roof", (0, 11.1), 6.0, 8.85, 4.2)
    for angle in (0.25, 0.75, 1.25, 1.75, 2.25, 2.75):
        x = math.cos(angle) * 5.35
        y = 11.1 + math.sin(angle) * 5.35
        pillar = g.box("Church apse buttress", (x, y, 2.7), (0.62, 0.85, 5.1), "StoneShade", 0.015)
        pillar.rotation_euler.z = angle - math.pi / 2

    for side in (-1, 1):
        x = side * 4.45
        g.box("Church bell tower", (x, -10.15, 7.4), (4.55, 5.1, 14.2), "Stone", 0.02)
        for z in (0.78, 6.9, 12.9, 14.55):
            g.box("Bell tower carved course", (x, -10.15, z), (4.8, 5.35, 0.24), "StoneTrim", 0.012)
        r.lancet(x, -12.73, 8.3, 0.9, 2.5)
        r.lancet(x, -12.73, 11.2, 0.72, 1.8)
        cone_roof("Church bell tower spire", (x, -10.15), 3.15, 14.7, 5.8)
        cross("Church tower cross", x, -10.15, 20.35, 1.4, 0.8)

    g.box("Church front gable", (0, -10.62, 5.2), (4.2, 4.2, 9.8), "Stone", 0.018)
    r.door(0, -12.77, 2.0, 3.7, 0.28)
    g.box("Church lower step", (0, -13.65, 0.16), (5.6, 1.7, 0.32), "StoneTrim", 0.018)
    g.box("Church upper step", (0, -13.25, 0.36), (4.7, 0.9, 0.22), "StoneTrim", 0.015)
    rose_window(0, -12.75, 7.7, 1.45)
    r.banner(-2.2, -12.83, 6.1, 0.72, 2.0)
    r.banner(2.2, -12.83, 6.1, 0.72, 2.0)
    for x in (-3.7, 3.7):
        g.box("Church portal pier", (x, -12.63, 3.4), (0.42, 0.55, 6.4), "StoneTrim", 0.014)


def anvil():
    g.cylinder("Blacksmith anvil stump", (0, 0, 0.55), 0.65, 1.1, "Timber", 0.025, 16)
    profile = [(-1.05, 1.18), (-0.55, 1.45), (-0.48, 1.72), (0.65, 1.72),
               (0.82, 1.56), (0.52, 1.35), (0.45, 1.12), (-0.65, 1.12)]
    g.extrude("Blacksmith forged anvil", profile, -0.34, 0.34, "Iron", 0.055)


def forge_hearth():
    g.box("Blacksmith forge masonry base", (0, 0, 0.72), (3.4, 2.4, 1.44), "Stone", 0.018)
    g.box("Blacksmith forge slab", (0, 0, 1.5), (3.65, 2.65, 0.2), "StoneTrim", 0.015)
    g.box("Blacksmith glowing coal bed", (0, -0.25, 1.64), (2.4, 1.3, 0.12), "Ember", 0.02)
    g.box("Blacksmith forge back", (0, 0.95, 2.45), (3.2, 0.42, 1.8), "StoneShade", 0.015)
    g.extrude("Blacksmith iron smoke hood", [(-1.55, 2.5), (1.55, 2.5), (0.75, 4.2), (-0.75, 4.2)],
              -0.2, 1.15, "Iron", 0.018)


def blacksmith():
    g.box("Blacksmith foundation", (0, 0, 0.27), (15.8, 12.4, 0.54), "StoneShade", 0.025)
    g.box("Blacksmith stone workshop", (-2.6, 0.5, 3.45), (9.6, 10.4, 6.4), "Stone", 0.02)
    g.box("Blacksmith timber upper wall", (-2.6, 0.45, 6.25), (9.1, 9.9, 2.0), "OchrePlaster", 0.018)
    r.shifted(lambda: gable_roof("Blacksmith workshop roof", 10.7, 11.5, 7.3, 3.8), (-2.6, 0.5, 0))
    r.door(-3.8, -4.78, 1.65, 2.9, 0.2)
    for x in (-0.9, 1.3):
        r.lancet(x, -4.73, 2.0, 0.85, 1.55, "Timber")
    for x in (-6.95, -4.55, -2.15, 0.25, 2.05):
        g.box("Blacksmith front timber", (x, -4.74, 6.28), (0.2, 0.2, 1.95), "Timber", 0.012)
    g.box("Blacksmith upper beam", (-2.6, -4.76, 5.32), (9.2, 0.22, 0.24), "Timber", 0.012)

    g.box("Blacksmith open bay roof", (5.0, 0.1, 5.1), (5.6, 11.0, 0.24), "Roof", 0.018)
    for x in (2.5, 7.5):
        for y in (-4.9, 4.9):
            g.box("Blacksmith open bay post", (x, y, 2.55), (0.3, 0.3, 5.1), "Timber", 0.014)
    for y in (-4.9, 4.9):
        g.box("Blacksmith open bay beam", (5.0, y, 4.78), (5.5, 0.32, 0.34), "Timber", 0.014)
    for x in (2.5, 7.5):
        g.beam("Blacksmith bay brace", (x, -4.9, 3.2), (x + (-0.9 if x > 5 else 0.9), -4.9, 4.72), 0.16, "Timber")

    r.shifted(forge_hearth, (5.0, 1.4, 0))
    g.box("Blacksmith chimney shaft", (5.0, 2.35, 7.0), (1.5, 1.6, 8.7), "Stone", 0.018)
    g.box("Blacksmith chimney cap", (5.0, 2.35, 11.38), (1.95, 2.05, 0.32), "StoneTrim", 0.014)
    for x in (4.62, 5.38):
        g.box("Blacksmith chimney pot", (x, 2.35, 11.9), (0.42, 0.48, 0.85), "StoneShade", 0.012)

    r.shifted(anvil, (4.8, -2.3, 0))
    g.box("Blacksmith quenching trough", (6.7, -2.1, 0.68), (1.5, 2.7, 1.2), "Timber", 0.025)
    g.box("Blacksmith trough water", (6.7, -2.1, 1.3), (1.25, 2.45, 0.06), "Water", 0.004)
    g.box("Blacksmith tool rack", (2.2, 3.8, 2.1), (0.28, 3.2, 3.6), "Timber", 0.015)
    for index, z in enumerate((1.0, 1.65, 2.3, 2.95)):
        g.box("Blacksmith hanging tool", (2.0, 3.05 + index * 0.45, z), (0.18, 0.16, 1.1), "Iron", 0.018)

    g.box("Blacksmith sign bracket", (0.7, -5.4, 4.5), (0.18, 1.6, 0.18), "Iron", 0.012)
    g.box("Blacksmith hanging sign", (0.7, -6.0, 3.75), (1.45, 0.16, 1.25), "Door", 0.025)
    g.box("Blacksmith sign hammer head", (0.7, -6.1, 3.9), (0.82, 0.08, 0.22), "Brass", 0.012)
    hammer = g.box("Blacksmith sign hammer handle", (0.7, -6.11, 3.53), (0.16, 0.08, 0.75), "Brass", 0.012)
    hammer.rotation_euler.y = -0.42
    for y in (-3.4, -2.3, -1.2, -0.1):
        g.cylinder("Blacksmith stacked firewood", (-7.2, y, 0.58), 0.28, 1.35, "Timber", 0.01, 12).rotation_euler.y = math.pi / 2


def build_preview(name, assets, positions, camera_position, target, scale, filename):
    scene = bpy.data.scenes.new(name)
    bpy.context.window.scene = scene
    for (collection, _), position in zip(assets, positions):
        instance = bpy.data.objects.new(collection.name + " Preview", None)
        instance.instance_type = "COLLECTION"
        instance.instance_collection = collection
        instance.location = position
        scene.collection.objects.link(instance)
    g.presentation(scene)
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 1200
    g.camera_at(scene, camera_position, target, scale)
    g.render(scene, filename)
    return scene


def model_record(collection, root):
    points = [obj.matrix_parent_inverse @ obj.matrix_basis @ Vector(corner)
              for obj in collection.objects if obj.type == "MESH" for corner in obj.bound_box]
    triangles = 0
    for obj in collection.objects:
        if obj.type == "MESH":
            obj.data.calc_loop_triangles()
            triangles += len(obj.data.loop_triangles)
    return {
        "name": root.name,
        "facility_type": root["facility_type"],
        "parts": sum(obj.type == "MESH" for obj in collection.objects),
        "triangles": triangles,
        "minimum": [min(point[axis] for point in points) for axis in range(3)],
        "maximum": [max(point[axis] for point in points) for axis in range(3)],
    }


def main():
    os.makedirs(REVIEW, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    base_scene = bpy.context.scene
    base_scene.name = "Editable Assets"
    base_scene.unit_settings.system = "METRIC"
    r.setup_materials()
    g.MATERIALS["OchrePlaster"] = r.surface("ChurchBlacksmith_OchrePlaster", "A88C68", "plain")
    g.MATERIALS["StainedGlass"] = r.surface("Church_StainedGlass", "315C70", "plain")
    g.MATERIALS["Ember"] = r.surface("Blacksmith_Ember", "B64221", "plain")
    g.MATERIALS["Water"] = r.surface("Blacksmith_Water", "426D76", "plain")
    assets = []
    for name, label, recipe in (("Church", "教会", church), ("Blacksmith", "鍛冶屋", blacksmith)):
        collection, root = g.make_asset(name, recipe)
        root["facility_type"] = label
        assets.append((collection, root))
        print("CHURCH_BLACKSMITH_CREATED", name, flush=True)
    records = [model_record(collection, root) for collection, root in assets]
    with open(os.path.join(REVIEW, "model_manifest.json"), "w") as handle:
        json.dump({"blender": bpy.app.version_string, "models": records}, handle, indent=2, ensure_ascii=False)

    build_preview("Church Preview", [assets[0]], [(0, 0, 0)], (27, -38, 25), (0, 0, 8), 32,
                  "Church_Preview.png")
    build_preview("Blacksmith Preview", [assets[1]], [(0, 0, 0)], (22, -30, 18), (0, 0, 4.5), 23,
                  "Blacksmith_Preview.png")
    build_preview("Combined Preview", assets, [(-13, 0, 0), (13, -1, 0)], (36, -47, 31), (0, 0, 7), 46,
                  "ChurchAndBlacksmith_Preview.png")
    bpy.context.window.scene = base_scene
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE)
    print("CHURCH_BLACKSMITH_COMPLETE", json.dumps(records, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
