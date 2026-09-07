import bpy
import bmesh
import json
import math
import os


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "ArtSource", "Blender", "FantasyCityVegetation.blend")
REVIEW = os.path.join(ROOT, "docs", "Art", "FantasyCityVegetation")
bpy.ops.wm.open_mainfile(filepath=SOURCE)
with open(os.path.join(REVIEW, "model_manifest.json")) as handle:
    manifest = json.load(handle)

expected = {
    "GroundPlant_GrassShort", "GroundPlant_GrassTuft", "GroundPlant_GrassTall",
    "GroundPlant_FernPatch", "Flower_WildPatch", "Flower_Border",
    "Tree_AlleyCypress", "Tree_Street", "Tree_Shade",
}
assert {entry["name"] for entry in manifest["models"]} == expected
categories = {entry["category"] for entry in manifest["models"]}
assert categories == {"ground_plant", "flower", "tree"}
records = []
dimensions_by_name = {}
for entry in manifest["models"]:
    collection = bpy.data.collections[entry["name"]]
    root = bpy.data.objects[entry["name"]]
    meshes = [obj for obj in collection.objects if obj.type == "MESH"]
    assert meshes and root.location.length == 0
    points = []
    triangles = 0
    for obj in meshes:
        assert obj.parent == root, (obj.name, "editable asset hierarchy")
        assert obj.material_slots and all(slot.material for slot in obj.material_slots), (obj.name, "material assignment")
        transform = obj.matrix_parent_inverse @ obj.matrix_basis
        points.extend(transform @ vertex.co for vertex in obj.data.vertices)
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        assert all(edge.is_manifold for edge in bm.edges), (obj.name, "closed surfaces")
        assert all(face.calc_area() > 0 for face in bm.faces), (obj.name, "nonzero faces")
        bm.free()
    assert all(math.isfinite(value) for point in points for value in point), (entry["name"], "finite geometry")
    minimum = [min(point[index] for point in points) for index in range(3)]
    maximum = [max(point[index] for point in points) for index in range(3)]
    resolution = max(abs(value) for value in minimum + maximum) * 2 ** -23
    assert abs(minimum[2]) <= resolution, (entry["name"], "ground contact", minimum[2])
    assert maximum[2] > 0, (entry["name"], "visible height")
    dimensions = [maximum[index] - minimum[index] for index in range(3)]
    dimensions_by_name[entry["name"]] = dimensions
    records.append({
        "name": entry["name"], "category": entry["category"], "status": "PASS",
        "mesh_parts": len(meshes), "triangles": triangles,
        "dimensions_m": dimensions,
    })

grass_heights = [dimensions_by_name[name][2] for name in
                 ("GroundPlant_GrassShort", "GroundPlant_GrassTuft", "GroundPlant_GrassTall")]
assert grass_heights[0] < grass_heights[1] < grass_heights[2], "ordered grass height variants"
for name in ("Tree_AlleyCypress", "Tree_Street", "Tree_Shade"):
    collection = bpy.data.collections[name]
    trunks = [obj for obj in collection.objects if "trunk" in obj.name.lower()]
    crowns = [obj for obj in collection.objects if "crown" in obj.name.lower() or "foliage" in obj.name.lower()]
    trunk_top = max(((obj.matrix_parent_inverse @ obj.matrix_basis) @ vertex.co).z
                    for obj in trunks for vertex in obj.data.vertices)
    crown_bottom = min(((obj.matrix_parent_inverse @ obj.matrix_basis) @ vertex.co).z
                       for obj in crowns for vertex in obj.data.vertices)
    assert trunk_top >= crown_bottom, (name, "connected trunk and canopy")

materials = {slot.material for name in expected for obj in bpy.data.collections[name].objects
             if obj.type == "MESH" for slot in obj.material_slots}
for material in materials:
    shader = material.node_tree.nodes.get("Principled BSDF") if material.use_nodes else None
    strength = shader.inputs.get("Emission Strength") if shader else None
    assert strength is None or strength.default_value == 0.0, (material.name, "non-emissive material")
assert not [image for image in bpy.data.images if image.source == "FILE" and not image.packed_file]
report = {
    "environment": "Blender " + bpy.app.version_string,
    "blend_reopen": "PASS",
    "editable_asset_hierarchy": "PASS",
    "ground_contact": "PASS",
    "closed_geometry": "PASS",
    "non_emissive_materials": "PASS",
    "grass_height_variants": "PASS",
    "connected_tree_canopies": "PASS",
    "external_textures_required": False,
    "models": records,
}
with open(os.path.join(REVIEW, "validation.json"), "w") as handle:
    json.dump(report, handle, indent=2, ensure_ascii=False)
print(json.dumps(report, indent=2, ensure_ascii=False))
