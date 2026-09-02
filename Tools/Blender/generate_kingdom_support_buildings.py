import bpy
import importlib.util
import json
import math
import os
import sys
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "ArtSource", "Blender", "FantasyKingdomSupportBuildings.blend")
REVIEW = os.path.join(ROOT, "docs", "Art", "FantasyKingdomSupportBuildings")
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("fantasy_style", os.path.join(os.path.dirname(__file__), "generate_realistic_fantasy_buildings.py"))
r = importlib.util.module_from_spec(spec)
spec.loader.exec_module(r)
g = r.g


def windows(width, depth, bottom, height, count, sides=True):
    for index in range(count):
        x = -width * 0.38 + width * 0.76 * index / max(1, count - 1)
        r.lancet(x, -depth / 2 - 0.02, bottom, 0.72, height)
    if sides:
        for side in (-1, 1):
            for y in (-depth * 0.22, depth * 0.22):
                r.shifted(lambda y=y: r.lancet(y, -width / 2 - 0.02, bottom, 0.7, height), angle=side * math.pi / 2)


def chimney(x, y, base, height=2.6):
    g.box("Masonry chimney", (x, y, base + height / 2), (0.76, 0.82, height), "Stone", 0.016)
    g.box("Chimney cap", (x, y, base + height + 0.08), (0.96, 1.02, 0.2), "StoneTrim", 0.012)


def common_building(label, width, depth, wall_height, roof_height, wall="Plaster", window_count=3):
    g.box(label + " footing", (0, 0, 0.24), (width + 0.8, depth + 0.8, 0.48), "StoneShade", 0.022)
    g.box(label + " stone plinth", (0, 0, 1.02), (width + 0.32, depth + 0.32, 1.56), "Stone", 0.018)
    g.box(label + " walls", (0, 0, (wall_height + 1.8) / 2), (width, depth, wall_height - 1.8), wall, 0.018)
    g.box(label + " eaves course", (0, 0, wall_height - 0.08), (width + 0.42, depth + 0.42, 0.2), "Timber", 0.014)
    r.roof(width + 1.05, depth + 1.05, wall_height, roof_height)
    r.door(0, -depth / 2 - 0.08, 1.52, 2.72, 0.18)
    g.box(label + " doorstep", (0, -depth / 2 - 0.52, 0.13), (2.35, 0.88, 0.26), "StoneTrim", 0.016)
    windows(width, depth, 2.05, 1.52, window_count)


def granary():
    common_building("Granary", 10.6, 7.6, 6.6, 2.8, "OchrePlaster", 3)
    for x in (-3.9, -1.3, 1.3, 3.9):
        g.box("Granary oak frame", (x, -3.83, 4.35), (0.22, 0.18, 4.28), "Timber", 0.012)
    for x in (-3.2, 3.2):
        g.box("Grain vent", (x, -3.93, 5.0), (0.92, 0.16, 0.92), "Iron", 0.08)
    g.box("Loading canopy", (0, -4.45, 3.35), (5.6, 1.7, 0.22), "Roof", 0.012)
    for x in (-2.5, 2.5):
        g.box("Loading canopy post", (x, -4.8, 1.65), (0.2, 0.2, 3.3), "Timber", 0.01)


def warehouse():
    common_building("Warehouse", 13.2, 8.8, 6.1, 2.4, "Stone", 2)
    for x in (-4.4, 4.4):
        r.door(x, -4.48, 2.25, 3.45, 0.12)
    g.box("Warehouse loading platform", (0, -5.05, 0.36), (11.8, 1.5, 0.72), "StoneTrim", 0.018)
    for x in (-5.6, -2.8, 0, 2.8, 5.6):
        g.box("Warehouse buttress", (x, 4.5, 2.65), (0.38, 0.5, 5.3), "StoneShade", 0.012)


def bakery():
    common_building("Bakery", 7.8, 6.8, 5.7, 2.35, "OchrePlaster", 2)
    chimney(2.45, 1.5, 4.9, 3.8)
    g.box("Bakery awning", (-1.5, -4.0, 3.15), (4.0, 1.55, 0.18), "Banner", 0.012)
    for x in (-3.0, 0.0):
        g.box("Bakery awning post", (x, -4.42, 1.58), (0.16, 0.16, 3.16), "Timber", 0.009)
    g.box("Bread sign", (2.55, -3.58, 3.8), (0.95, 0.14, 0.95), "Brass", 0.16)


