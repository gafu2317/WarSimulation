import bpy
import importlib.util
import json
import math
import os
import sys
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "ArtSource", "Blender", "FantasySchools.blend")
REVIEW = os.path.join(ROOT, "docs", "Art", "FantasySchools")
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


def cone_roof(name, x, y, radius, base, height, material="Roof"):
    bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=radius, radius2=0.1, depth=height,
                                   location=(x, y, base + height / 2))
    g.finish(bpy.context.object, name, material, 0.018)
    g.cylinder(name + " rim", (x, y, base + 0.04), radius + 0.08, 0.16, "RoofEdge", 0.01, 24)


def torus(name, position, major, minor, material, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=32, minor_segments=8,
                                    location=position, rotation=rotation)
    return g.finish(bpy.context.object, name, material, min(minor / 3, 0.01))


def crystal(name, position, radius, height, material):
    x, y, z = position
    vertices = [(x, y, z + height / 2)]
    vertices += [(x + math.cos(math.tau * i / 6) * radius, y + math.sin(math.tau * i / 6) * radius,
                  z + height * 0.12) for i in range(6)]
    vertices += [(x + math.cos(math.tau * i / 6) * radius * 0.78,
                  y + math.sin(math.tau * i / 6) * radius * 0.78, z - height * 0.18) for i in range(6)]
    vertices.append((x, y, z - height / 2))
    faces = []
    for i in range(6):
        nxt = (i + 1) % 6
        faces.append((0, 1 + i, 1 + nxt))
        faces.append((1 + i, 7 + i, 7 + nxt, 1 + nxt))
        faces.append((13, 7 + nxt, 7 + i))
    return g.mesh(name, vertices, faces, material, 0.018)


def sword(name, center, length, angle, material="Iron"):
    x, y, z = center
    dx, dz = math.sin(angle), math.cos(angle)
    start = Vector((x - dx * length * 0.38, y, z - dz * length * 0.38))
    end = Vector((x + dx * length * 0.5, y, z + dz * length * 0.5))
    g.beam(name + " blade", start, end, 0.12, material)
    guard = Vector((x - dx * length * 0.28, y, z - dz * length * 0.28))
    px, pz = dz, -dx
    g.beam(name + " crossguard", guard + Vector((px * 0.46, 0, pz * 0.46)),
           guard - Vector((px * 0.46, 0, pz * 0.46)), 0.13, "Brass")
    grip_end = start - Vector((dx * length * 0.14, 0, dz * length * 0.14))
    g.beam(name + " grip", start, grip_end, 0.16, "Timber")
    g.cylinder(name + " pommel", grip_end, 0.18, 0.13, "Brass", 0.008, 12).rotation_euler.x = math.pi / 2


def shield_emblem(x, y, z):
    profile = [(x - 1.35, z + 1.3), (x + 1.35, z + 1.3), (x + 1.15, z - 0.45),
               (x, z - 1.55), (x - 1.15, z - 0.45)]
    g.extrude("Warrior Academy shield crest", profile, y - 0.12, y + 0.04, "WarriorRed", 0.04)
    g.box("Warrior Academy shield boss", (x, y - 0.17, z + 0.05), (0.58, 0.16, 0.58), "Brass", 0.09)
    sword("Warrior Academy crossed sword", (x, y - 0.2, z), 3.3, 0.72)
    sword("Warrior Academy crossed sword", (x, y - 0.23, z), 3.3, -0.72)


def battlement(name, x, y, width, depth, base):
    g.box(name + " coping", (x, y, base), (width + 0.25, depth + 0.25, 0.24), "StoneTrim", 0.012)
    positions = []
    for side in (-1, 1):
        for offset in (-0.35, 0, 0.35):
            positions.append((x + side * width / 2 * 0.88, y + offset * depth, 0.72, 0.58))
            positions.append((x + offset * width, y + side * depth / 2 * 0.88, 0.58, 0.72))
    for px, py, sx, sy in positions:
        g.box(name + " merlon", (px, py, base + 0.45), (sx, sy, 0.72), "Stone", 0.015)


def training_dummy(x, y):
    g.cylinder("Warrior Academy training post", (x, y, 1.45), 0.16, 2.9, "Timber", 0.01, 12)
    g.beam("Warrior Academy training arm", (x - 0.9, y, 1.9), (x + 0.9, y, 1.9), 0.14, "Timber")
    g.cylinder("Warrior Academy straw head", (x, y, 2.75), 0.43, 0.55, "OchrePlaster", 0.04, 12)
    g.box("Warrior Academy target plate", (x, y - 0.2, 1.35), (1.05, 0.22, 1.15), "WarriorRed", 0.08)


