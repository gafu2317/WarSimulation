import math
import os
import sys

import bpy
import numpy as np


TREE_PREFIX = "NaturalTree_"
ROCK_PREFIX = "NaturalRock_"


def argument_after_separator():
    if "--" not in sys.argv:
        return None
    values = sys.argv[sys.argv.index("--") + 1 :]
    return values[0] if values else None


def material(name, color, roughness, noise_scale, noise_strength, color_variation=0.0):
    value = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    value.diffuse_color = (*color, 1.0)
    value.metallic = 0.0
    value.roughness = roughness
    value.use_nodes = True

    nodes = value.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    noise = nodes.new("ShaderNodeTexNoise")
    bump = nodes.new("ShaderNodeBump")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    noise.inputs["Scale"].default_value = noise_scale
    noise.inputs["Detail"].default_value = 3.0
    noise.inputs["Roughness"].default_value = 0.72
    bump.inputs["Strength"].default_value = noise_strength
    bump.inputs["Distance"].default_value = 0.08
    if color_variation > 0.0:
        ramp = nodes.new("ShaderNodeValToRGB")
        darker = tuple(max(0.0, channel - color_variation) for channel in color)
        lighter = tuple(min(1.0, channel + color_variation) for channel in color)
        ramp.color_ramp.elements[0].color = (*darker, 1.0)
        ramp.color_ramp.elements[1].color = (*lighter, 1.0)
        value.node_tree.links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
        value.node_tree.links.new(ramp.outputs["Color"], shader.inputs["Base Color"])
    value.node_tree.links.new(noise.outputs["Fac"], bump.inputs["Height"])
    value.node_tree.links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    value.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return value


def deterministic_unit(seed):
    value = math.sin(seed * 12.9898 + 78.233) * 43758.5453
    return value - math.floor(value)


def replace_materials(mesh, values):
    mesh.materials.clear()
    for value in values:
        mesh.materials.append(value)


def tree_materials():
    return {
        "bark": bark_material(),
        "leaf": [
            material("Natural Tree Leaf Deep", (0.090, 0.260, 0.075), 0.90, 5.5, 0.08, 0.014),
            material("Natural Tree Leaf", (0.110, 0.320, 0.085), 0.89, 5.8, 0.08, 0.014),
            material("Natural Tree Leaf Fresh", (0.140, 0.380, 0.100), 0.88, 6.0, 0.07, 0.014),
            material("Natural Tree Leaf Olive", (0.160, 0.300, 0.070), 0.91, 5.4, 0.08, 0.014),
        ],
    }


def bark_material():
    value = bpy.data.materials.get("Natural Tree Bark") or bpy.data.materials.new("Natural Tree Bark")
    value.diffuse_color = (0.360, 0.160, 0.055, 1.0)
    value.metallic = 0.0
    value.roughness = 0.88
    value.use_nodes = True

    nodes = value.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    coordinates = nodes.new("ShaderNodeTexCoord")
    mapping = nodes.new("ShaderNodeMapping")
    noise = nodes.new("ShaderNodeTexNoise")
    ramp = nodes.new("ShaderNodeValToRGB")
    bump = nodes.new("ShaderNodeBump")
    roughness = nodes.new("ShaderNodeMapRange")

    mapping.inputs["Scale"].default_value = (4.2, 4.2, 0.48)
    noise.noise_dimensions = "3D"
    noise.inputs["Scale"].default_value = 4.0
    noise.inputs["Detail"].default_value = 8.0
    noise.inputs["Roughness"].default_value = 0.82
    noise.inputs["Distortion"].default_value = 0.22
    ramp.color_ramp.elements[0].position = 0.25
    ramp.color_ramp.elements[0].color = (0.190, 0.070, 0.020, 1.0)
    ramp.color_ramp.elements[1].position = 0.78
    ramp.color_ramp.elements[1].color = (0.520, 0.270, 0.100, 1.0)
    bump.inputs["Strength"].default_value = 0.34
    bump.inputs["Distance"].default_value = 0.075
    roughness.inputs["From Min"].default_value = 0.0
    roughness.inputs["From Max"].default_value = 1.0
    roughness.inputs["To Min"].default_value = 0.76
    roughness.inputs["To Max"].default_value = 0.96

    value.node_tree.links.new(coordinates.outputs["Generated"], mapping.inputs["Vector"])
    value.node_tree.links.new(mapping.outputs["Vector"], noise.inputs["Vector"])
    value.node_tree.links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
    value.node_tree.links.new(ramp.outputs["Color"], shader.inputs["Base Color"])
    value.node_tree.links.new(noise.outputs["Fac"], bump.inputs["Height"])
    value.node_tree.links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    value.node_tree.links.new(noise.outputs["Fac"], roughness.inputs["Value"])
    value.node_tree.links.new(roughness.outputs["Result"], shader.inputs["Roughness"])
    value.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return value


