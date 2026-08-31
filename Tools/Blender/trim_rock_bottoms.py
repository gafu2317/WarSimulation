import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rebuild_rock_bases import ROOT, SOURCE, original_bottom_planes, unify_parts


OUTPUT = os.path.join(ROOT, "ArtSource", "Blender", "TrimmedRockReview")


def trim_part(obj, height):
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    bmesh.ops.transform(mesh, matrix=obj.matrix_world, verts=list(mesh.verts))
    bmesh.ops.bisect_plane(mesh, geom=list(mesh.verts) + list(mesh.edges) + list(mesh.faces),
                           plane_co=(0, 0, height), plane_no=(0, 0, 1), clear_inner=True)
    bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if not v.link_faces], context="VERTS")
    bmesh.ops.delete(mesh, geom=[e for e in mesh.edges if e.is_wire], context="EDGES")
    rim = [e for e in mesh.edges if e.is_boundary]
    if not rim:
        raise RuntimeError("Cut does not intersect " + obj.name)
    # 延長壁は作らず、元の形を切った断面だけを塞ぐ。
    for edge in rim:
        for vertex in edge.verts:
            vertex.co.z = height
    caps = bmesh.ops.holes_fill(mesh, edges=rim, sides=0)["faces"]
    for face in caps:
        face.smooth = False
    bmesh.ops.triangulate(mesh, faces=caps)
    for vertex in mesh.verts:
        vertex.co.z -= height
    bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
    if any(not e.is_manifold for e in mesh.edges):
        raise RuntimeError("Open cut surface: " + obj.name)
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.matrix_world = Matrix.Identity(4)
    obj.data.update()


def plain_material(name, color):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Roughness"].default_value = 1
    return mat


def side_sheet(collections):
    scene = bpy.data.scenes.new("Ten rocks - exact side view")
    ink = plain_material("Review ink", (0.65, 0.73, 0.77))

    def text(body, location, size):
        data = bpy.data.curves.new(body, "FONT")
        data.body = body
        data.align_x = "CENTER"
        data.size = size
        data.materials.append(ink)
        obj = bpy.data.objects.new(body, data)
        scene.collection.objects.link(obj)
        obj.location = location
        obj.rotation_euler.x = math.pi / 2

    for i, collection in enumerate(collections):
        x = (i % 5 - 2) * 4.2
        z = 4.5 if i < 5 else 0.3
        objects = [o for o in collection.all_objects if o.type == "MESH"]
        points = [o.matrix_world @ v.co for o in objects for v in o.data.vertices]
        cx = (min(p.x for p in points) + max(p.x for p in points)) / 2
        for original in objects:
            obj = original.copy()
            obj.data = original.data
            scene.collection.objects.link(obj)
            obj.matrix_world = Matrix.Translation((x - cx, 0, z)) @ original.matrix_world
        line = bpy.data.curves.new("Contact line", "CURVE")
        line.dimensions = "3D"
        line.bevel_depth = 0.008
        spline = line.splines.new("POLY")
        spline.points.add(1)
        spline.points[0].co = (x - 1.9, 2, z, 1)
        spline.points[1].co = (x + 1.9, 2, z, 1)
        line.materials.append(ink)
        scene.collection.objects.link(bpy.data.objects.new("Ground datum", line))
        text(f"{i + 1:02d}", (x, -2, z - 0.5), 0.32)
    text("TRIMMED ROCKS  /  SIDE VIEW", (0, -2, 8.65), 0.36)
    scene.world = bpy.data.worlds.new("Side sheet world")
    scene.world.use_nodes = True
    bg = next(n for n in scene.world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.055, 0.075, 0.09, 1)
    bg.inputs[1].default_value = 0.6
    for position, energy, size in [((-8, -10, 13), 2500, 8), ((9, -6, 9), 1800, 7), ((0, 4, 12), 2000, 6)]:
        data = bpy.data.lights.new("Sheet softbox", "AREA")
        data.energy = energy
        data.size = size
        obj = bpy.data.objects.new(data.name, data)
        scene.collection.objects.link(obj)
        obj.location = position
        obj.rotation_euler = (Vector((0, 0, 4)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    camera = bpy.data.objects.new("SideCamera", bpy.data.cameras.new("SideCamera"))
    scene.collection.objects.link(camera)
    camera.location = (0, -35, 4.2)
    camera.rotation_euler = Vector((0, 1, 0)).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 22
    scene.camera = camera
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 2200
    scene.render.resolution_y = 1100
    scene.render.resolution_percentage = 100
    scene.render.filepath = os.path.join(OUTPUT, "TenRocks_Side.png")
    bpy.ops.render.render(write_still=True, scene=scene.name)
    return scene


def main():
    os.makedirs(OUTPUT, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=SOURCE)
    collections = sorted((c for c in bpy.data.collections if c.name.startswith("NaturalRock_")
                          and len(c.name.split("_")) >= 3), key=lambda c: c.name)
    planes = original_bottom_planes()
    report = []
    for collection in collections:
        index = int(collection.name.split("_")[1])
        objects = [o for o in collection.all_objects if o.type == "MESH"]
        points = [o.matrix_world @ v.co for o in objects for v in o.data.vertices]
        original_height = max(p.z for p in points) - min(p.z for p in points)
        underside = [o.matrix_world @ v.co for o in objects for v in o.data.vertices
                     if v.co.z <= planes[(index, o.name.split(".")[0])]]
        # 共通の切断高にすることで、付属岩だけ別に持ち上げたり下げたりしない。
        cut = max(p.z for p in underside)
        for obj in objects:
            trim_part(obj, cut)
        unify_parts(collection)
        bpy.ops.object.select_all(action="DESELECT")
        meshes = [o for o in collection.all_objects if o.type == "MESH"]
        for obj in meshes:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUTPUT, collection.name + ".fbx"), use_selection=True,
                                object_types={"MESH"}, axis_forward="-Z", axis_up="Y", add_leaf_bones=False, bake_anim=False)
        report.append({"variant": collection.name, "cut_height": cut, "original_height": original_height,
                       "removed_height_percent": 100 * cut / original_height})
    scene = side_sheet(collections)
    bpy.context.window.scene = scene
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUTPUT, "TrimmedRockVariants.blend"))
    with open(os.path.join(OUTPUT, "cut_measurements.json"), "w") as stream:
        json.dump(report, stream, indent=2)
    print("TRIM_COMPLETE", json.dumps(report))


if __name__ == "__main__":
    main()
