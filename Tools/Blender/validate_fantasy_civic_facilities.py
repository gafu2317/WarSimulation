import bpy
import bmesh
import json
import math
import os


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REVIEW = os.path.join(ROOT, "docs", "Art", "FantasyCivicFacilities")
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, "ArtSource", "Blender", "FantasyCivicFacilities.blend"))
with open(os.path.join(REVIEW, "model_manifest.json")) as handle:
    manifest = json.load(handle)
assert {model["name"] for model in manifest["models"]} == {"Library", "Plaza", "Observatory"}
records = []
for entry in manifest["models"]:
    collection = bpy.data.collections[entry["name"]]
    root = bpy.data.objects[entry["name"]]
    points = []
    triangles = 0
    meshes = [obj for obj in collection.objects if obj.type == "MESH"]
    assert meshes, (entry["name"], "editable building geometry")
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
    assert all(math.isfinite(value) for point in points for value in point), "finite geometry"
    minimum = [min(point[i] for point in points) for i in range(3)]
    maximum = [max(point[i] for point in points) for i in range(3)]
    resolution = max(abs(v) for v in minimum + maximum) * 2 ** -23
    assert abs(minimum[2]) <= resolution, (entry["name"], "ground contact", minimum[2])
    records.append({"name": entry["name"], "facility_type": entry["facility_type"], "status": "PASS",
                    "mesh_parts": len(meshes), "triangles": triangles,
                    "dimensions_m": [b-a for a,b in zip(minimum, maximum)]})
assert not [image for image in bpy.data.images if image.source == "FILE" and not image.packed_file], "self-contained materials"
report = {"environment": "Blender " + bpy.app.version_string, "blend_reopen": "PASS",
          "models": records, "external_textures_required": False}
with open(os.path.join(REVIEW, "validation.json"), "w") as handle:
    json.dump(report, handle, indent=2, ensure_ascii=False)
print(json.dumps(report, indent=2, ensure_ascii=False))