def assign_bark(obj, value):
    assign_cylindrical_bark_uv(obj)
    replace_materials(obj.data, [value])
    for polygon in obj.data.polygons:
        polygon.material_index = 0


def assign_cylindrical_bark_uv(obj):
    mesh = obj.data
    uv = mesh.uv_layers.get("BarkUV") or mesh.uv_layers.new(name="BarkUV")
    minimum = min((vertex.co.z for vertex in mesh.vertices), default=0.0)
    maximum = max((vertex.co.z for vertex in mesh.vertices), default=1.0)
    span = max(maximum - minimum, 0.001)
    for polygon in mesh.polygons:
        values = []
        for loop_index in polygon.loop_indices:
            vertex = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            u = math.atan2(vertex.y, vertex.x) / math.tau + 0.5
            v = (vertex.z - minimum) / span * 2.0
            values.append([loop_index, u, v])
        if values and max(value[1] for value in values) - min(value[1] for value in values) > 0.5:
            for value in values:
                if value[1] < 0.5:
                    value[1] += 1.0
        for loop_index, u, v in values:
            uv.data[loop_index].uv = (u, v)


def assign_leaves(obj, value):
    replace_materials(obj.data, [value])
    for polygon in obj.data.polygons:
        polygon.material_index = 0


def refine_trees():
    values = tree_materials()
    collections = sorted(
        (c for c in bpy.data.collections if c.name.startswith(TREE_PREFIX)),
        key=lambda c: c.name,
    )
    palette_order = (1, 1, 2, 1, 0, 2, 1, 3, 1, 0)
    for collection_index, collection in enumerate(collections):
        leaf = values["leaf"][palette_order[collection_index % len(palette_order)]]
        for obj in collection.objects:
            if obj.type != "MESH":
                continue
            if obj.name.startswith("Foliage"):
                assign_leaves(obj, leaf)
            elif obj.name.startswith("Trunk"):
                assign_bark(obj, values["bark"])


