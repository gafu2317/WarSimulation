import bpy
import importlib.util
import json
import math
import os
import sys
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "ArtSource", "Blender", "FantasyCultureFacilities.blend")
REVIEW = os.path.join(ROOT, "docs", "Art", "FantasyCultureFacilities")
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("town_facilities", os.path.join(os.path.dirname(__file__), "generate_fantasy_town_facilities.py"))
t = importlib.util.module_from_spec(spec)
spec.loader.exec_module(t)
r, g = t.r, t.g
r.REVIEW = REVIEW


def ellipsoid(name, center, size, material):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, radius=1, location=center)
    obj = bpy.context.object
    obj.scale = size
    return g.finish(obj, name, material)


def torus(name, center, radius, tube, rotation, material):
    bpy.ops.mesh.primitive_torus_add(major_segments=48, minor_segments=8, major_radius=radius,
                                    minor_radius=tube, location=center, rotation=rotation)
    return g.finish(bpy.context.object, name, material)


def stairs(width, front, back, height):
    count = math.ceil(height / 0.18)
    depth = (back-front)/count
    for i in range(count):
        h = height*(i+1)/count
        g.box("Stone entrance tread", (0,front+depth*(i+0.5),h/2), (width,depth,h), "StoneTrim", 0)


def column(height):
    g.box("Column plinth", (0,0,0.12), (0.8,0.8,0.24), "StoneTrim", 0.014)
    g.cylinder("Column torus base", (0,0,0.31), 0.38,0.15,"Marble",0.012,24)
    bpy.ops.mesh.primitive_cone_add(vertices=24,radius1=0.29,radius2=0.245,depth=height-0.8,location=(0,0,height/2))
    g.finish(bpy.context.object,"Tapered stone column","Marble",0.008)
    for i in range(12):
        a=math.tau*i/12
        g.cylinder("Column fluting ridge",(0.248*math.cos(a),0.248*math.sin(a),height/2),
                   0.027,height-1.0,"Marble",0.002,8)
    g.cylinder("Column necking",(0,0,height-0.33),0.31,0.14,"Marble",0.008,24)
    g.box("Column capital",(0,0,height-0.12),(0.77,0.77,0.24),"StoneTrim",0.014)


def pediment(width, front, back, base, rise):
    g.extrude("Stone temple pediment",[(-width/2,base),(width/2,base),(0,base+rise)],front,back,"Marble",0.012)
    for side in (-1,1):
        g.beam("Pediment raking cornice",(side*width/2,front-0.05,base),(0,front-0.05,base+rise),0.18,"StoneTrim")
    g.box("Pediment horizontal cornice",(0,front-0.01,base),(width+0.17,0.42,0.21),"StoneTrim",0.012)


def mansard():
    base,top=6.6,9.25
    rings=[(6.05,4.65,base),(4.5,3.25,top)]
    vertices=[(x*sx,y*sy,z) for sx,sy,z in rings for x,y in ((-1,-1),(1,-1),(1,1),(-1,1))]
    g.mesh("Casino mansard slate roof",vertices,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],"Roof",0.015)
    g.box("Mansard eaves",(0,0,6.55),(12.21,9.41,0.19),"RoofEdge",0.014)
    g.box("Mansard upper ridge",(0,0,9.29),(9.11,6.61,0.13),"RoofEdge",0.012)
    for fraction in (0.2,0.4,0.6,0.8):
        x=6.05*(1-fraction)+4.5*fraction
        y=4.65*(1-fraction)+3.25*fraction
        z=base+(top-base)*fraction+0.012
        for side in (-1,1):
            for start,end in (((-x,side*y,z),(x,side*y,z)),((side*x,-y,z),(side*x,y,z))):
                direction=Vector(end)-Vector(start)
                course=g.box("Mansard slate course",(Vector(start)+Vector(end))/2,
                             (0.021,0.021,direction.length),"RoofEdge",0)
                course.rotation_euler=direction.to_track_quat("Z","Y").to_euler()
    for x in (-4.3,4.3):
        for y in (-3.03,3.03):
            g.cylinder("Gilded roof finial",(x,y,9.54),0.055,0.42,"Brass",0.006,12)
            ellipsoid("Finial gold orb",(x,y,9.8),(0.1,0.1,0.13),"Brass")


