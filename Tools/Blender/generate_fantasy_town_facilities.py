import bpy
import importlib.util
import json
import math
import os
import sys
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "ArtSource", "Blender", "FantasyTownFacilities.blend")
REVIEW = os.path.join(ROOT, "docs", "Art", "FantasyTownFacilities")
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("fantasy_style", os.path.join(os.path.dirname(__file__), "generate_realistic_fantasy_buildings.py"))
r = importlib.util.module_from_spec(spec)
spec.loader.exec_module(r)
g = r.g
r.REVIEW = REVIEW


def casement(x, y, bottom, width, height, frame="Timber", glass="Window"):
    g.box("Casement recess", (x,y,bottom+height/2), (width+0.2,0.14,height+0.2), frame, 0.014)
    g.box("Casement glazing", (x,y-0.079,bottom+height/2), (width,0.025,height), glass, 0.005)
    g.box("Casement mullion", (x,y-0.104,bottom+height/2), (0.063,0.065,height), frame, 0.005)
    g.box("Casement transom", (x,y-0.108,bottom+height*0.52), (width,0.065,0.058), frame, 0.005)
    g.box("Window sill", (x,y-0.07,bottom-0.1), (width+0.34,0.31,0.14), frame, 0.014)


def gable(width, depth, eave, rise, wall="Plaster"):
    half = width/2
    g.extrude("Gable infill", [(-half+0.33,eave),(half-0.33,eave),(0,eave+rise-0.22)],
              -depth/2+0.3,depth/2-0.3,wall,0.012)
    courses = math.ceil(math.hypot(half,rise)/0.45)
    for side in (-1,1):
        for index in range(courses):
            low=index/courses
            high=(index+1)/courses
            x0=side*half*(1-low)
            x1=side*half*(1-high)
            z0=eave+rise*low
            z1=eave+rise*high
            g.extrude("Slate roof course", [(x0,z0),(x1,z1),(x1,z1-0.08),(x0,z0-0.08)],
                      -depth/2,depth/2,"Roof",0.007)
        for y in (-depth/2-0.02,depth/2+0.02):
            g.beam("Gable verge flashing", (side*half,y,eave),(0,y,eave+rise),0.09,"RoofEdge")
        g.box("Oak roof eaves", (side*(half-0.04),0,eave-0.06), (0.15,depth+0.09,0.18), "Timber",0.012)
    g.beam("Slate ridge", (0,-depth/2-0.1,eave+rise),(0,depth/2+0.1,eave+rise),0.13,"RoofEdge")


def shed_roof(width, depth, front, back, material="Roof"):
    vertices=[(x,y,z+t) for t in (0,-0.1) for x,y,z in
              ((-width/2,-depth/2,front),(width/2,-depth/2,front),(width/2,depth/2,back),(-width/2,depth/2,back))]
    g.mesh("Lean-to roof",vertices,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],material,0.014)
    g.box("Canopy front fascia",(0,-depth/2,front-0.06),(width+0.05,0.13,0.2),"Timber",0.012)
    for side in (-1,1):
        g.beam("Canopy side fascia",(side*width/2,-depth/2,front),(side*width/2,depth/2,back),0.12,"Timber")


def barrel():
    levels=((0,0.34),(0.12,0.39),(0.52,0.435),(0.92,0.39),(1.04,0.34))
    sides=16
    vertices=[(radius*math.cos(math.tau*i/sides),radius*math.sin(math.tau*i/sides),z)
              for z,radius in levels for i in range(sides)]
    faces=[tuple(range(sides-1,-1,-1)),tuple(range((len(levels)-1)*sides,len(levels)*sides))]
    faces += [(row*sides+i,row*sides+(i+1)%sides,(row+1)*sides+(i+1)%sides,(row+1)*sides+i)
              for row in range(len(levels)-1) for i in range(sides)]
    g.mesh("Ale barrel staves",vertices,faces,"Door",0.008)
    for z,radius in ((0.15,0.401),(0.53,0.448),(0.89,0.405)):
        g.cylinder("Barrel iron hoop",(0,0,z),radius,0.06,"Iron",0.006,sides)
    g.cylinder("Barrel wooden lid",(0,0,1.046),0.337,0.028,"Timber",0.005,sides)
    for x in (-0.16,0,0.16):
        length=math.sqrt(0.32**2-x*x)*2
        g.box("Barrel lid seam",(x,0,1.064),(0.012,length,0.008),"Iron",0)


