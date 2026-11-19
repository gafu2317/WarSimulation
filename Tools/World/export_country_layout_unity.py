"""Transfer the approved Blender town meshes and placements to the Unity importer."""

from array import array
from collections import defaultdict
import gzip
import hashlib
import json
import math
from pathlib import Path
import struct
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from build_country_redesign import ROOT
from country_redesign_assets import bounds


MAGIC = 20261117
CUTOUTS = {'Weathering','Fern_Frond','Leaf_Card','Canopy_Card','Cypress_Card',
           'Flower_Card_Gold','Flower_Card_Pink','Grass_Tuft','Iron_Grate'}


def unity(vector):
    x,y,z = vector
    return [x,z,y]


def ancestry(socket):
    for link in socket.links:
        node = link.from_node
        yield node
        for entry in node.inputs:
            yield from ancestry(entry)


class Bundle:
    def __init__(self):
        self.materials = {}
        self.textures = {}
        self.geometry = []
        self.geometry_keys = {}
        self.prototypes = {}
        self.placements = []

    def image_ref(self,image):
        if image is None:
            return ''
        path = Path(bpy.path.abspath(image.filepath,library=image.library)).resolve()
        if path.is_file() and path.is_relative_to(ROOT/'Assets'):
            return str(path.relative_to(ROOT))
        if not image.packed_file:
            raise ValueError(f'Town texture is missing from the project: {image.name}')
        data = image.packed_file.data
        key = 'packed/'+hashlib.sha256(data).hexdigest()[:24]
        self.textures.setdefault(key,data)
        return key

    def material(self,material):
        if material in self.materials:
            return self.materials[material][0]
        shader = next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        inputs = shader.inputs
        color_chain = list(ancestry(inputs['Base Color']))
        color_node = next((node for node in color_chain if node.type=='TEX_IMAGE'),None)
        normal_chain = list(ancestry(inputs['Normal']))
        normal_node = next((node for node in normal_chain if node.type=='TEX_IMAGE'),None)
        bump = next((node.inputs['Strength'].default_value for node in normal_chain if node.type=='NORMAL_MAP'),0)
        base = self.image_ref(color_node.image) if color_node else ''
        normal = self.image_ref(normal_node.image) if normal_node else ''
        roof = 'tile_repeat_metres' in material
        tone = any(node.type=='RGBTOBW' for node in color_chain)
        opacity = 1.0
        for node in material.node_tree.nodes:
            if node.type=='MIX_SHADER' and any(link.from_node.type=='BSDF_TRANSPARENT' for link in node.inputs[1].links):
                opacity = max(.12,node.inputs[0].default_value)
        colors = {}
        if roof:
            top = color_chain[0]
            mid = top.inputs[1].links[0].from_node
            colors = {'roofShade':list(mid.inputs[1].default_value),
                      'roofEdge':list(top.inputs[2].default_value),
                      'color':list(mid.inputs[2].default_value)}
        elif tone:
            colors['color'] = list(material.diffuse_color)
        elif color_node is None:
            colors['color'] = list(inputs['Base Color'].default_value)
        else:
            colors['color'] = [1,1,1,1]
        cutout = any(Path(base).name.startswith(name+'_BaseColor.') for name in CUTOUTS)
        result = dict(name=material.name,base=base,normal=normal,bump=bump,roof=roof,tone=tone,
                      roughness=inputs['Roughness'].default_value,metallic=inputs['Metallic'].default_value,
                      emission=inputs['Emission Strength'].default_value,opacity=opacity,cutout=cutout,**colors)
        index = len(self.materials)
        self.materials[material] = index,result
        return index

    def mesh(self,parts):
        vertices = array('f')
        indices = {}
        by_material = defaultdict(lambda: array('I'))
        for obj,transform in parts:
            if obj.type!='MESH' or obj.hide_render:
                continue
            mesh = obj.data
            normal_matrix = transform.to_3x3().inverted().transposed()
            coordinates = [tuple(unity(transform@v.co)) for v in mesh.vertices]
            normals = [tuple(unity((normal_matrix@n.vector).normalized())) for n in mesh.corner_normals]
            uvs = mesh.uv_layers.active
            material_ids = [self.material(slot.material) for slot in obj.material_slots]
            reverse = transform.to_3x3().determinant()>0
            mesh.calc_loop_triangles()
            for triangle in mesh.loop_triangles:
                target = by_material[material_ids[triangle.material_index]]
                for i in (reversed(triangle.loops) if reverse else triangle.loops):
                    coord = coordinates[mesh.loops[i].vertex_index]
                    uv = tuple(uvs.uv[i].vector) if uvs else (0,0)
                    vertex = coord+normals[i]+uv
                    slot = indices.get(vertex)
                    if slot is None:
                        indices[vertex] = slot = len(indices)
                        vertices.extend(vertex)
                    target.append(slot)
        if not indices:
            raise ValueError('No visible polygons in town model')
        vertex_bytes = vertices.tobytes()
        submeshes = [values.tobytes() for values in by_material.values()]
        # The residential roof colors share the same vertex and index data.
        key = hashlib.sha256(vertex_bytes+b''.join(submeshes)+repr([len(s) for s in submeshes]).encode()).digest()
        if key not in self.geometry_keys:
            self.geometry_keys[key] = len(self.geometry)
            self.geometry.append((dict(vertices=len(indices),indices=[len(data)//4 for data in submeshes]),
                                  [vertex_bytes,*submeshes]))
        return self.geometry_keys[key],list(by_material)

    def prototype(self,key,parts,marker=None):
        if key not in self.prototypes:
            geometry,materials = self.mesh(parts)
            self.prototypes[key] = dict(geometry=geometry,materials=materials,entrance=marker)

    def instance(self,obj,name,group,kind):
        collection = obj.instance_collection
        key = collection.name
        marker = next((unity(entry.matrix_world.translation) for entry in collection.all_objects
                       if entry.type=='EMPTY' and entry.name.split('.')[0].endswith('_Entrance')),None)
        self.prototype(key,((mesh,mesh.matrix_world) for mesh in collection.all_objects),marker)
        self.placements.append(dict(name=name,key=key,group=group,kind=kind,position=unity(obj.location),
                                    scale=unity(obj.scale),yaw=math.degrees(-obj.rotation_euler.z)))

    def part(self,obj,name,group,offset=0):
        key = 'site/'+obj.name
        matrix = Matrix.Translation(Vector((0,0,offset))) @ obj.matrix_world
        self.prototype(key,[(obj,matrix)])
        self.placements.append(dict(name=name,key=key,group=group,kind='site',
                                    position=[0,0,0],scale=[1,1,1],yaw=0))

    def save(self,output,plan):
        data = dict(materials=[value for _,value in self.materials.values()],
                    textures=[dict(key=k,size=len(v)) for k,v in self.textures.items()],
                    geometry=[record for record,_ in self.geometry],
                    prototypes=[dict(key=k,**p) for k,p in self.prototypes.items()],
                    placements=self.placements,plan=json.dumps(plan,ensure_ascii=False,separators=(',',':')))
        encoded = json.dumps(data,separators=(',',':'),allow_nan=False).encode()
        with output.open('wb') as file:
            with gzip.GzipFile(fileobj=file,filename='',mode='wb',compresslevel=4,mtime=0) as stream:
                stream.write(struct.pack('<II',MAGIC,len(encoded)))
                stream.write(encoded)
                for raw in self.textures.values():
                    stream.write(raw)
                for _,buffers in self.geometry:
                    for buffer in buffers:
                        stream.write(buffer)


def center(obj):
    low,high = bounds(obj.instance_collection)
    origin = (obj.matrix_world @ Vector(((low[0]+high[0])/2,(low[1]+high[1])/2,low[2])))
    return origin.xy


def export(plan,output):
    bpy.context.view_layer.update()
    scene = bpy.context.scene
    bundle = Bundle()
    buildings = defaultdict(list)
    for obj in scene.objects:
        if obj.get('model'):
            buildings[obj['model']].append((center(obj),obj))
    for b in plan['buildings']:
        model = b.get('modernModel',b['name'])
        at = Vector((b['x'],b['z']))
        match = min(buildings[model],key=lambda item:(item[0]-at).length)
        assert (match[0]-at).length<.002,(model,b)
        buildings[model].remove(match)
        bundle.instance(match[1],b['name'],'Buildings and courtyards/'+b['zone'],'building')
    assert not any(buildings.values())
    props = defaultdict(list)
    for obj in scene.collection.children['Market, wells, seats and planting'].objects:
        props[obj.name.split('.')[0]].append((center(obj),obj))
    for p in plan['props']:
        at = Vector((p['x'],p['z']))
        match = min(props[p['name']],key=lambda item:(item[0]-at).length)
        assert (match[0]-at).length<.002,(p,match)
        props[p['name']].remove(match)
        bundle.instance(match[1],p['name'],'Market, yards and street life/'+p['zone'],'prop')
    assert not any(props.values())

    direct = {
        'Town ground and paved streets':'Connected streets and entrance paths',
        'Fortifications':'Complete city wall and gates',
        'River, bridge landings and working quay':'Market, yards and street life/Central river, millrace and working quay',
        'Public entrances and landings':'Public entrances and landings',
    }
    for origin,group in direct.items():
        for obj in scene.collection.children[origin].objects:
            name = obj.name.split('.')[0]
            if name in {'Surrounding meadow','Central river'}:
                continue
            if obj.instance_type=='COLLECTION':
                bundle.instance(obj,name,group,'site')
            elif name=='Town soil':
                bundle.part(obj,'New town earth','',offset=.016)
            else:
                bundle.part(obj,name,group)
    output.parent.mkdir(parents=True,exist_ok=True)
    bundle.save(output,plan)
    print(f'Exported {len(bundle.placements)} placements, {len(bundle.prototypes)} models, '
          f'{len(bundle.geometry)} meshes and {len(bundle.materials)} materials.')


if __name__ == '__main__':
    plan,output = map(Path,sys.argv[sys.argv.index('--')+1:])
    export(json.loads(plan.read_text()),output.resolve())