def warrior_banner_tower():
    g.box("Warrior Academy high banner tower", (0, 4.7, 10.0), (6.4, 5.6, 19.4), "Stone", 0.025)
    battlement("Warrior Academy high tower crown", 0, 4.7, 6.5, 5.7, 19.8)
    r.lancet(0, 1.86, 13.0, 1.15, 2.8)
    r.banner(0, 1.76, 18.5, 1.15, 3.4)
    for side in (-1, 1):
        g.box("Warrior Academy high tower buttress", (side * 2.75, 1.82, 7.0),
              (0.7, 0.8, 13.4), "StoneShade", 0.03)


def warrior_review_balcony():
    g.box("Warrior Academy instructors balcony", (0, -6.72, 12.35), (6.9, 1.15, 0.42), "StoneTrim", 0.025)
    g.box("Warrior Academy balcony parapet", (0, -7.25, 13.05), (7.2, 0.28, 1.35), "Stone", 0.02)
    for x in (-2.7, -1.35, 0, 1.35, 2.7):
        g.box("Warrior Academy balcony shield", (x, -7.42, 13.05), (0.72, 0.16, 0.92),
              "WarriorRed", 0.12)


def warrior_academy():
    g.box("Warrior Academy foundation", (0, 0, 0.3), (26.0, 26.0, 0.6), "StoneShade", 0.025)
    g.box("Warrior Academy great hall", (0, 2.2, 6.0), (14.5, 12.5, 11.4), "Stone", 0.02)
    warrior_banner_tower()
    r.shifted(lambda: gable_roof("Warrior Academy great hall roof", 15.8, 13.6, 11.85, 5.0), (0, 2.2, 0))
    for x in (-5.6, -2.8, 2.8, 5.6):
        r.lancet(x, -4.08, 2.1, 0.8, 1.8)
    for side in (-1, 1):
        x = side * 8.8
        g.box("Warrior Academy drill tower", (x, -2.8, 7.2), (5.0, 6.1, 13.8), "Stone", 0.02)
        battlement("Warrior Academy tower crown", x, -2.8, 5.1, 6.2, 14.2)
        for z in (3.4, 8.2):
            r.lancet(x, -5.88, z, 0.72, 1.6)
        r.banner(x, -5.96, 11.6, 0.8, 2.5)
    g.box("Warrior Academy gatehouse", (0, -3.5, 6.8), (6.8, 6.0, 12.4), "Stone", 0.02)
    battlement("Warrior Academy gatehouse crown", 0, -3.5, 6.9, 6.1, 13.2)
    r.door(0, -6.58, 2.0, 3.5, 0.25)
    g.box("Warrior Academy entry step", (0, -7.25, 0.18), (5.2, 1.35, 0.36), "StoneTrim", 0.018)
    shield_emblem(0, -6.62, 9.2)
    warrior_review_balcony()
    for x in (-9.0, -5.5, 5.5, 9.0):
        training_dummy(x, -8.4)
    for side in (-1, 1):
        x = side * 6.5
        g.box("Warrior Academy weapon rack", (x, -8.8, 1.2), (4.0, 0.45, 2.4), "Timber", 0.018)
        for offset in (-1.35, -0.45, 0.45, 1.35):
            sword("Warrior Academy rack sword", (x + offset, -9.07, 1.35), 2.0, 0)


def arcane_rune(x, y, z, radius):
    torus("Arcane Academy rune circle", (x, y, z), radius, 0.12, "ArcaneRune", (math.pi / 2, 0, 0))
    torus("Arcane Academy inner rune circle", (x, y - 0.03, z), radius * 0.58, 0.07, "Brass", (math.pi / 2, 0, 0))
    star = []
    for index in range(10):
        angle = math.pi / 2 + math.pi * 2 * index / 10
        distance = radius * (0.82 if index % 2 == 0 else 0.34)
        star.append((x + math.cos(angle) * distance, z + math.sin(angle) * distance))
    g.extrude("Arcane Academy five point star sigil", star, y - 0.13, y - 0.08, "ArcaneGold", 0.01)
    crystal("Arcane Academy rune crystal", (x, y - 0.2, z), radius * 0.22, radius * 0.9, "ArcaneCrystal")


