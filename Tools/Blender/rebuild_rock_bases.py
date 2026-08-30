import importlib.util
import json
import os

import bmesh
import bpy
from mathutils import Matrix, Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUTPUT = os.path.join(ROOT, "ArtSource", "Blender", "GroundedRockReview")
SOURCE = os.path.join(ROOT, "ArtSource", "Blender", "NaturalRockVariants.blend")


def original_bottom_planes():
    spec = importlib.util.spec_from_file_location("rock_recipe", os.path.join(ROOT, "Tools", "Blender", "generate_natural_rock_variants.py"))
    recipe = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(recipe)
    planes = {}

    def record(name, collection, material, scale, seed, **kwargs):
        planes[(collection, name)] = -scale[2] * kwargs.get("flatten_bottom", 0.58)

    recipe.add_rock = record
    recipe.new_variant = lambda root, index, label: index
    recipe.center_on_ground = lambda collection: None
    materials = {name: None for name in (
        "granite", "granite_dark", "river_stone", "basalt", "basalt_light",
        "slate", "slate_dark", "warm_stone", "warm_stone_dark")}
    recipe.create_variants(None, materials)
    return planes


def rebuild_base(obj, local_plane):
    matrix = obj.matrix_world.copy()
    underside = [matrix @ vertex.co for vertex in obj.data.vertices if vertex.co.z <= local_plane]
    if not underside:
        raise RuntimeError("Original underside was not found: " + obj.name)
    seam = max(point.z for point in underside)
    if seam <= 0:
        raise RuntimeError("Base seam must be above the ground: " + obj.name)

    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    bmesh.ops.transform(mesh, matrix=matrix, verts=list(mesh.verts))
    # 最下点だけの移動では主岩が付属岩の上に残るため、回転後の下部を接地面まで作り直す。
    bmesh.ops.bisect_plane(
        mesh, geom=list(mesh.verts) + list(mesh.edges) + list(mesh.faces),
        plane_co=(0, 0, seam), plane_no=(0, 0, 1), clear_inner=True)
    bmesh.ops.delete(mesh, geom=[vertex for vertex in mesh.verts if not vertex.link_faces], context="VERTS")
    bmesh.ops.delete(mesh, geom=[edge for edge in mesh.edges if edge.is_wire], context="EDGES")
    rim = [edge for edge in mesh.edges if edge.is_boundary]
    if not rim:
        raise RuntimeError("Open base contour was not found: " + obj.name)
    extrusion = bmesh.ops.extrude_edge_only(mesh, edges=rim)["geom"]
    for element in extrusion:
        if isinstance(element, bmesh.types.BMVert):
            element.co.z = 0
        elif isinstance(element, bmesh.types.BMFace):
            element.smooth = True
    caps = bmesh.ops.holes_fill(mesh, edges=[edge for edge in mesh.edges if edge.is_boundary], sides=0)["faces"]
    for face in caps:
        face.smooth = False
    bmesh.ops.triangulate(mesh, faces=caps)
    bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
    bottom_faces = [face for face in mesh.faces if all(vertex.co.z == 0 for vertex in face.verts)]
    area = sum(face.calc_area() for face in bottom_faces)
    nonmanifold = sum(not edge.is_manifold for edge in mesh.edges)
    if area <= 0 or nonmanifold or min(vertex.co.z for vertex in mesh.verts) != 0:
        raise RuntimeError(f"Invalid closed contact surface: {obj.name}; area={area}; nonmanifold={nonmanifold}; minZ={min(vertex.co.z for vertex in mesh.verts)}; seam={seam}")
    mesh.to_mesh(obj.data)
    obj.matrix_world = Matrix.Identity(4)
    obj.data.update()
    result = {"part": obj.name, "base_area": area, "seam_height": seam, "nonmanifold_edges": nonmanifold}
    mesh.free()
    return result


def material(name, color):
    value = bpy.data.materials.new(name)
    value.diffuse_color = (*color, 1)
    return value


def unify_parts(collection):
    objects = [obj for obj in collection.all_objects if obj.type == "MESH"]
    main = next((obj for obj in objects if obj.name.startswith("Rock_Main")), objects[0])
    bpy.ops.object.select_all(action="DESELECT")
    main.select_set(True)
    bpy.context.view_layer.objects.active = main
    for obj in objects:
        if obj == main:
            continue
        # 底面が重なる別Meshを残すと、同一平面上の二重面が黒くちらつく。
        union = main.modifiers.new("ConnectedRockBase", "BOOLEAN")
        union.operation = "UNION"
        union.solver = "EXACT"
        union.object = obj
        bpy.ops.object.modifier_apply(modifier=union.name)
        bpy.data.objects.remove(obj, do_unlink=True)
    mesh = bmesh.new()
    mesh.from_mesh(main.data)
    if any(not edge.is_manifold for edge in mesh.edges):
        raise RuntimeError("Union left an open surface: " + collection.name)
    mesh.free()


