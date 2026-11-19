"""Build editable concept models without modifying the live Unity assets."""

import argparse
import importlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
sys.path.insert(0,str(HERE.parent))
sys.path.insert(0,str(HERE))
from mesh import DEFAULT_ROOF_UV_METRES, Model
from materials import ALIASES, build_materials, city_surface
from preview import VIEWS, render
from roof_tiles import RoofTiles, TILE_ROOFS, build_tile_material


RECIPES = {
    "Observatory":"observatory", "Library":"library", "Museum":"museum", "Clinic":"clinic",
    "Ballroom":"ballroom", "MasqueradeSalon":"masquerade_salon", "WarriorAcademy_Lv5":"warrior_academy",
    "ArcaneAcademy_Lv5":"arcane_academy", "SpiritAcademy_Lv5":"spirit_academy", "Guardhouse":"guardhouse",
    "Arena":"arena", "Plaza":"plaza", "Blacksmith":"blacksmith", "Tavern":"tavern",
    "Casino":"casino", "CrimsonRowhouse":"crimson_rowhouse", "VeiledCourtyard":"veiled_courtyard",
}
MODEL_NAMES = (*RECIPES, "Church")
FACILITY_SCALE = .8


def validate(objects):
    meshes = [obj for obj in objects if obj.type=="MESH"]
    triangles = 0
    for obj in meshes:
        mesh = obj.data
        mesh.calc_loop_triangles()
        assert obj.matrix_world.is_identity and all(face.area>1e-8 for face in mesh.loop_triangles), obj.name
        uv = mesh.uv_layers.active
        assert uv and all(math.isfinite(c) for uv_coord in uv.uv for c in uv_coord.vector), obj.name
        triangles += len(mesh.loop_triangles)
    coords = [v.co for obj in meshes for v in obj.data.vertices]
    lower = [min(v[i] for v in coords) for i in range(3)]
    upper = [max(v[i] for v in coords) for i in range(3)]
    assert -.0001<=lower[2]<=.0001, lower
    return {
        "meshes":len(meshes), "triangles":triangles,
        "materials":sorted({slot.material.name for o in meshes for slot in o.material_slots}),
        "bounds_m": [round(b-a,3) for a,b in zip(lower,upper)],
        "lower":lower, "upper":upper,
    }


def make_concept(name, scale):
    recipe=importlib.import_module(RECIPES[name])
    tiles = TILE_ROOFS.get(name)
    repeat_m = tiles.repeat_m if tiles else DEFAULT_ROOF_UV_METRES
    model=Model(name,{**ALIASES,**recipe.PALETTE},roof_uv_metres=repeat_m)
    recipe.build(model)
    overrides=recipe.materials() if hasattr(recipe,"materials") else recipe.MATERIALS
    if tiles:
        roof_name = model.aliases["roof"]
        overrides = {**overrides, roof_name:build_tile_material(roof_name,RoofTiles(repeat_m*scale,tiles.palette))}
    objects=model.finish(build_materials(model,overrides))
    assert not any(o.name.startswith("ContinuousFoundation") for o in objects)
    checks = recipe.verify(objects) if hasattr(recipe,"verify") else {}
    for key,value in checks.items():
        if key.endswith("_m"):
            checks[key] = [round(m*scale,4) for m in value] if isinstance(value,list) else round(value*scale,4)
    checks["features"] = sorted(model.features)
    if tiles:
        checks["tile_roof"] = {"texture":"Roof_Slate", "repeat_m":round(repeat_m*scale,4), "palette":tiles.palette}
    return bpy.context.collection, checks, getattr(recipe,"CAMERA_DIRECTIONS",{})


def bring_church(scale):
    source = ROOT/"Art/RoleFacilities/CityV2_Facilities.blend"
    with bpy.data.libraries.load(str(source)) as (_, library):
        library.collections = ["Church"]
    collection = library.collections[0]
    collection.hide_render = False
    collection.hide_viewport = False
    bpy.context.scene.collection.children.link(collection)
    colors = {}
    for obj in collection.all_objects:
        if obj.type != "MESH":
            continue
        for index, material in enumerate(obj.data.materials):
            name = material.name
            if name not in colors:
                colors[name] = (build_tile_material(name,RoofTiles(2.2*1.3*scale,"Church_PatinaBlue"))
                                if name=="Roof_Slate" else city_surface(name,material))
            obj.data.materials[index] = colors[name]
    if not collection.objects.get("Church_Entrance"):
        entry = bpy.data.objects.new("Church_Entrance",None)
        boundary = min(v.co.y for o in collection.all_objects if o.type=="MESH" for v in o.data.vertices)
        entry.location = (0,boundary+.15,0)
        collection.objects.link(entry)
    return collection,{},{}


def build(name,args):
    bpy.ops.wm.read_homefile(use_factory_startup=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    scene=bpy.context.scene
    scene.name=name
    scene.unit_settings.system="METRIC"
    scene.unit_settings.scale_length=1
    collection,checks,directions = bring_church(args.scale) if name=="Church" else make_concept(name,args.scale)
    collection.name=name
    objects=list(collection.all_objects)
    root=bpy.data.objects.new(name,None)
    root.empty_display_size=.35
    collection.objects.link(root)
    sizing = Matrix.Scale(args.scale,4)
    for obj in objects:
        if not obj.parent:
            obj.parent=root
        if obj.type=="MESH":
            obj.data.transform(sizing)
            obj.data.update()
        else:
            obj.location *= args.scale
    bpy.context.view_layer.update()
    record=validate(objects)
    record.update(checks)
    entry = collection.objects.get(f"{name}_Entrance")
    record["entrance"] = [entry.location.x,entry.location.z,entry.location.y] if entry else None
    record["geometry_factor"] = args.scale
    scene["bounds_metres"]=record["bounds_m"]
    scene["geometry_factor"]=args.scale
    center=(Vector(record["lower"])+Vector(record["upper"]))*.5
    rotation=(-Vector(directions.get("three_quarter",VIEWS["three_quarter"]))).to_track_quat("-Z","Y")
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=="VIEW_3D":
                space=area.spaces.active
                space.region_3d.view_perspective="ORTHO"
                space.region_3d.view_location=center
                space.region_3d.view_rotation=rotation
                space.region_3d.view_distance=max(record["bounds_m"])*1.60
                space.shading.color_type="MATERIAL"
    bpy.ops.file.pack_all()
    record["packed_textures"]=sum(bool(i.packed_file) for i in bpy.data.images if i.source=="FILE")
    assert record["packed_textures"]>0
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(args.output/(name+".blend")),compress=True)
    (args.output/(name+".json")).write_text(json.dumps(record,indent=2)+"\n")
    print(f"CONCEPT {name}: {record['bounds_m']} m, {record['triangles']} triangles",flush=True)
    if args.views:
        render(scene,objects,args.views,args.output,name,directions)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("names",nargs="+",choices=MODEL_NAMES)
    parser.add_argument("--scale",type=float,default=FACILITY_SCALE,help="Uniform factor from the authored dimensions")
    parser.add_argument("--output",type=Path,default=ROOT/"Artifacts/RoleFacilityConceptChecks")
    parser.add_argument("--views",nargs="*",choices=list(VIEWS),default=["three_quarter"])
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    if not math.isfinite(args.scale) or args.scale<=0:
        parser.error("--scale must be positive and finite")
    args.output=args.output.resolve()
    args.output.mkdir(parents=True,exist_ok=True)
    for name in args.names:
        build(name,args)


if __name__=="__main__":
    main()
