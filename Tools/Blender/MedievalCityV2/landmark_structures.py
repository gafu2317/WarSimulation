"""Mills, fortifications and public landmarks with explicit structural assemblies."""
import math

from mathutils import Vector

from facility_buildings import arch, entrance, flag, identity, mass, pitched, steps


PI = math.pi


def watchtower(k):
    k.box('Watchtower stone foundation', (0, 0, .2), (4.8, 4.8, .4), 'Fieldstone')
    for xx in (-1.6, 1.6):
        for yy in (-1.6, 1.6):
            k.beam('Tapering timber watchtower leg', (xx, yy, .4), (xx * .85, yy * .85, 5.7), .25)
    for yy in (-1.6, 1.6):
        for direction in (-1, 1):
            k.beam('Tower frame diagonal', (-1.55 * direction, yy, .7), (1.4 * direction, yy * .9, 5.4), .16)
    for xx in (-1.6, 1.6):
        k.beam('Side tower frame brace', (xx, -1.5, .7), (xx * .88, 1.4, 5.4), .16)
    k.box('Lookout platform joists', (0, 0, 5.65), (4.2, 4.2, .28), 'DarkTimber')
    k.box('Lookout plank floor', (0, 0, 5.83), (4.4, 4.4, .16), 'Oak')
    for xx in (-1.9, 1.9):
        for yy in (-1.9, 1.9):
            k.box('Lookout roof corner post', (xx, yy, 6.95), (.16, .16, 2.24), 'DarkTimber')
    for xx in (-2.05, 2.05):
        k.box('Lookout low side screen', (xx, 0, 6.3), (.12, 4.2, .85), 'Oak')
    k.box('Lookout rear screen', (0, 2.05, 6.3), (4.1, .12, .85), 'Oak')
    for xx in (-1.45, 1.45):
        k.box('Lookout front railing', (xx, -2.05, 6.3), (1.25, .12, .85), 'Oak')
    k.roof('Covered watch platform roof', (0, 0, 8.08), 4.9, 4.9, 2.1, 'Roof_Slate', 'hip')
    for xx in (-.4, .4):
        k.beam('Lookout ladder rail', (xx, -4.4, .05), (xx, -2.05, 5.9), .09, 'Oak')
    for i in range(14):
        t=(i+.5)/14
        k.beam('Lookout ladder rung', (-.4, -4.4+t*2.35, .05+t*5.85), (.4, -4.4+t*2.35, .05+t*5.85), .07, 'Oak')
    k.box('Lookout front roof tie', (0, -1.9, 8), (4, .18, .18), 'DarkTimber')
    k.sign('Guard', (0, -2.18, 7.0), .85)
    identity(k, 'Braced timber watchtower on stone footing, accessible lookout ladder, railed observation deck and supported slate canopy')


def windmill(k):
    k.lathe('Tapered masonry windmill tower', (0, 0, 0), [(3.25, 0), (3.25, .4), (2.9, 1.2),
            (2.1, 8.2), (2.35, 8.2), (2.35, 8.55)], 'Plaster', 24)
    k.lathe('Windmill rotating roof cap', (0, 0, 8.55), [(2.7, 0), (2.2, 1.3), (.1, 3.0)], 'Thatch', 24)
    entrance(k, (0, -3.3, .05), 1.5, 2.55)
    k.window((0, -2.58, 4.3), .75, 1.55, 'Window_Amber', arched=True)
    axis=k.lathe('Wind shaft bearing', (0, 0, 0), [(.32, 0), (.32, 1.1)], 'Iron', 14)
    axis.rotation_euler.x=PI/2;axis.location=(0, -1.95, 7.6)
    k.ball('Windmill central hub', (0, -3.08, 7.6), .42, 'DarkTimber', segments=14, rings=8)
    for i in range(4):
        a=i*PI/2+.32
        k.beam('Windmill structural sail spar', (0,-3.15,7.6), (math.cos(a)*5,-3.15,7.6+math.sin(a)*5), .16)
        verts=[]
        for r,t in ((1.1,0),(4.9,0),(4.9,.88),(1.1,.88)):
            verts.append((math.cos(a)*r-math.sin(a)*t,-3.2,7.6+math.sin(a)*r+math.cos(a)*t))
        k.mesh('Tensioned linen windmill sail',verts,[(0,1,2,3)],'Linen',[(0,0),(0,1),(1,1),(1,0)])
        for j in range(6):
            r=1.25+j*.69
            k.beam('Sail cross batten',(math.cos(a)*r,-3.22,7.6+math.sin(a)*r),
                   (math.cos(a)*r-math.sin(a)*.88,-3.22,7.6+math.sin(a)*r+math.cos(a)*.88), .035, 'Oak')
    k.sign('Grain', (0, -2.86, 3.0), 1.1)
    for xx in (-3.7, 3.7):
        k.crate((xx, -1.9, 0), .95)
    identity(k, 'Tapering round masonry windmill with rotating thatch cap, shaft bearing, four framed linen sails and grain handling crates')


