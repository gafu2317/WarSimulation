"""Arrange street fronts and occupied lots for the compact town review."""
import json
import math
from collections import Counter
from pathlib import Path
import sys

ROOT = Path.cwd()
sys.path.insert(0, str(ROOT/'Tools/World'))
from build_country_redesign import Layout, Box, coords, rectangle, route_boxes, rotate

WORK = ROOT/'Artifacts/CountryStreetInfill-20261118'
ORIGINAL = json.loads((ROOT/'Art/CountryLayout/town_plan.json').read_text())
DIMENSIONS = json.loads((WORK/'dimensions.json').read_text())

LANES = (
    ('Smithy cross lane', 3.4, [(-48,-75.5),(0,-75.5)]),
    ('Clinic garden lane', 3.2, [(0,47.3),(62,47.3)]),
)
FACILITY_POSITIONS = {
    'Tavern': (-69.15, -22.08),
    'Clinic': (25.7, 35.52),
    'CrimsonRowhouse': (26.2, -20.28),
    'LanternSpire': (95.1, 34.11),
    'Blacksmith': (-30.2, -58.12),
    'Guardhouse': (-25.8, -94.25),
    'SpiritAcademy_Lv5': (47.1, 120.11),
    'VelvetTerrace': (-31.25, 86.0),
}
COMMERCIAL_STREETS = {
    'Market west street', 'Western market lane', 'Merchants gate road',
    'South gate high street', 'West houses street', 'Smithy cross lane',
    'South freight lane', 'Eastern roof lane', 'East gate lane',
}
HOUSING = {'Fantasy_House', 'MerchantHouse', 'WorkshopHouse'}
NAMES = {'Fantasy_House':'住宅','MerchantHouse':'商家','WorkshopHouse':'工房付き住宅'}
LIFE = {'Bench','Well','Water_Trough','Covered_Wagon','Merchant_Wagon','Stationary_Planter'}


def place(layout, building, entry, at=None):
    x,z=at if at else (building['x'],building['z'])
    return layout.building(building['name'],x,z,building['street'],building['zone'],
                           yaw=building['yaw'],scale=building['scale'],housing=not entry['paved'],
                           width=entry['width'],label=building.get('label'))


def prop_box(layout, prop):
    w,d,h=layout.size(prop['name'])
    scale=prop['scale']
    if prop['name'] in ('Tree_Shade','Tree_Street','Tree_AlleyCypress'):
        scale*= {'Tree_Shade':9,'Tree_Street':7,'Tree_AlleyCypress':8}[prop['name']]/h
    return Box.around(prop['x'], prop['z'], w*scale, d*scale, prop['yaw'])


