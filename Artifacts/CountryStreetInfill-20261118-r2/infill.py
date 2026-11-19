"""Fill unused lots and connect the rear lanes in the second layout preview."""
import json
import math
from collections import Counter
from pathlib import Path
import sys

ROOT = Path.cwd()
OUT = Path(__file__).resolve().parent
SOURCE = OUT.with_name('CountryStreetInfill-20261118')
sys.path.insert(0, str(ROOT/'Tools/World'))
from build_country_redesign import Box, Layout, coords, points, rectangle, rotate, route_boxes

BEFORE = json.loads((SOURCE/'town_plan.json').read_text())
DIMENSIONS = json.loads((SOURCE/'dimensions.json').read_text())
ACADEMY = 'Spirit academy rear lane'
PALACE = 'Ballroom rear lane'
ACADEMY_Z = 141.05
PALACE_Z = 105.05
LANES = (
    (ACADEMY, 2.35, [(35.93,ACADEMY_Z),(69.3,ACADEMY_Z)]),
    (PALACE, 2.9, [(-29.5,107),(-29.5,PALACE_Z),(-9,PALACE_Z)]),
)
LOTS = (
    (ACADEMY, 'WorkshopHouse', 42, ACADEMY_Z, 180, .84, .49, 'Academy quarter'),
    (ACADEMY, 'Fantasy_House', 52.15, ACADEMY_Z, 180, .84, .36, 'Academy quarter'),
    (PALACE, 'WorkshopHouse', -23.8, PALACE_Z, 180, .84, .48, 'Entertainment quarter'),
    (PALACE, 'Fantasy_House', -14.4, PALACE_Z, 180, .84, .5, 'Entertainment quarter'),
    ('Royal ascent', 'WorkshopHouse', 51, 0, 270, .84, .58, 'Civic quarter'),
    ('Merchants gate road', 'Fantasy_House', 72, -34, 0, .84, 2.5, 'Market quarter'),
    ('Southern river quay', 'Fantasy_House', 33.5, 2, 180, .84, 1.6, 'Residential'),
    ('Library lane', 'Fantasy_House', 102.75, 123.5, 0, .84, .58, 'Academy quarter'),
    ('South east street', 'WorkshopHouse', 20.5, -76, 0, .84, .58, 'Craft and freight quarter'),
    ('South freight lane', 'WorkshopHouse', 79.5, -101.5, 180, .84, .58, 'Craft and freight quarter'),
)
POCKETS = (
    ('Arena west seats','rest',(-97.1,-96.2,7.4,6.4),[(-96.7,-91.36),(-96.7,-94.15)],(
        ('Tree_Street',-99.3,-96.35,0,.52), ('Bench',-97.8,-98.25,0,.91),
        ('Bench',-94.55,-96.15,90,.82), ('Flower_Border',-95.2,-98.0,0,.70),
        ('Water_Cask',-99.48,-98.26,0,.69))),
    ('Clinic lane seats','garden',(56.62,53.07,4.25,6.4),[(56.72,47.3),(56.72,51.07)],(
        ('Bench',55.3,53.18,270,.88), ('Planter',57.8,52.1,0,.81),
        ('Flower_Border',57.85,54.4,90,.82), ('Flower_Border',55.56,55.38,0,.73))),
    ('Museum quay seats','rest',(89.5,-3.65,9.2,4.25),[(89.5,2),(89.5,-2.7)],(
        ('Bench',87.47,-4.82,180,.83), ('Bench',91.49,-4.82,180,.83),
        ('Tree_Street',86.2,-3.28,0,.46), ('Planter',92.8,-3.15,0,.85),
        ('Flower_Border',89.5,-4.78,0,.73))),
)
LIFE = {'Well','Bench','Water_Trough','Covered_Wagon','Merchant_Wagon'}


def add_lot(layout, lot):
    street, model, station, line, yaw, scale, setback, zone = lot
    _, depth, _ = layout.size(model)
    dx,dz = rotate(0,layout.streets[street]['width']/2+depth*scale/2+setback,yaw)
    x,z = (line+dx,station) if dx else (station,line+dz)
    assert layout.building(model,x,z,street,zone,yaw=yaw,scale=scale,housing=True,width=1.52), lot
    return layout.plan['buildings'][-1].copy()


def add_pocket(layout, site):
    label, kind, (x,z,w,d), approach, items = site
    area = dict(name=label,x=x,z=z,width=w,depth=d,use=kind)
    shape = rectangle(area)
    assert layout.free(shape,.12) and not any(shape.overlaps(a,.16) for a in layout.courts), label
    route = dict(name=label,width=1.45,points=points(approach))
    for reservation in route_boxes(route):
        assert not any(reservation.overlaps(b,.05) for b in layout.building_shapes+layout.entrances+layout.protected), label
        layout.amenities.append(reservation)
    layout.plan['activityAreas'].append(area)
    layout.plan['greens'].append(area)
    layout.plan['amenityPaths'].append(route)
    for model,px,pz,yaw,scale in items:
        assert layout.prop(model,px,pz,label,yaw=yaw,scale=scale,inside=shape,clearance=.16), (label,model)
    return area


