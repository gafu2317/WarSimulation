"""Build the editable town scene and render the model placement before Unity import."""

import argparse
import json
import math
import os
from pathlib import Path
import re
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0,str(Path(__file__).resolve().parent))
from build_country_redesign import BRIDGES, HOMES, Box, coords, rectangle, route_boxes
from country_redesign_assets import CONCEPT, MODEL_NAMES, bounds, load
from materials import TEXTURES, city_surface, surface
from preview import sun
from roof_tiles import PALETTES, RoofTiles, build_tile_material


HOME_PALETTES = ('Terracotta','FiredClay','RoseClay','Russet','Sienna','WeatheredClay',
                 'MutedBrick','WarmUmber','SmokedClay','SmokeSlate','DustSlate','StoneGrey')
FACILITY_PALETTES = {'Royal_Castle':'Castle_RoyalBlue','Chapel':'Chapel_Pewter',
                     'Bathhouse':'Bathhouse_Copper','Barracks':'Barracks_Brick','Guildhall':'Guildhall_Bronze',
                     'Bakery':'Bakery_Ochre','Warehouse':'Warehouse_Tin','Stable':'Stable_Walnut',
                     'Granary':'Granary_Clay','Market_Hall':'MarketHall_Moss'}
VIEWS = {
    '01_city': ((0,27,6),(.26,-1,.76),465),
    '02_avenue': ((-1,21,5),(.18,-1,.87),151),
    '03_academy': ((65,107,6),(-.25,-1,1.13),169),
    '04_entertainment': ((50,-50,5),(-.40,-1,1.04),166),
    '05_residential': ((-72,98,5),(-.62,-.82,.94),133),
    '06_market_and_yards': ((-40,-31,3),(.38,-1,.91),116),
    '07_school_gate': ((-76,84,4),(.26,-1,.93),62),
    '08_granary_and_bakery': ((-91,-26,2),(.33,-1,.72),44),
    '09_east_neighborhood': ((86,76,4),(-.38,-1,1.13),91),
    '10_workshop_entrance': ((-85.6,-41.2,2),(-.47,1,.95),39),
    '11_west_workyards': ((-102,83.5,1.7),(.91,-.64,.88),49),
}


def group(name):
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    return collection


def mesh(name, vertices, faces, materials, collection, uv_scale=2.6, material_indices=None):
    geometry = bpy.data.meshes.new(name)
    geometry.from_pydata(vertices,[],faces)
    geometry.update()
    for material in materials:
        geometry.materials.append(material)
    if material_indices:
        for face,index in zip(geometry.polygons,material_indices):
            face.material_index = index
    uv = geometry.uv_layers.new().uv
    for i,loop in enumerate(geometry.loops):
        x,y,z = geometry.vertices[loop.vertex_index].co
        uv[i].vector = (x/uv_scale,(y+.45*z)/uv_scale)
    obj = bpy.data.objects.new(name,geometry)
    collection.objects.link(obj)
    return obj


def polygon(name, outline, height, material, collection, uv_scale=3.6):
    return mesh(name,[(x,y,height) for x,y in outline],[tuple(range(len(outline)))],[material],collection,uv_scale)


def flat_box(name, box, height, material, collection):
    return polygon(name,[(box.left,box.south),(box.right,box.south),(box.right,box.north),(box.left,box.north)],
                   height,material,collection)


def palette_for(record):
    name = record['name']
    if name in FACILITY_PALETTES:
        return FACILITY_PALETTES[name]
    if name in HOMES:
        value = 2166136261
        for code in [*map(ord,name),round(record['x']*10),round(record['z']*10)]:
            value = ((value ^ code)*16777619) & 0xffffffff
        return HOME_PALETTES[value%len(HOME_PALETTES)]
    return None


