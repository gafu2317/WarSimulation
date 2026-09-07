import bpy
import bmesh
import json
import math
import os


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "ArtSource", "Blender", "FantasyChurchAndBlacksmith.blend")
REVIEW = os.path.join(ROOT, "docs", "Art", "FantasyChurchAndBlacksmith")
bpy.ops.wm.open_mainfile(filepath=SOURCE)
with open(os.path.join(REVIEW, "model_manifest.json")) as handle:
    manifest = json.load(handle)

assert {model["name"] for model in manifest["models"]} == {"Church", "Blacksmith"}
required_parts = {
    "Church": {"Church bell tower", "Church rose window surround", "Church transept", "Church apse"},
    "Blacksmith": {"Blacksmith forge masonry base", "Blacksmith forged anvil", "Blacksmith chimney shaft", "Blacksmith open bay roof"},
}
records = []
for entry in manifest["models"]:
    collection = bpy.data.collections[entry["name"]]
    root = bpy.data.objects[entry["name"]]
    meshes = [obj for obj in collection.objects if obj.type == "MESH"]
    names = {obj.name for obj in meshes}
    assert required_parts[entry["name"]] <= names, (entry["name"], "recognizable facility parts")
    assert root.location.length == 0, (entry["name"], "asset origin")
    points = []
    triangles = 0
    for obj in meshes:
        assert obj.parent == root, (obj.name, "facility hierarchy")
        assert obj.material_slots and all(slot.material for slot in obj.material_slots), (obj.name, "material assignment")
        # 未評価シーンの matrix_world では保存された姿勢を検証できないため、親子変換を直接使う。
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
    dimensions = [maximum[index] - minimum[index] for index in range(3)]
    if entry["name"] == "Church":
        assert dimensions[0] >= 19 and dimensions[1] >= 28 and dimensions[2] >= 21
    else:
        assert dimensions[0] >= 15 and dimensions[1] >= 12 and dimensions[2] >= 12
    records.append({
        "name": entry["name"],
        "facility_type": entry["facility_type"],
        "status": "PASS",
        "mesh_parts": len(meshes),
        "triangles": triangles,
        "dimensions_m": dimensions,
    })

assert not [image for image in bpy.data.images if image.source == "FILE" and not image.packed_file], "self-contained materials"
report = {
    "environment": "Blender " + bpy.app.version_string,
    "blend_reopen": "PASS",
    "models": records,
    "external_textures_required": False,
}
with open(os.path.join(REVIEW, "validation.json"), "w") as handle:
    json.dump(report, handle, indent=2, ensure_ascii=False)
print(json.dumps(report, indent=2, ensure_ascii=False))
