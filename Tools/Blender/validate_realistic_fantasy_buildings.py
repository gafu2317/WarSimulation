import bpy
import bmesh
import json
import os
from mathutils import Vector
from mathutils.bvhtree import BVHTree


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REVIEW = os.path.join(ROOT, "docs", "Art", "KingdomBuildings_RealisticFantasy")
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, "ArtSource", "Blender", "KingdomBuildings_RealisticFantasy.blend"))
with open(os.path.join(REVIEW, "model_manifest.json")) as handle:
    manifest = json.load(handle)
records = []
sections = {}
for entry in manifest["models"]:
    collection = bpy.data.collections[entry["name"]]
    root = bpy.data.objects[entry["name"]]
    bpy.context.window.scene = next(scene for scene in bpy.data.scenes if root.name in scene.objects)
    bpy.context.view_layer.update()
    points, faces = [], []
    triangles = 0
    for obj in collection.objects:
        if obj.type != "MESH":
            continue
        assert obj.parent == root, (obj.name, "editable asset hierarchy")
        assert all(slot.material is not None for slot in obj.material_slots), (obj.name, "assigned materials")
        # 非アクティブシーンの matrix_world は未評価になり得るため、保存された親子変換を使う。
        transform = obj.matrix_parent_inverse @ obj.matrix_basis
        offset = len(points)
        points.extend(transform @ vertex.co for vertex in obj.data.vertices)
        faces.extend(tuple(offset+i for i in face.vertices) for face in obj.data.polygons)
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        assert all(edge.is_manifold for edge in bm.edges), (obj.name, "closed surfaces")
        assert all(face.calc_area() > 0 for face in bm.faces), (obj.name, "nonzero faces")
        bm.free()
    minimum = [min(p[i] for p in points) for i in range(3)]
    maximum = [max(p[i] for p in points) for i in range(3)]
    resolution = max(abs(v) for v in minimum+maximum) * 2**-23
    assert abs(minimum[2]) <= resolution, (entry["name"], "ground contact", minimum)
    if entry["name"].endswith(("Straight", "Gate", "Corner")):
        section = {(round(v.y, 5), round(v.z, 5)) for v in points if round(v.x, 5) == 4}
        sections[entry["name"]] = section
        if entry["name"].endswith("Corner"):
            other = {(round(v.x, 5), round(v.z, 5)) for v in points if round(v.y, 5) == 4}
            assert other == section, (entry["name"], "matching corner ends")
        if entry["name"].endswith("Gate"):
            bvh = BVHTree.FromPolygons(points, faces)
            assert bvh.ray_cast(Vector((0,-3,1.7)), Vector((0,1,0)), 6)[0] is None, "gate opening"
    records.append({"name": entry["name"], "status": "PASS", "triangles": triangles,
                    "dimensions_m": [b-a for a,b in zip(minimum, maximum)]})
assert all(section == sections["Granite_Straight"] for section in sections.values()), "compatible wall sockets"
for role in ("Stone", "StoneTrim", "StoneShade"):
    colors = [tuple(bpy.data.materials["Fantasy_"+label+role].diffuse_color) for label in manifest["wall_colors"]]
    assert len(set(colors)) == len(manifest["wall_colors"]), (role, "distinct wall colors")
with open(os.path.join(ROOT, "docs", "Art", "KingdomBuildings", "validation.json")) as handle:
    original = json.load(handle)
old_castle = next(m for m in original["models"] if m["model"] == "Kingdom_Castle")
new_castle = next(m for m in records if m["name"] == "Royal_Castle")
assert all(a>b for a,b in zip(new_castle["dimensions_m"], old_castle["dimensions_m"])), "castle enlarged in all axes"
assert not [image.name for image in bpy.data.images if image.source == "FILE" and not image.packed_file], "no missing external texture dependency"
report = {"environment": "Blender "+bpy.app.version_string, "blend_reopen": "PASS", "models": records,
          "wall_connections": "PASS", "wall_color_variants": list(manifest["wall_colors"]),
          "castle_larger_than_original": "PASS", "external_textures_required": False}
with open(os.path.join(REVIEW, "validation.json"), "w") as handle:
    json.dump(report, handle, indent=2)
print(json.dumps(report, indent=2))