def arcane_orrery(x, y, z, radius):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=radius * 0.42, location=(x, y, z))
    g.finish(bpy.context.object, "Arcane Academy orrery core", "ArcaneCrystal", 0.025)
    torus("Arcane Academy orrery equator", (x, y, z), radius, 0.09, "ArcaneGold")
    torus("Arcane Academy orrery meridian", (x, y, z), radius, 0.09, "ArcaneGold", (math.pi / 2, 0, 0))
    torus("Arcane Academy orrery tilted orbit", (x, y, z), radius * 1.28, 0.07,
          "ArcaneRune", (0.65, 0.35, 0.2))


def arcane_spellbook(x, y):
    g.box("Arcane Academy spellbook pedestal", (x, y, 1.15), (1.2, 1.2, 2.3), "StoneTrim", 0.08)
    left = g.box("Arcane Academy open spellbook left page", (x - 0.72, y - 0.08, 2.65),
                 (1.5, 1.05, 0.12), "Parchment", 0.035)
    left.rotation_euler.y = -0.32
    right = g.box("Arcane Academy open spellbook right page", (x + 0.72, y - 0.08, 2.65),
                  (1.5, 1.05, 0.12), "Parchment", 0.035)
    right.rotation_euler.y = 0.32
    g.box("Arcane Academy spellbook spine", (x, y - 0.08, 2.48), (0.16, 1.12, 0.22), "ArcaneGold", 0.035)


def arcane_tower(name, x, y, radius, height, spire):
    g.cylinder(name + " foundation", (x, y, 0.3), radius + 0.35, 0.6, "StoneShade", 0.02, 32)
    g.cylinder(name + " shaft", (x, y, height / 2 + 0.3), radius, height, "ArcaneStone", 0.018, 32)
    for z in (1.0, height * 0.48, height - 0.4):
        g.cylinder(name + " carved course", (x, y, z), radius + 0.15, 0.22, "StoneTrim", 0.012, 32)
    torus(name + " arcane gold band", (x, y, height * 0.72), radius + 0.19, 0.08, "ArcaneGold")
    for angle in (0, math.pi / 2, math.pi, math.pi * 1.5):
        r.shifted(lambda: r.lancet(0, -radius - 0.03, height * 0.55, 0.7, 2.0), (x, y, 0), angle)
    cone_roof(name + " star roof", x, y, radius + 0.8, height + 0.28, spire, "ArcaneRoof")
    crystal(name + " crown crystal", (x, y, height + spire + 0.85), 0.5, 2.2, "ArcaneCrystal")


def arcane_observatory_balcony():
    g.cylinder("Arcane Academy observatory balcony", (0, 4.0, 14.8), 4.35, 0.36,
               "ArcaneGold", 0.018, 32)
    torus("Arcane Academy observatory railing", (0, 4.0, 15.65), 4.12, 0.1, "ArcaneGold")
    for index in range(12):
        angle = math.tau * index / 12
        x, y = math.cos(angle) * 4.12, 4.0 + math.sin(angle) * 4.12
        g.cylinder("Arcane Academy observatory post", (x, y, 15.2), 0.08, 0.95,
                   "ArcaneGold", 0.008, 10)


def arcane_sky_bridges():
    for side in (-1, 1):
        start = Vector((side * 5.7, -0.2, 13.1))
        end = Vector((side * 2.7, 3.0, 15.0))
        g.beam("Arcane Academy suspended bridge", start, end, 0.32, "ArcaneStone")
        for offset in (-0.42, 0.42):
            g.beam("Arcane Academy suspended bridge rail", start + Vector((0, offset, 0.55)),
                   end + Vector((0, offset, 0.55)), 0.09, "ArcaneGold")