def card_emblem(x,y,z,angle,suit):
    def profile(points):
        c,s=math.cos(angle),math.sin(angle)
        return [(x+c*px-s*pz,z+s*px+c*pz) for px,pz in points]
    g.extrude("Gilded playing card",profile([(-0.47,-0.74),(0.47,-0.74),(0.47,0.74),(-0.47,0.74)]),y,y+0.08,"Brass",0.016)
    g.extrude("Ivory playing card face",profile([(-0.4,-0.66),(0.4,-0.66),(0.4,0.66),(-0.4,0.66)]),y-0.025,y-0.01,"Ivory",0.003)
    if suit=="Diamond":
        symbol=[(0,0.43),(0.26,0),(0,-0.43),(-0.26,0)]
        mat="Banner"
    else:
        symbol=[(0,0.44),(0.27,0.09),(0.26,-0.12),(0.11,-0.22),(0.055,-0.13),
                (0.045,-0.28),(0.14,-0.36),(-0.14,-0.36),(-0.045,-0.28),
                (-0.055,-0.13),(-0.11,-0.22),(-0.26,-0.12),(-0.27,0.09)]
        mat="Iron"
    g.extrude(suit+" card relief",profile(symbol),y-0.047,y-0.035,mat,0)


def dice_statue():
    g.box("Dice statue base",(0,0,0.12),(0.99,0.99,0.24),"StoneShade",0.014)
    g.box("Dice statue pedestal",(0,0,0.5),(0.67,0.67,0.52),"Marble",0.012)
    g.box("Ivory die sculpture",(0,0,1.1),(0.72,0.72,0.72),"Ivory",0.045)
    for x,z in ((-0.2,0.9),(0.2,0.9),(0,1.1),(-0.2,1.3),(0.2,1.3)):
        pip=g.cylinder("Dice front pip",(x,-0.367,z),0.056,0.013,"Brass",0,16)
        pip.rotation_euler.x=math.pi/2
    for x,y in ((-0.19,-0.19),(0,0),(0.19,0.19)):
        g.cylinder("Dice upper pip",(x,y,1.467),0.056,0.013,"Brass",0,16)


def casino():
    g.box("Casino stone foundation",(0,0,0.24),(11.75,8.75,0.48),"StoneShade",0.02)
    g.box("Casino lower stone walls",(0,0,1.85),(11.3,8.3,3.36),"Stone",0.015)
    g.box("Casino upper plaster walls",(0,0,4.9),(11.3,8.3,2.76),"CasinoPlaster",0.018)
    for z in (0.61,3.52,6.26):
        g.box("Casino carved stringcourse",(0,0,z),(11.52,8.52,0.18),"StoneTrim",0.014)
    g.box("Casino eaves cornice",(0,0,6.43),(11.8,8.8,0.26),"StoneTrim",0.012)
    for x in (-5.51,-2.58,2.58,5.51):
        g.box("Casino facade pilaster",(x,-4.2,3.45),(0.27,0.22,5.72),"Marble",0.013)
        g.box("Casino pilaster capital",(x,-4.24,6.14),(0.46,0.34,0.22),"Brass",0.009)
    for side in (-1,1):
        for y in (-4.0,4.0):
            g.box("Casino side quoin",(side*5.65,y,3.45),(0.28,0.3,5.72),"StoneTrim",0.01)
    for x in (-4.13,-1.37,1.37,4.13):
        g.window(x,-4.19,4.01,0.89,1.75)
    for x in (-4.02,4.02):
        g.window(x,-4.19,1.21,1.28,1.71)
    r.shifted(lambda:[g.window(y,-5.7,z,1.08,1.55) for y in (-2.41,0,2.41) for z in (1.28,4.09)],angle=math.pi/2)
    r.door(0,-4.42,1.92,2.74,0.35)
    stairs(3.2,-5.7,-4.32,0.36)
    r.shifted(lambda:t.shed_roof(4.8,1.88,3.19,3.75,"Velvet"),(0,-4.71,0))
    for x in (-2.24,2.24):
        g.box("Casino canopy post foot",(x,-5.49,0.12),(0.27,0.27,0.24),"StoneTrim",0.012)
        g.cylinder("Bronze canopy post",(x,-5.49,1.68),0.063,3.13,"Brass",0.008,16)
    for i in range(8):
        x=-2.4+(i+0.5)*0.6
        g.extrude("Velvet canopy scallop",[(x-0.29,3.18),(x+0.29,3.18),(x+0.22,3.0),(x,2.91),(x-0.22,3.0)],
                  -5.675,-5.625,"Velvet",0.004)
    card_emblem(-0.37,-4.45,5.17,-0.16,"Diamond")
    card_emblem(0.37,-4.58,5.19,0.16,"Spade")
    for x in (-3.24,3.24):
        r.shifted(dice_statue,(x,-5.04,0))
        t.lantern(x,-4.63,2.88)
    mansard()