def build():
    layout=Layout(ORIGINAL,DIMENSIONS)
    for road in ORIGINAL['roads']:
        layout.road(road['name'],road['width'],coords(road),kind=road['kind'],housing=road['frontage'])
    for label,width,positions in LANES:
        layout.road(label,width,positions,kind='lane',housing=True)
    layout.finish_streets()

    protected_areas=[rectangle(p) for p in ORIGINAL['squares']+ORIGINAL['greens']+ORIGINAL['activityAreas']]
    pedestrian=[box for path in ORIGINAL['amenityPaths'] for box in route_boxes(path)]
    old_pairs=list(zip(ORIGINAL['buildings'],ORIGINAL['accessPaths'],strict=True))
    source_props=[(prop,prop_box(layout,prop)) for prop in ORIGINAL['props']]
    keep_props=[box for prop,box in source_props if prop['name'] in LIFE or prop['name'].startswith('Tree_')
                or prop['name'].endswith('_Stall') or any(box.overlaps(a) for a in protected_areas)]
    moved_facilities=[]
    pending=[]
    for index,(b,path) in enumerate(old_pairs):
        if not path['paved']:
            continue
        current=(b['x'],b['z'])
        target=FACILITY_POSITIONS.get(b['name'],current)
        shape=rectangle({**b,'x':target[0],'z':target[1]})
        if target!=current and any(shape.overlaps(x,.16) for x in protected_areas+pedestrian+keep_props):
            target=current
        try:
            place(layout,b,path,target)
        except ValueError:
            if target==current:
                raise
            place(layout,b,path,current)
            target=current
        if target!=current:
            moved_facilities.append({'name':b['name'],'label':b['label'],'before':current,'after':target})

    for index,(b,path) in enumerate(old_pairs):
        if path['paved']:
            continue
        if not place(layout,b,path):
            pending.append((index,b))

    layout.courts=protected_areas
    layout.amenities=pedestrian
    # Shared yards and established trees stay in their plots when adding frontage.
    layout.protected.extend(keep_props)
    slots=[]
    for road in layout.plan['roads']:
        if not road['frontage'] and road['name']!='Academy garden lane':
            continue
        for a,b in zip(coords(road),coords(road)[1:]):
            horizontal=a[1]==b[1]
            lo,hi=sorted((a[0],b[0]) if horizontal else (a[1],b[1]))
            fixed=a[1] if horizontal else a[0]
            for side in (1,-1):
                yaw=(0 if side==1 else 180) if horizontal else (90 if side==1 else 270)
                for quarter in range(math.ceil((lo+2.6)*4),math.floor((hi-2.6)*4)+1,2):
                    station=quarter/4
                    row=(road,horizontal,fixed,station,yaw,side,lo,hi)
                    slots.append(row)

    def lot(row, name, scale, setback):
        road,horizontal,fixed,station,yaw,side,lo,hi=row
        w,d,_=layout.size(name)
        half=w*scale/2
        if station-half<lo+.7 or station+half>hi-.7:
            return None
        across=fixed+side*(road['width']/2+d*scale/2+setback)
        x,z=(station,across) if horizontal else (across,station)
        box=Box.around(x,z,2*half,d*scale,yaw)
        if (not box.inside(layout.wall) or box.overlaps(layout.water,.5)
            or any(box.overlaps(a,.65) for a in layout.building_shapes)
            or any(box.overlaps(a,.22) for a in layout.streets_flat)
            or any(box.overlaps(a,.24) for a in layout.courts+layout.amenities+layout.protected)
            or any(box.overlaps(a,.12) for a in layout.entrances)):
            return None
        return x,z,box

    def add(row, name, scale, setback):
        chosen=lot(row,name,scale,setback)
        if chosen is None:
            return False
        x,z,box=chosen
        zone='Market quarter' if name=='MerchantHouse' else 'Craft and freight quarter' if name=='WorkshopHouse' else 'Residential'
        return layout.building(name,x,z,row[0]['name'],zone,yaw=row[4],scale=scale,
                               housing=True,width=1.55 if name!='Fantasy_House' else 1.45)

    def distance(row, x, z):
        _,horizontal,fixed,station,_,_,_,_=row
        edge=(station,fixed) if horizontal else (fixed,station)
        return math.dist(edge,(x,z))

    relocated=[]
    for index, house in pending:
        subset=sorted(slots,key=lambda row:distance(row,house['x'],house['z']))
        for row in subset:
            commercial=row[0]['name'] in COMMERCIAL_STREETS
            if row[0]['name']=='Clinic garden lane' and house['x']<45 and add(row,'WorkshopHouse',.84,.60):
                current=layout.plan['buildings'][-1]
                relocated.append({'name':house['name'],'before':(house['x'],house['z']),
                                  'after':(current['x'],current['z']),'model':current['name']})
                break
            wanted=('WorkshopHouse',.86) if commercial and house['z']<-60 else (
                    ('MerchantHouse',.87) if commercial else ('Fantasy_House',.91))
            if any(add(row,name,size,.60) for name,size in (wanted,('Fantasy_House',.84))):
                current=layout.plan['buildings'][-1]
                relocated.append({'name':house['name'],'before':(house['x'],house['z']),
                                  'after':(current['x'],current['z']),'model':current['name']})
                break
        else:
            raise ValueError(f'No street frontage for existing home {house}')

    inserted=[]
    shop_fronts=(
        ('Market west street',-54.75,0,'MerchantHouse',.86),
        ('Merchants gate road',29.75,180,'MerchantHouse',.86),
        ('East river street',79.25,0,'MerchantHouse',.86),
        ('Southern river quay',-67.25,180,'MerchantHouse',.86),
        ('Southern river quay',16.25,180,'WorkshopHouse',.84),
        ('Smithy cross lane',-34.25,0,'WorkshopHouse',.84),
        ('Clinic garden lane',26.75,0,'WorkshopHouse',.84),
        ('Academy garden lane',119.5,270,'WorkshopHouse',.84),
        ('South freight lane',68.25,180,'WorkshopHouse',.84),
    )
    for street,station,face,model,scale in shop_fronts:
        nearest=sorted((row for row in slots if row[0]['name']==street and row[4]==face and abs(row[3]-station)<2.1),
                       key=lambda row:abs(row[3]-station))
        if any(add(row,model,scale,.58) for row in nearest):
            inserted.append(layout.plan['buildings'][-1].copy())
        else:
            print('Skipped shop site',street,station)
    courtyard_focus=[
        (-69,-21,43), (22,37,37), (-30,-73,35), (29,-41,32),
        (90,41,30), (46,111,33), (90,-63,26), (-30,87,29),
        (-82,117,26),
    ]
    for ii,(x,z,radius) in enumerate(courtyard_focus):
        rows=[row for row in slots if distance(row,x,z)<radius]
        rows.sort(key=lambda row:distance(row,x,z) + (.7 if row[0]['kind']=='avenue' else 0))
        wanted=4 if ii in (0,1,2,3,5) else 3
        for row in rows:
            road=row[0]['name']
            if road in COMMERCIAL_STREETS:
                first=('WorkshopHouse',.86) if (ii in (2,3,6) and len(inserted)%3!=2) else ('MerchantHouse',.89)
            elif ii==5 and len(inserted)%4==0:
                first=('MerchantHouse',.90)
            else:
                first=('Fantasy_House',.96 if len(inserted)%3==0 else .91)
            if any(add(row,name,size,.58) for name,size in (first,('Fantasy_House',.84))):
                inserted.append(layout.plan['buildings'][-1].copy())
                wanted-=1
                if wanted<=0:
                    break

    layout.protected=layout.protected[:-len(keep_props)]
    layout.courts=[]
    # Existing courtyards already have their own documented placements and paths.
    shifted=[]
    blocked=[]
    def furnish(prop,x,z):
        return layout.prop(prop['name'],x,z,prop['zone'],yaw=prop['yaw'],scale=prop['scale'],
                           clearance=prop.get('clearance',.24))
    for prop,shape in source_props:
        if not furnish(prop,prop['x'],prop['z']):
            blocked.append(prop)
    def area(prop):
        box = prop_box(layout,prop)
        return (box.right-box.left)*(box.north-box.south)
    for prop in sorted(blocked,key=area,reverse=True):
        p=prop['x'],prop['z']
        for ring in range(1,61):
            candidates=[]
            for i in range(24):
                theta=i*math.tau/24
                q=(round(p[0]+ring*.4*math.cos(theta),3),round(p[1]+ring*.4*math.sin(theta),3))
                if any(a.contains(q) and not a.contains(p) for a in protected_areas):
                    continue
                candidates.append(q)
            if any(furnish(prop,*q) for q in candidates):
                o=layout.plan['props'][-1]
                shifted.append({'name':o['name'],'before':p,'after':(o['x'],o['z'])})
                break
        else:
            raise ValueError(f'No position for displaced furnishing {prop}')
    for plot in inserted:
        x,z=plot['x'],plot['z']
        for kind,gap in (('Pottery_Jars',.56),('Crate_Closed',.63)):
            dx,dz=rotate(-plot['width']/2+1.02,-plot['depth']/2-gap,plot['yaw'])
            if layout.prop(kind,x+dx,z+dz,'New doorstep props',yaw=plot['yaw'],scale=.77,clearance=.12):
                break

    layout.pave_frontages()
    layout.validate(ORIGINAL,ORIGINAL['counts']['houses']+len(inserted))
    keys=('roads','buildings','accessPaths','secondaryAccessPaths','frontages','props','counts')
    return {**ORIGINAL,**{k:layout.plan[k] for k in keys}}, {
        'facilities':moved_facilities,'relocated':relocated,'inserted':inserted,'furnishings':shifted,
    }


if __name__=='__main__':
    plan,changes=build()
    for file,content in (('town_plan.json',plan),('layout_changes.json',changes)):
        (WORK/file).write_text(json.dumps(content,ensure_ascii=False,indent=2)+'\n')
    print(plan['counts'])
    print({k:len(v) for k,v in changes.items()})
    for p in changes['facilities']+changes['relocated']:
        print(p)
    for p in changes['inserted']:
        print(p['name'],p['street'],p['x'],p['z'])
