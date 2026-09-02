import hashlib
import json
import math
import sys
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[3]
TOWN_BLOCK_TOOL = ROOT / "Tools/Blender/UnityTownBlock"
sys.path.insert(0, str(TOWN_BLOCK_TOOL))
import export_block as shared


OUTPUT = ROOT / "Assets/Models/Kingdom/City"
REVIEW = ROOT / "docs/Art/KingdomCity"
SOURCES = {
    "KingdomBuildings_RealisticFantasy": ["Royal_Castle", "Granite_Straight", "Granite_Gate", "Granite_Corner"],
    "FantasyTownFacilities": ["Tavern", "Clinic", "Guardhouse"],
    "FantasyCultureFacilities": ["Casino", "Museum", "Arena"],
    "FantasyCivicFacilities": ["Library", "Plaza", "Observatory"],
    "FantasyNightlifeBuildings": ["CrimsonRowhouse", "VelvetTerrace", "LanternSpire", "VeiledCourtyard"],
    "FantasyKingdomSupportBuildings": ["Granary", "Warehouse", "Bakery", "Stable", "Barracks", "Guildhall", "Bathhouse", "Chapel", "MerchantHouse", "WorkshopHouse"],
    "FantasyTownProps": ["Road_T", "Road_Cross", "Road_End", "Handcart", "Well", "Noticeboard", "Forge", "Anvil", "Water_Trough", "Firewood_Rack", "Hay_Bale", "Clothesline", "Signpost"],
}


def texture_size(surface_area):
    return 2 ** math.ceil(math.log2(math.sqrt(surface_area) * 32))


def main():
    shared.OUTPUT = OUTPUT
    for directory in (OUTPUT / "Models", OUTPUT / "Textures", REVIEW):
        directory.mkdir(parents=True, exist_ok=True)
    records = []
    for group, names in SOURCES.items():
        source = ROOT / "ArtSource/Blender" / f"{group}.blend"
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        for name in names:
            obj = shared.combined_mesh(source, name)
            if name.startswith("Granite_"):
                offset_x = {"Granite_Straight": -9.5, "Granite_Gate": 0, "Granite_Corner": 9.5}[name]
                offset_y = 13
                for vertex in obj.data.vertices:
                    vertex.co.x -= offset_x
                    vertex.co.y -= offset_y
            area = sum(poly.area for poly in obj.data.polygons)
            size = texture_size(area)
            print("BAKE_START", name, size, flush=True)
            shared.bake(obj, name, size)
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
            records.append({
                "name": name,
                "source": str(source.relative_to(ROOT)),
                "source_sha256": digest,
                "minimum": minimum,
                "maximum": maximum,
                "triangles": len(obj.data.polygons),
                "atlas_size": size,
                "fbx": f"Assets/Models/Kingdom/City/Models/{name}.fbx",
            })
            print("EXPORTED", name, flush=True)
        assert hashlib.sha256(source.read_bytes()).hexdigest() == digest
    (REVIEW / "export_manifest.json").write_text(json.dumps({
        "models": records,
        "blender": bpy.app.version_string,
        "uv_texels_per_meter": 32,
        "texel_basis": "The 176m-wide kingdom overview projects at about 16 pixels per meter; 32 preserves a two-times review margin.",
        "origin": "Original asset pivot preserved; wall modules are centered horizontally for socket placement",
    }, ensure_ascii=False, indent=2))
    print("EXPORT_COMPLETE", len(records), flush=True)


if __name__ == "__main__":
    main()
