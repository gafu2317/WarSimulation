"""Render the saved model on a display-only ground plane."""

import bpy
from mathutils import Vector

from materials import surface


VIEWS = {
    "three_quarter":(.53,-1,.65),
    "front":(0,-1,.24),
    "rear":(-.57,1,.73),
}


def sun(scene,name,direction,energy,color):
    light = bpy.data.lights.new(name,"SUN")
    light.energy,light.color,light.angle = energy,color,.18
    obj = bpy.data.objects.new(name,light)
    scene.collection.objects.link(obj)
    obj.rotation_euler = (-Vector(direction)).to_track_quat("-Z","Y").to_euler()


def setup(scene):
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = (.58,.62,.67,1)
    background.inputs["Strength"].default_value = .55
    bpy.ops.mesh.primitive_plane_add(size=2200,location=(0,0,-.012))
    bpy.context.object.name = "Preview Surface"
    bpy.context.object.data.materials.append(surface("Preview Ground",color="a8a8a6",roughness=.96))
    sun(scene,"Key",(-.65,-.86,1.26),2.5,(1,.92,.80))
    sun(scene,"Rim",(.5,.83,1),.69,(.81,.90,1))
    sun(scene,"Fill",(1,-.42,.5),.3,(.87,.93,1))
    camera = bpy.data.objects.new("Preview Camera",bpy.data.cameras.new("Preview Camera"))
    scene.collection.objects.link(camera)
    camera.data.type = "ORTHO"
    camera.data.clip_end = 320
    scene.camera = camera
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 22
    scene.cycles.adaptive_threshold = .055
    scene.cycles.use_denoising = True
    scene.view_settings.view_transform = "AgX"
    scene.render.resolution_x = 2532
    scene.render.resolution_y = 1170
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    return camera


def frame(camera,coords,direction):
    rotation = (-Vector(direction)).to_track_quat("-Z","Y")
    camera.rotation_euler = rotation.to_euler()
    inverse = rotation.inverted()
    projected = [inverse @ v for v in coords]
    low = [min(v[i] for v in projected) for i in range(3)]
    high = [max(v[i] for v in projected) for i in range(3)]
    focal_point = (Vector(low)+Vector(high))*.5
    camera.location = rotation @ (focal_point+Vector((0,0,105)))
    camera.data.ortho_scale = max((high[0]-low[0])/.82,(high[1]-low[1])*2532/1170/.83)


def render(scene,objects,views,out,name,directions):
    coords = [obj.matrix_world @ v.co for obj in objects if obj.type=="MESH" for v in obj.data.vertices]
    camera = setup(scene)
    for view in views:
        frame(camera,coords,directions.get(view,VIEWS[view]))
        scene.render.filepath=str(out/(name+"_"+view+".png"))
        bpy.ops.render.render(write_still=True)
