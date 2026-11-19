"""Build outdoor town furnishings from the CityV2 materials."""

import bpy

from materials import ALIASES, build_materials
from mesh import Model


def trestle(m, width, height, depth):
    for x in (-width*.37,width*.37):
        for z in (-depth*.39,depth*.39):
            m.beam((x,.035,z),(x*.96,height-.12,z*.67),.12,'timber')
        m.box((x,.38,0),(.16,.12,depth*.74),'timber')
    m.box((0,.38,0),(width*.75,.11,.11),'timber')
    for i in range(4):
        m.box((0,height-.038,depth*((i+.5)/4-.5)),(width,.076,depth/4-.009),'wood')


def outdoor_table(m):
    trestle(m,1.92,.82,.95)
    for x,z in ((-.56,.15),(.59,-.14),(.12,.26)):
        m.lathe(x,.82,z,[(.076,0),(.088,.015),(.091,.14),(.064,.14),(.061,.024)],
                'pottery',n=12)
    m.cylinder((-.20,.834,-.17),.17,.028,'wood',n=16)
    m.ellipsoid((-.20,.90,-.17),(.127,.074,.083),'canvas',n=10,rings=5)


def workbench(m):
    trestle(m,1.86,1.02,.78)
    m.box((.63,1.057,-.24),(.24,.11,.28),'metal')
    m.box((.63,1.13,-.25),(.30,.07,.08),'steel')
    m.box((.62,1.13,-.02),(.30,.07,.08),'steel')
    m.beam((.44,1.035,-.44),(.80,1.035,-.44),.034,'steel')
    m.box((-.29,1.069,.13),(.78,.10,.28),'wood')
    m.beam((-.30,1.12,-.23),(.02,1.12,-.10),.034,'wood')
    m.box((-.30,1.142,-.25),(.10,.063,.054),'steel')


def stool(m):
    m.lathe(0,0,0,[(.26,.47),(.31,.50),(.30,.55),(.28,.56)],'wood',n=14)
    for x,z in ((-.25,.14),(.25,.14),(0,-.27)):
        m.beam((x,.035,z),(x*.63,.49,z*.63),.075,'timber')


def stand(m, span, height):
    for x in (-span/2,span/2):
        m.box((x,.055,0),(.14,.11,.78),'timber')
        m.beam((x,.08,0),(x,height+.08,0),.10,'wood')
        for z in (-.26,.26):
            m.beam((x,.11,z),(x,.40,0),.055,'timber')
    m.beam((-span/2-.09,height,0),(span/2+.09,height,0),.077,'wood')


def herb_stand(m):
    stand(m,1.86,1.77)
    for x,depth in ((-.57,.44),(-.18,.33),(.21,.47),(.58,.38)):
        m.beam((x,1.74,0),(x,1.22,0),.025,'ivory')
        for dx,y,z in ((-.07,-.03,.035),(.053,.04,-.036),(.008,-.13,-.018)):
            m.ellipsoid((x+dx,1.33+y,z),(.105,depth*.42,.078),'leaf' if dx>0 else 'leaflight',
                        n=9,rings=5)
        m.cylinder((x,1.49,0),.055,.035,'canvas',n=10)


def cloth_display(m):
    stand(m,1.95,1.91)
    for x,w,length,material in ((-.56,.56,.96,'red'),(.03,.53,1.21,'blue'),(.61,.56,.84,'canvas')):
        for side in (-1,1):
            outer = [(.0,0),(.11,.085),(length*.83,.13),(length,.155)]
            for (a,az),(b,bz) in zip(outer,outer[1:]):
                m.face([(x-w/2,1.951-a,side*az),(x+w/2,1.951-a,side*az),
                        (x+w/2,1.951-b,side*bz),(x-w/2,1.951-b,side*bz)],material)
            m.box((x,1.958-length,side*.155),(w,.037,.015),'ivory')


def water_cask(m):
    for x in (-.49,.49):
        m.box((x,.09,0),(.22,.18,1.10),'timber')
        for z in (-.33,.33):
            m.box((x,.28,z),(.16,.38,.14),'timber')
    for r,h,x,mat in ((.45,1.35,0,'wood'),(.48,.77,0,'wood'),(.486,.069,-.32,'metal'),
                      (.486,.069,.32,'metal'),(.457,.061,-.60,'metal'),(.457,.061,.60,'metal')):
        m.cylinder((x,.73,0),r,h,mat,n=24,axis=(1,0,0))
    m.beam((.68,.51,0),(.83,.51,0),.054,'bronze')
    m.beam((.83,.51,0),(.83,.41,0),.06,'bronze')
    m.beam((.76,.55,-.075),(.76,.55,.075),.039,'wood')


def rope_coil(m):
    for index in range(4):
        radius = .34-index*.067
        m.ring(0,0,radius-.026,radius+.026,.026,.081,'ivory',n=28)
    m.ring(-.095,.025,.197,.248,.08,.13,'canvas',n=28)
    m.beam((.23,.105,.10),(.51,.041,.26),.060,'ivory')
    m.beam((.51,.041,.26),(.57,.041,.46),.055,'ivory')


def garden_bed(m):
    for side in (-1,1):
        m.box((side*1.17,.14,0),(.13,.28,1.31),'wood')
        m.box((0,.14,side*.59),(2.35,.28,.12),'wood')
        for x in (-1.11,1.11):
            m.box((x,.164,side*.54),(.11,.328,.11),'timber')
    m.box((0,.14,0),(2.23,.23,1.06),'soil')
    for row,z in enumerate((-.31,.28)):
        for i in range(6):
            x = -.91+i*.36+(row-.5)*.065
            for number,dx in enumerate((-.085,0,.08)):
                m.ellipsoid((x+dx,.32+.045*(number==1),z+.052*(number-1)),
                            (.093,.095+.035*(number==1),.098),'leaflight' if i%3==0 else 'leaf',
                            n=8,rings=5)


def chopping_block(m):
    m.lathe(0,0,0,[(.40,0),(.37,.07),(.30,.53),(.32,.58)],'bark',n=13)
    m.cylinder((0,.58,0),.303,.035,'wood',n=13)
    m.box((.015,.61,-.04),(.26,.055,.33),'wood',yaw=.22)
    m.beam((.06,.64,-.11),(.47,.89,-.19),.044,'timber')
    m.box((.45,.88,-.19),(.12,.085,.14),'steel')


RECIPES = {
    'Outdoor_Table': outdoor_table, 'Yard_Workbench': workbench, 'Three_Legged_Stool': stool,
    'Herb_Drying_Stand': herb_stand, 'Cloth_Display': cloth_display, 'Water_Cask': water_cask,
    'Rope_Coil': rope_coil, 'Raised_Garden_Bed': garden_bed, 'Chopping_Block': chopping_block,
}


def make_prop(name):
    model = Model(name,{**ALIASES,'pottery':'Terracotta'})
    model.feature(name)
    RECIPES[name](model)
    objects = model.finish(build_materials(model,{}))
    collection = bpy.data.collections.new(f'Town prop {name}')
    for obj in objects:
        parents = tuple(obj.users_collection)
        collection.objects.link(obj)
        for parent in parents:
            parent.objects.unlink(obj)
    return collection
