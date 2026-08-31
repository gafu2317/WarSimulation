import bpy
import bmesh
import json
import math
import os
from mathutils import Vector
from mathutils.bvhtree import BVHTree


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REVIEW = os.path.join(ROOT, "docs", "Art", "FantasyEntertainmentDistrict")
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, "ArtSource", "Blender", "FantasyEntertainmentDistrict.blend"))
with open(os.path.join(REVIEW, "model_manifest.json")) as handle:
    manifest = json.load(handle)
assert {model["name"] for model in manifest["models"]} == {"VelvetSalon", "CopperTankard", "LanternTheatre", "LanternLane"}
records = []
all_vertices, all_faces = [], []
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
        transform = root.matrix_basis @ obj.matrix_parent_inverse @ obj.matrix_basis
        world_points = [transform @ vertex.co for vertex in obj.data.vertices]
        points.extend(world_points)
        offset = len(all_vertices)
        all_vertices.extend(world_points)
        all_faces.extend(tuple(offset+i for i in face.vertices) for face in obj.data.polygons)
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
    assert abs(minimum[2]-entry["ground_z"]) <= resolution, (entry["name"], "ground contact", minimum[2])
    records.append({"name": entry["name"], "facility_type": entry["facility_type"], "status": "PASS",
                    "mesh_parts": len(meshes), "triangles": triangles,
                    "dimensions_m": [b-a for a,b in zip(minimum, maximum)]})
tree = BVHTree.FromPolygons(all_vertices, all_faces)
passage_hit = tree.ray_cast(Vector((0,-11.25,1.8)), Vector((0,1,0)), 13.2)
assert passage_hit[0] is None, "central lane remains clear below the gateway and lanterns"
assert not [image for image in bpy.data.images if image.source == "FILE" and not image.packed_file], "self-contained materials"
report = {"environment": "Blender " + bpy.app.version_string, "blend_reopen": "PASS",
          "models": records, "external_textures_required": False, "central_lane_clear": "PASS"}
with open(os.path.join(REVIEW, "validation.json"), "w") as handle:
    json.dump(report, handle, indent=2, ensure_ascii=False)
print(json.dumps(report, indent=2, ensure_ascii=False))