def stable():
    g.box("Stable footing", (0, 0, 0.22), (14.6, 8.8, 0.44), "StoneShade", 0.02)
    g.box("Stable walls", (0, 0, 2.72), (14.1, 8.3, 5.0), "OchrePlaster", 0.018)
    for x in (-5.2, -1.75, 1.75, 5.2):
        r.door(x, -4.23, 2.35, 3.95, 0.12)
        g.box("Stable stall awning", (x, -4.75, 4.15), (3.15, 1.45, 0.18), "Roof", 0.01)
    for x in (-7.0, -3.5, 0, 3.5, 7.0):
        g.box("Stable timber post", (x, -4.16, 2.65), (0.22, 0.2, 5.0), "Timber", 0.012)
    r.roof(15.2, 9.4, 5.3, 3.0)
    g.box("Stable hay loft door", (0, -4.23, 5.9), (2.1, 0.18, 1.8), "Door", 0.016)


def barracks():
    common_building("Barracks", 15.2, 10.2, 7.1, 2.8, "Stone", 5)
    for x in (-6.4, 6.4):
        g.box("Barracks corner tower", (x, 0, 4.15), (2.4, 10.8, 7.8), "Stone", 0.018)
        r.shifted(lambda: r.roof(3.2, 11.5, 8.05, 2.0), (x, 0, 0))
    for x in (-5.1, -2.55, 0, 2.55, 5.1):
        g.box("Barracks front pier", (x, -5.2, 3.45), (0.28, 0.28, 6.5), "StoneTrim", 0.01)
    r.banner(-4.8, -5.38, 7.0, 0.8, 2.0)
    r.banner(4.8, -5.38, 7.0, 0.8, 2.0)


def guildhall():
    common_building("Guildhall", 14.0, 10.6, 8.7, 3.6, "Plaster", 4)
    g.box("Guildhall upper timber band", (0, -5.33, 6.55), (14.25, 0.2, 0.3), "Timber", 0.014)
    for x in (-6.5, -3.25, 0, 3.25, 6.5):
        g.box("Guildhall oak upright", (x, -5.35, 6.8), (0.22, 0.2, 3.5), "Timber", 0.012)
    g.extrude("Guild crest", [(-0.7, 8.0), (0.7, 8.0), (0.55, 6.85), (0, 6.35), (-0.55, 6.85)], -5.48, -5.4, "Banner", 0.01)
    g.box("Guild crest medallion", (0, -5.51, 7.2), (0.62, 0.08, 0.62), "Brass", 0.12)
    chimney(-4.7, 2.8, 7.3, 3.5)


def bathhouse():
    g.box("Bathhouse footing", (0, 0, 0.25), (13.8, 11.8, 0.5), "StoneShade", 0.022)
    g.box("Bathhouse ashlar", (0, 0, 3.05), (13.2, 11.2, 5.6), "Stone", 0.018)
    for x in (-4.7, 0, 4.7):
        r.lancet(x, -5.65, 2.0, 1.1, 1.8)
    r.door(0, -5.7, 1.7, 2.9, 0.2)
    g.box("Bathhouse cornice", (0, 0, 5.75), (13.55, 11.55, 0.3), "StoneTrim", 0.014)
    g.cylinder("Bathhouse drum", (0, 0, 6.35), 4.0, 1.2, "StoneTrim", 0.014, 32)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=1, location=(0, 0, 7.35))
    bpy.context.object.scale = (4.2, 4.2, 2.1)
    g.finish(bpy.context.object, "Bathhouse copper dome", "Brass", 0.012)
    chimney(-4.9, 3.5, 5.3, 3.4)


