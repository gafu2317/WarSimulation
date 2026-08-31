import bpy
import importlib.util
import json
import math
import os
import sys
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "ArtSource", "Blender", "KingdomBuildings_RealisticFantasy.blend")
REVIEW = os.path.join(ROOT, "docs", "Art", "KingdomBuildings_RealisticFantasy")
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("building_geometry", os.path.join(os.path.dirname(__file__), "generate_kingdom_buildings.py"))
g = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g)
WALL_COLORS = {
    "Granite": ("777D7C", "A3A6A0", "565C5B"),
    "Sandstone": ("A08D6D", "C9B797", "79694F"),
    "Basalt": ("424951", "737981", "2F353D"),
}


def rgb(hex_color):
    return tuple(g.linear(int(hex_color[i:i + 2], 16) / 255) for i in (0, 2, 4))


def surface(name, color, kind="plain"):
    mat = g.material("Fantasy_" + name, color)
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = 0.87
    if kind == "metal":
        shader.inputs["Metallic"].default_value = 0.7
        shader.inputs["Roughness"].default_value = 0.4
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    geometry = nodes.new("ShaderNodeNewGeometry")
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 6 if kind == "wood" else 22
    noise.inputs["Detail"].default_value = 3
    links.new(geometry.outputs["Position"], noise.inputs["Vector"])
    ramp = nodes.new("ShaderNodeValToRGB")
    base = rgb(color)
    ramp.color_ramp.elements[0].color = tuple(c * 0.72 for c in base) + (1,)
    ramp.color_ramp.elements[1].color = tuple(min(c * 1.12, 1) for c in base) + (1,)
    links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], shader.inputs["Base Color"])
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.28
    bump.inputs["Distance"].default_value = 0.035 if kind == "stone" else 0.012
    links.new(noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    if kind != "stone":
        return mat
    normal = nodes.new("ShaderNodeSeparateXYZ")
    position = nodes.new("ShaderNodeSeparateXYZ")
    links.new(geometry.outputs["Normal"], normal.inputs[0])
    links.new(geometry.outputs["Position"], position.inputs[0])
    absolute = nodes.new("ShaderNodeMath")
    absolute.operation = "ABSOLUTE"
    links.new(normal.outputs["X"], absolute.inputs[0])
    select = nodes.new("ShaderNodeMath")
    select.operation = "GREATER_THAN"
    select.inputs[1].default_value = math.sqrt(0.5)
    links.new(absolute.outputs[0], select.inputs[0])
    horizontal = nodes.new("ShaderNodeMixRGB")
    links.new(select.outputs[0], horizontal.inputs[0])
    links.new(position.outputs["X"], horizontal.inputs[1])
    links.new(position.outputs["Y"], horizontal.inputs[2])
    mapping = nodes.new("ShaderNodeCombineXYZ")
    links.new(horizontal.outputs[0], mapping.inputs["X"])
    links.new(position.outputs["Z"], mapping.inputs["Y"])
    brick = nodes.new("ShaderNodeTexBrick")
    links.new(mapping.outputs[0], brick.inputs["Vector"])
    brick.inputs["Scale"].default_value = 1
    brick.inputs["Brick Width"].default_value = 0.92
    brick.inputs["Row Height"].default_value = 0.42
    brick.inputs["Mortar Size"].default_value = 0.013
    brick.inputs["Mortar Smooth"].default_value = 0.02
    brick.inputs["Color1"].default_value = tuple(c * 0.8 for c in base) + (1,)
    brick.inputs["Color2"].default_value = tuple(c * 1.08 for c in base) + (1,)
    brick.inputs["Mortar"].default_value = tuple(c * 0.53 for c in base) + (1,)
    multiply = nodes.new("ShaderNodeMixRGB")
    multiply.blend_type = "MULTIPLY"
    multiply.inputs[0].default_value = 0.18
    links.new(brick.outputs["Color"], multiply.inputs[1])
    links.new(noise.outputs["Fac"], multiply.inputs[2])
    links.new(multiply.outputs[0], shader.inputs["Base Color"])
    mortar = nodes.new("ShaderNodeBump")
    mortar.invert = True
    mortar.inputs["Strength"].default_value = 0.45
    mortar.inputs["Distance"].default_value = 0.065
    links.new(brick.outputs["Fac"], mortar.inputs["Height"])
    links.new(bump.outputs["Normal"], mortar.inputs["Normal"])
    links.new(mortar.outputs["Normal"], shader.inputs["Normal"])
    return mat


def setup_materials():
    specs = {
        "Stone": ("999588", "stone"), "StoneTrim": ("BCB3A0", "plain"),
        "StoneShade": ("6E7069", "stone"), "Roof": ("343F4A", "plain"),
        "RoofEdge": ("242C34", "metal"), "Timber": ("49372B", "wood"),
        "Door": ("65503A", "wood"), "Iron": ("252A2C", "metal"),
        "Brass": ("A18748", "metal"), "Window": ("1C2B32", "plain"),
        "Glass": ("657875", "plain"), "Banner": ("742E34", "plain"),
        "Plaster": ("B4AA91", "plain"), "Ground": ("747C70", "plain"),
        "Paving": ("858377", "stone"),
    }
    for name, (color, kind) in specs.items():
        g.MATERIALS[name] = surface(name, color, kind)
    for label, palette in WALL_COLORS.items():
        for role, color in zip(("Stone", "StoneTrim", "StoneShade"), palette):
            g.MATERIALS[label + role] = surface(label + role, color, "plain" if role == "StoneTrim" else "stone")


def shifted(recipe, position=(0, 0, 0), angle=0):
    before = set(g.PARTS.objects)
    recipe()
    c, s = math.cos(angle), math.sin(angle)
    for obj in set(g.PARTS.objects) - before:
        x, y, z = obj.location
        obj.location = (c * x - s * y + position[0], s * x + c * y + position[1], z + position[2])
        obj.rotation_euler.z += angle


def lancet_profile(x, bottom, width, height):
    r = width / 2
    spring = bottom + height - width * 0.8
    result = [(x-r, bottom), (x+r, bottom)]
    for i in range(9):
        t = i / 8
        result.append((x + r * (1-t*t), spring + (height + bottom - spring) * t))
    for i in range(1, 9):
        t = i / 8
        result.append((x-r*(2*t-t*t), bottom + height - (height + bottom - spring)*t))
    return result


def lancet(x, y, bottom, width, height, trim="StoneTrim"):
    border = width * 0.16
    g.extrude("Carved lancet surround", lancet_profile(x, bottom-border, width+border*2, height+border*2),
              y-0.035, y+0.12, trim, 0.009)
    g.extrude("Recessed window", lancet_profile(x, bottom, width, height), y-0.049, y-0.025, "Window", 0.003)
    g.box("Stone sill", (x,y-0.04,bottom-border), (width+border*2.4,0.3,0.14), trim, 0.012)
    g.box("Window center mullion", (x,y-0.068,bottom+height*0.39), (0.043,0.06,height*0.77), trim, 0.005)
    g.box("Window transom", (x,y-0.068,bottom+height*0.46), (width,0.055,0.047), trim, 0.004)


def door(x, y, width, height, bottom=0.15):
    g.extrude("Ironbound oak gate", g.arch_profile(x,bottom,width,height), y-0.06,y+0.04,"Door",0.012)
    g.arch_trim("Gateway voussoirs",x,y-0.07,bottom,width,height,0.27,"StoneTrim")
    for i in range(1,8):
        px = x-width/2+width*i/8
        g.box("Oak plank seam",(px,y-0.127,bottom+(height-width/2)/2),(0.018,0.012,height-width/2),"Timber",0)
    for z in (height*0.2,height*0.49,height*0.72):
        g.box("Forged iron strap",(x,y-0.144,bottom+z),(width*0.92,0.055,0.1),"Iron",0.009)
        for sign in (-1,1):
            g.box("Strap rivet",(x+sign*width*0.35,y-0.178,bottom+z),(0.045,0.032,0.045),"Brass",0.005)


def banner(x,y,z,width=0.65,height=2.1):
    g.beam("Banner iron rail",(x-width*0.68,y,z+0.1),(x+width*0.68,y,z+0.1),0.065,"Iron")
    profile=[(x-width/2,z),(x+width/2,z),(x+width/2,z-height+0.25),(x,z-height),(x-width/2,z-height+0.25)]
    g.extrude("Crimson heraldic banner",profile,y-0.012,y+0.018,"Banner",0)
    g.extrude("Heraldic gold diamond",[(x,z-0.4),(x+width*0.2,z-0.72),(x,z-1.04),(x-width*0.2,z-0.72)],
              y-0.033,y-0.02,"Brass",0)


def roof(width,depth,base,height):
    g.hip_roof("Slate hip roof",0,0,width,depth,base,height)
    for fraction in (0.22,0.44,0.66,0.86):
        halfwidth=width/2*(1-fraction)
        halfdepth=depth/2*(1-fraction)+depth*0.22*fraction
        z=base+height*fraction+0.013
        g.beam("Slate horizontal course",(-halfwidth,-halfdepth,z),(halfwidth,-halfdepth,z),0.026,"RoofEdge")
        g.beam("Slate horizontal course",(-halfwidth,halfdepth,z),(halfwidth,halfdepth,z),0.026,"RoofEdge")
        for side in (-1,1):
            g.beam("Slate side course",(side*halfwidth,-halfdepth,z),(side*halfwidth,halfdepth,z),0.026,"RoofEdge")


def battlement_tower(radius,height):
    g.cylinder("Bastion foundation",(0,0,0.25),radius+0.24,0.5,"StoneShade",0.02,32)
    g.cylinder("Bastion stone shaft",(0,0,height/2+0.25),radius,height-0.5,"Stone",0.02,32)
    for z in (0.6,height*0.48,height-0.36):
        g.cylinder("Tower string course",(0,0,z),radius+0.075,0.17,"StoneTrim",0.012,32)
    g.cylinder("Bastion deck",(0,0,height-0.1),radius+0.19,0.26,"StoneTrim",0.015,32)
    outer,inner=radius+0.18,radius-0.42
    vertices=[(r*math.cos(math.tau*i/32),r*math.sin(math.tau*i/32),z)
              for z in (height,height+0.64) for r in (outer,inner) for i in range(32)]
    faces=[]
    for i in range(32):
        n=(i+1)%32
        faces += [(i,n,64+n,64+i),(32+n,32+i,96+i,96+n),(64+i,64+n,96+n,96+i),(n,i,32+i,32+n)]
    g.mesh("Hollow stone parapet",vertices,faces,"Stone",0.006)
    for i in range(12):
        a=math.tau*i/12
        r=radius-0.11
        obj=g.box("Tower merlon",(r*math.cos(a),r*math.sin(a),height+0.97),(0.67,0.62,0.68),"Stone",0.012)
        obj.rotation_euler.z=a+math.pi/2
        obj=g.box("Machicolation corbel",((radius+0.13)*math.cos(a),(radius+0.13)*math.sin(a),height-0.56),
                  (0.24,0.42,0.56),"StoneTrim",0.012)
        obj.rotation_euler.z=a+math.pi/2
    for z in (height*0.32,height*0.68):
        for angle in (0,math.pi/2,math.pi,math.pi*1.5):
            shifted(lambda: lancet(0,-radius-0.014,z,0.23,1.24),angle=angle)


def parapets(length,height,thickness=1.25):
    g.box("Wall coping",(0,0,height),(length,thickness+0.16,0.18),"StoneTrim",0)
    for side in (-1,1):
        y=side*(thickness/2-0.13)
        g.box("Wall parapet",(0,y,height+0.37),(length,0.32,0.56),"Stone",0)
        count=round(length/1.33)
        spacing=length/count
        for i in range(count):
            g.box("Curtain merlon",(-length/2+spacing*(i+0.5),y,height+0.96),(spacing*0.49,0.38,0.62),"Stone",0.012)


def curtain(length=8,height=4.5):
    g.box("Curtain footing",(0,0,0.2),(length,1.52,0.4),"StoneShade",0)
    g.box("Curtain base course",(0,0,0.49),(length,1.36,0.18),"StoneTrim",0)
    g.box("Curtain ashlar",(0,0,(height-0.09+0.58)/2),(length,1.25,height-0.09-0.58),"Stone",0)
    parapets(length,height)
    for x in (-length/4,length/4):
        g.box("Curtain buttress",(x,-0.76,height*0.43),(0.46,0.5,height*0.86),"StoneShade",0.016)
        g.box("Buttress cap",(x,-0.77,height*0.86),(0.57,0.57,0.14),"StoneTrim",0.012)
        lancet(x,-1.021,height*0.52,0.17,0.94)


def open_gateway(length=8,height=4.5,radius=1.34,spring=2.25):
    for side in (-1,1):
        x=side*(length/2+radius)/2
        width=length/2-radius
        g.box("Gateway footing",(x,0,0.2),(width,1.52,0.4),"StoneShade",0)
        g.box("Gateway base course",(x,0,0.49),(width,1.36,0.18),"StoneTrim",0)
        g.box("Gateway pier",(x,0,(height-0.09+0.58)/2),(width,1.25,height-0.09-0.58),"Stone",0)
    profile=[(-radius,height-0.09),(radius,height-0.09)] + [
        (radius*math.cos(math.pi*i/16),spring+radius*math.sin(math.pi*i/16)) for i in range(17)]
    g.extrude("Gateway vaulted lintel",profile,-0.625,0.625,"Stone",0)
    for y in (-0.69,0.69):
        g.arch_trim("Gateway cut stone",0,y,0,radius*2,spring+radius,0.26,"StoneTrim")
    parapets(length,height)
    for side in (-1,1):
        banner(side*(radius+1.12),-0.68,height-0.55,0.55,1.55)
    for x in (-radius*0.7,-radius*0.35,0,radius*0.35,radius*0.7):
        g.box("Raised portcullis",(x,0,height-0.43),(0.06,0.08,0.88),"Iron",0.006)


def wall_corner():
    shifted(lambda: curtain(4),(2,0,0))
    shifted(lambda: curtain(4),(0,2,0),math.pi/2)
    shifted(lambda: battlement_tower(1.56,5.75))


def hall(width,depth,height):
    g.box("Hall stone foundation",(0,0,0.36),(width+0.45,depth+0.45,0.72),"StoneShade",0.018)
    g.box("Hall ashlar walls",(0,0,height/2),(width,depth,height),"Stone",0.014)
    for z in (0.88,height*0.31,height*0.62,height-0.3):
        g.box("Carved belt course",(0,0,z),(width+0.17,depth+0.17,0.18),"StoneTrim",0.012)
    for side in (-1,1):
        for y in (-depth/2,depth/2):
            g.box("Hall corner pier",(side*(width/2-0.15),y,height/2),(0.44,0.36,height),"StoneTrim",0.014)
    for z in (height*0.17,height*0.43,height*0.71):
        for x in (-width*0.29,0,width*0.29):
            lancet(x,-depth/2-0.03,z,0.65,height*0.14)
        shifted(lambda: [lancet(x,-width/2-0.03,z,0.6,height*0.14) for x in (-depth*0.26,depth*0.26)],angle=math.pi/2)
    shifted(lambda: roof(width+0.65,depth+0.65,height+0.1,height*0.23))


def castle():
    g.box("Castle stone terrace",(0,0,0.16),(30,27,0.32),"Paving",0.07)
    for y in (-11.7,11.7):
        if y<0:
            shifted(lambda: open_gateway(9,6,1.85,2.75),(0,y,0.32))
            for x in (-8.75,8.75):
                shifted(lambda: curtain(8.5,6),(x,y,0.32))
        else:
            shifted(lambda: curtain(26,6),(0,y,0.32))
    for x in (-13,13):
        shifted(lambda: curtain(23.4,6),(x,0,0.32),math.pi/2)
        for y in (-11.7,11.7):
            shifted(lambda: battlement_tower(2.15,9.25),(x,y,0.32))
    shifted(lambda: hall(8.5,8,15.6),(0,3.6,0.32))
    for x in (-6.75,6.75):
        shifted(lambda: hall(4.4,7.2,9.2),(x,3.2,0.32))
    for x in (-6.55,6.55):
        def keep_tower():
            g.box("High tower shaft",(0,0,9.5),(3.4,3.6,19),"Stone",0.018)
            for z in (0.7,6.1,12.4,18.65):
                g.box("Tower carved stringcourse",(0,0,z),(3.63,3.83,0.22),"StoneTrim",0.015)
            for z in (4,8.5,13,16.7):
                lancet(0,-1.84,z,0.61,1.75)
                shifted(lambda: lancet(0,-1.74,z,0.61,1.75),angle=math.pi/2)
            for sign in (-1,1):
                g.box("Tower corner quoin",(sign*1.62,-1.75,9.5),(0.25,0.29,19),"StoneTrim",0.01)
            roof(4.02,4.22,19.1,3.9)
            g.cylinder("Bronze tower finial",(0,0,23.24),0.085,0.42,"Brass",0.008,12)
        shifted(keep_tower,(x,6.5,0.32))
    def royal_spire():
        g.cylinder("Crown octagonal tower",(0,0,19),1.18,5.6,"Stone",0.012,8)
        for a in range(4):
            shifted(lambda: lancet(0,-1.1,19.2,0.44,1.8),angle=a*math.pi/2)
        g.cylinder("Crown cornice",(0,0,21.9),1.33,0.26,"StoneTrim",0.015,8)
        bpy.ops.mesh.primitive_cone_add(vertices=8,radius1=1.53,radius2=0.09,depth=3.5,location=(0,0,23.78))
        g.finish(bpy.context.object,"Royal slate spire","Roof",0.008)
        g.cylinder("Royal standard pole",(0,0,26.3),0.042,1.65,"Brass",0.004,12)
        g.extrude("Royal standard",[(0.06,27.08),(1.9,26.81),(1.55,26.23),(0.06,26.37)],-0.018,0.018,"Banner",0)
    shifted(royal_spire,(0,6,0.32))
    door(0,-0.49,2.5,3.4,0.72)
    for i in range(5):
        g.box("Grand entry stair",(0,-1.84+i*0.26,0.32+(i+1)*0.075),(4.0-i*0.2,0.34,(i+1)*0.15),"StoneTrim",0.012)
    for x in (-2.75,2.75):
        banner(x,-0.54,9.7,0.86,3.1)
    for x in (-3.5,3.5):
        def gatehouse_tower():
            g.box("Gatehouse ashlar",(0,0,4.65),(2.75,3.1,9.3),"Stone",0.018)
            for z in (0.6,5.8,9.1):
                g.box("Gatehouse moulding",(0,0,z),(2.97,3.32,0.19),"StoneTrim",0.012)
            lancet(0,-1.59,6.25,0.45,1.75)
            roof(3.26,3.59,9.4,2.5)
        shifted(gatehouse_tower,(x,-11.6,0.32))
    g.box("Gatehouse bridge gallery",(0,-11.6,6.75),(4.3,2.7,1.16),"Stone",0.012)
    for x in (-0.92,0.92):
        lancet(x,-12.98,6.54,0.34,0.69)
    shifted(lambda: roof(4.7,3.07,7.45,1.5),(0,-11.6,0.32))
    for x in (-5.2,5.2):
        g.box("Courtyard paving inset",(x,-4.5,0.336),(2.4,6.0,0.026),"StoneTrim",0.005)


def house():
    g.box("House stone foundation",(0,0,0.35),(5.5,4.8,0.7),"StoneShade",0.018)
    g.box("Lower house stonework",(0,0,1.72),(5.1,4.4,2.74),"Stone",0.018)
    g.box("Jettied upper storey",(0,0,4.34),(5.5,4.8,2.54),"Plaster",0.02)
    g.extrude("Plaster gable",[(-2.75,5.6),(2.75,5.6),(0,8.35)],-2.4,2.4,"Plaster",0.014)
    for z in (3.12,5.58):
        g.box("Oak floor beam",(0,0,z),(5.63,4.94,0.23),"Timber",0.016)
    for side in (-1,1):
        for x in (-2.61,0,2.61):
            g.box("Oak upright",(x,side*2.43,4.35),(0.18,0.16,2.4),"Timber",0.012)
        for x in (-1.72,1.72):
            g.beam("Oak diagonal brace",(x,side*2.45,3.24),(x+math.copysign(0.76,x),side*2.45,4.09),0.14,"Timber")
    for x in (-2.78,2.78):
        for y in (-2.26,0,2.26):
            g.box("Side oak post",(x,y,4.35),(0.16,0.18,2.4),"Timber",0.012)
    for x in (-1.24,1.24):
        lancet(x,-2.49,3.63,0.77,1.37,"Timber")
    shifted(lambda: [lancet(x,-2.84,3.63,0.77,1.37,"Timber") for x in (-1.13,1.13)],angle=math.pi/2)
    door(-1.13,-2.25,1.08,2.37,0.26)
    lancet(1.16,-2.25,1.18,0.91,1.15,"Timber")
    for sign in (-1,1):
        g.box("Ground floor shutter",(1.16+sign*0.66,-2.3,1.68),(0.33,0.11,1.03),"Door",0.012)
    lancet(0,-2.45,6.08,0.65,1.23,"Timber")
    for sign in (-1,1):
        g.beam("Gable timber",(sign*2.76,-2.44,5.57),(0,-2.44,8.36),0.17,"Timber")
    for side in (-1,1):
        for course in range(9):
            lo=course/9
            hi=(course+1)/9
            x0=side*3.07*(1-lo)
            x1=side*3.07*(1-hi)
            z0=5.46+3.05*lo
            z1=5.46+3.05*hi
            g.extrude("Slate roof course",[(x0,z0),(x1,z1),(x1,z1-0.09),(x0,z0-0.09)],-2.73,2.73,"Roof",0.009)
        g.beam("Gable iron flashing",(side*3.1,-2.77,5.45),(0,-2.77,8.53),0.075,"RoofEdge")
        g.box("Roof oak eaves",(side*3.02,0,5.43),(0.14,5.53,0.15),"Timber",0.012)
    g.beam("Slate ridge cap",(0,-2.84,8.53),(0,2.84,8.53),0.16,"RoofEdge")
    g.box("Tall stone chimney",(1.45,1.2,7.15),(0.75,0.83,3.7),"Stone",0.017)
    g.box("Chimney crown",(1.45,1.2,9.0),(0.91,0.99,0.24),"StoneTrim",0.016)
    for x in (1.25,1.65):
        g.cylinder("Chimney pot",(x,1.2,9.28),0.14,0.43,"StoneShade",0.006,12)
    g.box("House doorstep",(-1.13,-2.59,0.15),(1.63,0.67,0.3),"StoneTrim",0.012)


def duplicate_colored_asset(source,label,position,scene):
    source_collection,source_root=source
    target=bpy.data.collections.new(source_collection.name.replace("Source",label))
    scene.collection.children.link(target)
    root=source_root.copy()
    root.name=target.name
    root.location=position
    target.objects.link(root)
    for obj in source_collection.objects:
        if obj.type!="MESH":
            continue
        copy=obj.copy()
        copy.parent=root
        target.objects.link(copy)
        for slot in copy.material_slots:
            if slot.material:
                name=slot.material.name.removeprefix("Fantasy_")
                if name in ("Stone","StoneTrim","StoneShade"):
                    slot.link="OBJECT"
                    slot.material=g.MATERIALS[label+name]
    return target,root


def stage(scene,objects,position,target,filename,size=(2000,1500)):
    bpy.context.window.scene=scene
    scene.unit_settings.system="METRIC"
    g.PARTS=bpy.data.collections.new("Lighting and ground")
    scene.collection.children.link(g.PARTS)
    g.box("Presentation floor",(0,0,-0.14),(300,300,0.26),"Ground",0)
    world=bpy.data.worlds.new(scene.name+" World")
    world.use_nodes=True
    world.node_tree.nodes["Background"].inputs["Color"].default_value=(0.42,0.48,0.58,1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value=0.48
    scene.world=world
    for name,loc,power,width in (("Warm soft key",(-25,-35,50),14000,24),("Cool fill",(30,-5,34),8000,26)):
        lamp=bpy.data.objects.new(name,bpy.data.lights.new(name,"AREA"))
        g.PARTS.objects.link(lamp)
        lamp.location=loc
        lamp.data.energy=power
        lamp.data.shape="DISK"
        lamp.data.size=width
        lamp.rotation_euler=(Vector((0,0,3))-lamp.location).to_track_quat("-Z","Y").to_euler()
    sun=bpy.data.objects.new("Late afternoon sun",bpy.data.lights.new("Late afternoon sun","SUN"))
    g.PARTS.objects.link(sun)
    sun.rotation_euler=(math.radians(26),math.radians(-24),math.radians(-35))
    sun.data.energy=1.35
    sun.data.angle=math.radians(12)
    scene.render.engine="CYCLES"
    scene.cycles.samples=48
    scene.cycles.use_denoising=True
    scene.render.resolution_x,scene.render.resolution_y=size
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    scene.view_settings.view_transform="AgX"
    g.camera_at(scene,position,target,50)
    g.fit_camera(scene,objects)
    scene.render.filepath=os.path.join(REVIEW,filename)


def main():
    os.makedirs(REVIEW,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    setup_materials()
    castle_scene=bpy.context.scene
    castle_scene.name="01 Royal Castle"
    castle_asset=g.make_asset("Royal_Castle",castle)
    house_scene=bpy.data.scenes.new("02 Fantasy House")
    bpy.context.window.scene=house_scene
    house_asset=g.make_asset("Fantasy_House",house)
    wall_scene=bpy.data.scenes.new("03 Wall Color Comparison")
    bpy.context.window.scene=wall_scene
    wall_sources=[g.make_asset("Source_"+name,recipe) for name,recipe in
                  (("Straight",curtain),("Gate",open_gateway),("Corner",wall_corner))]
    wall_assets=[]
    for row,label in enumerate(WALL_COLORS):
        for column,source in enumerate(wall_sources):
            asset=duplicate_colored_asset(source,label,((column-1)*9.5,13-row*13,0),wall_scene)
            wall_assets.append(asset)
    for collection,root in wall_sources:
        for obj in list(collection.objects):
            bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.collections.remove(collection)
    labels=[]
    for row,title in enumerate(("01  WEATHERED GRANITE", "02  WARM SANDSTONE", "03  DARK BASALT")):
        font=bpy.data.curves.new(title,"FONT")
        font.body=title
        font.size=0.7
        text=bpy.data.objects.new(title,font)
        wall_scene.collection.objects.link(text)
        text.location=(-13.5,9.4-row*13,0.03)
        font.materials.append(g.MATERIALS["StoneTrim"])
        bpy.ops.object.select_all(action="DESELECT")
        text.select_set(True)
        bpy.context.view_layer.objects.active=text
        bpy.ops.object.convert(target="MESH")
        labels.append(bpy.context.object)
    stage(castle_scene,list(castle_asset[0].objects),(46,-66,47),(0,0,11),"Castle_Preview.png")
    stage(house_scene,list(house_asset[0].objects),(15,-23,13),(0,0,4),"House_Preview.png",(1600,1400))
    stage(wall_scene,[obj for collection,_ in wall_assets for obj in collection.objects]+labels,
          (12,-48,37),(0,0,2),"WallColors_Preview.png",(2200,1800))
    assets=[castle_asset,house_asset]+wall_assets
    records=[]
    for collection,root in assets:
        points=[obj.matrix_local@Vector(v) for obj in collection.objects if obj.type=="MESH" for v in obj.bound_box]
        records.append({"name":root.name,"parts":sum(o.type=="MESH" for o in collection.objects),
                        "minimum":[min(p[i] for p in points) for i in range(3)],
                        "maximum":[max(p[i] for p in points) for i in range(3)]})
    with open(os.path.join(REVIEW,"model_manifest.json"),"w") as handle:
        json.dump({"blender":bpy.app.version_string,"models":records,"wall_colors":WALL_COLORS,
                   "wall_sockets":{"straight":[[-4,0,0],[4,0,0]],"gate":[[-4,0,0],[4,0,0]],
                                   "corner":[[4,0,0],[0,4,0]]}},handle,indent=2)
    bpy.context.window.scene=castle_scene
    for area in bpy.context.screen.areas:
        if area.type=="VIEW_3D":
            area.spaces.active.region_3d.view_perspective="CAMERA"
            area.spaces.active.shading.type="MATERIAL"
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE)
    for scene in (castle_scene,house_scene,wall_scene):
        bpy.ops.render.render(write_still=True,scene=scene.name)
    print("FANTASY_BUILDINGS",json.dumps(records))


if __name__=="__main__":
    main()
