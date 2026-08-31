import bpy
import math
import os
import random
from mathutils import Vector


PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE_PATH = os.path.join(PROJECT_ROOT, "ArtSource", "Blender", "EnvironmentModels.blend")
MODEL_ROOT = os.path.join(PROJECT_ROOT, "Assets", "Models", "Environment")
PREVIEW_ROOT = os.path.join(PROJECT_ROOT, "docs", "Art", "EnvironmentModels")


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        if collection.name != "Collection":
            bpy.data.collections.remove(collection)
    root = bpy.context.scene.collection.children.get("Collection")
    root.name = "EnvironmentModels"
    return root


def material(name, color, roughness=0.8, emission=None):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = next(node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        emission_input = bsdf.inputs.get("Emission Color") or bsdf.inputs.get("Emission")
        if emission_input:
            emission_input.default_value = (*emission, 1.0)
        strength_input = bsdf.inputs.get("Emission Strength")
        if strength_input:
            strength_input.default_value = 1.8
    return mat


def link_only(obj, collection):
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    collection.objects.link(obj)


def add_collection(style, category, variant):
    style_collection = bpy.data.collections.get(style)
    if style_collection is None:
        style_collection = bpy.data.collections.new(style)
        bpy.context.scene.collection.children.link(style_collection)
    collection = bpy.data.collections.new(f"{style}_{category}_{variant}")
    style_collection.children.link(collection)
    return collection


def add_ico(name, location, scale, mat, collection, subdivisions=1, smooth=False):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    link_only(obj, collection)
    for polygon in obj.data.polygons:
        polygon.use_smooth = smooth
    return obj


def add_uv_sphere(name, location, scale, mat, collection, segments=16, rings=8, smooth=True):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    link_only(obj, collection)
    for polygon in obj.data.polygons:
        polygon.use_smooth = smooth
    return obj


def add_cylinder(name, location, radius, depth, vertices, mat, collection, scale=(1, 1, 1), rotation=(0, 0, 0), bevel=0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    link_only(obj, collection)
    if bevel > 0:
        modifier = obj.modifiers.new("Soft edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
    return obj


def add_tapered_branch(name, start, end, radius_start, radius_end, vertices, mat, collection, smooth=True):
    start_vector = Vector(start)
    end_vector = Vector(end)
    direction = end_vector - start_vector
    midpoint = (start_vector + end_vector) * 0.5
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=radius_start,
        radius2=radius_end,
        depth=direction.length,
        location=midpoint,
    )
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    obj.data.materials.append(mat)
    link_only(obj, collection)
    for polygon in obj.data.polygons:
        polygon.use_smooth = smooth
    return obj


def add_cube(name, location, scale, mat, collection, rotation=(0, 0, 0), bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    link_only(obj, collection)
    if bevel > 0:
        modifier = obj.modifiers.new("Rounded corners", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
    return obj


def add_torus(name, location, major_radius, minor_radius, mat, collection, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major_radius,
        minor_radius=minor_radius,
        major_segments=32,
        minor_segments=8,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    link_only(obj, collection)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def make_irregular_rock(name, collection, scale, mat, seed, smooth=False, bevel=0):
    obj = add_ico(name, (0, 0, scale[2] * 0.46), scale, mat, collection, subdivisions=2 if smooth else 1, smooth=smooth)
    rng = random.Random(seed)
    for vertex in obj.data.vertices:
        direction = vertex.co.normalized()
        horizontal = 0.82 + rng.random() * 0.28
        vertical = 0.88 + rng.random() * 0.18
        vertex.co.x *= horizontal
        vertex.co.y *= 0.9 + rng.random() * 0.22
        vertex.co.z *= vertical
        if direction.z < -0.45:
            vertex.co.z = max(vertex.co.z, -0.46)
    if bevel > 0:
        modifier = obj.modifiers.new("Storybook softness", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
    return obj


def make_bent_trunk(name, collection, points, radii, sides, mat, smooth=False):
    vertices = []
    faces = []
    for ring_index, point in enumerate(points):
        for side in range(sides):
            angle = math.tau * side / sides
            radius = radii[ring_index]
            vertices.append((point[0] + math.cos(angle) * radius, point[1] + math.sin(angle) * radius, point[2]))
    for ring_index in range(len(points) - 1):
        for side in range(sides):
            current = ring_index * sides + side
            next_side = ring_index * sides + (side + 1) % sides
            upper = (ring_index + 1) * sides + side
            upper_next = (ring_index + 1) * sides + (side + 1) % sides
            faces.append((current, next_side, upper_next, upper))
    faces.append(tuple(reversed(range(sides))))
    top_start = (len(points) - 1) * sides
    faces.append(tuple(top_start + side for side in range(sides)))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    for polygon in mesh.polygons:
        polygon.use_smooth = smooth
    return obj


def make_crystal(name, collection, location, radius, height, sides, mat, lean=(0, 0), rotation=0):
    x, y, z = location
    shoulder_z = z + height * 0.68
    lower_z = z + height * 0.08
    vertices = [(x, y, z), (x, y, z + height)]
    for ring_z in (lower_z, shoulder_z):
        for side in range(sides):
            angle = rotation + math.tau * side / sides
            t = (ring_z - z) / height
            vertices.append((x + math.cos(angle) * radius + lean[0] * t, y + math.sin(angle) * radius + lean[1] * t, ring_z))
    faces = []
    lower_start = 2
    upper_start = 2 + sides
    for side in range(sides):
        nxt = (side + 1) % sides
        faces.append((0, lower_start + nxt, lower_start + side))
        faces.append((lower_start + side, lower_start + nxt, upper_start + nxt, upper_start + side))
        faces.append((1, upper_start + side, upper_start + nxt))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    return obj


def make_cut_stone(name, collection, location, radius, height, sides, mat, rotation=0):
    x, y, z = location
    ring_specs = (
        (z, radius * 0.68),
        (z + height * 0.1, radius),
        (z + height * 0.84, radius),
        (z + height, radius * 0.62),
    )
    vertices = []
    for ring_z, ring_radius in ring_specs:
        for side in range(sides):
            angle = rotation + math.tau * side / sides
            vertices.append((x + math.cos(angle) * ring_radius, y + math.sin(angle) * ring_radius, ring_z))
    faces = []
    for ring_index in range(len(ring_specs) - 1):
        lower = ring_index * sides
        upper = (ring_index + 1) * sides
        for side in range(sides):
            nxt = (side + 1) % sides
            faces.append((lower + side, lower + nxt, upper + nxt, upper + side))
    faces.append(tuple(reversed(range(sides))))
    top = (len(ring_specs) - 1) * sides
    faces.append(tuple(top + side for side in range(sides)))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    return obj


def create_low_poly(materials):
    rock_a = add_collection("LowPoly", "Rock", "A")
    make_irregular_rock("Rock", rock_a, (1.35, 1.05, 0.9), materials["lp_rock"], 11)
    rock_b = add_collection("LowPoly", "Rock", "B")
    make_irregular_rock("Rock", rock_b, (1.05, 1.4, 1.18), materials["lp_rock_dark"], 23)
    add_ico("RockChip", (0.85, -0.45, 0.28), (0.42, 0.34, 0.32), materials["lp_rock"], rock_b)

    tree_a = add_collection("LowPoly", "Tree", "A")
    make_bent_trunk("Trunk", tree_a, [(0, 0, 0), (0.03, 0, 0.9), (-0.04, 0.02, 1.55)], [0.16, 0.135, 0.1], 7, materials["lp_trunk"])
    add_ico("Foliage", (0, 0, 2.0), (0.76, 0.72, 0.75), materials["lp_leaf"], tree_a, subdivisions=2)

    tree_b = add_collection("LowPoly", "Tree", "B")
    make_bent_trunk("Trunk", tree_b, [(0, 0, 0), (-0.05, 0.03, 0.78), (0.1, 0.04, 1.45)], [0.18, 0.14, 0.095], 7, materials["lp_trunk"])
    add_ico("Foliage", (-0.28, 0.02, 1.86), (0.63, 0.58, 0.62), materials["lp_leaf_dark"], tree_b, subdivisions=1)
    add_ico("Foliage", (0.28, 0.04, 2.04), (0.62, 0.61, 0.67), materials["lp_leaf"], tree_b, subdivisions=1)
    add_ico("Foliage", (0.02, -0.12, 2.34), (0.53, 0.5, 0.52), materials["lp_leaf_light"], tree_b, subdivisions=1)

    stone_a = add_collection("LowPoly", "MagicStone", "A")
    add_cylinder("Pedestal", (0, 0, 0.18), 0.82, 0.36, 8, materials["lp_pedestal"], stone_a, scale=(1.0, 0.9, 1.0))
    add_cylinder("Pedestal_Ring", (0, 0, 0.38), 0.61, 0.18, 8, materials["lp_pedestal_dark"], stone_a)
    make_crystal("Crystal", stone_a, (0, 0, 0.43), 0.48, 2.55, 5, materials["lp_crystal"], rotation=0.25)
    make_crystal("Crystal_Shard", stone_a, (0.47, 0.08, 0.39), 0.17, 1.0, 5, materials["lp_crystal_light"], lean=(0.1, 0.02), rotation=0.1)

    stone_b = add_collection("LowPoly", "MagicStone", "B")
    add_cylinder("Pedestal", (0, 0, 0.16), 0.9, 0.32, 6, materials["lp_pedestal_dark"], stone_b, scale=(1.0, 0.86, 1.0), rotation=(0, 0, 0.18))
    for index, angle in enumerate((0.3, 2.4, 4.4)):
        x, y = math.cos(angle) * 0.55, math.sin(angle) * 0.55
        add_cube(f"Pedestal_Block_{index}", (x, y, 0.38), (0.34, 0.3, 0.38), materials["lp_pedestal"], stone_b, rotation=(0, 0.12 * (-1) ** index, angle))
    make_crystal("Crystal", stone_b, (0.02, 0, 0.38), 0.43, 2.42, 6, materials["lp_crystal"], lean=(-0.08, 0.03), rotation=0.1)
    make_crystal("Crystal_Shard_A", stone_b, (-0.5, 0.08, 0.38), 0.18, 1.13, 5, materials["lp_crystal_light"], lean=(-0.12, 0.02), rotation=0.4)
    make_crystal("Crystal_Shard_B", stone_b, (0.43, -0.24, 0.37), 0.14, 0.82, 5, materials["lp_crystal_dark"], lean=(0.08, -0.04), rotation=0.2)


def create_storybook(materials):
    rock_a = add_collection("Storybook", "Rock", "A")
    make_irregular_rock("Rock", rock_a, (1.34, 1.08, 0.86), materials["sb_rock"], 31, smooth=True, bevel=0.05)
    add_uv_sphere("Moss", (-0.3, -0.55, 0.72), (0.48, 0.32, 0.13), materials["sb_moss"], rock_a, segments=12, rings=6)
    rock_b = add_collection("Storybook", "Rock", "B")
    make_irregular_rock("Rock", rock_b, (1.02, 1.32, 1.16), materials["sb_rock_warm"], 47, smooth=True, bevel=0.06)
    add_ico("RockPebble", (0.78, -0.52, 0.25), (0.38, 0.31, 0.28), materials["sb_rock"], rock_b, subdivisions=2, smooth=True)

    tree_a = add_collection("Storybook", "Tree", "A")
    make_bent_trunk("Trunk", tree_a, [(0, 0, 0), (0.08, 0, 0.7), (-0.06, 0.02, 1.42), (0.05, 0, 1.68)], [0.2, 0.17, 0.12, 0.08], 10, materials["sb_trunk"], smooth=True)
    add_uv_sphere("Foliage", (-0.2, 0, 1.95), (0.68, 0.62, 0.63), materials["sb_leaf"], tree_a, segments=12, rings=6)
    add_uv_sphere("Foliage", (0.36, 0.03, 2.04), (0.6, 0.57, 0.6), materials["sb_leaf_light"], tree_a, segments=12, rings=6)
    add_uv_sphere("Foliage", (0.08, -0.08, 2.38), (0.59, 0.55, 0.54), materials["sb_leaf"], tree_a, segments=12, rings=6)

    tree_b = add_collection("Storybook", "Tree", "B")
    make_bent_trunk("Trunk", tree_b, [(0, 0, 0), (-0.08, 0.02, 0.72), (0.12, 0.02, 1.32), (0.25, 0, 1.55)], [0.21, 0.175, 0.12, 0.075], 10, materials["sb_trunk_dark"], smooth=True)
    for index, (x, y, z, sx, sy, sz) in enumerate(((-0.28, 0, 1.88, .55, .52, .55), (.25, .02, 1.92, .62, .56, .58), (.07, -.08, 2.28, .58, .54, .53), (.5, -.06, 2.25, .43, .42, .4))):
        add_uv_sphere("Foliage", (x, y, z), (sx, sy, sz), materials["sb_leaf_light"] if index % 2 else materials["sb_leaf"], tree_b, segments=12, rings=6)

    stone_a = add_collection("Storybook", "MagicStone", "A")
    add_cylinder("Pedestal", (0, 0, 0.16), 0.91, 0.32, 12, materials["sb_pedestal"], stone_a, bevel=0.06)
    add_cylinder("Pedestal_Ring", (0, 0, 0.39), 0.66, 0.22, 12, materials["sb_pedestal_warm"], stone_a, bevel=0.045)
    make_crystal("Crystal", stone_a, (0, 0, 0.47), 0.5, 2.52, 6, materials["sb_crystal"], lean=(0.06, 0), rotation=0.2)
    make_crystal("Crystal_Shard_A", stone_a, (-0.48, 0.08, 0.43), 0.17, 0.97, 6, materials["sb_crystal_light"], lean=(-0.1, 0.02), rotation=0.1)
    make_crystal("Crystal_Shard_B", stone_a, (0.44, -0.12, 0.43), 0.15, 0.77, 6, materials["sb_crystal_light"], lean=(0.08, -0.02), rotation=0.4)

    stone_b = add_collection("Storybook", "MagicStone", "B")
    add_cylinder("Pedestal", (0, 0, 0.17), 0.94, 0.34, 10, materials["sb_pedestal_warm"], stone_b, bevel=0.07)
    for index, angle in enumerate((0.1, 1.68, 3.24, 4.82)):
        add_cube(f"Pedestal_Petal_{index}", (math.cos(angle) * .55, math.sin(angle) * .55, .43), (.32, .26, .32), materials["sb_pedestal"], stone_b, rotation=(0, 0.08, angle), bevel=0.05)
    make_crystal("Crystal", stone_b, (0, 0, 0.48), 0.46, 2.4, 7, materials["sb_crystal"], lean=(-0.08, 0.04), rotation=0.15)
    make_crystal("Crystal_Shard_A", stone_b, (-0.45, -0.1, 0.46), 0.18, 1.08, 6, materials["sb_crystal_light"], lean=(-0.12, -0.03), rotation=0.3)
    make_crystal("Crystal_Shard_B", stone_b, (0.42, 0.16, 0.46), 0.16, 0.88, 6, materials["sb_crystal_dark"], lean=(0.09, 0.03), rotation=0.1)


def add_realistic_crown(collection, center, spread, materials, seed):
    rng = random.Random(seed)
    for index in range(11):
        angle = math.tau * index / 11 + rng.uniform(-0.2, 0.2)
        ring = 0.18 + spread * (0.42 if index < 7 else 0.25)
        x = center[0] + math.cos(angle) * ring * rng.uniform(0.75, 1.15)
        y = center[1] + math.sin(angle) * ring * rng.uniform(0.75, 1.15)
        z = center[2] + rng.uniform(-0.28, 0.4)
        scale = rng.uniform(0.42, 0.62)
        mat = materials["real_leaf_light"] if index % 4 == 0 else materials["real_leaf"]
        add_ico("Foliage", (x, y, z), (scale * 1.2, scale, scale * 0.9), mat, collection, subdivisions=2, smooth=True)


def create_realistic(materials):
    rock_a = add_collection("Realistic", "Rock", "A")
    rock = make_irregular_rock("Rock", rock_a, (1.38, 1.12, 0.92), materials["real_rock"], 71, smooth=True, bevel=0.025)
    rock.data.materials.append(materials["real_rock_dark"])
    for polygon in rock.data.polygons:
        if polygon.center.z < -0.05 or polygon.normal.z < -0.3:
            polygon.material_index = 1
    add_uv_sphere("Moss", (-0.38, -0.46, 0.78), (0.52, 0.34, 0.055), materials["real_moss"], rock_a, segments=20, rings=10)

    rock_b = add_collection("Realistic", "Rock", "B")
    tall_rock = make_irregular_rock("Rock", rock_b, (1.06, 1.35, 1.28), materials["real_rock_warm"], 89, smooth=True, bevel=0.025)
    tall_rock.rotation_euler.z = 0.18
    add_ico("Rock_Fragment", (0.82, -0.5, 0.32), (0.42, 0.34, 0.31), materials["real_rock_dark"], rock_b, subdivisions=2, smooth=True)
    add_ico("Rock_Fragment", (-0.68, 0.56, 0.22), (0.3, 0.24, 0.22), materials["real_rock_warm"], rock_b, subdivisions=2, smooth=True)

    tree_a = add_collection("Realistic", "Tree", "A")
    add_tapered_branch("Trunk", (0, 0, 0), (0.03, 0, 1.8), 0.22, 0.105, 14, materials["real_trunk"], tree_a)
    add_tapered_branch("Trunk_Branch_A", (0.02, 0, 1.18), (0.62, 0.05, 1.82), 0.09, 0.035, 12, materials["real_trunk"], tree_a)
    add_tapered_branch("Trunk_Branch_B", (0.01, 0, 1.38), (-0.5, 0.18, 1.98), 0.075, 0.03, 12, materials["real_trunk"], tree_a)
    for index, angle in enumerate((0, 2.1, 4.2)):
        add_tapered_branch(f"Trunk_Root_{index}", (0, 0, 0.06), (math.cos(angle) * .42, math.sin(angle) * .42, 0), .115, .025, 10, materials["real_trunk_dark"], tree_a)
    add_realistic_crown(tree_a, (0.02, 0, 2.25), 1.05, materials, 101)

    tree_b = add_collection("Realistic", "Tree", "B")
    add_tapered_branch("Trunk", (0, 0, 0), (-0.08, 0.03, 1.72), 0.24, 0.11, 14, materials["real_trunk_dark"], tree_b)
    add_tapered_branch("Trunk_Branch_A", (-0.03, 0.02, 1.05), (0.58, -0.08, 1.73), 0.095, 0.032, 12, materials["real_trunk"], tree_b)
    add_tapered_branch("Trunk_Branch_B", (-0.06, 0.03, 1.3), (-0.62, 0.22, 1.87), 0.08, 0.03, 12, materials["real_trunk"], tree_b)
    add_tapered_branch("Trunk_Branch_C", (-0.07, 0.03, 1.48), (0.18, 0.48, 2.08), 0.065, 0.025, 10, materials["real_trunk_dark"], tree_b)
    for index, angle in enumerate((0.7, 2.8, 4.9)):
        add_tapered_branch(f"Trunk_Root_{index}", (0, 0, 0.06), (math.cos(angle) * .45, math.sin(angle) * .45, 0), .12, .025, 10, materials["real_trunk_dark"], tree_b)
    add_realistic_crown(tree_b, (0.02, 0.06, 2.28), 1.12, materials, 131)

    stone_a = add_collection("Realistic", "MagicStone", "A")
    add_cylinder("Pedestal", (0, 0, 0.14), 0.94, 0.28, 16, materials["real_pedestal_dark"], stone_a, scale=(1.0, 0.92, 1.0), bevel=0.025)
    add_cylinder("Pedestal_Ring", (0, 0, 0.34), 0.72, 0.22, 16, materials["real_pedestal"], stone_a, bevel=0.025)
    add_cylinder("Pedestal_Inlay", (0, 0, 0.47), 0.55, 0.06, 16, materials["real_metal"], stone_a)
    make_crystal("Crystal", stone_a, (0, 0, 0.49), 0.47, 2.5, 7, materials["real_crystal"], lean=(0.035, -0.015), rotation=0.12)
    make_crystal("Crystal_Shard_A", stone_a, (-0.47, 0.06, 0.48), 0.16, 1.04, 6, materials["real_crystal_light"], lean=(-0.1, 0.02), rotation=0.3)
    make_crystal("Crystal_Shard_B", stone_a, (0.43, -0.18, 0.48), 0.13, 0.78, 6, materials["real_crystal_dark"], lean=(0.08, -0.03), rotation=0.18)

    stone_b = add_collection("Realistic", "MagicStone", "B")
    add_cylinder("Pedestal", (0, 0, 0.15), 1.0, 0.3, 12, materials["real_pedestal_dark"], stone_b, scale=(1.0, 0.9, 1.0), bevel=0.035)
    add_cylinder("Pedestal_Ring", (0, 0, 0.36), 0.76, 0.24, 12, materials["real_pedestal"], stone_b, bevel=0.025)
    for index, angle in enumerate((0.15, 1.72, 3.3, 4.87)):
        add_cube(f"Pedestal_Buttress_{index}", (math.cos(angle) * .64, math.sin(angle) * .64, .4), (.24, .2, .32), materials["real_pedestal_dark"], stone_b, rotation=(0, 0.04, angle), bevel=0.025)
    make_crystal("Crystal", stone_b, (0, 0, 0.49), 0.44, 2.38, 8, materials["real_crystal"], lean=(-0.07, 0.025), rotation=0.05)
    make_crystal("Crystal_Shard_A", stone_b, (-0.46, -0.08, 0.48), 0.17, 1.12, 7, materials["real_crystal_light"], lean=(-0.11, -0.02), rotation=0.2)
    make_crystal("Crystal_Shard_B", stone_b, (0.44, 0.16, 0.48), 0.14, 0.9, 7, materials["real_crystal_dark"], lean=(0.08, 0.025), rotation=0.38)


def create_artificial_magic_stones(materials):
    obelisk = add_collection("Artificial", "MagicStone", "Obelisk")
    add_cube("Pedestal_Lower", (0, 0, 0.14), (0.92, 0.92, 0.28), materials["art_stone_dark"], obelisk, rotation=(0, 0, math.radians(45)), bevel=0.07)
    add_cylinder("Pedestal_Upper", (0, 0, 0.38), 0.68, 0.26, 8, materials["art_stone"], obelisk, bevel=0.035)
    add_cylinder("Pedestal_Inlay", (0, 0, 0.54), 0.55, 0.08, 8, materials["art_metal"], obelisk)
    make_cut_stone("Core", obelisk, (0, 0, 0.58), 0.46, 2.28, 8, materials["art_core"], rotation=math.radians(22.5))
    add_torus("Core_Band_Lower", (0, 0, 1.02), 0.47, 0.045, materials["art_metal"], obelisk)
    add_torus("Core_Band_Upper", (0, 0, 2.29), 0.47, 0.045, materials["art_metal"], obelisk)

    ring = add_collection("Artificial", "MagicStone", "Ring")
    add_cylinder("Pedestal_Lower", (0, 0, 0.13), 0.96, 0.26, 16, materials["art_stone_dark"], ring, scale=(1.0, 0.86, 1.0), bevel=0.05)
    add_cylinder("Pedestal_Upper", (0, 0, 0.34), 0.72, 0.2, 16, materials["art_stone"], ring, bevel=0.035)
    add_torus("Frame_Ring", (0, 0, 1.52), 0.88, 0.09, materials["art_metal_dark"], ring, rotation=(math.pi * 0.5, 0, 0))
    add_torus("Frame_Inlay", (0, 0, 1.52), 0.72, 0.025, materials["art_metal"], ring, rotation=(math.pi * 0.5, 0, 0))
    add_tapered_branch("Frame_Support_Left", (-0.66, 0, 0.42), (-0.77, 0, 1.08), 0.1, 0.07, 10, materials["art_metal_dark"], ring)
    add_tapered_branch("Frame_Support_Right", (0.66, 0, 0.42), (0.77, 0, 1.08), 0.1, 0.07, 10, materials["art_metal_dark"], ring)
    core = add_ico("Core", (0, 0, 1.52), (0.42, 0.24, 0.57), materials["art_core_light"], ring, subdivisions=2)
    core.rotation_euler = (0.18, 0.35, 0.12)

    reactor = add_collection("Artificial", "MagicStone", "Reactor")
    add_cube("Pedestal_Lower", (0, 0, 0.13), (0.88, 0.88, 0.26), materials["art_stone_dark"], reactor, rotation=(0, 0, math.radians(45)), bevel=0.06)
    add_cube("Pedestal_Upper", (0, 0, 0.36), (0.67, 0.67, 0.22), materials["art_stone"], reactor, rotation=(0, 0, math.radians(45)), bevel=0.04)
    add_cube("Core", (0, 0, 1.34), (0.43, 0.43, 0.43), materials["art_core_dark"], reactor, rotation=(math.radians(24), math.radians(18), math.radians(45)), bevel=0.08)
    for index, angle in enumerate((math.radians(45), math.radians(135), math.radians(225), math.radians(315))):
        x = math.cos(angle) * 0.58
        y = math.sin(angle) * 0.58
        add_tapered_branch(
            f"Frame_Claw_{index}",
            (x, y, 0.48),
            (math.cos(angle) * 0.34, math.sin(angle) * 0.34, 1.18),
            0.12,
            0.065,
            10,
            materials["art_metal_dark"],
            reactor,
        )
        add_cube(
            f"Frame_Cap_{index}",
            (math.cos(angle) * 0.31, math.sin(angle) * 0.31, 1.22),
            (0.13, 0.13, 0.22),
            materials["art_metal"],
            reactor,
            rotation=(0, math.radians(25), angle),
            bevel=0.025,
        )
    add_torus("Containment_Ring", (0, 0, 1.34), 0.62, 0.055, materials["art_metal"], reactor, rotation=(0.12, 0.18, 0))


def make_layered_rock(name, collection, scale, mat, dark_mat, seed, profile):
    rng = random.Random(seed)
    sides = 14
    angles = [math.tau * side / sides + rng.uniform(-0.08, 0.08) for side in range(sides)]
    radial_noise = [rng.uniform(0.82, 1.16) for _ in range(sides)]
    vertices = []
    for ring_index, (height_ratio, radius_ratio) in enumerate(profile):
        phase = 0.07 * ring_index
        for side, angle in enumerate(angles):
            radius = radius_ratio * radial_noise[side] * (1 + 0.05 * math.sin(side * 2.1 + ring_index))
            vertices.append((
                math.cos(angle + phase) * radius * scale[0],
                math.sin(angle + phase) * radius * scale[1],
                height_ratio * scale[2],
            ))
    faces = []
    for ring_index in range(len(profile) - 1):
        lower = ring_index * sides
        upper = (ring_index + 1) * sides
        for side in range(sides):
            nxt = (side + 1) % sides
            faces.append((lower + side, lower + nxt, upper + nxt, upper + side))
    faces.append(tuple(reversed(range(sides))))
    top = (len(profile) - 1) * sides
    faces.append(tuple(top + side for side in range(sides)))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(mat)
    mesh.materials.append(dark_mat)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    for polygon in mesh.polygons:
        if polygon.center.z < scale[2] * 0.35 or polygon.normal.z < -0.12:
            polygon.material_index = 1
    bevel = obj.modifiers.new("Weathered edges", "BEVEL")
    bevel.width = 0.025
    bevel.segments = 2
    return obj


def make_leaf_canopy(name, collection, center, radii, leaf_count, mats, seed):
    rng = random.Random(seed)
    vertices = []
    faces = []
    material_indices = []
    for leaf_index in range(leaf_count):
        azimuth = rng.uniform(0, math.tau)
        elevation = rng.uniform(-0.72, 0.88)
        distance = rng.uniform(0.38, 1.0) ** 0.6
        horizontal = math.cos(elevation)
        position = Vector((
            center[0] + math.cos(azimuth) * horizontal * radii[0] * distance,
            center[1] + math.sin(azimuth) * horizontal * radii[1] * distance,
            center[2] + math.sin(elevation) * radii[2] * distance,
        ))
        leaf_angle = rng.uniform(0, math.tau)
        length = rng.uniform(0.12, 0.19)
        width = length * rng.uniform(0.36, 0.52)
        axis = Vector((math.cos(leaf_angle) * length, math.sin(leaf_angle) * length, rng.uniform(-0.04, 0.08)))
        side = Vector((-math.sin(leaf_angle) * width, math.cos(leaf_angle) * width, rng.uniform(-0.025, 0.025)))
        base = len(vertices)
        vertices.extend((position - axis, position + side, position + axis, position - side))
        faces.extend(((base, base + 1, base + 2, base + 3), (base + 3, base + 2, base + 1, base)))
        mat_index = leaf_index % len(mats) if leaf_index % 5 == 0 else 0
        material_indices.extend((mat_index, mat_index))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    for mat in mats:
        mesh.materials.append(mat)
    for polygon, mat_index in zip(mesh.polygons, material_indices):
        polygon.material_index = mat_index
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    return obj


def make_cut_diamond(name, collection, location, radius, height, mat, sides=8):
    x, y, z = location
    lower = z + height * 0.36
    upper = z + height - (lower - z)
    vertices = [(x, y, z), (x, y, z + height)]
    for ring_z in (lower, upper):
        for side in range(sides):
            angle = math.tau * side / sides + math.radians(22.5)
            vertices.append((x + math.cos(angle) * radius, y + math.sin(angle) * radius, ring_z))
    faces = []
    for side in range(sides):
        nxt = (side + 1) % sides
        faces.append((0, 2 + nxt, 2 + side))
        faces.append((2 + side, 2 + nxt, 2 + sides + nxt, 2 + sides + side))
        faces.append((1, 2 + sides + side, 2 + sides + nxt))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    return obj


def add_simple_pedestal(collection, materials):
    add_cylinder("Pedestal_Lower", (0, 0, 0.13), 0.88, 0.26, 16, materials["ref_pedestal_dark"], collection, scale=(1.0, 0.92, 1.0), bevel=0.035)
    add_cylinder("Pedestal_Upper", (0, 0, 0.33), 0.66, 0.2, 16, materials["ref_pedestal"], collection, bevel=0.025)


def create_refined_models(materials):
    rock_a = add_collection("Refined", "Rock", "Boulder")
    make_layered_rock("Rock", rock_a, (1.38, 1.12, 1.18), materials["ref_rock"], materials["ref_rock_dark"], 211, ((0, .72), (.16, .96), (.46, 1.0), (.76, .83), (1.0, .38)))
    add_uv_sphere("Moss", (-0.34, -0.52, 1.04), (0.52, 0.3, 0.035), materials["ref_moss"], rock_a, segments=18, rings=8)

    rock_b = add_collection("Refined", "Rock", "Outcrop")
    make_layered_rock("Rock", rock_b, (1.18, 1.38, 1.55), materials["ref_rock_warm"], materials["ref_rock_dark"], 307, ((0, .68), (.13, .92), (.34, 1.0), (.55, .88), (.76, .7), (1.0, .32)))
    fragment = make_layered_rock("Rock_Fragment", rock_b, (.46, .38, .42), materials["ref_rock_warm"], materials["ref_rock_dark"], 311, ((0, .72), (.3, 1.0), (.72, .8), (1.0, .35)))
    fragment.location = (0.8, -0.5, 0)

    tree_a = add_collection("Refined", "Tree", "Broad")
    add_tapered_branch("Trunk", (0, 0, 0), (0.02, 0.01, 1.84), .23, .095, 16, materials["ref_trunk"], tree_a)
    branch_specs_a = (
        ((.01, 0, 1.0), (.76, .05, 1.82), .095),
        ((.01, 0, 1.16), (-.7, .18, 1.9), .09),
        ((.02, 0, 1.38), (.2, -.66, 2.14), .07),
        ((.02, 0, 1.5), (-.18, .52, 2.28), .065),
    )
    for index, (start, end, radius) in enumerate(branch_specs_a):
        add_tapered_branch(f"Trunk_Branch_{index}", start, end, radius, .025, 12, materials["ref_trunk"], tree_a)
    for index, angle in enumerate((.2, 2.25, 4.35)):
        add_tapered_branch(f"Trunk_Root_{index}", (0, 0, .05), (math.cos(angle) * .44, math.sin(angle) * .44, 0), .12, .02, 12, materials["ref_trunk_dark"], tree_a)
    make_leaf_canopy("Foliage", tree_a, (0.0, 0.0, 2.24), (1.18, .94, .78), 190, (materials["ref_leaf"], materials["ref_leaf_light"]), 401)

    tree_b = add_collection("Refined", "Tree", "Asymmetric")
    add_tapered_branch("Trunk", (0, 0, 0), (-.08, .03, 1.92), .25, .1, 16, materials["ref_trunk_dark"], tree_b)
    branch_specs_b = (
        ((-.04, .02, 1.0), (.86, -.12, 1.85), .1),
        ((-.06, .02, 1.25), (-.58, .35, 2.04), .085),
        ((-.07, .03, 1.48), (.38, .56, 2.3), .07),
        ((-.07, .03, 1.6), (-.35, -.48, 2.4), .06),
    )
    for index, (start, end, radius) in enumerate(branch_specs_b):
        add_tapered_branch(f"Trunk_Branch_{index}", start, end, radius, .025, 12, materials["ref_trunk"], tree_b)
    for index, angle in enumerate((1.0, 3.1, 5.2)):
        add_tapered_branch(f"Trunk_Root_{index}", (0, 0, .05), (math.cos(angle) * .46, math.sin(angle) * .46, 0), .125, .02, 12, materials["ref_trunk_dark"], tree_b)
    make_leaf_canopy("Foliage", tree_b, (.12, .04, 2.34), (1.3, .88, .82), 210, (materials["ref_leaf_dark"], materials["ref_leaf"]), 503)

    oval = add_collection("Refined", "MagicStone", "Oval")
    add_simple_pedestal(oval, materials)
    core = add_ico("Core", (0, 0, 1.43), (.52, .38, .92), materials["ref_core"], oval, subdivisions=3)
    core.rotation_euler.z = .16

    diamond = add_collection("Refined", "MagicStone", "Diamond")
    add_simple_pedestal(diamond, materials)
    make_cut_diamond("Core", diamond, (0, 0, .48), .66, 1.95, materials["ref_core_light"], sides=8)

    tablet = add_collection("Refined", "MagicStone", "Tablet")
    add_simple_pedestal(tablet, materials)
    add_cube("Core", (0, 0, 1.45), (.55, .27, .95), materials["ref_core_dark"], tablet, rotation=(0, 0, .08), bevel=.12)


def make_floating_gem(name, collection, location, radius, height, sides, mat, waist, rotation=0):
    x, y, z = location
    lower_ring = z + height * waist[0]
    upper_ring = z + height * waist[1]
    vertices = [(x, y, z), (x, y, z + height)]
    for ring_z, ring_scale in ((lower_ring, .88), (upper_ring, 1.0)):
        for side in range(sides):
            angle = rotation + math.tau * side / sides
            vertices.append((x + math.cos(angle) * radius * ring_scale, y + math.sin(angle) * radius * ring_scale, ring_z))
    faces = []
    for side in range(sides):
        nxt = (side + 1) % sides
        faces.append((0, 2 + nxt, 2 + side))
        faces.append((2 + side, 2 + nxt, 2 + sides + nxt, 2 + sides + side))
        faces.append((1, 2 + sides + side, 2 + sides + nxt))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    return obj


def add_turret_pedestal(collection, materials, crenellations):
    add_cylinder("Pedestal_Base", (0, 0, .12), .88, .24, 16, materials["ri_stone_dark"], collection, bevel=.035)
    add_cylinder("Pedestal_Body", (0, 0, .48), .67, .58, 16, materials["ri_stone"], collection, bevel=.025)
    add_cylinder("Pedestal_Top", (0, 0, .8), .78, .16, 16, materials["ri_stone_dark"], collection, bevel=.02)
    for index in range(crenellations):
        angle = math.tau * index / crenellations
        add_cube(
            f"Pedestal_Crenel_{index}",
            (math.cos(angle) * .62, math.sin(angle) * .62, .98),
            (.24, .2, .28),
            materials["ri_stone"],
            collection,
            rotation=(0, 0, angle),
            bevel=.018,
        )


def create_reference_inspired_magic_stones(materials):
    crown = add_collection("ReferenceInspired", "MagicStone", "Crown")
    add_turret_pedestal(crown, materials, 8)
    add_cylinder("Pedestal_Inlay", (0, 0, .91), .42, .06, 12, materials["ri_gold"], crown)
    make_floating_gem("Core", crown, (0, 0, 1.08), .53, 1.98, 6, materials["ri_core"], (.32, .61), rotation=math.radians(30))

    altar = add_collection("ReferenceInspired", "MagicStone", "Altar")
    add_cylinder("Pedestal_Base", (0, 0, .13), .92, .26, 12, materials["ri_stone_dark"], altar, scale=(1, .9, 1), bevel=.04)
    add_cylinder("Pedestal_Body", (0, 0, .37), .7, .24, 12, materials["ri_stone"], altar, bevel=.025)
    add_cylinder("Pedestal_Top", (0, 0, .55), .58, .12, 12, materials["ri_gold"], altar)
    for index, angle in enumerate((math.radians(45), math.radians(135), math.radians(225), math.radians(315))):
        add_cube(
            f"Pedestal_Corner_{index}",
            (math.cos(angle) * .52, math.sin(angle) * .52, .68),
            (.2, .18, .3),
            materials["ri_stone_dark"],
            altar,
            rotation=(0, 0, angle),
            bevel=.02,
        )
    make_floating_gem("Core", altar, (0, 0, .85), .62, 2.12, 8, materials["ri_core_light"], (.4, .66), rotation=math.radians(22.5))

    keep = add_collection("ReferenceInspired", "MagicStone", "Keep")
    add_cube("Pedestal_Base", (0, 0, .13), (1.0, .86, .26), materials["ri_stone_dark"], keep, rotation=(0, 0, math.radians(45)), bevel=.045)
    add_cube("Pedestal_Body", (0, 0, .42), (.72, .62, .38), materials["ri_stone"], keep, rotation=(0, 0, math.radians(45)), bevel=.025)
    add_cylinder("Pedestal_Top", (0, 0, .67), .64, .14, 8, materials["ri_gold_dark"], keep, bevel=.018)
    for index, angle in enumerate((0, math.pi * .5, math.pi, math.pi * 1.5)):
        add_cube(
            f"Pedestal_Crenel_{index}",
            (math.cos(angle) * .5, math.sin(angle) * .5, .84),
            (.22, .22, .26),
            materials["ri_stone_dark"],
            keep,
            rotation=(0, 0, angle),
            bevel=.015,
        )
    make_floating_gem("Core", keep, (0, 0, 1.0), .48, 2.02, 5, materials["ri_core_dark"], (.25, .56), rotation=math.radians(18))


def make_radial_brickwork(name, collection, rows, segments, radius_start, radius_end, z_start, row_height, mat, stagger=True):
    vertices = []
    faces = []
    for row in range(rows):
        radius = radius_start if rows == 1 else radius_start + (radius_end - radius_start) * row / (rows - 1)
        offset = math.pi / segments if stagger and row % 2 else 0
        tangential_width = math.tau * radius / segments * .86
        radial_depth = .13
        for segment in range(segments):
            angle = math.tau * segment / segments + offset
            center = Vector((math.cos(angle) * radius, math.sin(angle) * radius, z_start + row_height * (row + .5)))
            tangent = Vector((-math.sin(angle), math.cos(angle), 0))
            radial = Vector((math.cos(angle), math.sin(angle), 0))
            vertical = Vector((0, 0, 1))
            half_tangent = tangent * tangential_width * .5
            half_radial = radial * radial_depth * .5
            half_vertical = vertical * row_height * .43
            base = len(vertices)
            vertices.extend((
                center - half_tangent - half_radial - half_vertical,
                center + half_tangent - half_radial - half_vertical,
                center + half_tangent + half_radial - half_vertical,
                center - half_tangent + half_radial - half_vertical,
                center - half_tangent - half_radial + half_vertical,
                center + half_tangent - half_radial + half_vertical,
                center + half_tangent + half_radial + half_vertical,
                center - half_tangent + half_radial + half_vertical,
            ))
            faces.extend((
                (base, base + 1, base + 2, base + 3),
                (base + 4, base + 7, base + 6, base + 5),
                (base, base + 4, base + 5, base + 1),
                (base + 1, base + 5, base + 6, base + 2),
                (base + 2, base + 6, base + 7, base + 3),
                (base + 4, base, base + 3, base + 7),
            ))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    bevel = obj.modifiers.new("Worn mortar edges", "BEVEL")
    bevel.width = .012
    bevel.segments = 1
    return obj


def make_hanging_banner(name, collection, angle, radius, z_center, width, height, mat):
    tangent = Vector((-math.sin(angle), math.cos(angle), 0))
    radial = Vector((math.cos(angle), math.sin(angle), 0))
    center = radial * radius + Vector((0, 0, z_center))
    top_left = center - tangent * width * .5 + Vector((0, 0, height * .5))
    top_right = center + tangent * width * .5 + Vector((0, 0, height * .5))
    lower_right = center + tangent * width * .5 + Vector((0, 0, -height * .22))
    point = center + Vector((0, 0, -height * .5))
    lower_left = center - tangent * width * .5 + Vector((0, 0, -height * .22))
    lift = radial * .015
    vertices = [point + lift for point in (top_left, top_right, lower_right, point, lower_left)]
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], ((0, 1, 2, 3, 4), (4, 3, 2, 1, 0)))
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    solidify = obj.modifiers.new("Heavy cloth", "SOLIDIFY")
    solidify.thickness = .018
    return obj


def make_arch_door(name, collection, angle, radius, z_bottom, width, height, mat):
    tangent = Vector((-math.sin(angle), math.cos(angle), 0))
    radial = Vector((math.cos(angle), math.sin(angle), 0))
    center = radial * radius + Vector((0, 0, z_bottom))
    vertices = []
    steps = 8
    for index in range(steps + 1):
        arc = math.pi * index / steps
        local_x = math.cos(arc) * width * .5
        local_z = height - width * .5 + math.sin(arc) * width * .5
        vertices.append(center + tangent * local_x + Vector((0, 0, local_z)) + radial * .018)
    vertices.extend((
        center - tangent * width * .5 + radial * .018,
        center + tangent * width * .5 + radial * .018,
    ))
    face = tuple(range(steps + 1)) + (steps + 2, steps + 1)
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], (face, tuple(reversed(face))))
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    solidify = obj.modifiers.new("Door recess", "SOLIDIFY")
    solidify.thickness = .025
    return obj


def create_reference_tower(materials):
    tower = add_collection("ExactReference", "MagicStone", "Tower")
    add_cylinder("Pedestal_Base", (0, 0, .08), .82, .16, 20, materials["ex_metal_dark"], tower, bevel=.025)
    add_cylinder("Pedestal_Foundation", (0, 0, .2), .76, .18, 20, materials["ex_stone_dark"], tower, bevel=.02)
    make_radial_brickwork("Pedestal_FoundationBricks", tower, 3, 14, .68, .61, .29, .16, materials["ex_stone_light"])
    add_cylinder("Pedestal_FoundationBand", (0, 0, .79), .66, .12, 20, materials["ex_wood_dark"], tower, bevel=.015)
    add_cylinder("Pedestal_TowerCore", (0, 0, 1.42), .57, 1.18, 20, materials["ex_stone_dark"], tower)
    make_radial_brickwork("Pedestal_TowerBricks", tower, 6, 13, .59, .59, .85, .19, materials["ex_stone"])
    for index in range(8):
        angle = math.tau * index / 8
        add_cube(
            f"Pedestal_Rib_{index}",
            (math.cos(angle) * .625, math.sin(angle) * .625, 1.44),
            (.075, .11, 1.34),
            materials["ex_wood"],
            tower,
            rotation=(0, 0, angle),
            bevel=.012,
        )
    add_cylinder("Pedestal_UpperBand", (0, 0, 2.08), .66, .14, 20, materials["ex_wood_dark"], tower, bevel=.018)
    add_cylinder("Pedestal_CrownWall", (0, 0, 2.32), .71, .42, 16, materials["ex_stone_dark"], tower, bevel=.018)
    add_cylinder("Pedestal_CrownTop", (0, 0, 2.55), .74, .12, 16, materials["ex_stone"], tower, bevel=.015)
    for index in range(8):
        angle = math.tau * index / 8
        add_cube(
            f"Pedestal_Crenel_{index}",
            (math.cos(angle) * .61, math.sin(angle) * .61, 2.76),
            (.3, .28, .46),
            materials["ex_stone_dark"],
            tower,
            rotation=(0, math.radians(-7), angle),
            bevel=.025,
        )
    make_arch_door("Pedestal_Door", tower, -math.pi * .5, .705, .1, .28, .48, materials["ex_door"])
    for index, angle in enumerate((-math.pi * .5, math.radians(30), math.radians(150))):
        make_hanging_banner(f"Pedestal_Banner_{index}", tower, angle, .675, 1.62, .26, .62, materials["ex_banner"])
    add_cylinder("Pedestal_CoreSocket", (0, 0, 2.66), .21, .18, 8, materials["ex_gold"], tower, bevel=.015)
    make_floating_gem("Crystal_Main", tower, (0, 0, 2.76), .41, 1.08, 6, materials["ex_crystal"], (.37, .64), rotation=math.radians(30))
    make_floating_gem("Crystal_Left", tower, (-.48, -.05, 2.98), .15, .55, 5, materials["ex_crystal_dark"], (.38, .62), rotation=math.radians(18))
    make_floating_gem("Crystal_Right", tower, (.47, 0, 2.94), .17, .6, 5, materials["ex_crystal_light"], (.38, .62), rotation=math.radians(18))


def collections_for_model(model_collection):
    return list(model_collection.all_objects)


def export_models():
    for style in ("LowPoly", "Storybook", "Realistic", "Artificial", "Refined", "ReferenceInspired", "ExactReference"):
        style_collection = bpy.data.collections[style]
        output_dir = os.path.join(MODEL_ROOT, style)
        os.makedirs(output_dir, exist_ok=True)
        for model_collection in style_collection.children:
            bpy.ops.object.select_all(action="DESELECT")
            objects = collections_for_model(model_collection)
            for obj in objects:
                obj.select_set(True)
            if not objects:
                continue
            bpy.context.view_layer.objects.active = objects[0]
            output_path = os.path.join(output_dir, f"{model_collection.name}.fbx")
            bpy.ops.export_scene.fbx(
                filepath=output_path,
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


def configure_world():
    world = bpy.context.scene.world or bpy.data.worlds.new("PreviewWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.055, 0.075, 0.1, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.55
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False


def aim_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_style_preview(style, materials):
    for available_style in ("LowPoly", "Storybook", "Realistic", "Artificial", "Refined", "ReferenceInspired", "ExactReference"):
        bpy.data.collections[available_style].hide_render = available_style != style
    positions = {
        f"{style}_Rock_A": (-3.7, 1.4, 0),
        f"{style}_Rock_B": (-1.3, 1.4, 0),
        f"{style}_Tree_A": (1.4, 1.4, 0),
        f"{style}_Tree_B": (3.8, 1.4, 0),
        f"{style}_MagicStone_A": (-1.8, -1.8, 0),
        f"{style}_MagicStone_B": (1.8, -1.8, 0),
    }
    for child in bpy.data.collections[style].children:
        offset = positions[child.name]
        for obj in child.all_objects:
            obj.location.x += offset[0]
            obj.location.y += offset[1]
            obj.location.z += offset[2]
    preview_collection = bpy.data.collections.get("Preview")
    if preview_collection is None:
        preview_collection = bpy.data.collections.new("Preview")
        bpy.context.scene.collection.children.link(preview_collection)
        add_cube("PreviewGround", (0, 0, -0.08), (10, 7, 0.12), materials["ground"], preview_collection, bevel=0.08)
        bpy.ops.object.light_add(type="AREA", location=(-4.5, -4.0, 8.5))
        key = bpy.context.object
        key.name = "PreviewKey"
        key.data.energy = 1500
        key.data.shape = "DISK"
        key.data.size = 5.0
        link_only(key, preview_collection)
        aim_at(key, (0, 0, 1))
        bpy.ops.object.light_add(type="AREA", location=(5.0, 1.5, 5.0))
        fill = bpy.context.object
        fill.name = "PreviewFill"
        fill.data.energy = 900
        fill.data.size = 4.0
        link_only(fill, preview_collection)
        aim_at(fill, (0, 0, 1))
        bpy.ops.object.camera_add(location=(8.8, -11.8, 8.4))
        camera = bpy.context.object
        camera.name = "PreviewCamera"
        camera.data.lens = 52
        link_only(camera, preview_collection)
        aim_at(camera, (0, 0, 1.1))
        bpy.context.scene.camera = camera
    os.makedirs(PREVIEW_ROOT, exist_ok=True)
    bpy.context.scene.render.filepath = os.path.join(PREVIEW_ROOT, f"{style}_Preview.png")
    bpy.ops.render.render(write_still=True)
    for child in bpy.data.collections[style].children:
        offset = positions[child.name]
        for obj in child.all_objects:
            obj.location.x -= offset[0]
            obj.location.y -= offset[1]
            obj.location.z -= offset[2]


def render_artificial_preview():
    for available_style in ("LowPoly", "Storybook", "Realistic", "Artificial", "Refined", "ReferenceInspired", "ExactReference"):
        bpy.data.collections[available_style].hide_render = available_style != "Artificial"
    positions = {
        "Artificial_MagicStone_Obelisk": (-3.0, 0.0, 0),
        "Artificial_MagicStone_Ring": (0.0, 0.0, 0),
        "Artificial_MagicStone_Reactor": (3.0, 0.0, 0),
    }
    for child in bpy.data.collections["Artificial"].children:
        offset = positions[child.name]
        for obj in child.all_objects:
            obj.location.x += offset[0]
            obj.location.y += offset[1]
            obj.location.z += offset[2]
    os.makedirs(PREVIEW_ROOT, exist_ok=True)
    bpy.context.scene.render.filepath = os.path.join(PREVIEW_ROOT, "ArtificialMagicStone_Preview.png")
    bpy.ops.render.render(write_still=True)
    for child in bpy.data.collections["Artificial"].children:
        offset = positions[child.name]
        for obj in child.all_objects:
            obj.location.x -= offset[0]
            obj.location.y -= offset[1]
            obj.location.z -= offset[2]


def render_collection_preview(style, positions, filename):
    for available_style in ("LowPoly", "Storybook", "Realistic", "Artificial", "Refined", "ReferenceInspired", "ExactReference"):
        bpy.data.collections[available_style].hide_render = available_style != style
    hidden = []
    for child in bpy.data.collections[style].children:
        child.hide_render = child.name not in positions
        if child.hide_render:
            hidden.append(child)
            continue
        offset = positions[child.name]
        for obj in child.all_objects:
            obj.location.x += offset[0]
            obj.location.y += offset[1]
            obj.location.z += offset[2]
    bpy.context.scene.render.filepath = os.path.join(PREVIEW_ROOT, filename)
    bpy.ops.render.render(write_still=True)
    for child in bpy.data.collections[style].children:
        if child in hidden:
            child.hide_render = False
            continue
        offset = positions[child.name]
        for obj in child.all_objects:
            obj.location.x -= offset[0]
            obj.location.y -= offset[1]
            obj.location.z -= offset[2]


def render_refined_previews():
    render_collection_preview(
        "Refined",
        {
            "Refined_Rock_Boulder": (-3.5, .6, 0),
            "Refined_Rock_Outcrop": (-1.2, .6, 0),
            "Refined_Tree_Broad": (1.35, .5, 0),
            "Refined_Tree_Asymmetric": (3.7, .5, 0),
        },
        "RefinedNature_Preview.png",
    )
    render_collection_preview(
        "Refined",
        {
            "Refined_MagicStone_Oval": (-2.8, 0, 0),
            "Refined_MagicStone_Diamond": (0, 0, 0),
            "Refined_MagicStone_Tablet": (2.8, 0, 0),
        },
        "RefinedMagicStone_Preview.png",
    )


def render_reference_inspired_preview():
    render_collection_preview(
        "ReferenceInspired",
        {
            "ReferenceInspired_MagicStone_Crown": (-2.8, 0, 0),
            "ReferenceInspired_MagicStone_Altar": (0, 0, 0),
            "ReferenceInspired_MagicStone_Keep": (2.8, 0, 0),
        },
        "ReferenceInspiredMagicStone_Preview.png",
    )


def render_exact_reference_preview():
    camera = bpy.data.objects["PreviewCamera"]
    original_location = camera.location.copy()
    original_rotation = camera.rotation_euler.copy()
    camera.location = (6.5, -8.8, 5.15)
    aim_at(camera, (0, 0, 1.82))
    render_collection_preview(
        "ExactReference",
        {"ExactReference_MagicStone_Tower": (0, 0, 0)},
        "ExactReferenceMagicStone_Preview.png",
    )
    camera.location = original_location
    camera.rotation_euler = original_rotation


def main():
    os.makedirs(os.path.dirname(SOURCE_PATH), exist_ok=True)
    reset_scene()
    materials = {
        "lp_rock": material("LP Rock", (0.31, 0.36, 0.4)),
        "lp_rock_dark": material("LP Rock Dark", (0.23, 0.28, 0.32)),
        "lp_trunk": material("LP Trunk", (0.33, 0.16, 0.075)),
        "lp_leaf": material("LP Leaf", (0.10, 0.42, 0.16)),
        "lp_leaf_dark": material("LP Leaf Dark", (0.07, 0.31, 0.13)),
        "lp_leaf_light": material("LP Leaf Light", (0.19, 0.52, 0.18)),
        "lp_pedestal": material("LP Pedestal", (0.24, 0.25, 0.29)),
        "lp_pedestal_dark": material("LP Pedestal Dark", (0.15, 0.16, 0.2)),
        "lp_crystal": material("LP Crystal", (0.72, 0.035, 0.09), 0.28, (0.8, 0.01, 0.03)),
        "lp_crystal_light": material("LP Crystal Light", (1.0, 0.12, 0.19), 0.22, (1.0, 0.03, 0.05)),
        "lp_crystal_dark": material("LP Crystal Dark", (0.38, 0.008, 0.045), 0.3, (0.45, 0.005, 0.02)),
        "sb_rock": material("SB Rock", (0.39, 0.43, 0.47)),
        "sb_rock_warm": material("SB Rock Warm", (0.45, 0.41, 0.38)),
        "sb_moss": material("SB Moss", (0.26, 0.47, 0.19)),
        "sb_trunk": material("SB Trunk", (0.43, 0.22, 0.11)),
        "sb_trunk_dark": material("SB Trunk Dark", (0.34, 0.17, 0.09)),
        "sb_leaf": material("SB Leaf", (0.16, 0.5, 0.22)),
        "sb_leaf_light": material("SB Leaf Light", (0.29, 0.62, 0.27)),
        "sb_pedestal": material("SB Pedestal", (0.34, 0.32, 0.36)),
        "sb_pedestal_warm": material("SB Pedestal Warm", (0.46, 0.34, 0.29)),
        "sb_crystal": material("SB Crystal", (0.76, 0.045, 0.14), 0.25, (0.9, 0.02, 0.05)),
        "sb_crystal_light": material("SB Crystal Light", (1.0, 0.18, 0.26), 0.2, (1.0, 0.05, 0.08)),
        "sb_crystal_dark": material("SB Crystal Dark", (0.45, 0.015, 0.07), 0.28, (0.5, 0.01, 0.03)),
        "real_rock": material("Real Rock", (0.28, 0.3, 0.31), 0.92),
        "real_rock_dark": material("Real Rock Dark", (0.16, 0.18, 0.19), 0.96),
        "real_rock_warm": material("Real Rock Warm", (0.34, 0.31, 0.28), 0.9),
        "real_moss": material("Real Moss", (0.16, 0.27, 0.09), 1.0),
        "real_trunk": material("Real Trunk", (0.24, 0.11, 0.045), 0.95),
        "real_trunk_dark": material("Real Trunk Dark", (0.13, 0.055, 0.022), 0.98),
        "real_leaf": material("Real Leaf", (0.045, 0.22, 0.075), 0.9),
        "real_leaf_light": material("Real Leaf Light", (0.09, 0.34, 0.11), 0.88),
        "real_pedestal": material("Real Pedestal", (0.25, 0.26, 0.27), 0.88),
        "real_pedestal_dark": material("Real Pedestal Dark", (0.12, 0.13, 0.14), 0.94),
        "real_metal": material("Real Metal Inlay", (0.33, 0.2, 0.08), 0.35),
        "real_crystal": material("Real Crystal", (0.56, 0.008, 0.04), 0.16, (0.9, 0.005, 0.02)),
        "real_crystal_light": material("Real Crystal Light", (0.9, 0.035, 0.08), 0.12, (1.0, 0.02, 0.04)),
        "real_crystal_dark": material("Real Crystal Dark", (0.22, 0.002, 0.015), 0.2, (0.35, 0.002, 0.01)),
        "art_stone": material("Artificial Stone", (0.25, 0.27, 0.3), 0.84),
        "art_stone_dark": material("Artificial Stone Dark", (0.1, 0.115, 0.14), 0.92),
        "art_metal": material("Artificial Brass", (0.48, 0.27, 0.07), 0.3),
        "art_metal_dark": material("Artificial Dark Metal", (0.12, 0.09, 0.075), 0.38),
        "art_core": material("Artificial Core", (0.55, 0.006, 0.025), 0.1, (1.0, 0.006, 0.015)),
        "art_core_light": material("Artificial Core Light", (0.9, 0.025, 0.07), 0.08, (1.0, 0.02, 0.05)),
        "art_core_dark": material("Artificial Core Dark", (0.35, 0.001, 0.015), 0.12, (0.75, 0.003, 0.015)),
        "ref_rock": material("Refined Granite", (0.3, 0.31, 0.3), 0.96),
        "ref_rock_warm": material("Refined Warm Rock", (0.34, 0.3, 0.25), 0.94),
        "ref_rock_dark": material("Refined Rock Shadow", (0.13, 0.14, 0.13), 1.0),
        "ref_moss": material("Refined Moss", (0.12, 0.22, 0.055), 1.0),
        "ref_trunk": material("Refined Bark", (0.2, 0.085, 0.028), 1.0),
        "ref_trunk_dark": material("Refined Bark Dark", (0.095, 0.036, 0.012), 1.0),
        "ref_leaf": material("Refined Leaf", (0.035, 0.2, 0.055), 0.92),
        "ref_leaf_light": material("Refined Leaf Light", (0.09, 0.34, 0.09), 0.9),
        "ref_leaf_dark": material("Refined Leaf Dark", (0.018, 0.105, 0.035), 0.96),
        "ref_pedestal": material("Refined Pedestal", (0.3, 0.31, 0.32), 0.9),
        "ref_pedestal_dark": material("Refined Pedestal Dark", (0.14, 0.15, 0.16), 0.96),
        "ref_core": material("Refined Oval Core", (0.58, 0.008, 0.035), 0.12, (0.85, 0.006, 0.02)),
        "ref_core_light": material("Refined Diamond Core", (0.8, 0.018, 0.055), 0.1, (1.0, 0.015, 0.04)),
        "ref_core_dark": material("Refined Tablet Core", (0.38, 0.003, 0.018), 0.16, (0.6, 0.003, 0.015)),
        "ri_stone": material("Reference Stone", (0.24, 0.235, 0.23), 0.94),
        "ri_stone_dark": material("Reference Stone Dark", (0.105, 0.105, 0.11), 0.98),
        "ri_gold": material("Reference Gold", (0.48, 0.25, 0.055), 0.34),
        "ri_gold_dark": material("Reference Dark Gold", (0.27, 0.12, 0.025), 0.45),
        "ri_core": material("Reference Core", (0.64, 0.006, 0.025), 0.1, (1.0, 0.008, 0.018)),
        "ri_core_light": material("Reference Core Light", (0.85, 0.018, 0.05), 0.08, (1.0, 0.02, 0.04)),
        "ri_core_dark": material("Reference Core Dark", (0.44, 0.002, 0.018), 0.13, (0.75, 0.004, 0.015)),
        "ex_stone": material("Tower Stone", (0.21, 0.205, 0.19), 0.98),
        "ex_stone_light": material("Tower Foundation Stone", (0.34, 0.32, 0.27), 0.96),
        "ex_stone_dark": material("Tower Dark Stone", (0.095, 0.09, 0.085), 1.0),
        "ex_wood": material("Tower Timber", (0.24, 0.115, 0.045), 0.94),
        "ex_wood_dark": material("Tower Dark Timber", (0.11, 0.045, 0.018), 0.98),
        "ex_metal_dark": material("Tower Iron", (0.055, 0.05, 0.048), 0.62),
        "ex_gold": material("Tower Socket", (0.38, 0.18, 0.035), 0.36),
        "ex_banner": material("Tower Red Banner", (0.56, 0.012, 0.018), 0.82),
        "ex_door": material("Tower Door", (0.015, 0.012, 0.01), 1.0),
        "ex_crystal": material("Tower Main Crystal", (0.5, 0.004, 0.014), 0.09, (0.24, 0.002, 0.006)),
        "ex_crystal_light": material("Tower Light Crystal", (0.68, 0.012, 0.027), 0.08, (0.3, 0.006, 0.012)),
        "ex_crystal_dark": material("Tower Dark Crystal", (0.28, 0.001, 0.008), 0.12, (0.16, 0.001, 0.004)),
        "ground": material("Preview Ground", (0.09, 0.13, 0.12)),
    }
    create_low_poly(materials)
    create_storybook(materials)
    create_realistic(materials)
    create_artificial_magic_stones(materials)
    create_refined_models(materials)
    create_reference_inspired_magic_stones(materials)
    create_reference_tower(materials)
    export_models()
    configure_world()
    render_style_preview("LowPoly", materials)
    render_style_preview("Storybook", materials)
    render_style_preview("Realistic", materials)
    render_artificial_preview()
    render_refined_previews()
    render_reference_inspired_preview()
    render_exact_reference_preview()
    bpy.data.collections["LowPoly"].hide_render = False
    bpy.data.collections["Storybook"].hide_render = False
    bpy.data.collections["Realistic"].hide_render = False
    bpy.data.collections["Artificial"].hide_render = False
    bpy.data.collections["Refined"].hide_render = False
    bpy.data.collections["ReferenceInspired"].hide_render = False
    bpy.data.collections["ExactReference"].hide_render = False
    bpy.data.collections["Preview"].hide_render = True
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE_PATH)


if __name__ == "__main__":
    main()