def keep_prop(layout, item, x, z):
    if not layout.prop(item['name'],x,z,item['zone'],yaw=item['yaw'],scale=item['scale'],
                       clearance=item.get('clearance',.24)):
        return False
    placed=layout.plan['props'][-1]
    layout.plan['props'][-1] = {**item,'x':placed['x'],'z':placed['z']}
    return True


def move_prop(layout,item):
    x,z = item['x'],item['z']
    for step in range(1,30):
        for i in range(24):
            angle = i*math.tau/24
            px,pz = x+.25*step*math.cos(angle), z+.25*step*math.sin(angle)
            if keep_prop(layout,item,px,pz):
                p = layout.plan['props'][-1]
                return dict(name=p['name'],zone=p['zone'],before=(x,z),after=(p['x'],p['z']))
    raise ValueError(f'No site near {item}')


def build():
    layout=Layout(BEFORE,DIMENSIONS)
    for route in BEFORE['roads']:
        layout.road(route['name'],route['width'],coords(route),kind=route['kind'],housing=route['frontage'])
    for label,width,line in LANES:
        layout.road(label,width,line,kind='lane',housing=True)
    layout.finish_streets()
    moved_buildings=[]
    for plot,path in zip(BEFORE['buildings'],BEFORE['accessPaths'],strict=True):
        if plot['name']=='SpiritAcademy_Lv5':
            moved_buildings.append(dict(name=plot['name'],before=(plot['x'],plot['z']),after=(plot['x'],plot['z']-.45)))
            plot={**plot,'z':plot['z']-.45}
        assert layout.building(plot['name'],plot['x'],plot['z'],plot['street'],plot['zone'],
                               yaw=plot['yaw'],scale=plot['scale'],housing=not path['paved'],
                               width=path['width'],label=plot.get('label')), plot
    for before, after in zip(BEFORE['buildings'],layout.plan['buildings'],strict=True):
        assert before==after or before['name']=='SpiritAcademy_Lv5', (before,after)
    for key in ('squares','greens','activityAreas','amenityPaths'):
        layout.plan[key] = BEFORE[key].copy()
    for i,route in enumerate(layout.plan['amenityPaths']):
        if route['name']=='Washing' and coords(route)[0][0]==35.6556:
            layout.plan['amenityPaths'][i] = {**route,'width':1.05,'points':points([(35.93,154.73),(35.93,ACADEMY_Z)])}
            break
    else:
        raise ValueError('The existing north washing path was not found.')
    layout.amenities = [box for route in layout.plan['amenityPaths'] for box in route_boxes(route)]
    layout.courts = [rectangle(area) for key in ('squares','greens','activityAreas') for area in BEFORE[key]]
    for area in route_boxes(layout.plan['amenityPaths'][i]):
        assert not any(area.overlaps(b,.06) for b in layout.building_shapes), area
    inserted=[add_lot(layout,lot) for lot in LOTS]
    pockets=[add_pocket(layout, site) for site in POCKETS]
    original_props=BEFORE['props']
    waiting=[item for item in original_props if not keep_prop(layout,item,item['x'],item['z'])]
    for item in waiting:
        if item['name'] in LIFE:
            raise ValueError(f'Moves an existing public furnishing: {item}')
    waiting.sort(key=lambda p:math.prod(layout.size(p['name'])[:2])*p['scale']**2,reverse=True)
    moved=[move_prop(layout,item) for item in waiting]
    for plot in inserted:
        zone=plot['zone']+' doorsteps'
        choices=('Firewood_Rack','Crate_Closed') if plot['name']=='WorkshopHouse' else ('Planter','Pottery_Jars')
        for side in (1,-1):
            for name in choices:
                w,d,_ = layout.size(name)
                dx,dz = rotate(side*(plot['width']/2-w*.34-.18),-plot['depth']/2-d*.34-.12,plot['yaw'])
                if layout.prop(name,plot['x']+dx,plot['z']+dz,zone,yaw=plot['yaw'],scale=.68,clearance=.11):
                    break
    layout.pave_frontages()
    layout.validate(BEFORE, BEFORE['counts']['houses']+len(inserted))
    keys=('roads','buildings','accessPaths','secondaryAccessPaths','frontages','props','counts',
          'squares','greens','activityAreas','amenityPaths')
    draft={**BEFORE,**{key:layout.plan[key] for key in keys}}
    return draft,dict(inserted=inserted,relocated=moved_buildings,areas=pockets,furnishings=moved)


if __name__ == '__main__':
    draft,changes=build()
    for name,data in (('town_plan.json',draft),('layout_changes.json',changes)):
        (OUT/name).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
    print(draft['counts'])
    print('Added lots',Counter(b['name'] for b in changes['inserted']))
    print('Moved props',len(changes['furnishings']),changes['furnishings'])