class Assets:
    def __init__(self, concept_dir):
        self.collections = {}
        self.materials = {}
        self.concept_dir = concept_dir

    def collection(self, name, palette=None):
        key = name,palette
        if key in self.collections:
            return self.collections[key][0]
        if name in MODEL_NAMES:
            model = load(name,link=True,concept_dir=self.concept_dir)
        elif palette and (name,None) in self.collections:
            source = self.collections[name,None][0]
            model = bpy.data.collections.new(f'{source.name} · {palette}')
            for obj in source.all_objects:
                if obj.type != 'MESH':
                    continue
                clone = obj.copy()
                clone.parent = None
                clone.matrix_world = obj.matrix_world.copy()
                model.objects.link(clone)
        else:
            model = load(name)
            self.clean(name,model)
        if name not in MODEL_NAMES:
            self.shade(model,palette)
        self.collections[key] = model,bounds(model)
        return model

    def clean(self, name, collection):
        if name == 'Granite_Gate':
            for obj in collection.all_objects:
                if obj.type != 'MESH':
                    continue
                remove = [i for i,mat in enumerate(obj.data.materials) if mat and 'Iron_Grate' in mat.name]
                if remove:
                    editable = bmesh.new()
                    editable.from_mesh(obj.data)
                    faces = [f for f in editable.faces if f.material_index in remove]
                    bmesh.ops.delete(editable,geom=faces,context='FACES')
                    editable.to_mesh(obj.data)
                    editable.free()

    def shade(self, collection, palette):
        for obj in collection.all_objects:
            if obj.type != 'MESH':
                continue
            for i,slot in enumerate(obj.material_slots):
                if not slot.material:
                    continue
                origin = obj.data.materials[i] or slot.material
                name = re.sub(r'\.\d{3}$','',origin.name)
                is_roof = name.startswith('Roof_')
                choice = palette if is_roof and palette and (PALETTES/f'{palette}_Roof_Slate.mat').is_file() else None
                key = name,choice
                if key not in self.materials:
                    if choice:
                        material = build_tile_material(f'{choice} · {name}',RoofTiles(2.2,choice))
                    else:
                        material = city_surface(name,origin)
                    self.materials[key] = material
                slot.link = 'OBJECT'
                slot.material = self.materials[key]

    def place(self, name, label, x, y, z, yaw, scale, collection, palette=None):
        if palette and name in HOMES and (name,None) not in self.collections:
            self.collection(name)
        model = self.collection(name,palette)
        lower,upper = self.collections[name,palette][1]
        center = Vector(((lower[0]+upper[0])/2,(lower[1]+upper[1])/2,lower[2]))
        if isinstance(scale,(float,int)):
            scale = (scale,)*3
        rotation = Matrix.Rotation(math.radians(-yaw),3,'Z')
        offset = rotation @ Vector([a*b for a,b in zip(center,scale)])
        instance = bpy.data.objects.new(label,None)
        instance.instance_type = 'COLLECTION'
        instance.instance_collection = model
        instance.empty_display_size = .2
        instance.rotation_euler.z = math.radians(-yaw)
        instance.location = Vector((x,y,z))-offset
        instance.scale = scale
        collection.objects.link(instance)
        return instance

    def dimensions(self, name):
        self.collection(name)
        low,high = self.collections[name,None][1]
        return tuple(b-a for a,b in zip(low,high))


def furnishings(assets,plan):
    props = group('Market, wells, seats and planting')
    for record in plan['props']:
        name = record['name']
        scale = record['scale']
        if name.startswith('Tree_'):
            scale *= {'Tree_Shade':9,'Tree_Street':7,'Tree_AlleyCypress':8}[name]/assets.dimensions(name)[2]
        assets.place(name,name,record['x'],record['z'],record['y'],record['yaw'],scale,props)


def buildings(assets,plan):
    districts = {zone:group(zone) for zone in dict.fromkeys(b['zone'] for b in plan['buildings'])}
    access = group('Public entrances and landings')
    for record in plan['buildings']:
        name = record.get('modernModel',record['name'])
        yaw = record['yaw']
        scale = record['scale']
        palette = palette_for(record)
        obj = assets.place(name,record.get('label',name),record['x'],record['z'],0,yaw,scale,districts[record['zone']],palette)
        obj['model'] = name
        obj['entrance_street'] = record['street']
        low,high = assets.collections[name,palette][1]
        measured = [(b-a)*scale for a,b in zip(low,high)]
        assert abs(measured[0]-record['width'])<.013 and abs(measured[1]-record['depth'])<.013,(name,measured,record)
        if name in {'WarriorAcademy_Lv5','Granary','Warehouse'}:
            approach_works(name,obj,access)