def watermill(k):
    mass(k, 'Mill processing hall', (0, 0, 0), 7, 9.5, 5.4, 'Plaster', 'Roof_Clay', 3.1)
    for xx in (-3.3, 0, 3.3):
        k.box('Mill front timber post',(xx,-4.8,2.7),(.2,.2,5.4),'DarkTimber')
    entrance(k, (0, -4.95, .1), 1.9, 3.1, 'DarkTimber')
    for xx in (-2.35, 2.35):
        k.window((xx,-4.85,3.6),.65,1.3,'Window_Amber')
    for xx in (3.45,6.15):
        k.box('Millrace masonry bank',(xx,0,.35),(.3,13,.7),'Fieldstone')
    k.horizontal('Flowing millrace water',(4.8,0,.18),2.4,13,'Water')
    for xx in (4.1,4.82):
        wheel=k.lathe('Open waterwheel side rim',(0,0,0),[(2.65,0),(2.65,.18),(2.25,.18),(2.25,0),(2.65,0)],'DarkTimber',24,caps=False)
        wheel.rotation_euler.y=PI/2;wheel.location=(xx,0,2.8)
    for i in range(16):
        a=i*PI/8;yy=math.cos(a)*2.45;zz=2.8+math.sin(a)*2.45
        paddle=k.box('Radial waterwheel paddle',(0,0,0),(.92,.32,.38),'Oak')
        paddle.rotation_euler.x=a-PI/2;paddle.location=(4.55,yy,zz)
        k.beam('Waterwheel spoke',(4.55,0,2.8),(4.55,yy,zz),.13,'Oak')
    k.beam('Waterwheel drive shaft',(3.2,0,2.8),(6.45,0,2.8),.26,'DarkTimber')
    k.box('External wheel bearing pier',(6.25,0,1.4),(.6,.9,2.8),'Fieldstone')
    k.sign('Grain',(0,-5.1,5.55),1.6)
    for xx in (-2.7,2.7):
        k.basket((xx,-5.7,0),.43)
    identity(k, 'Long timber mill hall with connected axle and radial paddle wheel, flowing stone-banked millrace and external bearing pier')


def curtain(k, p, length, h=4.4, side=False):
    x,y=p
    size=(.9,length,h) if side else (length,.9,h)
    k.box('Castle curtain wall',(x,y,h/2),size,'Fieldstone')
    k.box('Castle wall-walk coping',(x,y,h+.15),(1.15,length,.3) if side else (length,1.15,.3),'Limestone')
    count=max(2,round(length/1.4))
    for i in range(count):
        off=(i+.5)/count*length-length/2
        k.box('Curtain wall merlon',(x if side else x+off,y+off if side else y,h+.7),
              (1.05,.65,.85) if side else (.65,1.05,.85),'Limestone')


