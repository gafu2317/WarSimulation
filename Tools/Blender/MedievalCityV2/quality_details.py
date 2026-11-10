"""Working joints, container walls and equipment for the existing city kit."""
import math
import bpy
from mathutils import Vector

SUPPORT_ASSETS = {
    'Crate_Closed','Crate_Open','Vegetable_Crate','Fish_Crate','Barrel','Bench','Clothesline',
    'Grain_Sack','Sack_Stack','Covered_Wagon','Merchant_Wagon','Handcart','Wheelbarrow',
    'Basket_Empty','Produce_Basket','Bread_Basket','Rolled_Fabric','Shield_Rack','Weapon_Rack',
    'Fountain','Bollard','Forge','Street_Brazier','Water_Trough','Well','Firewood_Rack',
    'Training_Dummy','Stone_Stairs','Wooden_Stairs','Dock_Section','Wooden_Bridge','Fence_Gate','Road_Cross',
    'Fantasy_House','MerchantHouse','WorkshopHouse','Outhouse','Watchtower','Windmill','Watermill',
    'Cloth_Stall','Produce_Stall','Fish_Stall','Bread_Stall','Spice_Stall','Flower_Stall',
    'Butcher_Stall','Potion_Stall','Book_Stall','Weapon_Stall'
}


def plank(k,name,p,size,grain,mat='Oak'):
    obj=k.box(name,p,size,mat);obj['grain_axis']=grain
    return obj


def crate(k,p=(0,0,0),size=1,closed=True,contents=None):
    x,y,z=p;w=size;h=.8*size
    for i in range(4):
        plank(k,'Crate bottom board',(x+(i-1.5)*w/4,y,z+.045),(w/4-.008,w*.8,.09),1)
    for xx in (-1,1):
        for yy in (-1,1):plank(k,'Crate inside corner post',(x+xx*(w/2-.06),y+yy*(w*.4-.06),z+h/2),(.075,.075,h),2,'DarkTimber')
    for row in range(4):
        zz=z+(row+.5)*h/4
        for side in (-1,1):
            plank(k,'Crate long side board',(x,y+side*w*.4,zz),(w,.07,h/4-.008),0)
            plank(k,'Crate short side board',(x+side*w*.5,y,zz),(.07,w*.8,h/4-.008),1)
    if closed:
        for i in range(4):plank(k,'Crate lid board',(x+(i-1.5)*w/4,y,z+h),(w/4-.008,w*.8,.07),1)
        for side in (-1,1):k.beam('Crate nailed diagonal brace',(x-w*.43,y+side*w*.445,z+.09),(x+w*.43,y+side*w*.445,z+h-.09),.075,'DarkTimber')
    if contents:k.horizontal('Contents texture',(x,y,z+h+.005),w*.9,w*.7,contents)


def basket(k,p=(0,0,0),r=.45,contents=None):
    x,y,z=p
    k.lathe('Woven basket body',p,[(r*.65,0),(r,.55),(r,.61),(r*.86,.61),(r*.55,.08)],'Wicker',16)
    for row in range(7):
        height=.06+row*.08;radius=r*(.65+.35*height/.55)
        k.lathe('Horizontal wicker braid',(x,y,z+height),[(radius,.0),(radius+.018,.02),(radius,.04)],'Wicker',16,caps=False)
    for i in range(18):
        a=i*math.tau/18
        k.beam('Basket upright weave',(x+math.cos(a)*r*.68,y+math.sin(a)*r*.68,z+.025),
               (x+math.cos(a)*r*.97,y+math.sin(a)*r*.97,z+.57),.018,'Wicker')
    if contents:k.horizontal('Basket goods',(x,y,z+.6),r*1.36,r*1.36,contents)


