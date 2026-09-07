import bpy
import importlib.util
import json
import math
import os
import sys
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "ArtSource", "Blender", "FantasyCityVegetation.blend")
REVIEW = os.path.join(ROOT, "docs", "Art", "FantasyCityVegetation")
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location(
    "fantasy_style", os.path.join(os.path.dirname(__file__), "generate_realistic_fantasy_buildings.py")
)
r = importlib.util.module_from_spec(spec)
spec.loader.exec_module(r)
g = r.g
g.REVIEW = REVIEW


def ellipsoid(name, position, scale, material, rotation=0, subdivisions=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=position)
    obj = bpy.context.object
    obj.scale = scale
    obj.rotation_euler.z = rotation
    return g.finish(obj, name, material, min(scale) * 0.12)


def blade(name, x, y, height, width, angle, material):
    lean = width * 0.55
    profile = [(-width / 2, 0), (width / 2, 0), (width * 0.42, height * 0.55),
               (lean, height), (-width * 0.2, height * 0.52)]
    r.shifted(lambda: g.extrude(name, profile, -0.025, 0.025, material, 0.006), (x, y, 0), angle)


def grass_tuft(height_scale=1.0, radius_scale=1.0):
    for index in range(20):
        angle = math.tau * index / 20 + (index % 3) * 0.19
        radius = (0.18 + (index % 5) * 0.11) * radius_scale
        blade("Grass tuft blade", math.cos(angle) * radius, math.sin(angle) * radius,
              (0.42 + (index % 4) * 0.11) * height_scale,
              (0.12 + (index % 2) * 0.025) * radius_scale, angle, "Grass")
    for index in range(7):
        angle = math.tau * index / 7
        blade("Grass tuft dry blade", math.cos(angle) * 0.42 * radius_scale,
              math.sin(angle) * 0.42 * radius_scale,
              (0.34 + (index % 3) * 0.08) * height_scale,
              0.1 * radius_scale, angle + 0.3, "DryGrass")


def grass_short():
    grass_tuft(0.55, 0.78)


def grass_medium():
    grass_tuft()


def grass_tall():
    grass_tuft(1.45, 1.08)


def fern_patch():
    g.cylinder("Fern root crown", (0, 0, 0.06), 0.22, 0.12, "FernStem", 0.012, 12)
    for frond in range(8):
        angle = math.tau * frond / 8 + 0.16
        length = 0.68 + (frond % 3) * 0.16
        end = Vector((math.cos(angle) * length, math.sin(angle) * length, 0.56 + (frond % 2) * 0.16))
        g.beam("Fern curved stem", (0, 0, 0.08), end, 0.035, "FernStem")
        for step in (0.3, 0.48, 0.66, 0.82):
            center = end * step + Vector((0, 0, 0.08 * math.sin(step * math.pi)))
            side = Vector((-math.sin(angle), math.cos(angle), 0)) * (0.1 + step * 0.07)
            for sign in (-1, 1):
                leaf = ellipsoid("Fern leaflet", center + side * sign, (0.24, 0.075, 0.035),
                                 "Fern", angle + sign * 0.42)
                leaf.rotation_euler.x = sign * 0.12
        ellipsoid("Fern tip", end, (0.23, 0.07, 0.035), "FernLight", angle)


def flower(name, x, y, height, material):
    g.cylinder(name + " stem", (x, y, height / 2), 0.025, height, "FlowerStem", 0.004, 8)
    ellipsoid(name + " low leaf", (x - 0.1, y, height * 0.38), (0.2, 0.07, 0.035),
              "FlowerLeaf", 0.45)
    ellipsoid(name + " high leaf", (x + 0.1, y, height * 0.58), (0.18, 0.06, 0.03),
              "FlowerLeaf", -0.55)
    for index in range(6):
        angle = math.tau * index / 6
        center = (x + math.cos(angle) * 0.12, y + math.sin(angle) * 0.12, height)
        petal = ellipsoid(name + " petal", center, (0.16, 0.085, 0.045), material, angle)
        petal.rotation_euler.y = 0.12
    ellipsoid(name + " center", (x, y, height + 0.025), (0.095, 0.095, 0.075), "FlowerCenter", 0, 2)