def arcane_academy():
    g.box("Arcane Academy foundation", (0, 0, 0.3), (26.0, 26.0, 0.6), "StoneShade", 0.025)
    g.box("Arcane Academy lecture hall", (0, 1.4, 6.0), (14.5, 12.8, 11.4), "ArcaneStone", 0.02)
    r.shifted(lambda: gable_roof("Arcane Academy lecture hall roof", 15.8, 14.0, 11.85, 4.8, "ArcaneRoof"), (0, 1.4, 0))
    for x in (-5.5, -2.75, 2.75, 5.5):
        r.lancet(x, -5.05, 2.0, 0.95, 2.4, "Brass")
    arcane_tower("Arcane Academy grand tower", 0, 4.0, 3.8, 17.0, 6.0)
    arcane_tower("Arcane Academy west tower", -7.9, -1.5, 2.55, 14.0, 5.0)
    arcane_tower("Arcane Academy east tower", 7.9, -1.5, 2.55, 14.0, 5.0)
    arcane_observatory_balcony()
    arcane_sky_bridges()
    g.box("Arcane Academy entrance portal", (0, -5.45, 5.1), (5.2, 2.8, 9.5), "ArcaneStone", 0.018)
    r.door(0, -6.92, 1.8, 3.3, 0.22)
    g.box("Arcane Academy entry step", (0, -7.62, 0.16), (5.0, 1.5, 0.32), "StoneTrim", 0.018)
    arcane_rune(0, -6.94, 7.1, 1.55)
    arcane_orrery(0, -7.28, 11.0, 1.65)
    arcane_spellbook(-5.7, -8.7)
    for side in (-1, 1):
        r.banner(side * 2.0, -6.96, 4.9, 0.7, 2.2)
    for x in (-9.0, -6.7, 6.7, 9.0):
        crystal("Arcane Academy courtyard crystal", (x, -7.6, 1.25), 0.48, 2.5, "ArcaneCrystal")
        torus("Arcane Academy crystal ward", (x, -7.6, 0.12), 0.82, 0.08, "ArcaneRune")


def sacred_tree():
    g.cylinder("Spirit Academy sacred tree trunk", (0, 0, 9.0), 1.15, 18.0, "SacredBark", 0.05, 16)
    for angle, height in ((0.35, 15.0), (1.3, 13.8), (2.25, 14.6), (3.25, 14.1), (4.3, 15.3), (5.2, 13.6)):
        start = Vector((0, 0, height))
        end = Vector((math.cos(angle) * 3.5, math.sin(angle) * 3.5, height + 3.0))
        g.beam("Spirit Academy sacred branch", start, end, 0.5, "SacredBark")
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=2.55, location=end)
        leaf = g.finish(bpy.context.object, "Spirit Academy sacred crown", "SpiritFoliage", 0.05)
        leaf.scale.z = 0.78
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=3.6, location=(0, 0, 20.3))
    crown = g.finish(bpy.context.object, "Spirit Academy central crown", "SpiritFoliage", 0.05)
    crown.scale.z = 0.82


def spirit_tower(name, x, y):
    g.cylinder(name + " foundation", (x, y, 0.3), 2.75, 0.6, "StoneShade", 0.025, 24)
    g.cylinder(name + " shaft", (x, y, 8.1), 2.45, 15.6, "SpiritStone", 0.025, 24)
    for z in (1.0, 7.5, 15.55):
        g.cylinder(name + " carved band", (x, y, z), 2.62, 0.24, "SpiritTrim", 0.012, 24)
    r.shifted(lambda: r.lancet(0, -2.5, 6.0, 0.85, 2.6, "SpiritTrim"), (x, y, 0))
    cone_roof(name + " sanctuary spire", x, y, 3.15, 15.85, 5.2, "SpiritRoof")
    crystal(name + " carved leaf finial", (x, y, 21.65), 0.42, 1.4, "SpiritRune")


def spirit_mask(x, y, z):
    profile = [(x, z + 1.7), (x + 1.35, z + 0.75), (x + 1.05, z - 0.85),
               (x, z - 1.65), (x - 1.05, z - 0.85), (x - 1.35, z + 0.75)]
    g.extrude("Spirit Academy guardian spirit mask", profile, y - 0.12, y + 0.04, "SpiritRune", 0.04)
    for side in (-1, 1):
        g.box("Spirit Academy guardian mask eye", (x + side * 0.48, y - 0.17, z + 0.15),
              (0.42, 0.16, 0.22), "SpiritPupil", 0.06).rotation_euler.y = side * 0.28
        g.beam("Spirit Academy guardian antler", (x + side * 0.72, y, z + 1.05),
               (x + side * 2.0, y, z + 2.45), 0.16, "SacredBark")
        g.beam("Spirit Academy guardian antler branch", (x + side * 1.35, y, z + 1.72),
               (x + side * 2.05, y, z + 1.72), 0.11, "SacredBark")


