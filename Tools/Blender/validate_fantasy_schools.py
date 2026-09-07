import bpy
import bmesh
import json
import math
import os


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "ArtSource", "Blender", "FantasySchools.blend")
REVIEW = os.path.join(ROOT, "docs", "Art", "FantasySchools")
bpy.ops.wm.open_mainfile(filepath=SOURCE)
with open(os.path.join(REVIEW, "model_manifest.json")) as handle:
    manifest = json.load(handle)

expected = {"WarriorAcademy", "ArcaneAcademy", "SpiritAcademy"}
assert {model["name"] for model in manifest["models"]} == expected
required_parts = {
    "WarriorAcademy": {
        "Warrior Academy shield crest", "Warrior Academy crossed sword blade",
        "Warrior Academy training post", "Warrior Academy weapon rack",
        "Warrior Academy high banner tower", "Warrior Academy instructors balcony",
    },
    "ArcaneAcademy": {
        "Arcane Academy grand tower shaft", "Arcane Academy rune circle",
        "Arcane Academy five point star sigil", "Arcane Academy orrery core",
        "Arcane Academy open spellbook left page", "Arcane Academy courtyard crystal",
        "Arcane Academy observatory balcony", "Arcane Academy suspended bridge",
    },
    "SpiritAcademy": {
        "Spirit Academy west elder tower shaft", "Spirit Academy east elder tower shaft",
        "Spirit Academy ceremonial gatehouse", "Spirit Academy guardian spirit mask",
        "Spirit Academy sacred tree trunk", "Spirit Academy exposed sacred root",
        "Spirit Academy green standard",
    },
}
previous_heights = {
    "WarriorAcademy": 12.14000129699707,
    "ArcaneAcademy": 22.014984130859375,
    "SpiritAcademy": 11.74000072479248,
}
records = []
footprints = []
for entry in manifest["models"]:
    collection = bpy.data.collections[entry["name"]]
    root = bpy.data.objects[entry["name"]]
    meshes = [obj for obj in collection.objects if obj.type == "MESH"]
    names = {obj.name for obj in meshes}
    assert required_parts[entry["name"]] <= names, (entry["name"], "readable school identity")
    if entry["name"] == "SpiritAcademy":
        removed_front_cluster = {"Spirit Academy bound spirit focus", "Spirit Academy summoning circle",
                                 "Spirit Academy teaching wisp 1 core"}
        assert names.isdisjoint(removed_front_cluster), (entry["name"], "removed front courtyard cluster")
    assert root.location.length == 0, (entry["name"], "asset origin")
    points = []
    triangles = 0
    for obj in meshes:
        assert obj.parent == root, (obj.name, "school hierarchy")
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
    assert dimensions[2] > previous_heights[entry["name"]], (entry["name"], "increased height")
    footprints.append(dimensions[:2])
    records.append({
        "name": entry["name"],
        "facility_type": entry["facility_type"],
        "visual_identity": entry["visual_identity"],
        "status": "PASS",
        "mesh_parts": len(meshes),
        "triangles": triangles,
        "dimensions_m": dimensions,
    })

assert all(math.isclose(value, footprints[0][axis], abs_tol=1e-5)
           for footprint in footprints for axis, value in enumerate(footprint)), "aligned facility footprints"
materials = {slot.material for name in expected for obj in bpy.data.collections[name].objects
             if obj.type == "MESH" for slot in obj.material_slots}
for material in materials:
    shader = material.node_tree.nodes.get("Principled BSDF") if material.use_nodes else None
    strength = shader.inputs.get("Emission Strength") if shader else None
    assert strength is None or strength.default_value == 0.0, (material.name, "non-emissive material")
assert not [image for image in bpy.data.images if image.source == "FILE" and not image.packed_file], "self-contained materials"
report = {
    "environment": "Blender " + bpy.app.version_string,
    "blend_reopen": "PASS",
    "school_identity_parts": "PASS",
    "aligned_facility_footprints": "PASS",
    "increased_building_heights": "PASS",
    "non_emissive_materials": "PASS",
    "spirit_front_courtyard_cluster_removed": "PASS",
    "models": records,
    "external_textures_required": False,
}
with open(os.path.join(REVIEW, "validation.json"), "w") as handle:
    json.dump(report, handle, indent=2, ensure_ascii=False)
print(json.dumps(report, indent=2, ensure_ascii=False))