def bust():
    g.box("Sculpture plinth",(0,0,0.12),(1.05,0.92,0.24),"StoneShade",0.016)
    g.box("Sculpture pedestal",(0,0,0.69),(0.72,0.67,0.91),"Marble",0.012)
    g.box("Pedestal cornice",(0,0,1.2),(0.92,0.83,0.17),"StoneTrim",0.012)
    g.cylinder("Bust socle",(0,0,1.37),0.26,0.2,"Marble",0.01,24)
    ellipsoid("Sculpted draped shoulders",(0,0,1.68),(0.46,0.23,0.27),"Marble")
    g.cylinder("Sculpture neck",(0,0,1.96),0.123,0.25,"Marble",0.006,16)
    ellipsoid("Sculpted head",(0,-0.015,2.24),(0.23,0.215,0.3),"Marble")
    ellipsoid("Statue nose",(0,-0.22,2.24),(0.045,0.073,0.075),"Marble")
    for x in (-0.07,0.07):
        g.beam("Sculpted brow",(x-0.039,-0.211,2.34),(x+0.039,-0.211,2.34),0.022,"Marble")
    g.beam("Drapery fold",(-0.34,-0.155,1.82),(0.19,-0.204,1.51),0.043,"StoneTrim")


def armillary():
    g.box("Armillary plinth",(0,0,0.12),(1.02,0.94,0.24),"StoneShade",0.012)
    g.box("Armillary pedestal",(0,0,0.68),(0.67,0.67,0.88),"Marble",0.012)
    g.cylinder("Armillary bronze stem",(0,0,1.3),0.082,0.48,"Brass",0.008,16)
    center=(0,0,2.0)
    for rotation in ((math.pi/2,0,0),(math.pi/2,0,math.pi/2),(0.47,0.3,0)):
        torus("Bronze celestial ring",center,0.64,0.027,rotation,"Brass")
    ellipsoid("Celestial globe",center,(0.17,0.17,0.17),"Brass")
    g.beam("Armillary polar axis",(0,0,1.26),(0,0,2.76),0.044,"Brass")


def museum():
    g.box("Museum podium",(0,-0.35,0.36),(13.8,10.6,0.72),"StoneShade",0.02)
    g.box("Museum podium cornice",(0,-0.35,0.72),(13.93,10.73,0.18),"StoneTrim",0.014)
    g.box("Museum gallery walls",(0,0.4,3.48),(12.8,7.8,5.6),"MuseumPlaster",0.014)
    for z in (1.13,6.19):
        g.box("Gallery limestone stringcourse",(0,0.4,z),(13.02,8.02,0.21),"StoneTrim",0.012)
    g.box("Museum eaves cornice",(0,0.4,6.35),(13.15,8.15,0.22),"StoneTrim",0.012)
    r.shifted(lambda:r.roof(13.6,8.6,6.44,2.2),(0,0.4,0))
    for x in (-4.14,-2.77,-1.4,1.4,2.77,4.14):
        r.shifted(lambda:column(4.65),(x,-4.93,0.81))
    g.box("Portico architrave",(0,-4.83,5.63),(9.68,1.2,0.34),"Marble",0.012)
    g.box("Portico frieze",(0,-4.83,5.98),(9.85,1.3,0.34),"StoneTrim",0.012)
    pediment(10.15,-5.54,-4.18,6.22,1.8)
    for x in (-4.43,-3.66,-2.89,-2.12,-1.35,-0.58,0.19,0.96,1.73,2.5,3.27,4.04):
        g.box("Museum dentil",(x,-5.56,6.16),(0.18,0.18,0.18),"Marble",0.004)
    torus("Pediment bronze medallion",(0,-5.59,6.86),0.34,0.037,(math.pi/2,0,0),"Brass")
    for i in range(8):
        a=math.tau*i/8
        g.beam("Art medallion rays",(0.09*math.cos(a),-5.595,6.86+0.09*math.sin(a)),
               (0.26*math.cos(a),-5.595,6.86+0.26*math.sin(a)),0.025,"Brass")
    r.door(0,-3.59,2.03,3.33,0.85)
    for x in (-5.3,5.3):
        g.window(x,-3.54,2.0,1.24,2.35)
    r.shifted(lambda:[g.window(y,-6.45,2.1,1.24,2.5) for y in (-2.1,0.4,2.9)],angle=math.pi/2)
    stairs(10.1,-7.18,-5.73,0.81)
    r.shifted(bust,(-5.8,-4.79,0.81))
    r.shifted(armillary,(5.8,-4.79,0.81))