def spirit_banner(x, y, z):
    g.beam("Spirit Academy banner rail", (x - 0.7, y, z + 0.1), (x + 0.7, y, z + 0.1), 0.06, "Iron")
    profile = [(x - 0.52, z), (x + 0.52, z), (x + 0.52, z - 2.3),
               (x, z - 2.75), (x - 0.52, z - 2.3)]
    g.extrude("Spirit Academy green standard", profile, y - 0.012, y + 0.018, "SpiritCloth", 0)
    leaf = [(x, z - 0.42), (x + 0.25, z - 0.93), (x, z - 1.42), (x - 0.25, z - 0.93)]
    g.extrude("Spirit Academy leaf heraldry", leaf, y - 0.033, y - 0.02, "SpiritTrim", 0)


def spirit_academy():
    g.box("Spirit Academy foundation", (0, 0, 0.3), (26.0, 26.0, 0.6), "StoneShade", 0.025)
    g.box("Spirit Academy sanctuary hall", (0, 4.2, 6.0), (14.8, 10.0, 11.4), "SpiritStone", 0.02)
    for side in (-1, 1):
        r.shifted(lambda: gable_roof("Spirit Academy sanctuary wing roof", 7.5, 11.2, 11.85, 5.2, "SpiritRoof"),
                  (side * 4.2, 4.2, 0))
    for x in (-5.6, -2.8, 2.8, 5.6):
        r.lancet(x, -0.86, 2.0, 0.95, 2.25, "SpiritTrim")
        r.lancet(x, -0.88, 7.0, 0.82, 2.1, "SpiritTrim")
    spirit_tower("Spirit Academy west elder tower", -6.6, 2.0)
    spirit_tower("Spirit Academy east elder tower", 6.6, 2.0)
    g.box("Spirit Academy ceremonial gatehouse", (0, -1.15, 6.3), (5.8, 3.4, 12.0), "SpiritStone", 0.025)
    r.shifted(lambda: gable_roof("Spirit Academy ceremonial gatehouse roof", 6.7, 4.2, 12.45, 4.3, "SpiritRoof"),
              (0, -1.15, 0))
    r.door(0, -2.92, 2.0, 3.8, 0.22)
    g.box("Spirit Academy ceremonial entry step", (0, -3.72, 0.18), (5.3, 1.55, 0.36), "SpiritTrim", 0.018)
    spirit_mask(0, -2.98, 8.9)
    for side in (-1, 1):
        g.box("Spirit Academy timber buttress", (side * 2.7, -2.94, 5.5), (0.42, 0.42, 10.4), "SacredBark", 0.025)
        g.beam("Spirit Academy branching facade brace", (side * 2.7, -3.05, 7.6),
               (side * 4.6, -1.0, 10.8), 0.2, "SacredBark")
    for side in (-1, 1):
        x = side * 9.2
        g.box("Spirit Academy open pavilion roof", (x, 2.0, 7.2), (5.3, 10.5, 0.24), "SpiritRoof", 0.018)
        for px in (x - 2.25, x + 2.25):
            for y in (-2.7, 6.7):
                g.cylinder("Spirit Academy pavilion column", (px, y, 3.5), 0.24, 7.0, "Timber", 0.015, 12)
        g.box("Spirit Academy pavilion beam", (x, -2.7, 6.92), (5.2, 0.32, 0.36), "Timber", 0.014)
        g.box("Spirit Academy pavilion beam", (x, 6.7, 6.92), (5.2, 0.32, 0.36), "Timber", 0.014)
        spirit_banner(x, -2.92, 6.55)
    r.shifted(sacred_tree, (0, 4.2, 0))
    for end in ((-5.0, -1.0, 0.45), (-2.8, -3.5, 0.38), (2.8, -3.5, 0.38), (5.0, -1.0, 0.45)):
        g.beam("Spirit Academy exposed sacred root", (0, 3.8, 0.75), end, 0.34, "SacredBark")
    for side in (-1, 1):
        x = side * 7.6
        for y in (-7.2, -4.0):
            g.cylinder("Spirit Academy standing stone", (x, y, 2.05), 0.68, 4.0, "SpiritStone", 0.08, 8)
            torus("Spirit Academy standing stone sigil", (x, y - 0.7, 2.35), 0.52, 0.08,
                  "SpiritRune", (math.pi / 2, 0, 0))


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
        "visual_identity": root["visual_identity"],
        "parts": sum(obj.type == "MESH" for obj in collection.objects),
        "triangles": triangles,
        "minimum": [min(point[axis] for point in points) for axis in range(3)],
        "maximum": [max(point[axis] for point in points) for axis in range(3)],
    }