def wildflower_patch():
    flowers = (
        (-0.62, -0.32, 0.66, "FlowerBlue"), (-0.22, 0.14, 0.82, "FlowerWhite"),
        (0.3, -0.38, 0.72, "FlowerRed"), (0.62, 0.22, 0.88, "FlowerYellow"),
        (-0.48, 0.48, 0.58, "FlowerYellow"), (0.08, 0.5, 0.7, "FlowerBlue"),
        (0.52, -0.02, 0.6, "FlowerWhite"), (0.0, -0.58, 0.76, "FlowerRed"),
    )
    for index, (x, y, height, material) in enumerate(flowers):
        flower("Wildflower " + str(index + 1), x, y, height, material)
    for index in range(12):
        angle = math.tau * index / 12
        blade("Wildflower ground leaf", math.cos(angle) * 0.62, math.sin(angle) * 0.48,
              0.28 + (index % 3) * 0.07, 0.11, angle, "FlowerLeaf")


def flower_border():
    for index in range(12):
        x = -1.55 + index * 0.28
        y = (-0.18, 0.18)[index % 2]
        material = ("FlowerRed", "FlowerWhite", "FlowerYellow", "FlowerBlue")[index % 4]
        flower("Border flower " + str(index + 1), x, y, 0.55 + (index % 3) * 0.09, material)
    for index in range(17):
        x = -1.65 + index * 0.2
        blade("Flower border foliage", x, 0, 0.3 + (index % 4) * 0.05, 0.11,
              -0.7 + (index % 5) * 0.32, "FlowerLeaf")


def tapered_branch(name, start, end, base_radius, tip_radius, material="Bark", sides=12):
    direction = Vector(end) - Vector(start)
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=base_radius, radius2=tip_radius,
                                   depth=direction.length, location=(Vector(start) + Vector(end)) / 2)
    obj = bpy.context.object
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    return g.finish(obj, name, material, min(tip_radius * 0.35, 0.018))


def broadleaf_tree(name, height, spread, trunk_radius, lobes):
    trunk_height = height - spread * 0.28
    tapered_branch(name + " trunk", (0, 0, 0), (0, 0, trunk_height),
                   trunk_radius, trunk_radius * 0.68, "Bark", 14)
    directions = (-2.55, -1.65, -0.65, 0.25, 1.25, 2.25)
    for index, angle in enumerate(directions):
        start = Vector((0, 0, trunk_height * (0.56 + (index % 3) * 0.07)))
        end = Vector((math.cos(angle) * spread * 0.46, math.sin(angle) * spread * 0.46,
                      height - spread * (0.38 + (index % 2) * 0.06)))
        tapered_branch(name + " main branch", start, end, trunk_radius * 0.44,
                       trunk_radius * 0.13)
        fork_start = start.lerp(end, 0.62)
        fork_end = end + Vector((-math.sin(angle) * spread * 0.22,
                                 math.cos(angle) * spread * 0.22, spread * 0.16))
        tapered_branch(name + " crown fork", fork_start, fork_end, trunk_radius * 0.16,
                       trunk_radius * 0.055)
    for index, (ox, oy, oz, size) in enumerate(lobes):
        ellipsoid(name + " crown", (ox * spread, oy * spread, height + oz * spread),
                  (size * spread, size * spread * 0.92, size * spread * 0.78),
                  "Foliage" if index % 3 else "FoliageLight", index * 0.37, 2)


def street_tree():
    lobes = ((0, 0, -0.06, 0.72), (-0.55, -0.08, -0.18, 0.48), (0.48, 0.16, -0.14, 0.5),
             (-0.12, 0.48, -0.12, 0.46), (0.08, -0.48, -0.16, 0.44))
    broadleaf_tree("Street tree", 6.7, 2.25, 0.38, lobes)


def shade_tree():
    lobes = ((0, 0, -0.08, 0.68), (-0.6, -0.12, -0.2, 0.48), (0.58, 0.05, -0.18, 0.5),
             (-0.15, 0.58, -0.18, 0.45), (0.12, -0.56, -0.16, 0.47),
             (-0.42, 0.42, -0.05, 0.4), (0.45, -0.4, -0.08, 0.42))
    broadleaf_tree("Shade tree", 9.2, 3.45, 0.58, lobes)


def alley_cypress():
    tapered_branch("Alley cypress trunk", (0, 0, 0), (0, 0, 8.5), 0.28, 0.14, "Bark", 12)
    layers = ((2.6, 1.35, 3.6), (4.4, 1.18, 3.8), (6.2, 0.96, 3.5), (7.7, 0.68, 2.9), (9.0, 0.38, 2.0))
    for index, (z, radius, depth) in enumerate(layers):
        bpy.ops.mesh.primitive_cone_add(vertices=16, radius1=radius, radius2=radius * 0.18,
                                       depth=depth, location=(0, 0, z))
        g.finish(bpy.context.object, "Alley cypress foliage layer", "Cypress" if index % 2 else "CypressLight", 0.02)


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
        "category": root["category"],
        "intended_use": root["intended_use"],
        "parts": sum(obj.type == "MESH" for obj in collection.objects),
        "triangles": triangles,
        "minimum": [min(point[axis] for point in points) for axis in range(3)],
        "maximum": [max(point[axis] for point in points) for axis in range(3)],
    }


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