def lantern(x,y,z):
    g.beam("Lantern bracket",(x,y+0.35,z+0.28),(x,y,z+0.28),0.055,"Iron")
    g.box("Lantern amber glass",(x,y,z),(0.2,0.18,0.34),"Lantern",0.012)
    for dz in (-0.19,0.19):
        g.box("Lantern iron cap",(x,y,z+dz),(0.27,0.25,0.07),"Iron",0.009)
    for dx in (-0.11,0.11):
        g.box("Lantern iron frame",(x+dx,y-0.1,z),(0.025,0.027,0.38),"Iron",0.003)


def tavern_sign():
    g.beam("Tavern sign bracket",(2.67,-3.07,5.13),(2.67,-4.33,5.13),0.09,"Iron")
    g.beam("Sign bracket brace",(2.67,-3.09,4.64),(2.67,-4.12,5.13),0.055,"Iron")
    g.beam("Sign suspension crossbar",(2.23,-4.24,5.13),(3.11,-4.24,5.13),0.06,"Iron")
    g.box("Tavern hanging sign",(2.67,-4.24,4.27),(1.18,0.14,1.12),"Timber",0.035)
    g.box("Tavern sign face",(2.67,-4.323,4.27),(1.03,0.035,0.97),"Door",0.02)
    for x in (2.3,3.04):
        g.beam("Sign suspension",(x,-4.24,4.85),(x,-4.24,5.13),0.035,"Iron")
    mug=[(2.34,4.57),(2.86,4.57),(2.82,3.97),(2.4,3.97)]
    g.extrude("Golden tankard emblem",mug,-4.364,-4.348,"Brass",0.003)
    for start,end in (((2.87,-4.357,4.46),(3.0,-4.357,4.46)),
                      ((3.0,-4.357,4.46),(3.0,-4.357,4.12)),
                      ((3.0,-4.357,4.12),(2.85,-4.357,4.12))):
        g.beam("Tankard handle emblem",start,end,0.052,"Brass")


def tavern():
    g.box("Tavern foundation",(0,0,0.27),(7.95,6.45,0.54),"StoneShade",0.025)
    g.box("Tavern ground floor",(0,0,1.83),(7.5,6,3.16),"Stone",0.018)
    g.box("Jettied tavern upper floor",(0,0,4.77),(7.9,6.4,2.72),"OchrePlaster",0.02)
    for z in (3.48,6.09):
        g.box("Tavern floor beam",(0,0,z),(8.04,6.54,0.2),"Timber",0.018)
    for side in (-1,1):
        for x in (-3.82,-1.29,1.29,3.82):
            g.box("Tavern front oak upright",(x,side*3.24,4.78),(0.18,0.15,2.53),"Timber",0.012)
        for x in (-3.7,1.45):
            g.beam("Tavern timber brace",(x,side*3.25,3.58),(x+0.78,side*3.25,4.31),0.14,"Timber")
    for x in (-3.99,3.99):
        for y in (-3.08,0,3.08):
            g.box("Tavern side oak upright",(x,y,4.78),(0.15,0.18,2.53),"Timber",0.012)
    for x in (-2.53,0,2.53):
        casement(x,-3.3,4.17,1.08,1.25,glass="WarmGlass")
    r.shifted(lambda: [casement(x,-4.05,4.17,1.04,1.25,glass="WarmGlass") for x in (-1.57,1.57)],angle=math.pi/2)
    gable(8.65,7.06,6.17,2.75,"OchrePlaster")
    casement(0,-3.29,6.69,0.79,0.89,glass="WarmGlass")
    for side in (-1,1):
        g.beam("Tavern gable timber",(side*3.91,-3.29,6.17),(0,-3.29,8.65),0.14,"Timber")
    r.door(-0.9,-3.04,1.46,2.65,0.18)
    casement(2.17,-3.07,1.2,1.27,1.42,glass="WarmGlass")
    casement(-2.78,-3.07,1.2,0.93,1.42,glass="WarmGlass")
    g.box("Tavern entrance step",(-0.9,-3.52,0.13),(2.02,0.73,0.26),"StoneTrim",0.018)
    r.shifted(lambda: shed_roof(4.3,1.66,2.95,3.48),(-0.92,-3.77,0))
    for x in (-2.94,1.1):
        g.box("Porch post shoe",(x,-4.48,0.13),(0.34,0.34,0.26),"StoneShade",0.012)
        g.box("Tavern porch post",(x,-4.48,1.6),(0.18,0.18,2.92),"Timber",0.012)
        g.beam("Porch diagonal brace",(x,-4.48,2.25),(x+(-0.48 if x>0 else 0.48),-4.48,2.95),0.13,"Timber")
    tavern_sign()
    for position in ((-3.64,-3.93,0),(-4.48,-3.38,0)):
        r.shifted(barrel,position)
    g.box("Tavern outdoor bench",(2.61,-3.87,0.59),(2.19,0.47,0.16),"Door",0.025)
    for x in (1.84,3.37):
        g.box("Bench leg",(x,-3.87,0.27),(0.19,0.4,0.54),"Timber",0.012)
    g.box("Tavern chimney",(-2.3,1.18,7.25),(0.84,0.92,4.09),"Stone",0.018)
    g.box("Tavern chimney crown",(-2.3,1.18,9.32),(1.02,1.1,0.23),"StoneTrim",0.015)
    for x in (-2.52,-2.08):
        g.cylinder("Tavern chimney pot",(x,1.18,9.65),0.16,0.46,"StoneShade",0.008,12)
    lantern(-1.98,-3.41,2.62)