def approach_works(name, instance, collection):
    rotation = instance.rotation_euler.to_matrix()
    ratio = instance.instance_collection.get('layout_reference_ratio',1)

    def world(x,y,z):
        return instance.location+rotation @ Vector((x*instance.scale.x,y*instance.scale.y,z*instance.scale.z))*ratio

    if name == 'WarriorAcademy_Lv5':
        stone = surface('School yard path',source='Earth',color='c5b28d',roughness=.91,relief=.13)
        outline = [(-2.18,-12.95),(-1.06,-12.95),(-1.06,.76),(-.76,1.06),(.26,1.06),
                   (.56,1.36),(.56,2.58),(-.56,2.58),(-.80,2.18),(-1.86,2.18),(-2.18,1.86)]
        mesh('Training yard passage', [world(x,y,.110) for x,y in outline],
             [tuple(range(len(outline)))],[stone],collection,3.2)
        return

    oak = city_surface('Oak',None)

    def timber(name,x0,x1,y0,y1,z0,z1):
        vertices = [world(x,y,z) for z in (z0,z1) for x,y in ((x0,y0),(x1,y0),(x1,y1),(x0,y1))]
        faces = [(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
        mesh(name,vertices,faces,[oak],collection,1.45)

    if name == 'Warehouse':
        timber('Warehouse loading tread',-1.64,1.64,-7.48,-7.025,.22,.40)
        return

    for i in range(5):
        x = -1.12+i*.448
        timber('Granary landing plank',x,x+.443,-4.976,-4.34,1.20,1.32)
    for x in (-.96,.96):
        timber('Granary landing support',x-.085,x+.085,-4.79,-4.60,.22,1.20)


def resolve_textures():
    shared = {}
    for image in list(bpy.data.images):
        if image.library or image.packed_file or image.source!='FILE' or not image.filepath:
            continue
        path = Path(bpy.path.abspath(image.filepath))
        if not path.is_file():
            path = TEXTURES/path.name
        assert path.is_file(), image.filepath
        path = path.resolve()
        color_space = 'sRGB' if path.name.endswith('_BaseColor.png') else 'Non-Color'
        if path in shared:
            image.user_remap(shared[path])
            bpy.data.images.remove(image)
        else:
            image.filepath = str(path)
            image.colorspace_settings.name = color_space
            shared[path] = image


def paved_surfaces(plan, ground, materials):
    # Subdivide every planar overlap once so paving and crossings share a single visible surface.
    layers = []
    layers.extend((rectangle(square),0) for square in plan['squares'])
    layers.extend((rectangle(green),1) for green in plan['greens'])
    accessible = plan['accessPaths']+plan.get('secondaryAccessPaths',[])
    layers.extend((box,4 if path['paved'] else 2) for path in accessible for box in route_boxes(path))
    layers.extend((box,3) for path in plan['frontages'] for box in route_boxes(path))
    layers.extend((box,2 if path.get('gravel') else 4) for path in plan['amenityPaths'] for box in route_boxes(path))
    river = plan['riverZ']
    for route in plan['roads']:
        priority = 5 if route['avenue'] else 3 if route['kind']=='lane' else 4
        for box in route_boxes(route):
            if route['name'] in BRIDGES:
                if box.south < river-4.7:
                    layers.append((Box(box.left,box.south,box.right,river-4.7),priority))
                if box.north > river+4.7:
                    layers.append((Box(box.left,river+4.7,box.right,box.north),priority))
            else:
                layers.append((box,priority))
    x_edges = sorted({round(value,5) for box,_ in layers for value in (box.left,box.right)})
    vertices,faces,slots = [],[],[]
    for l,r in zip(x_edges,x_edges[1:]):
        x = (l+r)/2
        spans = [(box,slot) for box,slot in layers if box.left < x < box.right]
        z_edges = sorted({round(v,5) for box,_ in spans for v in (box.south,box.north)})
        for s,n in zip(z_edges,z_edges[1:]):
            z = (s+n)/2
            visible = max((slot for box,slot in spans if box.south < z < box.north),default=-1)
            if visible == -1:
                continue
            i = len(vertices)
            vertices.extend(((l,s,.028),(r,s,.028),(r,n,.028),(l,n,.028)))
            faces.append((i,i+1,i+2,i+3))
            slots.append(visible)
    mesh('Continuous stone streets and courts',vertices,faces,materials,ground,2.55,slots)


def collision_mesh(collection):
    vertices,faces = [],[]
    for obj in collection.all_objects:
        if obj.type != 'MESH':
            continue
        start = len(vertices)
        vertices.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
        faces.extend(tuple(start+i for i in face.vertices) for face in obj.data.polygons)
    return BVHTree.FromPolygons(vertices,faces)


def walls(assets,plan):
    masonry = group('Fortifications')
    model = assets.collection('Granite_Gate')
    tree = collision_mesh(model)
    low,high = assets.collections['Granite_Gate',None][1]
    half = min(abs(low[0]),abs(high[0]))
    opening = next(i*half/300 for i in range(1,300)
                   if any(tree.ray_cast((side*i*half/300,low[1]-1,low[2]+1.15),(0,1,0),high[1]-low[1]+2)[0]
                          is not None for side in (-1,1))) * 2
    opening -= .02
    assert opening > 2, opening
    base = assets.dimensions('Granite_Straight')
    water_scale = (plan['riverWidth']+.48)/opening
    cuts = [(0,-112,plan['gates'][0]['width']/opening*half),
            (112,-34,plan['gates'][1]['width']/opening*half),
            (-112,plan['riverZ'],half*water_scale),(112,plan['riverZ'],half*water_scale)]
    wall = coords({'points':plan['wallOutline']})
    for a,b in zip(wall,wall[1:]+wall[:1]):
        dx,dy = b[0]-a[0],b[1]-a[1]
        length = math.dist(a,b)
        ux,uy = dx/length,dy/length
        cuts_here = []
        for x,y,extent in cuts:
            at = (x-a[0])*ux+(y-a[1])*uy
            if 0<at<length and abs((x-a[0])*uy-(y-a[1])*ux)<.05:
                cuts_here.append((at-extent,at+extent))
        runs = zip([0,*[right for _,right in sorted(cuts_here)]],[*[left for left,_ in sorted(cuts_here)],length])
        for start,end in runs:
            sections = math.ceil((end-start)/5.5)
            step = (end-start)/sections
            for i in range(sections):
                along = start+step*(i+.5)
                assets.place('Granite_Straight','Curtain wall',a[0]+along*ux,a[1]+along*uy,-.06,
                             -math.degrees(math.atan2(dy,dx)),(step/base[0],1.28/base[1],5.6/base[2]),masonry)
    for gate in plan['gates']:
        x,y,yaw,width = (gate[k] for k in ('x','z','yaw','width'))
        assets.place('Granite_Gate','Open town gate',x,y,0,yaw,
                     (width/opening,2.6/(high[1]-low[1]),6.5/(high[2]-low[2])),masonry)
    for x in (-112,112):
        assets.place('Granite_Gate','River watergate',x,plan['riverZ'],-.48,90,
                     (water_scale,1.4/(high[1]-low[1]),5.6/(high[2]-low[2])),masonry)
    for t in plan['watchtowers']:
        w,d,h = assets.dimensions('Watchtower')
        assets.place('Watchtower','Watchtower',t['x'],t['z'],0,0,(t['width']/w,t['depth']/d,1),masonry)


def bridges(assets,plan,water,paving):
    river = group('River, bridge landings and working quay')
    center = plan['riverZ']
    half = plan['riverWidth']/2
    flat_box('Central river',Box(-650,center-half,650,center+half),.012,water,river)
    checks = {}
    banks = {s:[] for s in (-1,1)}
    for route in (r for r in plan['roads'] if r['name'] in BRIDGES):
        width = route['width']
        x = coords(route)[0][0]
        name = 'Wooden_Bridge' if route['name']=='Residential bridge crossing' else 'Stone_Bridge'
        model = assets.collection(name)
        low,high = assets.collections[name,None][1]
        cx,cy = ((a+b)/2 for a,b in zip(low[:2],high[:2]))
        depth = high[1]-low[1]
        tree = collision_mesh(model)

        def height(sample_x,sample_y):
            hit = tree.ray_cast((sample_x,sample_y,high[2]+1),(0,0,-1))[0]
            return hit.z if hit is not None else None

        samples = [(side,cy+side*(depth/2-.14)) for side in (-1,1)]
        inside = []
        for side,deck_y in samples:
            mid = height(cx,deck_y)
            assert mid is not None,name
            step = (high[0]-low[0])/200
            radius = next((i*step for i in range(1,100)
                           if any((z:=height(cx+sign*i*step,deck_y)) is None or abs(z-mid)>.05
                                  for sign in (-1,1))), (high[0]-low[0])/2)
            inside.append((side,deck_y,mid,2*(radius-step)))
        clear = min(s[3] for s in inside)
        assert clear>1,name
        sx = width/clear
        sy = 9.4/depth
        sz = (1.9 if name=='Stone_Bridge' else 1.36)/(high[2]-low[2])
        base = -.04
        assets.place(name,route['name'],x,center,base,0,(sx,sy,sz),river)
        for side,deck_y,mid,_ in inside:
            end = center+(deck_y-cy)*sy
            bank = end + side*2.05
            high_z = base+(mid-low[2])*sz + .014
            v = [(x-width/2,bank,.029),(x+width/2,bank,.029),
                 (x+width/2,end,high_z),(x-width/2,end,high_z)]
            mesh('Gradual bridge landing',v,[(0,1,2,3) if side==-1 else (3,2,1,0)],[paving],river)
            checks.setdefault(route['name'],dict(clear_width_m=width,bank_join_m=[]))['bank_join_m'].append(round(bank,3))
            banks[side].append((x-width/2-.9,x+width/2+.9))
    banks[-1].append((31.1,39.9))
    banks[1].append((-74,-60))
    w,d,h = assets.dimensions('Stone_Wall')
    for side in (-1,1):
        openings = sorted(banks[side])
        for left,right in zip([-111,*[b for a,b in openings]],[*[a for a,b in openings],111]):
            count = max(1,math.ceil((right-left)/4.8))
            step = (right-left)/count
            for i in range(count):
                assets.place('Stone_Wall','Quay retaining edge',left+step*(i+.5),center+side*half,-.31,
                             0,(step/w,.47/d,.54/h),river)
    dock = assets.collection('Dock_Section')
    low,high = assets.collections['Dock_Section',None][1]
    w,d,_ = assets.dimensions('Dock_Section')
    ray = collision_mesh(dock)
    landing = ray.ray_cast(((high[0]+low[0])/2,low[1]+.12,high[2]+1),(0,0,-1))[0]
    assert landing is not None, 'Landing has no walking surface'
    for x in (33.4,37.6):
        assets.place('Dock_Section','River landing',x,5.40,.029-landing.z+low[2],0,(4.2/w,3.3/d,1),river)
    return checks


def stage(plan, concept_dir):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.name = 'Country · New town layout'
    scene.unit_settings.system = 'METRIC'
    ground = group('Town ground and paved streets')
    meadow = surface('Meadow',source='Grass',color='697e4e',relief=.10)
    earth = surface('Town earth',source='Earth',color='a88e69',roughness=.92,relief=.10)
    court = surface('Packed courts',source='Earth',color='b2a084',roughness=.87,relief=.12)
    lawn = surface('Garden grass',source='Grass',color='728a53',roughness=.90,relief=.14)
    foot = surface('Frontage gravel',source='Earth',color='b8ac94',roughness=.9,relief=.18)
    lane = surface('Lane stone',source='Cobble',color='938e80',roughness=.87,relief=.22)
    streets = surface('District street stone',source='Cobble',color='a29e91',roughness=.86,relief=.25)
    avenue = surface('Avenue stone',source='Cobble',color='c1bbab',roughness=.81,relief=.25)
    water = surface('River water',source='Water',color='467e89',roughness=.31,relief=.22,metallic=.1)
    flat_box('Surrounding meadow',Box(-800,-800,800,800),-.055,meadow,ground)
    polygon('Town soil',coords({'points':plan['wallOutline']}),-.018,earth,ground,5.2)
    paved_surfaces(plan,ground,[court,lawn,foot,lane,streets,avenue])
    assets = Assets(concept_dir)
    buildings(assets,plan)
    furnishings(assets,plan)
    walls(assets,plan)
    checks = bridges(assets,plan,water,avenue)
    resolve_textures()
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes['Background']
    background.inputs['Color'].default_value = (.60,.69,.80,1)
    background.inputs['Strength'].default_value = .57
    sun(scene,'Late morning sun',(-.72,-.9,1.67),2.65,(1,.92,.78))
    sun(scene,'Sky reflection',(.7,.46,1),.42,(.78,.87,1))
    camera = bpy.data.objects.new('Layout review camera',bpy.data.cameras.new('Layout review camera'))
    scene.collection.objects.link(camera)
    camera.data.type = 'ORTHO'
    camera.data.clip_end = 1800
    scene.camera = camera
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 18
    scene.cycles.adaptive_threshold = .065
    scene.cycles.use_denoising = True
    scene.view_settings.view_transform = 'AgX'
    scene.render.resolution_x,scene.render.resolution_y = 2532,1170
    scene.render.resolution_percentage = 100
    return scene,checks


def frame(camera, target, direction, width):
    axis = Vector(direction).normalized()
    camera.location = Vector(target)+axis*460
    camera.rotation_euler = (-axis).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale = width


def save(scene,path):
    frame(scene.camera,*VIEWS['01_city'])
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                view = area.spaces.active
                view.region_3d.view_rotation = scene.camera.rotation_euler.to_quaternion()
                view.region_3d.view_location = VIEWS['01_city'][0]
                view.region_3d.view_distance = 285
                view.region_3d.view_perspective = 'ORTHO'
                view.shading.color_type = 'MATERIAL'
    path.parent.mkdir(parents=True,exist_ok=True)
    for datablock in (*bpy.data.libraries,*bpy.data.images):
        if datablock.library is None and getattr(datablock,'filepath',''):
            file = Path(bpy.path.abspath(datablock.filepath))
            if file.is_file():
                datablock.filepath = '//'+os.path.relpath(file,path.parent)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True,relative_remap=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plan',type=Path)
    parser.add_argument('output',type=Path)
    parser.add_argument('--save',type=Path)
    parser.add_argument('--concept-dir',type=Path,default=CONCEPT)
    parser.add_argument('--views',nargs='*',choices=VIEWS,default=list(VIEWS))
    parser.add_argument('--samples',type=int,default=18)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    plan = json.loads(args.plan.read_text())
    scene,checks = stage(plan,args.concept_dir.resolve())
    args.output.mkdir(parents=True,exist_ok=True)
    (args.output/'structure_checks.json').write_text(json.dumps(checks,indent=2)+'\n')
    if args.save:
        save(scene,args.save.resolve())
    scene.cycles.samples = args.samples
    scene.render.use_persistent_data = len(args.views)>1
    for view in args.views:
        frame(scene.camera,*VIEWS[view])
        scene.render.filepath = str((args.output/f'{view}.png').resolve())
        bpy.ops.render.render(write_still=True)


if __name__ == '__main__':
    main()
