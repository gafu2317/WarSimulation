import bpy
import sys
import math
from pathlib import Path
from mathutils import Vector
sys.path.insert(0, str(Path(__file__).resolve().parent))
import generate_school_levels as s
ROOT = Path(__file__).resolve().parents[2]
PREFIX = 'School icon flag '

def add_flag(col, root, kind):
    for obj in list(col.objects):
        if obj.name.startswith(PREFIX):
            bpy.data.objects.remove(obj, do_unlink=True)
    # Replace the two older rooftop military standards with one clear school marker.
    for obj in list(col.objects):
        if obj.name.startswith('Ceremonial flagstaff'):
            bpy.data.objects.remove(obj, do_unlink=True)
    if kind == 'WarriorAcademy' and root.get('level') == 5:
        for obj in col.objects:
            if obj.type != 'MESH' or not obj.name.startswith(('Red school standards','Banner rail','White sword')):continue
            mesh=obj.data
            vs=[tuple(v.co) for v in mesh.vertices]
            fs=[tuple(p.vertices) for p in mesh.polygons if not all(abs(vs[i][0])>2.7 and vs[i][2]>12 for i in p.vertices)]
            mats=list(mesh.materials)
            mesh.clear_geometry();mesh.from_pydata(vs,[],fs);mesh.update()
    meshes = [o for o in col.objects if o.type == 'MESH']
    if kind == 'WarriorAcademy':
        anchor = next((o for o in meshes if o.name.startswith('Central command tower')), None)
        if anchor:
            z = max(v.co.z for v in anchor.data.vertices)
            x, y = 0, 2.3
        else:
            anchor = next(o for o in meshes if o.name.startswith('Slate hip roof'))
            z = max(v.co.z for v in anchor.data.vertices)
            x, y = 0, 0
    elif kind == 'SpiritAcademy':
        ridge = next(o for o in meshes if o.name.startswith('Roof ridge'))
        z = max(v.co.z for v in ridge.data.vertices)
        x, y = 0, 2.4 if root.get('level', 1) >= 2 else 0
    else:
        candidates = [o for o in meshes if o.name.startswith(('Lantern cap', 'Roof finial'))]
        p = max((v.co for o in candidates for v in o.data.vertices), key=lambda v:v.z)
        x, y, z = p.x, p.y, p.z - .12
    b = s.Builder('marker')
    b.beam(PREFIX+'mast', (x,y,z-.15), (x,y,z+4.25), .075, 'gold', 10)
    b.cylinder(PREFIX+'finial',x,y,z+4.25,.14,.24,'gold',12,.02)
    left, bottom = x+.12, z+1.2
    width, height = 3.6, 2.7
    color = {'WarriorAcademy':'red','ArcaneAcademy':'blue','SpiritAcademy':'green'}[kind]
    profile=[(left,bottom),(left+width,bottom),(left+width-.3,bottom+height/2),(left+width,bottom+height),(left,bottom+height)]
    b.prism(PREFIX+'cloth',profile,y,.055,color)
    for a,c in zip(profile, profile[1:]+profile[:1]):
        b.beam(PREFIX+'hem',(a[0],y,a[1]),(c[0],y,c[1]),.028,'gold')
    cx,cz=left+1.65,bottom+1.35
    for face in [-1,1]:
        yy=y+face*.065
        def line(points,r=.045,mat='white'):
            b.curve(PREFIX+'emblem',[(cx+a,yy,cz+c) for a,c in points],r,mat)
        def shape(points,mat='white'):
            b.prism(PREFIX+'emblem',[(cx+a,cz+c) for a,c in points],yy,.025,mat)
        if kind=='WarriorAcademy':
            shape([(-.83,.55),(.12,.55),(.08,-.2),(-.36,-.72),(-.79,-.2)],'gold')
            shape([(-.68,.4),(-.03,.4),(-.08,-.15),(-.36,-.49),(-.63,-.15)],'white')
            shape([(.43,-.5),(.59,-.5),(.59,.62),(.51,.94),(.43,.62)])
            line([(.25,-.38),(.77,-.38)],.07,'gold')
            line([(.51,-.48),(.51,-.86)],.07,'gold')
        elif kind=='ArcaneAcademy':
            for r in [.88,1.02]:line([(r*math.cos(i*math.tau/48),r*math.sin(i*math.tau/48)) for i in range(49)],.027)
            pts=[(.77*math.cos(math.pi/2+i*math.tau/5),.77*math.sin(math.pi/2+i*math.tau/5)) for i in range(5)]
            line([pts[i] for i in [0,2,4,1,3,0]],.04,'gold')
            for i in range(8):
                a=i*math.tau/8
                line([(1.06*math.cos(a),1.06*math.sin(a)),(1.17*math.cos(a),1.17*math.sin(a))],.03)
        else:
            for sign in [-1,1]:
                shape([(sign*.17,.1),(sign*.32,.6),(sign*.95,.81),(sign*.84,.22),(sign*.35,-.04)],'gold')
                shape([(sign*.2,-.05),(sign*.8,-.15),(sign*.64,-.61),(sign*.25,-.4)],'white')
            shape([(-.14,.15),(.14,.15),(.24,-.39),(0,-.9),(-.24,-.39)])
            shape([(.2*math.cos(i*math.tau/16),.43+.2*math.sin(i*math.tau/16)) for i in range(16)])
    for (role,mat),(vs,fs) in b.groups.items():
        mesh=bpy.data.meshes.new(role);mesh.from_pydata(vs,[],fs);mesh.update()
        obj=bpy.data.objects.new(role,mesh);col.objects.link(obj);obj.parent=root
        obj.data.materials.append(bpy.data.materials.get(mat))
    root['roof_icon']=kind

def main():
    for filename in ['FantasySchoolLevels.blend','SchoolLevelsPresentation.blend']:
        path=ROOT/'ArtSource/Blender'/filename
        bpy.ops.wm.open_mainfile(filepath=str(path))
        count=0
        for col in list(bpy.data.collections):
            if not any(col.name.startswith(k+'_Lv') for k in s.KINDS):continue
            root=next(o for o in col.objects if o.get('asset_root'))
            add_flag(col,root,next(k for k in s.KINDS if col.name.startswith(k)))
            count+=1
        assert count==15, count
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(path))
        print('FLAGGED', filename, count, flush=True)
        if filename=='FantasySchoolLevels.blend':
            scene=bpy.context.scene;scene.render.engine='BLENDER_WORKBENCH'
            scene.display.shading.light='STUDIO';scene.display.shading.color_type='MATERIAL'
            scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True
            scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
            camera=bpy.data.objects.new('Review camera',bpy.data.cameras.new('Review camera'));scene.collection.objects.link(camera);scene.camera=camera
            camera.data.type='ORTHO';camera.data.ortho_scale=39
            for kind in s.KINDS:
                col=bpy.data.collections[kind+'_Lv5'];root=next(o for o in col.objects if o.get('asset_root'))
                for other in bpy.data.collections:
                    if any(other.name.startswith(k+'_Lv') for k in s.KINDS):other.hide_render=other!=col
                target=root.location+Vector((0,0,11))
                camera.location=target+Vector((27,-48,28));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
                scene.render.filepath=str(ROOT/'docs/Art/FantasySchoolLevels'/f'{kind}_IconFlag.png')
                bpy.ops.render.render(write_still=True)
    

if __name__ == '__main__':
    main()