def ellipse_disk(name,rx,ry,bottom,top,material):
    sides=96
    vertices=[(rx*math.cos(math.tau*i/sides),ry*math.sin(math.tau*i/sides),z)
              for z in (bottom,top) for i in range(sides)]
    faces=[tuple(range(sides-1,-1,-1)),tuple(range(sides,sides*2))]
    faces += [(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)]
    return g.mesh(name,vertices,faces,material)


def ring_section(name,inner,outer,bottom,top,start,end,material):
    segments=math.ceil((end-start)/(math.tau/96))
    count=segments+1
    vertices=[(rx*math.cos(start+(end-start)*i/segments),ry*math.sin(start+(end-start)*i/segments),z)
              for z in (bottom,top) for rx,ry in (outer,inner) for i in range(count)]
    faces=[]
    for i in range(segments):
        n=i+1
        faces += [(i,n,2*count+n,2*count+i),(count+n,count+i,3*count+i,3*count+n),
                  (2*count+i,2*count+n,3*count+n,3*count+i),(n,i,count+i,count+n)]
    faces += [(0,2*count,3*count,count),(segments,count+segments,3*count+segments,2*count+segments)]
    return g.mesh(name,vertices,faces,material)


def arcade_cell(width,main_gate):
    radius=width*(0.36 if main_gate else 0.265)
    spring=2.53 if main_gate else 1.84
    height=5.36
    pier_width=width/2-radius
    for side in (-1,1):
        g.box("Arena arcade pier",(side*(width/2+radius)/2,0,2.81),(pier_width+0.025,0.9,5.02),"Stone",0)
    profile=[(-radius,height),(radius,height)] + [
        (radius*math.cos(math.pi*i/16),spring+radius*math.sin(math.pi*i/16)) for i in range(17)]
    g.extrude("Arena stone arch spandrel",profile,-0.45,0.45,"Stone",0)
    g.arch_trim("Arena dressed arch",0,-0.51,0.31,radius*2,spring+radius-0.31,0.18,"StoneTrim")
    for side in (-1,1):
        x=side*(width/2-0.08)
        g.box("Arena facade pilaster",(x,-0.5,2.75),(0.25,0.22,4.8),"StoneTrim",0.008)
        if side==1:
            g.box("Arcade pilaster capital",(width/2,-0.54,5.12),(0.48,0.31,0.18),"StoneTrim",0.008)


def arena():
    ellipse_disk("Arena limestone foundation",14.8,11.8,0,0.31,"StoneShade")
    ellipse_disk("Arena sand fighting floor",7.98,5.18,0.31,0.35,"ArenaSand")
    gap=math.pi/32
    for tier in range(6):
        inner=(8.0+tier*0.9,5.2+tier*0.8)
        outer=(8.9+tier*0.9,6.0+tier*0.8)
        height=1.25+tier*0.78
        for quarter in range(4):
            start=-math.pi/2+quarter*math.pi/2+gap
            end=start+math.pi/2-gap*2
            ring_section("Stepped spectator terrace",inner,outer,0.31,height,start,end,"Stone")
            ring_section("Stone spectator bench",(inner[0]+0.27,inner[1]+0.23),
                         (outer[0]-0.06,outer[1]-0.05),height,height+0.13,start,end,"SeatStone")
    for side in (0,math.pi):
        for tread in range(12):
            inner=(8.0+tread*0.45,5.2+tread*0.4)
            outer=(inner[0]+0.45,inner[1]+0.4)
            ring_section("Radial spectator stair",inner,outer,0.31,0.86+tread*0.39,side-gap,side+gap,"StoneTrim")
    for i in range(24):
        angle=-math.pi/2+math.tau*i/24
        half=math.pi/24
        p0=Vector((14*math.cos(angle-half),11*math.sin(angle-half),0))
        p1=Vector((14*math.cos(angle+half),11*math.sin(angle+half),0))
        center=(p0+p1)/2
        tangent=(p1-p0).normalized()
        rotation=math.atan2(tangent.y,tangent.x)
        width=(p1-p0).length
        r.shifted(lambda width=width,i=i:arcade_cell(width,i in (0,12)),center,rotation)
    for start,end in ((0,math.pi),(math.pi,math.tau)):
        ring_section("Arena upper cornice",(13.34,10.34),(14.22,11.22),5.34,5.57,start,end,"StoneTrim")
        ring_section("Arena upper parapet",(13.54,10.54),(14.06,11.06),5.57,6.13,start,end,"Stone")
        ring_section("Arena parapet coping",(13.49,10.49),(14.11,11.11),6.13,6.27,start,end,"StoneTrim")
    for x in (-2.35,2.35):
        g.box("Arena gate banner buttress",(x,-11.18,2.83),(0.47,0.49,5.04),"StoneTrim",0.012)
        r.banner(x,-11.49,4.68,0.7,1.92)
    stairs(3.5,-12.77,-11.79,0.31)
    for x in (-2.8,2.8):
        g.cylinder("Arena entrance brazier base",(x,-12.05,0.12),0.35,0.24,"StoneTrim",0.008,16)
        g.cylinder("Arena entrance torch stand",(x,-12.05,0.88),0.065,1.42,"Iron",0.006,12)
        bpy.ops.mesh.primitive_cone_add(vertices=16,radius1=0.16,radius2=0.33,depth=0.28,location=(x,-12.05,1.62))
        g.finish(bpy.context.object,"Unlit arena brazier","Iron",0.006)


