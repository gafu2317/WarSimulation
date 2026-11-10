"""Export the reviewed GrandRoyalCastle blend as the Unity Royal_Castle asset."""

import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/Blender/Kingdom/GrandRoyalCastle.blend"
OUTPUT = ROOT / "Assets/Models/Kingdom/City"
REVIEW = ROOT / "docs/Art/Records/Castle"
sys.path.insert(0, str(ROOT / "Tools/Blender/UnityTownBlock"))
import export_block as shared

TOWN_COLORS = {
    "door": "65503A", "glass": "1C2B32", "gold": "A18748", "ground": "68734F",
    "iron": "252A2C", "mortar": "817E71", "oak": "B29862", "paving": "848375",
    "red": "742E34", "roof": "343F4A", "shade": "6E7069", "stone": "999588",
    "trim": "BCB3A0", "white": "D4C9B0", "wood": "49372B",
}


def bake_palette(obj, output):
    size, cells = 256, 4
    materials = sorted(set(obj.data.materials), key=lambda mat: mat.name)
    if len(materials) > cells * cells:
        raise ValueError("The castle palette exceeds its padded cells.")
    base = np.ones((size, size, 4), dtype=np.float32)
    normal = np.full_like(base, (0.5, 0.5, 1, 1))
    mask = np.full_like(base, (0, 1, 0, 0.15))
    records = []
    for index, mat in enumerate(materials):
        shader = next(node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
        inputs = [shader.inputs[name] for name in ("Base Color", "Metallic", "Roughness")]
        if any(value.is_linked for value in inputs):
            raise ValueError("The castle palette requires constant source materials: " + mat.name)
        source_color = list(inputs[0].default_value[:3])
        hex_color = TOWN_COLORS[mat.name]
        srgb = np.array([int(hex_color[i:i + 2], 16) / 255 for i in (0, 2, 4)])
        color = np.where(srgb <= 0.04045, srgb / 12.92, ((srgb + 0.055) / 1.055) ** 2.4)
        x, y = index % cells * 64, index // cells * 64
        base[y:y + 64, x:x + 64, :3] = srgb
        mask[y:y + 64, x:x + 64] = (inputs[1].default_value, 1, 0, 1 - inputs[2].default_value)
        records.append({"material": mat.name, "cell": index, "srgb_hex": hex_color,
                        "linear_color": color.tolist(), "source_linear_color": source_color})
    # Tiny masonry islands cannot retain padding; constant materials can share wide, inset swatches.
    uv = obj.data.uv_layers.active.data
    for poly in obj.data.polygons:
        index = materials.index(obj.data.materials[poly.material_index])
        x, y = index % cells * 64, index // cells * 64
        for loop, (u, v) in zip(poly.loop_indices, ((24, 24), (40, 24), (32, 40))):
            uv[loop].uv = ((x + u) / size, (y + v) / size)
    for suffix, pixels, color in (("BaseColor", base, True), ("Normal", normal, False), ("MetallicSmoothness", mask, False)):
        image = shared.image("Royal_Castle_" + suffix, size, color)
        image.pixels.foreach_set(pixels.ravel())
        image.filepath_raw = str(output / "Textures" / ("Royal_Castle_" + suffix + ".png"))
        image.save()
    material = bpy.data.materials.new("Royal_Castle_Baked")
    material.use_nodes = True
    shader = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    texture = material.node_tree.nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images["Royal_Castle_BaseColor"]
    material.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for poly in obj.data.polygons:
        poly.material_index = 0
    return size, records


def main(output):
    for directory in (output / "Models", output / "Textures"):
        directory.mkdir(parents=True, exist_ok=True)

    name = "Royal_Castle"
    obj = shared.combined_mesh(SOURCE, "Grand_Royal_Castle_Grounds")
    size, palette = bake_palette(obj, output)

    points = [vertex.co for vertex in obj.data.vertices]
    minimum = [min(point[axis] for point in points) for axis in range(3)]
    maximum = [max(point[axis] for point in points) for axis in range(3)]
    bpy.ops.export_scene.fbx(
        filepath=str(output / "Models" / f"{name}.fbx"),
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
    )

    record = {
        "name": name,
        "source": str(SOURCE.relative_to(ROOT)),
        "source_sha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
        "minimum": minimum,
        "maximum": maximum,
        "triangles": len(obj.data.polygons),
        "atlas_size": size,
        "texture_layout": "material_palette",
        "palette": palette,
        "fbx": "Assets/Models/Kingdom/City/Models/Royal_Castle.fbx",
    }
    manifest = REVIEW / "unity_export_manifest.json" if output == OUTPUT else output / "export_manifest.json"
    manifest.parent.mkdir(parents=True, exist_ok=True)
    manifest.write_text(
        json.dumps({"models": [record], "blender": bpy.app.version_string}, indent=2),
        encoding="utf-8",
    )
    print("EXPORTED", json.dumps(record), flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    main(args.output)