def main():
    os.makedirs(REVIEW, exist_ok=True)
    os.makedirs(os.path.dirname(SOURCE), exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    base_scene = bpy.context.scene
    base_scene.name = "Editable Vegetation Assets"
    base_scene.unit_settings.system = "METRIC"
    r.setup_materials()
    materials = {
        "Grass": ("5F7D45", "plain"), "DryGrass": ("8A7B4E", "plain"),
        "Fern": ("456B43", "plain"), "FernLight": ("668654", "plain"),
        "FernStem": ("3F5835", "plain"), "FlowerStem": ("48613A", "plain"),
        "FlowerLeaf": ("587744", "plain"), "FlowerRed": ("A94C55", "plain"),
        "FlowerBlue": ("536EA6", "plain"), "FlowerWhite": ("D8D1BC", "plain"),
        "FlowerYellow": ("C69A45", "plain"), "FlowerCenter": ("6D4A27", "plain"),
        "Bark": ("55402F", "wood"), "Foliage": ("3F7043", "plain"),
        "FoliageLight": ("568052", "plain"), "Cypress": ("2F5A43", "plain"),
        "CypressLight": ("3C684C", "plain"),
    }
    for key, (color, kind) in materials.items():
        g.MATERIALS[key] = r.surface("City_Vegetation_" + key, color, kind)
    recipes = (
        ("GroundPlant_GrassShort", "ground_plant", "石畳の目地や道端の短い草", grass_short),
        ("GroundPlant_GrassTuft", "ground_plant", "建物際や空き地の中くらいの草", grass_medium),
        ("GroundPlant_GrassTall", "ground_plant", "未整備地や城壁際の高い草", grass_tall),
        ("GroundPlant_FernPatch", "ground_plant", "日陰や建物裏の隙間", fern_patch),
        ("Flower_WildPatch", "flower", "小広場や家の前", wildflower_patch),
        ("Flower_Border", "flower", "壁沿い・道路沿いの帯状空地", flower_border),
        ("Tree_AlleyCypress", "tree", "狭い路地や門周辺", alley_cypress),
        ("Tree_Street", "tree", "街路・中庭", street_tree),
        ("Tree_Shade", "tree", "広場・邸宅・公共施設脇", shade_tree),
    )
    assets = []
    for name, category, intended_use, recipe in recipes:
        collection, root = g.make_asset(name, recipe)
        root["category"] = category
        root["intended_use"] = intended_use
        assets.append((collection, root))
        print("CITY_VEGETATION_CREATED", name, flush=True)
    records = [model_record(collection, root) for collection, root in assets]
    with open(os.path.join(REVIEW, "model_manifest.json"), "w") as handle:
        json.dump({"blender": bpy.app.version_string, "models": records}, handle, indent=2, ensure_ascii=False)
    build_preview("Ground Plants Preview", assets[:4], [(-3.0, 0, 0), (-1.0, 0, 0), (1.1, 0, 0), (3.3, 0, 0)],
                  (7, -11, 6), (0, 0, 0.5), 7.5, "GroundPlants_Preview.png")
    build_preview("Flowers Preview", assets[4:6], [(-2.0, 0, 0), (1.5, 0, 0)],
                  (6, -10, 6), (0, 0, 0.45), 6.0, "Flowers_Preview.png")
    build_preview("Trees Preview", assets[6:], [(-5.0, 0, 0), (0, 0, 0), (5.5, 0, 0)],
                  (17, -24, 15), (0, 0, 4.8), 17.5, "Trees_Preview.png")
    build_preview("City Vegetation Overview", assets,
                  [(-7.0, -5, 0), (-5.0, -5, 0), (-2.8, -5, 0), (-0.4, -5, 0),
                   (2.2, -5, 0), (5.7, -5, 0), (-5.5, 3, 0), (0, 3, 0), (6, 3, 0)],
                  (23, -31, 19), (0, 0, 4.3), 24.0, "FantasyCityVegetation_Preview.png")
    bpy.context.window.scene = base_scene
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE)
    print("CITY_VEGETATION_COMPLETE", json.dumps(records, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