def main():
    os.makedirs(REVIEW,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    r.setup_materials()
    for name,color,kind in (("Ivory","E1D7BB","plain"),("CasinoPlaster","BCAD96","plain"),
                            ("MuseumPlaster","C2BEAE","plain"),("Marble","CBC7B9","plain"),
                            ("Velvet","512F46","plain"),("ArenaSand","AC9874","plain"),
                            ("SeatStone","ABA48F","plain"),("Lantern","CCA05D","plain")):
        g.MATERIALS[name]=r.surface(name,color,kind)
    assets=[]
    scenes=[]
    for index,(name,label,recipe,camera) in enumerate((
        ("Casino","カジノ",casino,(23,-35,21)),
        ("Museum","美術館",museum,(24,-39,24)),
        ("Arena","闘技場",arena,(36,-51,43))),start=1):
        scene=bpy.context.scene if index==1 else bpy.data.scenes.new(name)
        scene.name=f"0{index} {name}"
        bpy.context.window.scene=scene
        asset=g.make_asset(name,recipe)
        asset[1]["facility_type"]=label
        assets.append(asset)
        scenes.append(scene)
        r.stage(scene,list(asset[0].objects),camera,(0,0,4),name+"_Preview.png",(1900,1500))
        scene.view_settings.exposure=0.3
        print("FACILITY_CREATED",name,flush=True)
    overview=bpy.data.scenes.new("00 All Culture Facilities")
    bpy.context.window.scene=overview
    positions=((-22,-2,0),(-5,0,0),(20,0,0))
    displays=[t.gallery_copy(asset,position,overview) for asset,position in zip(assets,positions)]
    r.stage(overview,[obj for collection in displays for obj in collection.objects],
            (20,-68,47),(0,0,4),"Facilities_Preview.png",(2800,1300))
    overview.view_settings.exposure=0.3
    records=[]
    for collection,root in assets:
        points=[obj.matrix_parent_inverse@obj.matrix_basis@Vector(v)
                for obj in collection.objects if obj.type=="MESH" for v in obj.bound_box]
        records.append({"name":root.name,"facility_type":root["facility_type"],
                        "parts":sum(obj.type=="MESH" for obj in collection.objects),
                        "minimum":[min(p[i] for p in points) for i in range(3)],
                        "maximum":[max(p[i] for p in points) for i in range(3)]})
    with open(os.path.join(REVIEW,"model_manifest.json"),"w") as handle:
        json.dump({"blender":bpy.app.version_string,"models":records,"arena":"Open sand floor, tiered seating, entry passage"},
                  handle,indent=2,ensure_ascii=False)
    for area in bpy.context.screen.areas:
        if area.type=="VIEW_3D":
            area.spaces.active.region_3d.view_perspective="CAMERA"
            area.spaces.active.shading.type="MATERIAL"
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE)
    for scene in [overview]+scenes:
        bpy.ops.render.render(write_still=True,scene=scene.name)
    print("CULTURE_FACILITIES_COMPLETE",json.dumps(records,ensure_ascii=False),flush=True)


if __name__=="__main__":
    main()