def castle(k):
    k.box('Royal castle paved precinct',(0,0,.05),(25,24,.1),'Cobble')
    mass(k,'Royal great keep',(0,4.8,.1),10,10.4,11.5,'Limestone','Roof_Slate',4)
    for xx in (-2.9,2.9):
        for zz in (4.1,8.0):
            k.window((xx,-.47,zz),1.3,2.4,'Window_Amber',arched=True)
    entrance(k,(0,-.5,.58),2.7,3.3)
    steps(k,(0,-2.1,.1),5,3,.16,.4)
    for xx in (-6.0,6.0):
        for yy in (.2,9.3):
            k.tower((xx,yy,.1),1.8,13.0,'Roof_Slate')
    for xx in (-8.8,8.8):
        mass(k,'Royal household range',(xx,4.0,.1),3.4,10.5,6.4,'Limestone','Roof_Slate',2)
    for xx in (-12.5,12.5):
        curtain(k,(xx,0),23,4.4,True)
        for yy in (-11,11):
            k.tower((xx,yy,0),1.9,7.1,'Roof_Slate',False)
    curtain(k,(0,11.5),25,4.4)
    for xx in (-7.75,7.75):
        curtain(k,(xx,-11.5),9.5,4.4)
    for xx in (-2.6,2.6):
        k.box('Royal gatehouse flanking pier',(xx,-11.5,3.5),(1.2,2.5,7),'Limestone')
    for yy in (-12.7,-10.3):
        arch(k,(0,yy,.1),4,5.2,.6,trim=.32)
    k.box('Royal gatehouse central chamber',(0,-11.5,6.25),(4,2.1,1.5),'Limestone')
    pitched(k,'Royal gatehouse ceremonial roof',(0,-11.5,7.0),7,3.2,2.3,'Roof_Slate')
    k.sign('Crown',(0,-13.35,6.4),2.0)
    for xx in (-3.9,3.9):
        flag(k,(xx,-11.6,4.2),'Canvas_Red',4)
    for xx in (-3.5,3.5):
        k.lamp((xx,-3.4,.1))
    identity(k,'Walled royal citadel; great keep with four corner spires, lower household ranges, open ceremonial gate, wall walks, defensive towers and large crown heraldry')


def arena(k):
    k.lathe('Arena sand floor',(0,0,0),[(6.1,0),(6.1,.08)],'Earth',32)
    profile=[(5.9,0),(5.9,.35)]
    for i in range(7):
        r=5.9+(i+1)*.62;z=.35+i*.44
        profile.extend([(r,z),(r,z+.44)])
    profile.extend([(10.3,3.43),(10.3,0),(5.9,0)])
    count=64;gap=.22;vertices=[]
    for r,z in profile:
        for i in range(count+1):
            a=-PI/2+gap+i/count*(2*PI-2*gap)
            vertices.append((math.cos(a)*r,math.sin(a)*r,z))
    stride=count+1
    faces=[(j*stride+i,j*stride+i+1,(j+1)*stride+i+1,(j+1)*stride+i)
           for j in range(len(profile)-1) for i in range(count)]
    faces.extend([tuple(j*stride for j in range(len(profile)-1)),
                  tuple(j*stride+count for j in reversed(range(len(profile)-1)))])
    k.mesh('Tiered stone spectator seating with entrance aisle',vertices,faces,'Limestone')
    for i in range(24):
        a=i*PI/12
        def bay():
            arch(k,(0,0,0),2.28,3.15,.48,'Fieldstone',trim=.24)
        from facility_buildings import rotated
        rotated(k,(math.cos(a)*10.5,math.sin(a)*10.5,0),a+PI/2,bay)
    k.lathe('Arena continuous crown parapet',(0,0,3.38),[(10.85,0),(10.85,.95),
            (10.3,.95),(10.3,0),(10.85,0)],'Limestone',48,caps=False)
    for xx in (-1.9,1.9):
        k.box('Arena ceremonial gate pier',(xx,-10.85,2.7),(.65,1.2,5.4),'Limestone')
        flag(k,(xx,-10.85,5.4),'Canvas_Red',2.3)
    k.box('Arena ceremonial gate frieze',(0,-10.85,5.0),(3.2,1.2,1.4),'Limestone')
    k.sign('Warrior',(0,-11.55,4.1),1.5)
    identity(k,'Open circular amphitheatre with actual perimeter arches, continuous crown walk, tiered spectator benches, entrance aisle, sand arena and heraldic gate')


RECIPES={'Watchtower':watchtower,'Windmill':windmill,'Watermill':watermill,'Royal_Castle':castle,'Arena':arena}


def build(k,name):
    recipe=RECIPES.get(name)
    if recipe is None:return False
    recipe(k)
    return True