def render_stage(name, entries, view):
    scene = bpy.data.scenes.new(name)
    stage = bpy.data.collections.new(name + "_Stage")
    scene.collection.children.link(stage)
    for objects, offset, label in entries:
        for source in objects:
            copy = source.copy()
            copy.data = source.data
            stage.objects.link(copy)
            copy.hide_render = False
            copy.matrix_world = Matrix.Translation(Vector(offset)) @ source.matrix_world
        if view != "bottom":
            font = bpy.data.curves.new(label, "FONT")
            font.body = label
            font.align_x = "CENTER"
            font.size = 0.26
            text = bpy.data.objects.new(label, font)
            stage.objects.link(text)
            text.location = (offset[0], offset[1] - 1.65, offset[2] + 0.01)
            font.materials.append(material(label + "_Ink", (0.06, 0.08, 0.09)))
    if view != "bottom":
        floor_mesh = bpy.data.meshes.new("ReviewFloor")
        floor_mesh.from_pydata([(-30, -25, 0), (30, -25, 0), (30, 25, 0), (-30, 25, 0)], [], [(0, 1, 2, 3)])
        floor = bpy.data.objects.new("ReviewFloor", floor_mesh)
        stage.objects.link(floor)
        floor.data.materials.append(material("ReviewFloor", (0.32, 0.38, 0.4)))
    scene.world = bpy.data.worlds.new(name + "_World")
    scene.world.use_nodes = True
    background = next(node for node in scene.world.node_tree.nodes if node.type == "BACKGROUND")
    background.inputs[0].default_value = (0.14, 0.18, 0.22, 1)
    background.inputs[1].default_value = 0.5
    for position, power, size in [((-5, -8, 10), 1700, 7), ((7, -1, 7), 1100, 6), ((0, 5, 10), 1800, 5)]:
        light = bpy.data.lights.new("ReviewLight", "AREA")
        light.energy = power
        light.shape = "DISK"
        light.size = size
        obj = bpy.data.objects.new("ReviewLight", light)
        stage.objects.link(obj)
        obj.location = position if view != "bottom" else (position[0], position[1], -position[2])
        obj.rotation_euler = (-obj.location).to_track_quat("-Z", "Y").to_euler()
    camera = bpy.data.objects.new("ReviewCamera", bpy.data.cameras.new("ReviewCamera"))
    stage.objects.link(camera)
    scene.camera = camera
    camera.data.type = "ORTHO"
    if view == "side":
        camera.location = (0, -16, 1.8)
        target = Vector((0, 0, 0.9))
        camera.data.ortho_scale = 8.0
    elif view == "bottom":
        camera.location = (0, -0.01, -14)
        target = Vector((0, 0, 0))
        camera.data.ortho_scale = 8.0
    else:
        camera.location = (0, -17, 14)
        target = Vector((0, 0, 0.65))
        camera.data.ortho_scale = 20
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = os.path.join(OUTPUT, name + ".png")
    bpy.ops.render.render(write_still=True, scene=scene.name)
    return scene


def main():
    os.makedirs(OUTPUT, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=SOURCE)
    collections = sorted((c for c in bpy.data.collections if c.name.startswith("NaturalRock_") and len(c.name.split("_")) >= 3), key=lambda c: c.name)
    if len(collections) != 10:
        raise RuntimeError("Expected ten original variants")
    planes = original_bottom_planes()
    before = []
    for obj in collections[5].all_objects:
        if obj.type != "MESH":
            continue
        copy = obj.copy()
        copy.data = obj.data.copy()
        before.append(copy)
    report = []
    # まず問題の目立つ06を修正し、その接地面の成立を確認してから他の種類を処理する。
    order = [collections[5]] + [c for c in collections if c != collections[5]]
    for collection in order:
        index = int(collection.name.split("_")[1])
        parts = []
        for obj in collection.all_objects:
            if obj.type == "MESH":
                parts.append(rebuild_base(obj, planes[(index, obj.name.split(".")[0])]))
        unify_parts(collection)
        report.append({"variant": collection.name, "parts": parts})
    objects = [[obj for obj in c.all_objects if obj.type == "MESH"] for c in collections]
    comparison = [(before, (-2.1, 0, 0), "BEFORE"), (objects[5], (2.1, 0, 0), "REBUILT")]
    render_stage("06_side_comparison", comparison, "side")
    render_stage("06_bottom_comparison", comparison, "bottom")
    entries = [(objects[i], (-7.6 + (i % 5) * 3.8, 2.4 - (i // 5) * 4.8, 0), f"{i+1:02d}") for i in range(10)]
    review = render_stage("all_10_review", entries, "overview")
    for collection, meshes in zip(collections, objects):
        bpy.ops.object.select_all(action="DESELECT")
        for obj in meshes:
            obj.hide_set(False)
            obj.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUTPUT, collection.name + ".fbx"), use_selection=True,
            object_types={"MESH"}, axis_forward="-Z", axis_up="Y", add_leaf_bones=False, bake_anim=False)
    with open(os.path.join(OUTPUT, "base_measurements.json"), "w") as stream:
        json.dump(report, stream, indent=2)
    bpy.context.window.scene = review
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUTPUT, "GroundedRockVariants.blend"))
    print("ROCK_BASE_REBUILD_COMPLETE", OUTPUT)


if __name__ == "__main__":
    main()