def chapel():
    g.box("Chapel footing", (0, 0, 0.26), (10.4, 15.4, 0.52), "StoneShade", 0.02)
    g.box("Chapel nave", (0, 0.8, 4.45), (9.6, 13.0, 8.4), "Stone", 0.018)
    r.roof(10.8, 14.2, 8.65, 4.4)
    r.door(0, -5.78, 1.75, 3.2, 0.18)
    for y in (-2.4, 0.7, 3.8):
        r.shifted(lambda y=y: r.lancet(y, -4.86, 3.1, 1.0, 2.8), angle=math.pi / 2)
        r.shifted(lambda y=y: r.lancet(-y, -4.86, 3.1, 1.0, 2.8), angle=-math.pi / 2)
    g.box("Chapel bell tower", (0, -5.0, 7.8), (5.0, 4.8, 14.8), "Stone", 0.018)
    for x in (-1.15, 1.15):
        r.lancet(x, -7.43, 9.4, 0.72, 2.4)
    r.shifted(lambda: r.roof(5.8, 5.6, 15.2, 4.0), (0, -5.0, 0))
    g.cylinder("Chapel finial", (0, -5.0, 19.45), 0.1, 1.1, "Brass", 0.005, 12)


def merchant_house():
    common_building("Merchant house", 9.2, 8.0, 7.5, 3.1, "OchrePlaster", 3)
    g.box("Merchant jettied floor", (0, 0, 6.0), (9.75, 8.55, 2.75), "Plaster", 0.018)
    for x in (-4.45, -2.2, 0, 2.2, 4.45):
        g.box("Merchant timber upright", (x, -4.3, 6.0), (0.2, 0.18, 2.7), "Timber", 0.01)
    g.box("Merchant shop awning", (-1.6, -4.75, 3.4), (5.2, 1.6, 0.18), "Banner", 0.01)
    chimney(3.1, 2.0, 7.0, 3.4)


def workshop_house():
    common_building("Workshop house", 8.2, 7.4, 6.3, 2.6, "Plaster", 2)
    g.box("Workshop shutters", (-2.1, -3.78, 2.15), (2.2, 0.18, 1.8), "Door", 0.014)
    g.box("Workshop canopy", (-2.1, -4.28, 3.4), (3.6, 1.45, 0.18), "Roof", 0.01)
    for x in (-3.35, -0.85):
        g.box("Workshop canopy post", (x, -4.65, 1.7), (0.17, 0.17, 3.4), "Timber", 0.01)
    chimney(2.6, 1.6, 5.8, 3.1)


def main():
    os.makedirs(REVIEW, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    r.setup_materials()
    g.MATERIALS["OchrePlaster"] = r.surface("Support_OchrePlaster", "B69C73", "plain")
    assets = []
    recipes = [
        ("Granary", "穀物庫", granary),
        ("Warehouse", "倉庫", warehouse),
        ("Bakery", "パン工房", bakery),
        ("Stable", "厩舎", stable),
        ("Barracks", "兵舎", barracks),
        ("Guildhall", "職人組合会館", guildhall),
        ("Bathhouse", "公衆浴場", bathhouse),
        ("Chapel", "礼拝堂", chapel),
        ("MerchantHouse", "商人邸", merchant_house),
        ("WorkshopHouse", "工房付き住宅", workshop_house),
    ]
    for name, label, recipe in recipes:
        collection, root = g.make_asset(name, recipe)
        root["facility_type"] = label
        assets.append((collection, root))
        print("SUPPORT_BUILDING_CREATED", name, flush=True)
    records = []
    for collection, root in assets:
        points = [obj.matrix_parent_inverse @ obj.matrix_basis @ Vector(vertex)
                  for obj in collection.objects if obj.type == "MESH" for vertex in obj.bound_box]
        records.append({
            "name": root.name,
            "facility_type": root["facility_type"],
            "parts": sum(obj.type == "MESH" for obj in collection.objects),
            "minimum": [min(point[axis] for point in points) for axis in range(3)],
            "maximum": [max(point[axis] for point in points) for axis in range(3)],
        })
    with open(os.path.join(REVIEW, "model_manifest.json"), "w") as handle:
        json.dump({"blender": bpy.app.version_string, "models": records}, handle, indent=2, ensure_ascii=False)
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE)
    print("SUPPORT_BUILDINGS_COMPLETE", json.dumps(records, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