def herb_plant(height, angle):
    g.cylinder("Herb stem",(0,0,height/2),0.018,height,"HerbDark",0,8)
    for layer in range(3):
        z=height*(0.32+layer*0.22)
        for side in (-1,1):
            a=angle+layer*0.8+(math.pi if side<0 else 0)
            direction=Vector((math.cos(a),math.sin(a),0.35))
            start=Vector((0,0,z))
            tip=start+direction*height*0.48
            center=(start+tip)/2
            lateral=Vector((-math.sin(a),math.cos(a),0))*height*0.16
            top=center+Vector((0,0,0.027))
            bottom=center-Vector((0,0,0.018))
            vertices=[start,tip,center+lateral,center-lateral,top,bottom]
            faces=[(0,2,4),(2,1,4),(1,3,4),(3,0,4),(2,0,5),(1,2,5),(3,1,5),(0,3,5)]
            g.mesh("Medicinal herb leaf",vertices,faces,"Herb",0)


def herb_planter():
    g.box("Herb trough body",(0,0,0.27),(2.36,0.61,0.54),"Door",0.022)
    g.box("Planter soil",(0,0,0.548),(2.18,0.45,0.022),"Soil",0)
    for x in (-1.12,1.12):
        g.box("Planter rim",(x,0,0.59),(0.14,0.69,0.14),"Timber",0.012)
    for y in (-0.29,0.29):
        g.box("Planter rim",(0,y,0.59),(2.36,0.14,0.14),"Timber",0.012)
    for i in range(5):
        r.shifted(lambda i=i: herb_plant(0.45+(i%2)*0.14,i*1.6),(-0.89+i*0.44,0,0.56))


def medical_emblem():
    g.extrude("Clinic green crest",[(-0.57,3.42),(0.57,3.42),(0.57,2.7),(0,2.43),(-0.57,2.7)],
              -2.907,-2.79,"Sage",0.023)
    x,z,arm,reach=0,2.98,0.105,0.36
    profile=[(x-arm,z+reach),(x+arm,z+reach),(x+arm,z+arm),(x+reach,z+arm),
             (x+reach,z-arm),(x+arm,z-arm),(x+arm,z-reach),(x-arm,z-reach),
             (x-arm,z-arm),(x-reach,z-arm),(x-reach,z+arm),(x-arm,z+arm)]
    g.extrude("Clinic healing emblem",profile,-2.945,-2.918,"Ivory",0.008)