def forge(k,p=(0,0,0),scale=1):
    x,y,z=p;s=scale
    k.box('Hearth masonry base',(x,y,z+.18*s),(1.5*s,1*s,.36*s),'Brick')
    for side in (-1,1):k.box('Hearth sidewall',(x+side*.64*s,y,z+.73*s),(.22*s,1*s,.75*s),'Brick')
    k.box('Hearth fireback',(x,y+.40*s,z+.73*s),(1.1*s,.2*s,.75*s),'Brick')
    k.box('Recessed fire bed',(x,y,z+.42*s),(1.05*s,.72*s,.1*s),'Coal')
    for i in range(9):k.ball('Hot forge fuel',(x+(i%3-1)*.24*s,y+(i//3-1)*.2*s,z+.51*s),.12*s,'Glowing_Coals' if i%3 else 'Coal',segments=8,rings=4)
    k.lathe('Open iron smoke hood',(x,y,z+1.1*s),[(.85*s,0),(.35*s,1*s),(.35*s,2*s),(.28*s,2*s),(.28*s,1.7*s)],'Iron',16,caps=False)
    for side in (-1,1):k.beam('Hood supporting iron strap',(x+side*.66*s,y+.33*s,z+.8*s),(x+side*.60*s,y+.33*s,z+1.35*s),.07*s,'Iron')


def weapon_rack(k,p=(0,0,0),swords=False):
    x,y,z=p
    for xx in (-.9,.9):
        plank(k,'Rack upright',(x+xx,y,z+1),(.12,.16,2),2,'DarkTimber')
        plank(k,'Rack braced foot',(x+xx,y,z+.06),(.28,.50,.12),1,'DarkTimber')
    for zz in (.32,1.6):plank(k,'Rack support rail',(x,y,z+zz),(2.1,.16,.15),0)
    for xx in (-.6,0,.6):
        if swords:
            vertices=[(x+xx+u,y-.12+v,z+h) for u,v,h in [(-.055,0,.48),(0,-.025,.48),(.055,0,.48),(-.055,0,1.53),(0,-.025,1.53),(.055,0,1.53),(0,0,.31)]]
            k.mesh('Forged blade with central ridge',vertices,[(0,3,4,1),(1,4,5,2),(0,1,6),(1,2,6),(2,5,3,0)],'Iron')
            k.beam('Sword crossguard',(x+xx-.18,y-.12,z+1.55),(x+xx+.18,y-.12,z+1.55),.055,'Brass')
            k.beam('Wrapped sword grip',(x+xx,y-.12,z+1.59),(x+xx,y-.12,z+1.84),.065,'Leather')
            k.ball('Sword pommel',(x+xx,y-.12,z+1.89),.065,'Iron',segments=10,rings=6)
            for side in (-1,1):k.beam('Rack weapon retaining peg',(x+xx+side*.10,y,z+1.57),(x+xx+side*.10,y-.20,z+1.57),.035,'Iron')
        else:
            outline=[(-.25,.72),(0,.56),(.25,.72),(.25,1.38),(0,1.45),(-.25,1.38)]
            vertices=[(x+xx+a,y+dep,z+b) for dep in (-.18,-.10) for a,b in outline]
            n=len(outline)
            k.mesh('Solid curved shield board',vertices,[tuple(range(n)),tuple(reversed(range(n,n*2)))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],'Oak')
            for a,b in zip(outline,outline[1:]+outline[:1]):k.beam('Shield bound metal rim',(x+xx+a[0],y-.19,z+a[1]),(x+xx+b[0],y-.19,z+b[1]),.035,'Iron')
            boss=k.lathe('Shield boss',(0,0,0),[(.09,0),(.12,.025),(.08,.09),(.01,.12)],'Iron',12)
            boss.rotation_euler.x=math.pi/2;boss.location=(x+xx,y-.18,z+1.04)
            k.beam('Shield hanging strap',(x+xx-.08,y-.05,z+1.35),(x+xx+.08,y-.05,z+1.64),.025,'Leather')


def finish(k,name):
    objects=list(k.COLLECTION.objects)
    for obj in objects:
        if obj.type!='MESH':continue
        if name in ('Fountain','Bollard','Stone_Stairs') or any(word in obj.name for word in ('window sill','stone reveal','Window jamb','Window lintel','Entrance threshold','Tower cornice')):
            for slot in obj.material_slots:
                if slot.material and slot.material.name in ('Limestone','Fieldstone'):slot.material=k.material('Carved_Limestone')
        if name in ('Fantasy_House','MerchantHouse','WorkshopHouse','Outhouse','Watchtower','Windmill','Watermill') and obj.name.startswith(('Painted shop sign','Sign bracket')):
            bpy.data.objects.remove(obj,do_unlink=True)
    if name=='Clothesline':
        for obj in list(k.COLLECTION.objects):
            if obj.name.startswith('Linen washing'):bpy.data.objects.remove(obj,do_unlink=True)
        for x in (-1,0,1):
            vertices=[(x+(i/8-.5)*.7,-.018+math.sin(i/8*math.tau*2)*.025*row,2.4-row*1.3) for row in (0,1) for i in range(9)]
            k.mesh('Folded hanging linen',vertices,[(i,i+1,i+10,i+9) for i in range(8)],'Linen')
            for dx in (-.23,.23):plank(k,'Washing wooden peg',(x+dx,-.015,2.40),(.035,.075,.12),2)
    if name in ('Covered_Wagon','Merchant_Wagon','Handcart'):
        w,d=(1.5,2.2) if name=='Handcart' else (2.1,4)
        for side in (-1,1):
            for zz in (1.0,1.28,1.57):plank(k,'Wagon exterior horizontal rail',(side*(w/2+.025),0,zz),(.045,d,.055),1,'DarkTimber')
            for yy in (-d*.39,d*.39):plank(k,'Wagon side stake',(side*(w/2+.04),yy,1.25),(.055,.10,.75),2,'DarkTimber')
        if name=='Handcart':plank(k,'Cart resting prop',(0,-d*.36,.4),(.12,.12,.8),2,'DarkTimber')
        if name=='Covered_Wagon':
            for yy in (-d/2,d*.25,0,-d*.25,d/2):
                for i in range(20):
                    a=i*math.pi/20;b=(i+1)*math.pi/20
                    k.beam('Canvas supporting bentwood hoop',(math.cos(a)*w*.535,yy,1.60+math.sin(a)*1.06),(math.cos(b)*w*.535,yy,1.60+math.sin(b)*1.06),.045,'DarkTimber')
    if name in ('Grain_Sack','Sack_Stack'):
        for x,y,z in ([(0,0,0)] if name=='Grain_Sack' else [(-.4,0,0),(.4,.1,0),(0,.1,.8)]):
            k.lathe('Sack mouth binding',(x,y,z+1.12),[(.09,0),(.09,.045)],'Leather',12)
            k.beam('Sack tie ends',(x,y-.095,z+1.13),(x+.08,y-.12,z+1.0),.025,'Linen')
    if name=='Rolled_Fabric':
        for x in (-.4,0,.4):
            for i in range(56):
                a=i*.34;b=(i+1)*.34;ra=.025+i*.0026;rb=.025+(i+1)*.0026
                k.beam('Rolled cloth spiral edge',(x+math.cos(a)*ra,math.sin(a)*ra,1.452),(x+math.cos(b)*rb,math.sin(b)*rb,1.452),.008,'Linen')
    if name=='Street_Brazier':
        for i in range(10):
            a=i*2.4;r=.12+(i%3)*.07
            k.ball('Brazier fuel',(math.cos(a)*r,math.sin(a)*r,.83),.10,'Coal' if i%2 else 'Glowing_Coals',segments=8,rings=4)
    if name=='Dock_Section':
        for obj in list(k.COLLECTION.objects):
            if obj.name.startswith('Dock deck'):bpy.data.objects.remove(obj,do_unlink=True)
        for i in range(14):plank(k,'Dock individual plank',(0,(i-6.5)*5/14,.8),(3,5/14-.014,.2),0)
        for xx in (-1.3,1.3):plank(k,'Dock longitudinal support',(xx,0,.65),(.18,4.4,.20),1,'DarkTimber')
    if name=='Fence_Gate':
        for zz in (.42,1.10):
            k.box('Gate hinge strap',(-1.38,-.065,zz),(.35,.035,.07),'Iron')
        k.box('Gate closing latch',(1.25,-.065,.95),(.34,.04,.055),'Iron')
    if name=='Firewood_Rack':
        for obj in list(k.COLLECTION.objects):
            if obj.name.startswith('Split firewood'):
                for sign in (0,1):
                    disc=k.lathe('Split firewood cut end',(0,0,0),[(.143,0),(.143,.012)],'Oak',6)
                    disc.rotation_euler=obj.rotation_euler;disc.location=obj.location+Vector((1.70*sign-.006,0,0))
    if name=='Bench':
        for obj in list(k.COLLECTION.objects):
            if obj.name.startswith('Bench back') and 'support' not in obj.name:bpy.data.objects.remove(obj,do_unlink=True)
        for zz in (.78,1.07):plank(k,'Bench back board',(0,.22,zz),(2,.1,.24),0)
    if name=='Well':
        k.lathe('Well shadowed water',(0,0,.20),[(.76,0),(.76,.01)],'Water',24)
    if name=='Water_Trough':
        for obj in k.COLLECTION.objects:
            if obj.name.startswith(('Trough rim','Trough end')):
                for slot in obj.material_slots:slot.material=k.material('Carved_Limestone')
