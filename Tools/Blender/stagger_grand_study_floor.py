"""Replace the aligned floor grid with staggered oak planks across the study."""
import random
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[2]
BLEND = ROOT / "ArtSource/Blender/GrandStudy.blend"
X0, X1 = -3.0, 3.0
Y0, Y1 = -4.0, 4.0
GAP = 0.007
TINTS = ("Oak plank pale", "Oak plank light", "Oak plank honey", "Oak plank amber")
COLORS = {
    "Oak plank pale": (0.72, 0.64, 0.50, 1),
    "Oak plank light": (0.84, 0.76, 0.62, 1),
    "Oak plank honey": (0.62, 0.50, 0.34, 1),
    "Oak plank amber": (0.48, 0.36, 0.22, 1),
    "Oak seam": (0.16, 0.11, 0.07, 1),
}


def material(name):
    existing = bpy.data.materials.get(name)
    mat = existing or bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = COLORS[name]
    principled = next(node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    principled.inputs["Base Color"].default_value = COLORS[name]
    principled.inputs["Roughness"].default_value = 0.72
    principled.inputs["Metallic"].default_value = 0
    return mat


def add_box(collection, name, x, y, w, h, z0, z1, mat):
    x += GAP * 0.5
    y += GAP * 0.5
    w -= GAP
    h -= GAP
    vertices = [
        (x, y, z0), (x + w, y, z0), (x + w, y + h, z0), (x, y + h, z0),
        (x, y, z1), (x + w, y, z1), (x + w, y + h, z1), (x, y + h, z1),
    ]
    faces = [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    uv_layer = mesh.uv_layers.new(name="UVMap")
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            vertex = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv_layer.data[loop_index].uv = ((vertex.x - x) / w, (vertex.y - y) / h)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("Edge", "BEVEL")
    bevel.width = 0.0015
    bevel.segments = 1
    return obj


def plank_rows():
    rng = random.Random(23)
    rows = []
    x = X0
    row_index = 0
    while x < X1 - 0.04:
        width = min(rng.uniform(0.155, 0.215), X1 - x)
        if X1 - (x + width) < 0.09:
            width = X1 - x
        lengths = []
        remaining = Y1 - Y0
        if row_index % 2:
            lengths.append(rng.uniform(0.32, 0.68))
        while sum(lengths) < remaining - 0.05:
            lengths.append(rng.uniform(0.72, 1.65))
        overflow = sum(lengths) - remaining
        lengths[-1] -= overflow
        if lengths[-1] < 0.22 and len(lengths) > 1:
            lengths[-2] += lengths[-1]
            lengths.pop()
        rows.append((x, width, lengths))
        x += width
        row_index += 1
    return rows


def rebuild_floor():
    bpy.ops.wm.open_mainfile(filepath=str(BLEND))
    collection = bpy.data.collections["Floor_2m"]
    for obj in list(collection.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    materials = {name: material(name) for name in COLORS}
    add_box(collection, "Floor seam", X0, Y0, X1 - X0, Y1 - Y0, -0.095, -0.07, materials["Oak seam"])
    rng = random.Random(29)
    previous = None
    for x, width, lengths in plank_rows():
        y = Y0
        for length in lengths:
            choices = [name for name in TINTS if name != previous]
            tint = rng.choice(choices)
            previous = tint
            add_box(collection, "Floorboard", x, y, width, length, -0.09, 0, materials[tint])
            y += length
    room = bpy.data.collections["Room assembly"]
    for obj in list(room.objects):
        if obj.instance_collection and obj.instance_collection.name == "Floor_2m":
            bpy.data.objects.remove(obj, do_unlink=True)
    instance = bpy.data.objects.new("Floor", None)
    instance.instance_type = "COLLECTION"
    instance.instance_collection = collection
    room.objects.link(instance)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    print(f"STAGGERED FLOOR boards={len(collection.objects) - 1}", flush=True)


if __name__ == "__main__":
    rebuild_floor()
