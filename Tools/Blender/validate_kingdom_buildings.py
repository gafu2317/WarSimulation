import bpy
import bmesh
import json
import math
import os
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REVIEW = os.path.join(ROOT, "docs/Art/KingdomBuildings")
with open(os.path.join(REVIEW, "model_manifest.json")) as handle:
    manifest = json.load(handle)
results = []
profiles = {}
for entry in manifest["models"]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path = os.path.join(ROOT, "Assets/Models/Kingdom/Buildings", entry["name"] + ".fbx")
    bpy.ops.import_scene.fbx(filepath=path)
    objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    assert len(objects) == 1, (entry["name"], "single mesh")
    obj = objects[0]
    data = obj.data
    data.calc_loop_triangles()
    assert len(data.loop_triangles) == entry["triangles"], (entry["name"], "triangles preserved")
    points = [obj.matrix_world @ v.co for v in data.vertices]
    minimum = [min(v[i] for v in points) for i in range(3)]
    maximum = [max(v[i] for v in points) for i in range(3)]
    error = max(abs(actual - expected) for actuals, expecteds in ((minimum, entry["minimum"]), (maximum, entry["maximum"]))
                for actual, expected in zip(actuals, expecteds))
    float32_resolution = max(abs(v) for v in minimum + maximum) * 2 ** -23
    assert error <= float32_resolution, (entry["name"], "bounds preserved", error)
    assert abs(minimum[2]) <= float32_resolution, (entry["name"], "ground contact")
    assert all(math.isfinite(c) for p in points for c in p), "finite vertices"
    assert all(p.area > 0 for p in data.polygons), (entry["name"], "nonzero faces")
    assert set(m.name for m in data.materials) == set(entry["materials"]), "material slots preserved"
    bm = bmesh.new()
    bm.from_mesh(data)
    open_edges = sum(not edge.is_manifold for edge in bm.edges)
    assert open_edges == 0, (entry["name"], "closed component surfaces", open_edges)
    bvh = BVHTree.FromPolygons(points, [p.vertices[:] for p in data.polygons])
    bm.free()
    if entry["name"].endswith("Gate"):
        assert bvh.ray_cast(Vector((0, -3, 1)), Vector((0, 1, 0)), 6)[0] is None, "gate passage open"
        assert bvh.ray_cast(Vector((0, -3, 2.6)), Vector((0, 1, 0)), 6)[0] is not None, "gate lintel exists"
    if "Wall" in entry["name"]:
        section = {(round(v.y, 5), round(v.z, 5)) for v in points if round(v.x, 5) == 3}
        profiles[entry["name"]] = section
        if entry["name"].endswith("Corner"):
            other = {(round(v.x, 5), round(v.z, 5)) for v in points if round(v.y, 5) == 3}
            assert other == section, "corner ends share the same section"
    results.append({"model": entry["name"], "fbx_reimport": "PASS", "mesh_count": len(objects),
                    "triangles": len(data.loop_triangles), "materials": len(data.materials),
                    "bounds_error_m": error, "open_edges": open_edges,
                    "ground_minimum_m": minimum[2], "dimensions_m": [b-a for a,b in zip(minimum, maximum)]})
assert all(section == profiles["Kingdom_Wall_Straight"] for section in profiles.values()), "matching wall connections"
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, "ArtSource/Blender/KingdomBuildings.blend"))
assert {"Buildings", "Wall Modules", "Wall Connection Example"}.issubset(bpy.data.scenes.keys())
output = {"environment": "Blender " + bpy.app.version_string, "models": results,
          "wall_endpoint_sections": "PASS", "gate_opening": "PASS", "blend_reopen": "PASS",
          "unity_editor_render": "NOT_RUN: MCP is connected to another project"}
with open(os.path.join(REVIEW, "validation.json"), "w") as handle:
    json.dump(output, handle, indent=2)
print(json.dumps(output, indent=2))