def clinic():
    g.box("Clinic footing",(0,0,0.22),(7.26,5.98,0.44),"StoneShade",0.022)
    g.box("Clinic limestone lower wall",(0,0,0.76),(6.95,5.7,1.08),"Stone",0.015)
    g.box("Clinic limewashed walls",(0,0,2.54),(6.85,5.6,2.52),"ClinicPlaster",0.018)
    g.box("Clinic plinth trim",(0,0,1.29),(7.01,5.75,0.14),"StoneTrim",0.014)
    for x in (-3.3,3.3):
        g.box("Clinic corner pier",(x,-2.78,1.95),(0.27,0.21,3.66),"StoneTrim",0.012)
    r.roof(7.54,6.29,3.88,1.9)
    r.door(0,-2.84,1.34,2.3,0.17)
    for x in (-2.12,2.12):
        r.lancet(x,-2.84,1.61,1.1,1.57)
        for side in (-1,1):
            g.box("Clinic sage shutter",(x+side*0.76,-2.87,2.28),(0.28,0.12,1.28),"Sage",0.014)
    r.shifted(medical_emblem,(0,0,0.35))
    g.box("Clinic broad doorstep",(0,-3.31,0.12),(2.01,0.73,0.24),"StoneTrim",0.018)
    for x in (-2.18,2.18):
        r.shifted(herb_planter,(x,-3.35,0))
    def treatment_wing():
        g.box("Treatment wing foundation",(0,0,0.22),(2.68,4.6,0.44),"StoneShade",0.02)
        g.box("Treatment wing wall",(0,0,1.64),(2.5,4.42,2.93),"ClinicPlaster",0.016)
        r.roof(2.94,4.89,3.17,1.03)
        casement(0,-2.29,1.25,1.01,1.12,"Sage","Glass")
        r.shifted(lambda: [casement(x,-1.3,1.25,0.88,1.12,"Sage","Glass") for x in (-1.03,1.03)],angle=math.pi/2)
    r.shifted(treatment_wing,(4.48,0.3,0))
    g.box("Clinic chimney",(-1.88,1.53,4.9),(0.56,0.66,2.5),"Stone",0.015)
    g.box("Clinic chimney cap",(-1.88,1.53,6.19),(0.74,0.82,0.16),"StoneTrim",0.013)
    lantern(-0.95,-3.07,2.54)


def shield_emblem():
    outline=[(-0.58,0.69),(0.58,0.69),(0.5,-0.11),(0,-0.66),(-0.5,-0.11)]
    g.extrude("Guard shield iron rim",outline,-0.05,0.05,"Iron",0.012)
    g.extrude("Guard shield crimson face",[(x*0.83,z*0.83) for x,z in outline],-0.084,-0.062,"Banner",0.008)
    g.extrude("Guard shield insignia",[(0,0.45),(0.18,0.08),(0,-0.3),(-0.18,0.08)],-0.105,-0.092,"Brass",0.006)


def spear(height=2.55):
    g.cylinder("Spear ash shaft",(0,0,(height-0.37)/2),0.028,height-0.37,"Door",0,10)
    g.extrude("Forged spearhead",[(-0.115,height-0.38),(0,height),(0.115,height-0.38),(0,height-0.49)],
              -0.024,0.024,"Iron",0.003)
    g.cylinder("Spear socket",(0,0,height-0.45),0.046,0.19,"Iron",0.004,10)


def square_watchtower():
    g.box("Watchtower footing",(0,0,0.25),(4.63,4.83,0.5),"StoneShade",0.016)
    g.box("Watchtower ashlar",(0,0,3.72),(4.2,4.4,6.94),"Stone",0.013)
    for z in (0.71,3.52,6.93):
        g.box("Tower stringcourse",(0,0,z),(4.38,4.58,0.18),"StoneTrim",0.012)
    for x in (-2.06,2.06):
        for y in (-2.16,2.16):
            g.box("Watchtower corner quoins",(x,y,3.73),(0.24,0.24,6.97),"StoneTrim",0.012)
    for z in (1.7,4.89):
        r.lancet(0,-2.24,z,0.32,1.35)
        r.shifted(lambda z=z: r.lancet(0,-2.14,z,0.32,1.35),angle=math.pi/2)
    g.box("Watch platform coping",(0,0,7.23),(4.65,4.85,0.24),"StoneTrim",0.014)
    for side in (-1,1):
        g.box("Tower front parapet",(0,side*2.2,7.61),(4.61,0.43,0.52),"Stone",0.009)
        g.box("Tower side parapet",(side*2.08,0,7.61),(0.43,3.97,0.52),"Stone",0.009)
    for x in (-2.08,-0.69,0.69,2.08):
        for y in (-2.2,2.2):
            g.box("Watchtower front merlon",(x,y,8.23),(0.73,0.5,0.75),"Stone",0.014)
    for x in (-2.08,2.08):
        for y in (-0.73,0.73):
            g.box("Watchtower side merlon",(x,y,8.23),(0.5,0.77,0.75),"Stone",0.014)
    g.cylinder("Watchtower banner pole",(-1.25,0.97,8.57),0.037,2.78,"Iron",0.004,12)
    g.extrude("Watchtower royal flag",[(-1.21,9.85),(0.15,9.63),(-0.04,9.02),(-1.21,9.17)],
              0.95,0.985,"Banner",0.005)