def build_preview(name, assets, positions, camera_position, target, scale, filename, height=1200):
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
    scene.render.resolution_y = height
    g.camera_at(scene, camera_position, target, scale)
    g.render(scene, filename)


def main():
    os.makedirs(REVIEW, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    base_scene = bpy.context.scene
    base_scene.name = "Editable Assets"
    base_scene.unit_settings.system = "METRIC"
    r.setup_materials()
    g.MATERIALS["OchrePlaster"] = r.surface("School_OchrePlaster", "A98C67", "plain")
    g.MATERIALS["WarriorRed"] = r.surface("Warrior_Academy_Red", "7C2830", "plain")
    g.MATERIALS["ArcaneStone"] = r.surface("Arcane_Academy_Stone", "777888", "stone")
    g.MATERIALS["ArcaneRoof"] = r.surface("Arcane_Academy_Roof", "332752", "plain")
    g.MATERIALS["ArcaneCrystal"] = r.surface("Arcane_Academy_Crystal", "315FA8", "plain")
    g.MATERIALS["ArcaneRune"] = r.surface("Arcane_Academy_Runes", "7554A3", "plain")
    g.MATERIALS["ArcaneGold"] = r.surface("Arcane_Academy_Gold", "B28A3D", "metal")
    g.MATERIALS["Parchment"] = r.surface("Arcane_Academy_Parchment", "C9B98D", "plain")
    g.MATERIALS["SpiritStone"] = r.surface("Spirit_Academy_Stone", "7C897C", "stone")
    g.MATERIALS["SpiritRoof"] = r.surface("Spirit_Academy_Roof", "385349", "plain")
    g.MATERIALS["SpiritRune"] = r.surface("Spirit_Academy_Runes", "6DAA91", "plain")
    g.MATERIALS["SpiritTrim"] = r.surface("Spirit_Academy_Trim", "93A98E", "plain")
    g.MATERIALS["SpiritCloth"] = r.surface("Spirit_Academy_Cloth", "28513A", "plain")
    g.MATERIALS["SpiritFoliage"] = r.surface("Spirit_Academy_Foliage", "3F7948", "plain")
    g.MATERIALS["SacredBark"] = r.surface("Spirit_Academy_Sacred_Bark", "57422F", "wood")
    g.MATERIALS["SpiritPupil"] = r.surface("Spirit_Pupil", "24251F", "plain")
    recipes = (
        ("WarriorAcademy", "戦士学校", "高い軍旗塔、教官閲兵台、剣盾紋章、訓練人形、武器棚", warrior_academy),
        ("ArcaneAcademy", "魔法学校", "三本の魔術塔、天文観測台、空中回廊、五芒星魔法陣、空中儀、呪文書", arcane_academy),
        ("SpiritAcademy", "精霊使役学校", "双塔聖堂、校舎を貫く聖樹、守護精霊面、露出した根、葉紋章", spirit_academy),
    )
    assets = []
    for name, label, identity, recipe in recipes:
        collection, root = g.make_asset(name, recipe)
        root["facility_type"] = label
        root["visual_identity"] = identity
        assets.append((collection, root))
        print("FANTASY_SCHOOL_CREATED", name, flush=True)
    records = [model_record(collection, root) for collection, root in assets]
    with open(os.path.join(REVIEW, "model_manifest.json"), "w") as handle:
        json.dump({"blender": bpy.app.version_string, "models": records}, handle, indent=2, ensure_ascii=False)

    previews = (
        ("Warrior Academy Preview", [assets[0]], [(0, 0, 0)], (33, -46, 33), (0, 0, 10), 42, "WarriorAcademy_Preview.png"),
        ("Arcane Academy Preview", [assets[1]], [(0, 0, 0)], (34, -47, 35), (0, 0, 12), 45, "ArcaneAcademy_Preview.png"),
        ("Spirit Academy Preview", [assets[2]], [(0, 0, 0)], (34, -47, 35), (0, 0, 11), 44, "SpiritAcademy_Preview.png"),
    )
    for arguments in previews:
        build_preview(*arguments)
    build_preview("Fantasy Schools Comparison", assets, [(-27, 0, 0), (0, 0, 0), (27, 0, 0)],
                  (57, -70, 46), (0, 0, 10), 88, "FantasySchools_Preview.png", 1050)
    bpy.context.window.scene = base_scene
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE)
    print("FANTASY_SCHOOLS_COMPLETE", json.dumps(records, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
