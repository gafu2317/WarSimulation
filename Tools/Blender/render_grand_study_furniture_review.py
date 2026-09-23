"""Render every Grand Study furniture asset from three fixed review angles."""
import math
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/Blender/GrandStudy.blend"
OUTPUT = ROOT / "docs/Art/GrandStudy/FurnitureReview"
ANGLES = {
    "front": Vector((0, -1, 0.25)),
    "side": Vector((1, 0, 0.2)),
    "three_quarter": Vector((1, -1, 0.32)),
}


def collection_bounds(collection):
    points = [
        obj.matrix_world @ Vector(corner)
        for obj in collection.all_objects
        if obj.type in {"MESH", "CURVE", "FONT"}
        for corner in obj.bound_box
    ]
    minimum = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    return minimum, maximum


def point_camera(camera, target):
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
OUTPUT.mkdir(parents=True, exist_ok=True)

scene = bpy.data.scenes.new("__FurnitureReview")
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 480
scene.render.resolution_y = 480
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = "WORLD"

world = bpy.data.worlds.new("__FurnitureReviewWorld")
world.color = (0.055, 0.055, 0.055)
scene.world = world

camera_data = bpy.data.cameras.new("__FurnitureReviewCamera")
camera = bpy.data.objects.new("__FurnitureReviewCamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera_data.lens = 52

furniture = sorted((
    collection
    for collection in bpy.data.collections
    if collection.get("category") == "Furniture"
), key=lambda collection: collection.name)

for collection in furniture:
    instance = bpy.data.objects.new(collection.name, None)
    instance.instance_type = "COLLECTION"
    instance.instance_collection = collection
    scene.collection.objects.link(instance)

    minimum, maximum = collection_bounds(collection)
    target = (minimum + maximum) / 2
    size = maximum - minimum
    distance = max(size.x, size.y, size.z) * 2.35 + 0.35
    target.z = minimum.z + size.z * 0.48

    for angle_name, direction in ANGLES.items():
        camera.location = target + direction.normalized() * distance
        point_camera(camera, target)
        camera_data.clip_start = max(0.01, distance / 100)
        camera_data.clip_end = distance * 5
        scene.render.filepath = str(OUTPUT / f"{collection.name}_{angle_name}.png")
        bpy.context.window.scene = scene
        bpy.ops.render.render(write_still=True)

    bpy.data.objects.remove(instance, do_unlink=True)

print(f"Rendered {len(furniture)} furniture assets from {len(ANGLES)} angles to {OUTPUT}")