def guardhouse():
    r.shifted(square_watchtower,(2.38,0.46,0))
    g.box("Barracks foundation",(-2.28,0,0.23),(5.69,5.15,0.46),"StoneShade",0.022)
    g.box("Guard barracks stonework",(-2.28,0,1.85),(5.35,4.8,3.36),"Stone",0.016)
    g.box("Barracks plinth course",(-2.28,0,0.66),(5.54,4.99,0.15),"StoneTrim",0.012)
    g.box("Barracks eaves course",(-2.28,0,3.575),(5.51,4.96,0.21),"StoneTrim",0.012)
    r.shifted(lambda: gable(6.04,5.46,3.62,2.05,"Stone"),(-2.28,0,0))
    r.door(-2.35,-2.44,1.26,2.25,0.18)
    for x in (-4.18,-0.49):
        r.lancet(x,-2.44,1.59,0.3,1.2)
    r.shifted(shield_emblem,(-2.35,-2.48,4.25))
    g.box("Guard entrance step",(-2.35,-2.8,0.12),(1.89,0.63,0.24),"StoneTrim",0.016)
    g.box("Spear rack feet",(-4.26,-2.94,0.12),(1.04,0.5,0.24),"Timber",0.012)
    for x in (-4.7,-3.82):
        g.box("Spear rack post",(x,-2.91,0.84),(0.1,0.14,1.68),"Timber",0.012)
    g.box("Spear rack upper rail",(-4.26,-2.93,1.6),(1.05,0.18,0.13),"Timber",0.012)
    for x in (-4.58,-4.25,-3.92):
        r.shifted(spear,(x,-3.05,0.24))
    r.banner(2.38,-1.83,4.56,0.84,1.73)
    lantern(-1.39,-2.79,2.52)


def gallery_copy(asset, position, scene):
    collection,source_root=asset
    display=bpy.data.collections.new("Display "+source_root.name)
    scene.collection.children.link(display)
    root=bpy.data.objects.new("Display "+source_root.name,None)
    root.location=position
    display.objects.link(root)
    for obj in collection.objects:
        if obj.type=="MESH":
            copy=obj.copy()
            copy.parent=root
            display.objects.link(copy)
    return display


def main():
    os.makedirs(REVIEW,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    r.setup_materials()
    for name,color,kind in (("OchrePlaster","BAA580","plain"),("WarmGlass","886E47","plain"),
                            ("ClinicPlaster","CCC6B2","plain"),("Sage","48634F","wood"),
                            ("Ivory","DCD6B9","plain"),("Herb","617347","plain"),
                            ("HerbDark","3F5233","plain"),("Soil","40382C","plain"),
                            ("Lantern","D3A25B","plain")):
        g.MATERIALS[name]=r.surface(name,color,kind)
    assets=[]
    scenes=[]
    for index,(name,label,recipe) in enumerate((("Tavern","酒場",tavern),("Clinic","診療所",clinic),("Guardhouse","衛兵所",guardhouse)),start=1):
        scene=bpy.context.scene if index==1 else bpy.data.scenes.new(name)
        scene.name=f"0{index} {name}"
        bpy.context.window.scene=scene
        asset=g.make_asset(name,recipe)
        asset[1]["facility_type"]=label
        assets.append(asset)
        scenes.append(scene)
        r.stage(scene,list(asset[0].objects),(19,-30,18),(0,0,4),name+"_Preview.png",(1700,1450))
        scene.view_settings.exposure=0.3
        print("FACILITY_CREATED",name,flush=True)
    overview=bpy.data.scenes.new("00 All Facilities")
    bpy.context.window.scene=overview
    displays=[gallery_copy(asset,((index-1)*13,0,0),overview) for index,asset in enumerate(assets)]
    r.stage(overview,[obj for collection in displays for obj in collection.objects],
            (15,-48,28),(0,0,4),"Facilities_Preview.png",(2600,1300))
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
        json.dump({"blender":bpy.app.version_string,"models":records,"scope":"Exterior-only fantasy town facilities"},handle,indent=2,ensure_ascii=False)
    for area in bpy.context.screen.areas:
        if area.type=="VIEW_3D":
            area.spaces.active.region_3d.view_perspective="CAMERA"
            area.spaces.active.shading.type="MATERIAL"
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE)
    for scene in [overview]+scenes:
        bpy.ops.render.render(write_still=True,scene=scene.name)
    print("FACILITIES_COMPLETE",json.dumps(records,ensure_ascii=False),flush=True)


if __name__=="__main__":
    main()