def generate_tree_texture(name, base_color, bark=False):
    size = 256
    image_name = f"NaturalTree_{name}_Albedo"
    image = bpy.data.images.get(image_name)
    if image is not None:
        bpy.data.images.remove(image)
    image = bpy.data.images.new(image_name, width=size, height=size, alpha=False)
    pixels = []
    heights = []
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            if bark:
                broad = math.sin(math.tau * (u * 3.0 + 0.15 * math.sin(math.tau * v))) * 0.55
                fine = math.sin(math.tau * (u * 11.0 + v * 1.5)) * 0.20
                grain = deterministic_unit(x * 31 + y * 79 + 17) - 0.5
                variation = broad * 0.055 + fine * 0.025 + grain * 0.025
                heights.append(variation)
            else:
                broad = (
                    math.sin(math.tau * (u * 2.0 + v * 1.0 + 0.13)) * 0.45
                    + math.cos(math.tau * (u * 1.0 - v * 2.0 + 0.37)) * 0.35
                    + math.sin(math.tau * (u * 4.0 + v * 3.0 + 0.61)) * 0.20
                )
                grain = deterministic_unit(x * 23 + y * 61 + len(name) * 97) - 0.5
                variation = broad * 0.035 + grain * 0.012
            color = [max(0.0, min(1.0, channel + variation)) for channel in base_color]
            pixels.extend((*color, 1.0))
    image.colorspace_settings.name = "sRGB"
    image.pixels.foreach_set(np.asarray(pixels, dtype=np.float32))
    image.update()
    repository = os.path.dirname(os.path.dirname(os.path.dirname(bpy.data.filepath)))
    directory = os.path.join(repository, "Assets/Models/Environment/NaturalTreeVariants/Textures")
    os.makedirs(directory, exist_ok=True)
    image.filepath_raw = os.path.join(directory, f"{image_name}.png")
    image.file_format = "PNG"
    image.save()
    if bark:
        height = np.asarray(heights, dtype=np.float32).reshape((size, size))
        dx = np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)
        dy = np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)
        nx = -dx * 5.0
        ny = -dy * 2.0
        nz = np.ones_like(height)
        length = np.sqrt(nx * nx + ny * ny + nz * nz)
        normal = np.stack((nx / length, ny / length, nz / length), axis=-1) * 0.5 + 0.5
        normal_pixels = np.concatenate(
            (normal, np.ones((size, size, 1), dtype=np.float32)),
            axis=-1,
        ).reshape(-1)
        normal_image = bpy.data.images.get("NaturalTree_Bark_Normal")
        if normal_image is not None:
            bpy.data.images.remove(normal_image)
        normal_image = bpy.data.images.new("NaturalTree_Bark_Normal", width=size, height=size, alpha=False)
        normal_image.colorspace_settings.name = "Non-Color"
        normal_image.pixels.foreach_set(normal_pixels)
        normal_image.update()
        normal_image.filepath_raw = os.path.join(directory, "NaturalTree_Bark_Normal.png")
        normal_image.file_format = "PNG"
        normal_image.save()


def generate_tree_textures():
    generate_tree_texture("Bark", (0.360, 0.160, 0.055), bark=True)
    generate_tree_texture("Leaf_Deep", (0.090, 0.260, 0.075))
    generate_tree_texture("Leaf_Forest", (0.110, 0.320, 0.085))
    generate_tree_texture("Leaf_Fresh", (0.140, 0.380, 0.100))
    generate_tree_texture("Leaf_Olive", (0.160, 0.300, 0.070))


def rock_materials():
    families = [
        ("Slate", (0.31, 0.35, 0.35)),
        ("Granite", (0.38, 0.40, 0.37)),
        ("Warm", (0.40, 0.35, 0.29)),
    ]
    return {name: generate_rock_texture(name, color) for name, color in families}


def generate_rock_texture(name, base_color):
    size = 256
    image_name = f"NaturalRock_{name}_Albedo"
    image = bpy.data.images.get(image_name)
    if image is not None:
        bpy.data.images.remove(image)
    image = bpy.data.images.new(image_name, width=size, height=size, alpha=False)
    pixels = []
    for y in range(size):
        v = y / size
        for x in range(size):
            u = x / size
            broad = (
                math.sin(math.tau * (u * 2.0 + v * 1.0 + 0.17)) * 0.45
                + math.cos(math.tau * (u * 1.0 - v * 3.0 + 0.31)) * 0.30
                + math.sin(math.tau * (u * 5.0 + v * 4.0 + 0.53)) * 0.15
                + math.cos(math.tau * (u * 11.0 - v * 7.0 + 0.71)) * 0.10
            )
            grain = deterministic_unit(x * 19 + y * 83 + len(name) * 101) - 0.5
            variation = broad * 0.075 + grain * 0.035
            color = [max(0.0, min(1.0, channel + variation)) for channel in base_color]
            if deterministic_unit(x * 47 + y * 131) > 0.992:
                color = [min(1.0, channel + 0.10) for channel in color]
            pixels.extend((*color, 1.0))
    image.colorspace_settings.name = "sRGB"
    image.pixels.foreach_set(np.asarray(pixels, dtype=np.float32))
    image.update()
    directory = os.path.join(os.path.dirname(bpy.data.filepath), "Textures")
    os.makedirs(directory, exist_ok=True)
    image.filepath_raw = os.path.join(directory, f"{image_name}.png")
    image.file_format = "PNG"
    image.save()
    return image


