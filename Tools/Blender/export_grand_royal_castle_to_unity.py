"""Export the reviewed GrandRoyalCastle blend as the Unity Royal_Castle asset."""

import hashlib
import json
import sys
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/Blender/GrandRoyalCastle.blend"
OUTPUT = ROOT / "Assets/Models/Kingdom/City"
REVIEW = ROOT / "docs/Art/GrandRoyalCastle"
sys.path.insert(0, str(ROOT / "Tools/Blender/UnityTownBlock"))
import export_block as shared


def main():
    for directory in (OUTPUT / "Models", OUTPUT / "Textures", REVIEW):
        directory.mkdir(parents=True, exist_ok=True)

    shared.OUTPUT = OUTPUT
    name = "Royal_Castle"
    obj = shared.combined_mesh(SOURCE, "Grand_Royal_Castle_Grounds")
    # Keep the same texture resolution class as the previous city export.
    shared.bake(obj, name, 4096)

    points = [vertex.co for vertex in obj.data.vertices]
    minimum = [min(point[axis] for point in points) for axis in range(3)]
    maximum = [max(point[axis] for point in points) for axis in range(3)]
    bpy.ops.export_scene.fbx(
        filepath=str(OUTPUT / "Models" / f"{name}.fbx"),
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
        "atlas_size": 4096,
        "fbx": "Assets/Models/Kingdom/City/Models/Royal_Castle.fbx",
    }
    (REVIEW / "unity_export_manifest.json").write_text(
        json.dumps({"models": [record], "blender": bpy.app.version_string}, indent=2),
        encoding="utf-8",
    )
    print("EXPORTED", json.dumps(record), flush=True)


if __name__ == "__main__":
    main()