def rock_textured_material(name, image):
    value = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    value.diffuse_color = (0.35, 0.36, 0.34, 1.0)
    value.metallic = 0.0
    value.roughness = 0.95
    value.use_nodes = True
    nodes = value.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    texture = nodes.new("ShaderNodeTexImage")
    bump = nodes.new("ShaderNodeBump")
    texture.image = image
    shader.inputs["Roughness"].default_value = 0.95
    bump.inputs["Strength"].default_value = 0.16
    bump.inputs["Distance"].default_value = 0.045
    value.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    value.node_tree.links.new(texture.outputs["Color"], bump.inputs["Height"])
    value.node_tree.links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    value.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return value


def assign_rock(obj, values):
    material_name, family = rock_material_profile(obj.name)
    selected = rock_textured_material(material_name, values[family])
    replace_materials(obj.data, [selected])
    for polygon in obj.data.polygons:
        polygon.material_index = 0


def rock_material_profile(object_name):
    profiles = {
        "Rock_Main": ("Rock Granite Dark", "Granite"),
        "Rock_Base": ("Rock Granite", "Granite"),
        "Rock_Main.001": ("Rock Granite", "Granite"),
        "Rock_Main.002": ("Rock River Stone", "Slate"),
        "Rock_Main.003": ("Rock Basalt", "Slate"),
        "Rock_Fragment": ("Rock Basalt Light", "Slate"),
        "Rock_Main.004": ("Rock Slate", "Slate"),
        "Rock_Main.005": ("Rock Warm Stone", "Warm"),
        "Rock_Front": ("Rock Warm Stone Dark", "Warm"),
        "Rock_Main.006": ("Rock Slate Dark", "Slate"),
        "Rock_Left": ("Rock Granite", "Granite"),
        "Rock_Right": ("Rock Granite Dark", "Granite"),
        "Rock_Main.007": ("Rock Basalt Light", "Slate"),
        "Rock_Side_A": ("Rock Basalt", "Slate"),
        "Rock_Side_B": ("Rock Basalt", "Slate"),
        "Rock_Main.008": ("Rock Warm Stone Dark", "Warm"),
    }
    return profiles[object_name]


def refine_rocks():
    values = rock_materials()
    collections = sorted(
        (c for c in bpy.data.collections if c.name.startswith(ROCK_PREFIX)),
        key=lambda c: c.name,
    )
    for collection in collections:
        for obj in collection.objects:
            if obj.type == "MESH":
                assign_rock(obj, values)


def export_collection(collection, relative_directory):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in collection.objects:
        if obj.type == "MESH":
            obj.select_set(True)
    repository = os.path.dirname(os.path.dirname(os.path.dirname(bpy.data.filepath)))
    output = os.path.join(repository, relative_directory)
    os.makedirs(output, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(output, f"{collection.name}.fbx"),
        use_selection=True,
        object_types={"MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="AUTO",
        embed_textures=False,
    )


def export_trees_to_unity():
    collections = sorted(
        (c for c in bpy.data.collections if c.name.startswith(TREE_PREFIX)),
        key=lambda c: c.name,
    )
    for collection in collections:
        export_collection(collection, "Assets/Models/Environment/NaturalTreeVariants")


def main():
    requested = argument_after_separator()
    if requested == "tree-textures":
        generate_tree_textures()
        return
    if requested == "tree-export":
        export_trees_to_unity()
        return
    if requested == "tree" or (requested is None and "NaturalTree" in bpy.data.filepath):
        refine_trees()
    elif requested == "rock" or (requested is None and "NaturalRock" in bpy.data.filepath):
        refine_rocks()
    else:
        raise RuntimeError("Pass tree or rock after --, or open a matching source file.")
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)


main()
