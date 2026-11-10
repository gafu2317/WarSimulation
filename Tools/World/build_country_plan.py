"""Author one town plan shared by the old and new asset sets."""
import json
import math
import heapq
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Data/CountryComparison'
roads, buildings, props, gardens = [], [], [], []
building_shapes = []
NORTH = 168
RIVER_Z = 8
RIVER_WIDTH = 7.4
WALL = [(-92,-112),(92,-112),(112,-92),(112,NORTH-20),(92,NORTH),(-92,NORTH),(-112,NORTH-20),(-112,-92)]

def within_walls(shape, inset=2):
    return all((b[0]-a[0])*(z-a[1])-(b[1]-a[1])*(x-a[0]) >= inset*math.dist(a,b)
               for x,z in shape for a,b in zip(WALL,WALL[1:]+WALL[:1]))
squares, enclosures = [], []
model_sizes = {}
for variant, files in [('old',['docs/Art/Records/City/export_manifest.json',
                              'docs/Art/Records/TownProps/model_manifest.json',
                              'docs/Art/Records/Vegetation/model_manifest.json',
                              'Assets/Models/Kingdom/City/legacy_parity_manifest.json']),
                       ('new',['ArtSource/Deliveries/MedievalCity_2026-10-22/asset_manifest.json'])]:
    model_sizes[variant] = {}
    for file in files:
        manifest = json.loads((ROOT/file).read_text())
        for model in manifest.get('models',manifest.get('assets',[])):
            model_sizes[variant][model['name']] = [b-a for a,b in zip(model['minimum'],model['maximum'])]
for record in json.loads((OUT/'model_dimensions.json').read_text())['models']:
    size=record['size']
    model_sizes[record['set']][record['name']]=[size['x'],size['z'],size['y']]


def road(name, width, points, frontage=True, *, avenue=False):
    roads.append(dict(name=name, width=width, frontage=frontage, avenue=avenue, points=[dict(x=x, z=z) for x, z in points]))


def rectangle(x, z, width, depth, yaw=0, margin=0):
    angle = math.radians(yaw)
    c, s = math.cos(angle), math.sin(angle)
    return [(x+c*a+s*b, z-s*a+c*b) for a, b in
            [(-width/2-margin, -depth/2-margin), (width/2+margin, -depth/2-margin),
             (width/2+margin, depth/2+margin), (-width/2-margin, depth/2+margin)]]


def overlaps(a, b):
    for shape in (a, b):
        for p, q in zip(shape, shape[1:]+shape[:1]):
            axis = (q[1]-p[1], p[0]-q[0])
            aa = [v[0]*axis[0]+v[1]*axis[1] for v in a]
            bb = [v[0]*axis[0]+v[1]*axis[1] for v in b]
            if max(aa) <= min(bb) or max(bb) <= min(aa):
                return False
    return True


def street_boxes(margin=.8, routes=roads):
    for r in routes:
        for a, b in zip(r['points'], r['points'][1:]):
            dx, dz = b['x']-a['x'], b['z']-a['z']
            yield rectangle((a['x']+b['x'])/2, (a['z']+b['z'])/2,
                            r['width']+margin, math.hypot(dx, dz), math.degrees(math.atan2(dx, dz)))


def add(name, x, z, width, depth, yaw=0, zone='Residential', required=True):
    shape = rectangle(x, z, width, depth, yaw, .16)
    box=rectangle((min(p[0] for p in shape)+max(p[0] for p in shape))/2,
                  (min(p[1] for p in shape)+max(p[1] for p in shape))/2,
                  max(p[0] for p in shape)-min(p[0] for p in shape),max(p[1] for p in shape)-min(p[1] for p in shape))
    if any(overlaps(shape, p) for p in building_shapes):
        if required:
            raise ValueError('Building plots overlap: '+name+' at '+str((x,z)))
        return False
    if not required and (not within_walls(shape) or not within_walls(box,1.1) or
                         any(overlaps(shape,p) for p in street_reserves+court_reserves)):
        return False
    buildings.append(dict(name=name, x=round(x, 3), z=round(z, 3), width=width,
                          depth=depth, yaw=round(yaw, 3), zone=zone))
    building_shapes.append(shape)
    return True


def prop(name, x, z, yaw=0, scale=1, zone='Street life', y=0):
    props.append(dict(name=name, old=name, x=x, y=y, z=z, yaw=yaw, scale=scale, zone=zone))


def prop_shape(p):
    if p['name'].startswith(('Tree_','NaturalTree_')):
        return rectangle(p['x'],p['z'],1,1)
    sizes = [model_sizes[variant][p[key]][:2] for variant,key in [('new','name'),('old','old')]]
    width,depth = [max(s[i] for s in sizes)*p['scale'] for i in (0,1)]
    return rectangle(p['x'],p['z'],width,depth,p['yaw'],.08)


def square(name, x, z, width, depth, points=None):
    item=dict(name=name,x=x,z=z,width=width,depth=depth)
    if points:item['points']=[dict(x=a,z=b) for a,b in points]
    squares.append(item)


def square_shape(p):
    return [(q['x'],q['z']) for q in p['points']] if p.get('points') else rectangle(p['x'],p['z'],p['width'],p['depth'])


def housing_frontage(r, infill=False):
    if not r['frontage']:return
    for segment,(a,b) in enumerate(zip(r['points'],r['points'][1:])):
        dx,dz=b['x']-a['x'],b['z']-a['z']
        length=math.hypot(dx,dz);dx,dz=dx/length,dz/length
        for side in (-1,1):
            distance=.4
            while distance<length-6:
                x,z=a['x']+dx*distance,a['z']+dz*distance
                zone='Scholars quarter' if z>14 and x>22 else 'Northern residential' if z>80 else 'Civic quarter' if z>14 else 'Residential' if x<-20 else 'Craft and freight quarter' if x>40 else 'Market quarter'
                index=len(buildings)+segment
                profiles=[]
                if r['width']>=5 or zone=='Market quarter':
                    width=(10.2,10.8,11.4)[index%3];profiles.append(('MerchantHouse',width,width*1.063))
                elif zone=='Craft and freight quarter' and index%3==0:
                    profiles.append(('WorkshopHouse',10.0,6.55))
                width=(9.4,10.0,10.6)[index%3]
                profiles.extend(('Fantasy_House',w,round(w*1.163,3)) for w in (width,9.0,8.0,7.0,6.4))
                if infill:profiles=[('Fantasy_House',w,round(w*1.163,3)) for w in (7.0,6.4,6.0)]
                for name,width,depth in profiles:
                    along=distance+width/2
                    if along>length-width/2:continue
                    x,z=a['x']+dx*along,a['z']+dz*along
                    setback=r['width']/2+depth/2+.4
                    xx,zz=x-dz*side*setback,z+dx*side*setback
                    yaw=math.degrees(math.atan2(-dz*side,dx*side))
                    if add(name,xx,zz,width,depth,yaw,zone,required=False):
                        buildings[-1]['street']=r['name']
                        buildings[-1]['frontageTarget']=[x,z]
                        distance+=width+.35
                        break
                else:
                    distance+=.45


# Straight streets keep frontage continuous; turns clear the existing civic plots.
road('South gate high street',6.8,[(0,-110),(0,-50)],avenue=True)
road('Market crossing',6.5,[(0,-50),(0,1.8)],False,avenue=True)
road('Royal bridge crossing',6.5,[(0,1.8),(0,14.2)],False,avenue=True)
road('Royal ascent',5.5,[(0,14.2),(0,104)],avenue=True)
road('Merchants gate road',6.8,[(110,-34),(0,-34)],avenue=True)
road('Southern river quay',3.2,[(-109,1.8),(109,1.8)])
road('Northern river quay',3.0,[(-109,14.2),(-77.5,14.2),(-77.5,22.5),(-56,22.5),(-56,14.2),(109,14.2)])
road('Residential bridge crossing',3.4,[(-82,1.8),(-82,14.2)],False)
road('Scholars bridge crossing',4.2,[(62,1.8),(62,14.2)],False)

road('Old town spine',4.0,[(-62,1.8),(-62,-100),(0,-100)])
road('West wall lane',2.6,[(-98,1.8),(-98,-100),(-62,-100)])
road('Mercers lane',3.3,[(-98,-16),(0,-16)])
road('Well street',3.0,[(-98,-41),(-13,-41)])
road('Chapel lane',2.8,[(-98,-58.5),(-48,-58.5),(-48,-67),(0,-67)])

road('Foundry street',4.4,[(54,1.8),(54,-100)])
road('Bakers street',3.5,[(0,-62),(100,-62)])
road('Lantern lane',2.8,[(21,-100),(21,-62)])
road('Southern wall lane',2.6,[(0,-100),(100,-100),(100,1.8)])
road('Warehouse passage',2.6,[(77.5,1.8),(77.5,-62)])
road('Millers lane',2.8,[(54,-73),(100,-73)])
road('Shopkeepers passage',2.2,[(43,-34),(43,1.8)])
road('Wagonmakers lane',2.4,[(77,-100),(77,-73)])

road('Cathedral approach',3.5,[(-98,14.2),(-98,48),(-73,48),(-73,23),(0,23)])
road('Hospital street',3.0,[(-73,39),(-46,39),(-46,23)])
road('Garrison street',4.0,[(-58,68),(-58,156.5)])
road('Military court street',3.2,[(-98,92),(-58,92)])
road('Northwest wall lane',2.5,[(-98,14.2),(-98,140),(-86,156.5),(86,156.5),(100,140)])
road('Barracks lane',2.6,[(-98,129),(-35,129),(-35,104),(0,104)])
road('Bellfounders lane',2.7,[(-98,68),(0,68)])
road('Western close lane',2.5,[(-73,39),(-73,68)])
road('Royal household lane',2.6,[(-35,68),(-35,156.5)])
road('Scholars street',4.0,[(62,14.2),(62,156.5)])
road('Artists street',3.4,[(0,38),(100,38)])
road('College terrace',3.4,[(0,91),(62,91)])
road('Observatory lane',2.8,[(62,100),(100,100)])
road('Eastern college lane',2.5,[(100,14.2),(100,140)])
road('Fountain street',3.8,[(0,68),(100,68)])
road('Library back passage',2.3,[(22,38),(22,68)])
road('Palace east service',2.6,[(28,126),(62,126)])
road('Orchard lane',2.4,[(28,104),(28,156.5)])

square('Market square',-1,-31,40,40,[(-17,-51),(16,-48),(20,-18),(-9,-12),(-22,-29)])
square('Royal forecourt',-1.5,103,57,6)
fountain_x, fountain_z = 10.25, 77
square('Fountain square',fountain_x,fountain_z,29,26,[(-7,65),(18,63),(24,82),(12,91),(-6,88)])
square('Cathedral close',-30,24,31,4)
square('Library court',37,38,18,7)
square('Museum court',82,40,24,4)
square('Forge yard',66.3,-27.8,20,5)
square('Warehouse loading court',89,-28,19,5.4)
square('Stable yard',88.7,-58.5,19,3.8)
square('Granary yard',66.2,-58,17,4.5)
square('Training yard',-80,86,27,11)
square('Tournament forecourt',-80.4,-97.5,32,5)
square('Tavern court',-33,-66.5,15,5)
for name,x,z,w,d in [('Cathedral garden',-30,45.5,29,41),('Royal grounds',-1.5,130,55.8,49.8),('Arcane college garden',45,109,29.5,31),('Spirit college garden',83,120,31,34.5)]:
    enclosures.append(dict(name=name,x=x,z=z,width=w,depth=d))
canal_reserves=[rectangle(0,RIVER_Z,226,9.8)]
fountain_reserve = rectangle(fountain_x,fountain_z,15,15)
service_reserves=[rectangle(83,148,32,27),rectangle(-62.5,144,9,11),rectangle(35.5,1,15,11),fountain_reserve,rectangle(-65,25.7,7,3),rectangle(-46.5,-43.3,3,3),rectangle(-54,33,5,7)]
for x,z in [(0,-67),(54,-95),(-98,140),(86,156.5),(-80.4,-98)]:service_reserves.append(rectangle(x,z,6.3,6.3))
watchtowers=[];watchtower_shapes=[]
for i,corner in enumerate(WALL):
    previous,following=WALL[i-1],WALL[(i+1)%len(WALL)]
    dx,dz=corner[0]-previous[0],corner[1]-previous[1];length=math.hypot(dx,dz);normal=(-dz/length,dx/length)
    dx,dz=following[0]-corner[0],following[1]-corner[1];length=math.hypot(dx,dz)
    inward=(normal[0]-dz/length,normal[1]+dx/length);length=math.hypot(*inward);inward=tuple(v/length for v in inward)
    distance=(3*math.sqrt(2)+1.97/2+.5)/sum(a*b for a,b in zip(inward,normal))
    x,z=corner[0]+inward[0]*distance,corner[1]+inward[1]*distance
    watchtowers.append(dict(x=round(x,4),z=round(z,4),width=6,depth=6,zone='Wall sentries'))
    watchtower_shapes.append(rectangle(x,z,6,6,margin=.25))
service_reserves.extend(watchtower_shapes)
street_reserves=list(street_boxes(margin=.4))
court_reserves=[square_shape(p) for p in squares+enclosures]+canal_reserves+service_reserves

add('Royal_Castle',-1.5,130,52.8,46.8,zone='Royal precinct')
add('Church',-30,45.5,27.3,39.0,zone='Cathedral close')
add('Chapel',-88.5,-49,12.1,12.1,zone='Residential')
add('Clinic',-60,52.5,18.9,17.55,zone='Cathedral close')
add('Bathhouse',-85.7,35.5,20.4,20.4,zone='Civic quarter')
add('WarriorAcademy_Lv5',-80,109.5,30.0,30.0,zone='Military quarter')
add('Barracks',-77.5,140,23.4,18.2,zone='Military quarter')
add('ArcaneAcademy_Lv5',45,109,27.6,30.0,zone='Scholars quarter')
add('SpiritAcademy_Lv5',83,120,30.0,32.5,zone='Scholars quarter')
add('Library',37,50,18.75,17.5,zone='Scholars quarter')
add('Museum',82,52.5,22.5,21.25,zone='Scholars quarter')
add('Observatory',85,88,13,13,zone='Scholars quarter')
add('Guildhall',28,-12,20.0,20.0,zone='Market quarter')
add('Tavern',-33,-57,15.6,16.8,zone='Market quarter')
add('Bakery',33,-47,15.6,15.6,zone='Market quarter')
add('Casino',31,-80,14.4,15.6,zone='Entertainment quarter')
add('Blacksmith',66.3,-15,19.2,18.0,zone='Craft and freight quarter')
add('Warehouse',89,-15,18.75,18.75,zone='Craft and freight quarter')
add('Stable',88.7,-48.5,19.2,16.8,zone='Craft and freight quarter')
add('Granary',66.2,-47,16.25,17.5,zone='Craft and freight quarter')
add('WorkshopHouse',68,-83,11,7.2,zone='Craft and freight quarter')
add('Guardhouse',-15,-89.6,16.0,16.0,zone='Gate ward')
add('Market_Hall',-33,-28.6,25.5,21.0,zone='Market quarter')
add('Arena',-80.4,-79.3,30.4,32.0,zone='Tournament quarter')
add('Windmill',88,-88,14,15,zone='Craft and freight quarter')
add('VeiledCourtyard',11,-78.5,12.5,16.25,zone='Entertainment quarter')
add('VelvetTerrace',11,-49.9,12.0,13.2,zone='Entertainment quarter')
add('LanternSpire',44.5,-92.4,8.75,11.25,zone='Entertainment quarter')
add('CrimsonRowhouse',11,-94.3,9,8,zone='Entertainment quarter')
add('Watermill',-67,14.15,12,16,90,zone='Mill quay')
for b in buildings:
    shape=rectangle(b['x'],b['z'],b['width'],b['depth'],b['yaw'])
    for r in roads:
        for a,c in zip(r['points'],r['points'][1:]):
            dx,dz=c['x']-a['x'],c['z']-a['z']
            segment=rectangle((a['x']+c['x'])/2,(a['z']+c['z'])/2,r['width'],math.hypot(dx,dz),math.degrees(math.atan2(dx,dz)))
            if overlaps(shape,segment):raise ValueError('Facility meets street: '+b['name']+' / '+r['name'])
    angle=math.radians(b['yaw']);distance=b['depth']/2+1.5
    court_reserves.append(rectangle(b['x']-math.sin(angle)*distance,b['z']-math.cos(angle)*distance,3,3,b['yaw']))
facility_count = len(buildings)
for r in sorted(roads,key=lambda r:-r['width']):housing_frontage(r)
for r in roads:housing_frontage(r, infill=True)

# Refine the streets after frontage generation so neighboring lots keep their setbacks.
by_street = {r['name']:r for r in roads}
college = by_street['College terrace']
service = by_street['Palace east service']
barracks = by_street['Barracks lane']
royal_ascent = by_street['Royal ascent']
palace_crossing_z = 101.4
orchard_x = 28.8
wall_lane = by_street['Northwest wall lane']
wall_lane.update(width=2.25,points=[dict(x=x,z=z) for x,z in
                [(-98,14.2),(-98,156.25),(100,156.25),(100,140)]])
by_street['Eastern college lane']['width'] = wall_lane['width']
college['points'] = [dict(x=-35,z=palace_crossing_z),dict(x=100,z=palace_crossing_z)]
royal_ascent['points'][-1]['z'] = round(palace_crossing_z + college['width']/2,3)
by_street['Orchard lane'].update(width=2.2,points=[dict(x=orchard_x,z=palace_crossing_z),dict(x=orchard_x,z=156.25)])
barracks['points'] = barracks['points'][:2]
service['points'] = [dict(x=orchard_x,z=136),dict(x=62,z=136)]
roads.remove(by_street['Observatory lane'])
next(b for b in buildings if b['name']=='Royal_Castle')['x'] = royal_ascent['points'][-1]['x']
next(s for s in squares if s['name']=='Royal forecourt')['x'] = royal_ascent['points'][-1]['x']
next(g for g in enclosures if g['name']=='Royal grounds').update(x=-.3,width=55.2)
magic = next(b for b in buildings if b['name']=='ArcaneAcademy_Lv5')
spirit = next(b for b in buildings if b['name']=='SpiritAcademy_Lv5')
magic['z'] = spirit['z'] - (spirit['depth']-magic['depth'])/2
next(g for g in enclosures if g['name']=='Arcane college garden')['z'] = magic['z']
academy_reserve = [rectangle(magic['x'],magic['z'],magic['width'],magic['depth'],0,.16),
                   *street_boxes(0,[college,service])]
relots = [i for i in range(facility_count,len(buildings))
          if any(overlaps(building_shapes[i],shape) for shape in academy_reserve)]
new_lots = [(43.6,141.77,7,8.141,0,'Scholars quarter',service),
            (16.2,94.379,6.4,7.443,180,'Northern residential',college),
            (24.75,92.285,10,11.63,180,'Northern residential',college),
            (34.6,92.867,9,10.467,180,'Scholars quarter',college)]
assert len(relots)==len(new_lots), 'Review the lots around the academy street.'
for b in buildings[facility_count:]:
    if b.get('street') == barracks['name'] and b['frontageTarget'][1] != barracks['points'][-1]['z']:
        b['street'] = 'Royal household lane'
    if b.get('street') in (college['name'],service['name'],'Observatory lane'):
        b.pop('frontageTarget',None)
        b['street'] = college['name'] if b['street']=='Observatory lane' else b['street']
for i,(x,z,width,depth,yaw,zone,street) in zip(relots,new_lots):
    buildings[i] = dict(name='Fantasy_House',x=x,z=z,width=width,depth=depth,yaw=yaw,zone=zone,
                        street=street['name'],frontageTarget=[x,street['points'][0]['z']])
corner_lots = [b for b in buildings if b.get('street')==wall_lane['name'] and abs(b['yaw']%90)>.001]
assert len(corner_lots)==2, 'Review the two lots along the former diagonal.'
corner_lots[0].update(x=-93.5,z=151.004,yaw=180,frontageTarget=[-93.5,156.25])
corner_lots[1].update(x=-103.014,z=139,yaw=-90,frontageTarget=[-98,139])
for old_x,new_x in [(34.6,35.15),(43.45,44)]:
    lot = next(b for b in buildings if b.get('street')==wall_lane['name'] and b['x']==old_x and 145<b['z']<156)
    lot.update(x=new_x,frontageTarget=[new_x,156.25])
next(b for b in buildings if b.get('street')==service['name'] and b['x']==43.6).update(x=44,frontageTarget=[44,136])
building_shapes[:] = [rectangle(b['x'],b['z'],b['width'],b['depth'],b['yaw'],.16) for b in buildings]
assert not any(overlaps(a,b) for i,a in enumerate(building_shapes) for b in building_shapes[i+1:]), 'Building plots overlap.'
assert all(within_walls(p) for p in building_shapes), 'A lot leaves the walls.'
street_reserves = list(street_boxes(margin=.4))
court_reserves = [square_shape(p) for p in squares+enclosures]+canal_reserves+service_reserves

stall_types=['Produce_Stall','Bread_Stall','Cloth_Stall','Flower_Stall','Fish_Stall','Potion_Stall','Book_Stall','Spice_Stall','Butcher_Stall','Weapon_Stall']
market_outline=square_shape(next(p for p in squares if p['name']=='Market square'))
market_stalls=[]
for row,z in enumerate((-40.5,-27.9,-23.9,-19.9)):
    for column,(x,yaw) in enumerate(((-11.5,270),(-4.8,90),(4.8,270),(11.5,90))):
        name=stall_types[(row*4+column)%len(stall_types)]
        prop(name,x,z,yaw,.9,zone='Market square')
        shape=prop_shape(props[-1])
        assert all((b[0]-a[0])*(pz-a[1])-(b[1]-a[1])*(px-a[0])>=0
                   for px,pz in shape for a,b in zip(market_outline,market_outline[1:]+market_outline[:1])), 'A stall leaves the market square.'
        assert not any(overlaps(shape,p) for p in building_shapes+street_reserves+market_stalls), 'A market row blocks a street or another plot.'
        market_stalls.append(shape)
prop('Well',-17,-27.5,0,.9,zone='Market square')
prop('Noticeboard',-12,-47,0,.85,zone='Market square')
for x,z,yaw in [(66,-28,90)]:prop('Merchant_Wagon',x,z,yaw,.75,zone='Market and freight deliveries')
for i,(x,z) in enumerate([(-11.5,-43.4),(-10.1,-43.4),(13.8,-40.5),(-13.8,-23.9),(13.8,-23.9),(13.8,-19.9)]):prop('Produce_Basket' if i%2 else 'Vegetable_Crate',x,z,0,.75,zone='Market goods')
for x,z in [(-8,-108),(8,-108),(-17,-51),(18,-17),(-5,15),(6,15),(-23,111),(23,111),(-44,24),(49,38),(86,40),(104,-39)]:prop('Streetlamp',x,z,0,.9,zone='Public streets')
for x,z,yaw in [(-43,24,90),(-21,24,270),(30,39,0),(44,39,0),(67,41,0),(85,41,0),(-3,84,0),(19,82,270)]:prop('Bench',x,z,yaw,.8,zone='Civic courts')
for x,z in [(-44,27),(-20,27),(-44,58),(-20,58),(-25,105.5),(25,105.5),(-24,154.15),(24,154.15),(31,93),(57,93),(68,107),(96,107)]:prop('Tree_AlleyCypress',x,z,0,.75,zone='Civic gardens')
for x,z in [(-5,89),(20,89),(-43,112),(51,130),(74,30),(103,-7),(-54,-9),(-78,-93)]:prop('Tree_Shade',x,z,0,.75,zone='Neighborhood shade')
for x,z in [(-38,-98),(-45,-49),(-77,-26),(40,-66),(97,-76),(-55,88),(40,84)]:prop('Tree_Street',x,z,0,.7,zone='Street corners')
for x,z in [(83,-28),(95,-28),(60,-28),(72,-28),(83,-58.5),(94,-58.5),(66,-58)]:prop('Crate_Closed' if int(x)%2 else 'Barrel',x,z,0,.9,zone='Freight yard')
for name,x,z in [('Anvil',61,-28),('Forge',66,-27.8),('Weapon_Rack',72,-28),('Water_Trough',83,-58.5),('Hay_Bale',94,-58.5),('Grain_Sack',66,-58),('Training_Dummy',-87,86),('Shield_Rack',-74,86),('Street_Brazier',-80,84),('Grain_Sack',-67,25.7),('Barrel',-64.5,25.7),('Signpost',-7,-106)]:prop(name,x,z,0,.8,zone='Working courts')
prop('Covered_Wagon',89,-28,90,.75,zone='Warehouse loading court')

prop_shapes=[rectangle(-62.5,144,4.8,6.8),rectangle(-46.5,-43.3,1,1)];kept=[]
for p in props:
    shape=prop_shape(p)
    if not within_walls(shape) or any(overlaps(shape,b) for b in building_shapes+prop_shapes+canal_reserves):continue
    kept.append(p);prop_shapes.append(shape)
props[:]=kept


def furnish(name,x,z,zone,scale=1):
    if any(p['name']==name for p in props):return
    candidates=[(x+dx*.75,z+dz*.75) for dx in range(-12,13) for dz in range(-10,11)]
    candidates.sort(key=lambda p:(p[0]-x)**2+(p[1]-z)**2)
    for xx,zz in candidates:
        p=dict(name=name,old=name,x=xx,y=0,z=zz,yaw=0,scale=scale,zone=zone)
        shape=prop_shape(p)
        if not within_walls(shape) or any(overlaps(shape,b) for b in building_shapes+prop_shapes+street_reserves+canal_reserves):continue
        props.append(p);prop_shapes.append(shape)
        return
    raise ValueError('No working space for '+name)


assert len([p for p in props if p['name'] in stall_types])==16, 'A planned market stall was removed.'
assert {p['name'] for p in props if p['name'] in stall_types}==set(stall_types), 'A market stall type is missing.'
for name,x,z in [('Anvil',62.5,-26.7),('Water_Trough',86,-58.3),('Signpost',-7,-108),('Noticeboard',-12,-47),('Covered_Wagon',89,-28),('Forge',66,-27.8),('Weapon_Rack',72,-28),('Wheelbarrow',-63,142),('Crate_Open',96,-28),('Basket_Empty',-8.7,-43.4),('Pottery_Jars',-13.8,-29.0),('Fish_Crate',13.8,-27.9),('Bread_Basket',-4.8,-43.4),('Rolled_Fabric',4.2,-43.2),('Market_Scale',-6.5,-43.4),('Planter',-43,26),('Bucket',-75,-34),('Wash_Tub',-77,-35),('Fountain',37,38),('Outhouse',-63,148),('Drain_Grate',97,-29),('Bollard',43,-2),('Shop_Sign',-16,-23.5),('Handcart',-62,141),('Firewood_Rack',60,-28),('Sack_Stack',71,-58),('Clothesline',-63,148),('Hay_Bale',94,-58.5)]:furnish(name,x,z,'Courts and daily life',.65 if name=='Fountain' else .8)
market_goods={'Well','Noticeboard','Produce_Basket','Vegetable_Crate','Basket_Empty','Pottery_Jars','Fish_Crate','Bread_Basket','Rolled_Fabric','Market_Scale','Shop_Sign'}
assert market_goods<={p['name'] for p in props}, 'Market goods are missing.'
for p in props:
    if p['name'] not in market_goods|set(stall_types):continue
    shape=prop_shape(p)
    assert not any(overlaps(shape,s) for s in street_reserves), 'Market goods block a street: '+p['name']
    assert all((b[0]-a[0])*(pz-a[1])-(b[1]-a[1])*(px-a[0])>=0
               for px,pz in shape for a,b in zip(market_outline,market_outline[1:]+market_outline[:1])), 'Market goods leave the square: '+p['name']
for i,name in enumerate(['GroundPlant_GrassShort','GroundPlant_GrassTall','GroundPlant_FernPatch','Flower_WildPatch','Flower_Border']):prop(name,72+i*4.5,150+(i%2)*4,0,.85,zone='College arboretum')
for i,(x,z) in enumerate([(72,142),(82,146),(94,139),(74,155),(88,155),(98,150),(69,149),(83,139),(92,158),(77,161)]):prop(f'NaturalTree_{i+1:02}',x,z,i*37,.8,zone='College arboretum')
for i,(number,x,z) in enumerate([(2,72,147),(4,88,144),(7,96,154),(8,78,153),(11,87,160)]):prop(f'NaturalRock_{number:02}',x,z,i*47,.55,zone='College arboretum')
for i,b in enumerate(buildings[30:]):
    angle=math.radians(b['yaw']);front=(-math.sin(angle),-math.cos(angle))
    x=b['x']-front[0]*(b['depth']/2+1.4);z=b['z']-front[1]*(b['depth']/2+1.4)
    shape=rectangle(x,z,b['width']*.65,2,b['yaw'],.15)
    if not within_walls(shape,2) or any(overlaps(shape,p) for p in building_shapes+street_reserves+court_reserves+prop_shapes):continue
    if any(overlaps(shape,rectangle(p['x'],p['z'],p['width'],p['depth'],p['yaw'])) for p in gardens):continue
    if i%3==0:gardens.append(dict(x=round(x,2),z=round(z,2),width=round(b['width']*.65,2),depth=2,yaw=b['yaw']))



def point_inside(point, polygon):
    signs=[]
    for a,b in zip(polygon,polygon[1:]+polygon[:1]):
        signs.append((b[0]-a[0])*(point[1]-a[1])-(b[1]-a[1])*(point[0]-a[0]))
    return all(v>=0 for v in signs) or all(v<=0 for v in signs)


def cells_in(polygons):
    cells=set()
    for p in polygons:
        for x in range(max(-109,math.floor(min(v[0] for v in p))),min(109,math.ceil(max(v[0] for v in p)))+1):
            for z in range(max(-109,math.floor(min(v[1] for v in p))),min(NORTH-3,math.ceil(max(v[1] for v in p)))+1):
                if point_inside((x,z),p):cells.add((x,z))
    return cells


# Keep the wider setbacks when matching the paving on both sides of a junction.
by_street['South gate high street']['width'] = by_street['Market crossing']['width']
by_street['Fountain street']['width'] = by_street['Bellfounders lane']['width']
street_shapes = list(street_boxes(margin=-.4))
plots = [rectangle(b['x'],b['z'],b['width'],b['depth'],b['yaw']) for b in buildings]
obstacles = [rectangle(b['x'],b['z'],b['width'],b['depth'],b['yaw'],.9) for b in buildings]
river_shape = rectangle(0,RIVER_Z,224,RIVER_WIDTH)
water_blocked = cells_in([rectangle(0,RIVER_Z,224,8.5)]) - cells_in(
    [rectangle(x,RIVER_Z,w,14) for x,w in [(-82,3.2),(0,6.5),(62,4.2)]])
wide_entrances = {'Royal_Castle':royal_ascent['width'],'Church':2.4,'WarriorAcademy_Lv5':2.4,
                  'ArcaneAcademy_Lv5':2.4,'SpiritAcademy_Lv5':2.4}


def reaches_street(point):
    return any(point_inside(point,shape) for shape in street_shapes)


def access_is_blocked(point):
    return tuple(round(v) for v in point) in water_blocked or any(point_inside(point,shape) for shape in obstacles)


def centered_street_target(index, threshold, front, width):
    b = buildings[index]
    axis = 0 if front[0] else 1
    sign = front[axis]
    facade = (b['x'],b['z'])[axis]*sign + b['depth']/2
    candidates = []
    for shape in street_shapes:
        lateral = [v[1-axis] for v in shape]
        if not min(lateral)-.001 <= threshold[1-axis] <= max(lateral)+.001:continue
        distances = [v[axis]*sign for v in shape]
        target = max(min(distances)+.3,facade+.1)
        if target > min(max(distances),facade+24):continue
        point = list(threshold)
        point[axis] = target*sign
        length = math.dist(threshold,point)
        patch = rectangle((threshold[0]+point[0])/2,(threshold[1]+point[1])/2,width,length,b['yaw'])
        if overlaps(patch,river_shape) or any(overlaps(patch,p) for i,p in enumerate(plots) if i!=index):continue
        candidates.append((length,tuple(point)))
    return min(candidates)[1] if candidates else None


def turning_access(entrance):
    start = (0,0)
    frontier = [(0,start)]
    cost = {start:0}
    came = {}
    while frontier:
        _,current=heapq.heappop(frontier)
        point = (entrance[0]+current[0],entrance[1]+current[1])
        if reaches_street(point):
            path=[current]
            while path[-1] != start:path.append(came[path[-1]])
            path.reverse()
            bends=[]
            for i in range(1,len(path)-1):
                if (path[i][0]-path[i-1][0],path[i][1]-path[i-1][1]) != (path[i+1][0]-path[i][0],path[i+1][1]-path[i][1]):
                    bends.append(path[i])
            if current != start:bends.append(current)
            return [(entrance[0]+x,entrance[1]+z) for x,z in bends]
        for dx,dz in [(1,0),(-1,0),(0,1),(0,-1)]:
            nxt=(current[0]+dx,current[1]+dz)
            value=cost[current]+1
            if value>=cost.get(nxt,float('inf')):continue
            x,z = entrance[0]+nxt[0],entrance[1]+nxt[1]
            if not (-109<=x<=109 and -109<=z<=NORTH-3) or access_is_blocked((x,z)):continue
            came[nxt]=current;cost[nxt]=value;heapq.heappush(frontier,(value,nxt))
    raise ValueError('No street access from '+str(entrance))


def access_path(index,b):
    angle = math.radians(b['yaw'])
    front = (-round(math.sin(angle)),-round(math.cos(angle)))
    center = (b['x'],b['z'])
    frontage = b.get('frontageTarget')
    width = wide_entrances.get(b['name'],1.35 if frontage else 1.5)
    inset = 1.3 if frontage else 2.5
    threshold = tuple(c+d*max(0,b['depth']/2-inset) for c,d in zip(center,front))
    if frontage:
        target = tuple(p if d else c for p,c,d in zip(frontage,center,front))
        path = [threshold,target]
    else:
        target = centered_street_target(index,threshold,front,width)
        if target:
            path = [threshold,target]
        else:
            for offset in (.8,1.3,1.8,2.3):
                entrance = tuple(c+d*(b['depth']/2+offset) for c,d in zip(center,front))
                if not access_is_blocked(entrance):break
            else:raise ValueError('Entrance is blocked: '+b['name'])
            path = [threshold,entrance,*turning_access(entrance)]
    return dict(name=b['name']+' entrance',width=width,paved=index<facility_count,
                points=[dict(x=round(x,3),z=round(z,3)) for x,z in path])


def access_shapes(path, margin=0):
    for a,b in zip(path['points'],path['points'][1:]):
        dx,dz=b['x']-a['x'],b['z']-a['z']
        length=math.hypot(dx,dz)
        if length>.01:
            yield rectangle((a['x']+b['x'])/2,(a['z']+b['z'])/2,path['width']+margin,length,math.degrees(math.atan2(dx,dz)))


access=[access_path(i,b) for i,b in enumerate(buildings)]
assert len(access)==len(buildings)
streets=list(street_boxes(margin=0))
assert not any(overlaps(b,s) for b in plots for s in streets), 'A street crosses a building plot.'
assert not any(overlaps(t,p) for t in watchtower_shapes for p in plots+streets), 'A watchtower crosses a building or street.'
for index,path in enumerate(access):
    for paving in access_shapes(path):
        assert not any(overlaps(paving,p) for i,p in enumerate(plots) if i!=index), 'An entrance path crosses another plot: '+path['name']
life_obstacles=[];life_cells={}
def nearby_cells(shape):
    return [(x,z) for x in range(math.floor(min(p[0] for p in shape)/8),math.floor(max(p[0] for p in shape)/8)+1)
                 for z in range(math.floor(min(p[1] for p in shape)/8),math.floor(max(p[1] for p in shape)/8)+1)]

def reserve_life(shape):
    index=len(life_obstacles);life_obstacles.append(shape)
    for cell in nearby_cells(shape):life_cells.setdefault(cell,[]).append(index)

for shape in building_shapes+street_reserves+canal_reserves+service_reserves+prop_shapes+[prop_shape(p) for p in props]+[square_shape(p) for p in enclosures]+[square_shape(squares[0])]:reserve_life(shape)
for garden in gardens:reserve_life(rectangle(garden['x'],garden['z'],garden['width'],garden['depth'],garden['yaw'],.15))
for path in access:
    for shape in access_shapes(path,.4):reserve_life(shape)

new_gardens=0
for i,b in enumerate(buildings[30:]):
    if i%2:continue
    angle=math.radians(b['yaw']);normal=(math.sin(angle),math.cos(angle));tangent=(math.cos(angle),-math.sin(angle))
    placed=False
    for width,depth in ((min(6,b['width']*.8),3.2),(3.6,2.4),(2.5,1.6)):
        if placed:break
        for along in (0,-b['width']*.25,b['width']*.25):
            if placed:break
            for extra in (0,.7,1.5):
                distance=b['depth']/2+.35+depth/2+extra
                x=b['x']+normal[0]*distance+tangent[0]*along;z=b['z']+normal[1]*distance+tangent[1]*along
                shape=rectangle(x,z,width,depth,b['yaw'],.2)
                if not within_walls(shape) or any(overlaps(shape,square_shape(q)) for q in squares):continue
                indices={index for cell in nearby_cells(shape) for index in life_cells.get(cell,[])}
                if any(overlaps(shape,life_obstacles[j]) for j in indices):continue
                gardens.append(dict(x=round(x,3),z=round(z,3),width=round(width,3),depth=depth,yaw=b['yaw'],fenced=True))
                reserve_life(shape);new_gardens+=1;placed=True;break

life_themes={
    'fuel':[('Firewood_Rack',-.8,0,0,.9),('Barrel',1.4,0,0,.9),('Bucket',1.6,1.15,0,.9)],
    'laundry':[('Clothesline',0,0,0,.9),('Wash_Tub',-1,1,0,.8),('Bucket',1.2,1.1,0,.9)],
    'storage':[('Crate_Closed',-1.2,0,0,.9),('Barrel',.1,0,0,.9),('Grain_Sack',1.3,1.2,0,.85)],
    'garden':[('Flower_Border',-.9,0,0,.8),('Planter',1.5,0,0,.75)],
    'rest':[('Bench',-.8,0,0,.9),('Planter',1.6,0,0,.8)],
    'delivery':[('Handcart',-1.3,0,90,.8),('Crate_Closed',1.4,0,0,.9),('Grain_Sack',1.4,1.3,0,.85)],
    'garrison':[('Weapon_Rack',-1.1,0,0,.9),('Shield_Rack',1.1,0,0,.9),('Crate_Closed',1.4,1.3,0,.9)],
    'drying':[('Clothesline',0,0,0,.9)],
    'seat':[('Bench',0,0,0,.9)]
}
life_start=len(props);life_groups=[]
for i,b in enumerate(buildings):
    residential=b['name'] in ('Fantasy_House','MerchantHouse','WorkshopHouse','CrimsonRowhouse')
    for wall,(yaw,half_length,half_depth) in enumerate(((b['yaw']+180,b['width']/2,b['depth']/2),(b['yaw']+90,b['depth']/2,b['width']/2),(b['yaw']-90,b['depth']/2,b['width']/2))):
        angle=math.radians(yaw);normal=(-math.sin(angle),-math.cos(angle));tangent=(math.cos(angle),-math.sin(angle))
        if b['zone']=='Military quarter' or b['name']=='Guardhouse':themes=['garrison','storage','fuel']
        elif b['name'] in ('MerchantHouse','WorkshopHouse') or b['zone']=='Craft and freight quarter':themes=['delivery','storage','fuel']
        elif residential:themes=[('fuel','laundry','garden')[(i+wall)%3],'storage','rest']
        else:themes=['rest','garden','storage']
        if not any(g['building']==i for g in life_groups):themes+=['drying','seat'] if residential else ['seat']
        placed=False
        for centre in (0,-half_length*.3,half_length*.3):
            if placed:break
            for layer in (0,.6,1.3):
                if placed:break
                for theme in themes:
                    pending=[];shapes=[]
                    for name,along,extra,turn,scale in life_themes[theme]:
                        w,d=[max(model_sizes[v][name][j] for v in ('old','new'))*scale for j in (0,1)]
                        turn_angle=math.radians(turn);extent=(w*abs(math.cos(turn_angle))+d*abs(math.sin(turn_angle)))/2
                        depth=(w*abs(math.sin(turn_angle))+d*abs(math.cos(turn_angle)))/2
                        if abs(centre+along)+extent>half_length-.1:continue
                        distance=half_depth+.3+depth+layer+extra
                        x=b['x']+tangent[0]*(centre+along)+normal[0]*distance
                        z=b['z']+tangent[1]*(centre+along)+normal[1]*distance
                        item=dict(name=name,old=name,x=round(x,3),y=0,z=round(z,3),yaw=round(yaw+turn,3),scale=scale,zone='Street life / '+theme)
                        shape=prop_shape(item)
                        if not within_walls(shape):continue
                        indices={index for cell in nearby_cells(shape) for index in life_cells.get(cell,[])}
                        if any(overlaps(shape,life_obstacles[j]) for j in indices) or any(overlaps(shape,s) for s in shapes):continue
                        pending.append(item);shapes.append(shape)
                    if len(pending)<(1 if theme in ('drying','seat') else 2):continue
                    props.extend(pending)
                    for shape in shapes:reserve_life(shape)
                    life_groups.append(dict(building=i,wall=wall,theme=theme,props=len(pending)))
                    placed=True;break

# Apply the same clearance rule to the original furnishings and later additions.
entrance_shapes=[shape for path in access for shape in access_shapes(path,.4)]
clearance=building_shapes+street_reserves+entrance_shapes+watchtower_shapes+canal_reserves
garden_shapes=[rectangle(g['x'],g['z'],g['width'],g['depth'],g['yaw'],.15) for g in gardens]
fixed_structures=[rectangle(-62.5,144,4.8,9),rectangle(35.5,1.4,14,8),fountain_reserve,
                  rectangle(-46.5,-43.3,1,1),rectangle(-54,33,3,1.2)]
life_obstacles.clear();life_cells.clear()
for shape in clearance+garden_shapes:reserve_life(shape)

def space_free(shape):
    if not within_walls(shape,1.5):return False
    indices={index for cell in nearby_cells(shape) for index in life_cells.get(cell,[])}
    return not any(overlaps(shape,life_obstacles[j]) for j in indices)

relocated=[]
for p in props:
    if p['name']=='Outhouse':p.update(x=-62.5,z=146,zone='Workers service court')
    if p['name']=='Clothesline' and p['zone']=='Courts and daily life':p.update(x=-62.5,z=140.3,zone='Workers service court')
    shape=prop_shape(p)
    if not space_free(shape) or p['zone']!='Workers service court' and any(overlaps(shape,s) for s in fixed_structures):
        original=(p['x'],p['z'])
        offsets=sorted(((dx*.5,dz*.5) for dx in range(-24,25) for dz in range(-24,25)),key=lambda v:v[0]**2+v[1]**2)
        for dx,dz in offsets:
            candidate=dict(p,x=original[0]+dx,z=original[1]+dz)
            shape=prop_shape(candidate)
            if p['name'] in market_goods|set(stall_types) and not all(point_inside(q,market_outline) for q in shape):continue
            if any(overlaps(shape,s) for s in fixed_structures) and p['zone']!='Workers service court':continue
            if not space_free(shape):continue
            p.update(x=candidate['x'],z=candidate['z']);relocated.append(dict(name=p['name'],before=original,after=[p['x'],p['z']]))
            break
        else:raise ValueError(f'No unobstructed position for {p["name"]} near {original}')
    reserve_life(prop_shape(p))
for shape in fixed_structures:reserve_life(shape)

activity_areas=[dict(name='Workers service court',use='washing and household services',x=-62.5,z=144,width=4.8,depth=9,surface='soil'),
                dict(name='River cargo landing',use='cargo handling',x=35.5,z=1.4,width=14,depth=8,surface='soil')]
for r in roads:
    if 'bridge crossing' in r['name']:continue
    for a,b in zip(r['points'],r['points'][1:]):
        dx,dz=b['x']-a['x'],b['z']-a['z'];length=math.hypot(dx,dz)
        activity_areas.append(dict(name=r['name']+' pedestrian edge',use='street access',
            x=(a['x']+b['x'])/2,z=(a['z']+b['z'])/2,width=r['width']+3.2,depth=length,yaw=math.degrees(math.atan2(dx,dz))))

def largest_court(cells):
    heights=[0]*219;best=None;score=0
    for z in range(-109,NORTH-2):
        heights=[height+1 if (x-109,z) in cells else 0 for x,height in enumerate(heights)]
        stack=[]
        for x,height in enumerate(heights+[0]):
            start=x
            while stack and stack[-1][1]>height:
                left,h=stack.pop();start=left;width=x-left
                if min(width,h)<4:continue
                w,d=min(width,14),min(h,12)
                if w*d>score:score=w*d;best=(left-109,z-h+1,w,d)
            if not stack or stack[-1][1]<height:stack.append((start,height))
    return best

walk_shapes=[square_shape(a) for a in activity_areas]
free=cells_in([WALL])-cells_in(building_shapes+street_reserves+entrance_shapes+canal_reserves+watchtower_shapes+
    [square_shape(g) for g in enclosures+squares]+garden_shapes+fixed_structures+walk_shapes)
activity_start=len(props);court_count=0
for _ in range(180):
    found=largest_court(free)
    if found is None:break
    left,bottom,w,d=found
    for xx in range(left,left+w):
        for zz in range(bottom,bottom+d):free.discard((xx,zz))
    x,z=left+(w-1)/2,bottom+(d-1)/2;w-=1.1;d-=1.1
    if w*d<12:continue
    area_shape=rectangle(x,z,w,d)
    if not within_walls(area_shape,2) or any(overlaps(area_shape,s) for s in clearance+garden_shapes):continue
    owner=min(buildings,key=lambda b:max(abs(x-b['x'])-b['width']/2,0)**2+max(abs(z-b['z'])-b['depth']/2,0)**2)
    kind='cargo' if owner['zone']=='Craft and freight quarter' else 'drill' if owner['zone']=='Military quarter' else 'garden' if owner['zone'] in ('Residential','Northern residential') else 'rest'
    before=len(props)
    def add_court_prop(name,xx,zz,yaw=0,scale=.85):
        item=dict(name=name,old=name,x=round(xx,3),y=0,z=round(zz,3),yaw=yaw,scale=scale,zone='Shared courts / '+kind)
        shape=prop_shape(item)
        if not all(point_inside(p,area_shape) for p in shape) or not space_free(shape):return False
        props.append(item);reserve_life(shape);return True
    if kind in ('cargo','drill'):
        central='Handcart' if kind=='cargo' else 'Training_Dummy'
        add_court_prop(central,x-w*.18,z,90 if kind=='cargo' else 0)
        choices=['Crate_Closed','Barrel','Sack_Stack','Crate_Open'] if kind=='cargo' else ['Weapon_Rack','Shield_Rack','Barrel','Training_Dummy']
        for row,zz in enumerate((z-d/2+1.0,z+d/2-1.0)):
            for column,xx in enumerate((x-w*.3,x,x+w*.3)):
                add_court_prop(choices[(row*3+column)%len(choices)],xx,zz,180 if row==0 else 0)
    else:
        if kind=='rest':
            if d>5:add_court_prop('Tree_Street',x,z+d/2-1.0,0,.72)
            for zz in (z-d*.27,z+d*.12):add_court_prop('Bench',x-w/2+.65,zz,-90)
            if w>4.7:add_court_prop('Planter',x+w/2-.9,z-d/2+1)
        else:
            for side in (-1,1):
                for zz in (z-d*.27,z+d*.27):add_court_prop('Firewood_Rack',x+side*(w/2-.65),zz,90*side)
            for xx,zz in ((x-w*.3,z-d*.32),(x+w*.3,z+d*.32)):add_court_prop('Planter',xx,zz)
        if kind=='garden' and w>5 and d>5:
            gw,gd=min(5,w-2),min(2.4,d*.34)
            gx,gz=x,z+d/2-gd/2-.4
            shape=rectangle(gx,gz,gw,gd,margin=.2)
            if space_free(shape):
                gardens.append(dict(x=gx,z=gz,width=gw,depth=gd,yaw=0,fenced=True));reserve_life(shape)
    if len(props)-before<2:continue
    court_count+=1
    activity_areas.append(dict(name=owner['name']+' '+kind+' court '+str(court_count),use=kind,x=x,z=z,width=w,depth=d,surface='soil',furnishings=len(props)-before))
    if kind=='rest' and d>9:
        bed=dict(x=x,z=z+d/2-1.4,width=min(w-.6,4),depth=2.2,yaw=0,fenced=True,flowers=True)
        shape=rectangle(bed['x'],bed['z'],bed['width'],bed['depth'],margin=.12)
        occupants=[p for p in props if overlaps(shape,prop_shape(p))]
        if not any(overlaps(shape,s) for s in clearance+garden_shapes) and all(p['name']=='Tree_Street' for p in occupants):
            gardens.append(bed);reserve_life(shape)

for x,z,w,d in [(8.5,86.8,8,2),(19.5,77.5,1.8,6),(-4.5,78.5,1.6,6)]:
    shape=rectangle(x,z,w,d,margin=.15)
    if not space_free(shape):continue
    gardens.append(dict(x=x,z=z,width=w,depth=d,yaw=0,fenced=True,flowers=True))
    for xx,zz in ([(x-w*.35,z),(x+w*.35,z)] if w>d else [(x,z-d*.3),(x,z+d*.3)]):
        item=dict(name='Tree_Street',old='Tree_Street',x=xx,y=0,z=zz,yaw=0,scale=.68,zone='Fountain square planted edges')
        if space_free(prop_shape(item)):props.append(item);reserve_life(prop_shape(item))
    reserve_life(shape)

for x in (-89,-84,-79,-74):
    item=dict(name='Training_Dummy',old='Training_Dummy',x=x,y=0,z=85.3,yaw=0,scale=.85,zone='Organized drill yard')
    if space_free(prop_shape(item)):props.append(item);reserve_life(prop_shape(item))

# Broad public courts retain a clear crossing and put furniture along their edges.
for area in squares:
    shape=square_shape(area)
    for side in (-1,1):
        for along in (-.3,0,.3):
            x,z=area['x']+along*area['width'],area['z']+side*(area['depth']/2-.8)
            name='Crate_Closed' if any(word in area['name'] for word in ('yard','loading','Granary')) else 'Planter'
            item=dict(name=name,old=name,x=x,y=0,z=z,yaw=0,scale=.8,zone=area['name'])
            footprint=prop_shape(item)
            if all(point_inside(p,shape) for p in footprint) and space_free(footprint):props.append(item);reserve_life(footprint)

# Place the work areas after the plots, paths, and gardens have been set.
east_courts = {
    'Forge yard': [
        ('Firewood_Rack',58.2,-26.23,0,.86), ('Forge',61,-26.24,0,.9),
        ('Anvil',63.05,-26.35,0,.9), ('Bucket',64.56,-26.35,0,.9),
        ('Crate_Closed',57.55,-29.5,0,.88), ('Crate_Closed',59,-29.5,0,.88),
        ('Barrel',60.43,-29.5,0,.88), ('Weapon_Rack',68.95,-26.03,0,.85),
        ('Weapon_Rack',71.44,-26.03,0,.85), ('Crate_Open',73.55,-26.03,0,.85),
        ('Crate_Closed',75.13,-26.03,0,.88), ('Wheelbarrow',72.45,-29.43,90,.85),
    ],
    'Warehouse loading court': [
        ('Covered_Wagon',83.85,-29.15,270,.75), ('Crate_Closed',80.34,-26.1,0,.88),
        ('Crate_Closed',81.84,-26.1,0,.88), ('Crate_Closed',83.34,-26.1,0,.88),
        ('Crate_Open',84.86,-26.1,0,.88), ('Crate_Closed',86.38,-26.1,0,.88),
        ('Bollard',87.59,-29.15,0,.85), ('Crate_Closed',90.86,-26.1,0,.88),
        ('Crate_Closed',92.4,-26.1,0,.88), ('Barrel',93.78,-26.1,0,.88),
        ('Barrel',94.98,-26.1,0,.88), ('Crate_Open',96.12,-26.1,0,.88),
        ('Handcart',93.5,-29.24,90,.85), ('Drain_Grate',96.83,-29.31,0,.85),
        ('Signpost',97.57,-26.1,0,.8),
    ],
    'Granary yard': [
        ('Sack_Stack',58.72,-56.61,0,.85), ('Sack_Stack',60.52,-56.61,0,.85),
        ('Grain_Sack',62.17,-56.45,0,.88), ('Grain_Sack',63.32,-56.45,0,.88),
        ('Grain_Sack',64.51,-56.45,0,.88), ('Crate_Open',59.04,-59.45,0,.88),
        ('Basket_Empty',60.54,-59.45,0,.88), ('Basket_Empty',61.95,-59.45,0,.88),
        ('Grain_Sack',63.38,-59.45,0,.88), ('Market_Scale',68.28,-56.48,0,.9),
        ('Grain_Sack',69.91,-56.48,0,.88), ('Grain_Sack',71.08,-56.48,0,.88),
        ('Sack_Stack',72.83,-56.61,0,.85), ('Wheelbarrow',71.33,-59.36,270,.84),
    ],
    'Stable yard': [
        ('Barrel',80.7,-57.67,0,.88), ('Water_Trough',83.04,-57.67,0,.88),
        ('Bucket',85.07,-57.67,0,.88), ('Barrel',86.31,-57.67,0,.88),
        ('Hay_Bale',90.61,-57.67,0,.88), ('Hay_Bale',92.29,-57.67,0,.88),
        ('Hay_Bale',93.97,-57.67,0,.88), ('Wheelbarrow',96.24,-57.67,90,.8),
    ],
}
east_bounds = {area['name']:square_shape(area) for area in squares if area['name'] in east_courts}
old_drying = [p for p in props if p['zone']=='Street life / drying']
props[:] = [p for p in props if p['zone']!='Street life / drying' and
            not any(point_inside((p['x'],p['z']),area) for area in east_bounds.values()) and
            not (p['name'] in market_goods-{'Well','Noticeboard'} and point_inside((p['x'],p['z']),market_outline)) and
            not (p['name']=='Streetlamp' and p['x'] in (-5,-3.5) and p['z']==16.5)]
def reset_furnishing_space():
    life_obstacles.clear()
    life_cells.clear()
    garden_clearance = [rectangle(g['x'],g['z'],g['width'],g['depth'],g['yaw'],.1) for g in gardens]
    enclosed_plots = [square_shape(g) for g in enclosures]
    for shape in clearance + fixed_structures + garden_clearance + enclosed_plots:
        reserve_life(shape)
    for item in props:
        reserve_life(prop_shape(item))

reset_furnishing_space()


def place_group(zone, anchor, items, inside=None):
    x,z,yaw=anchor
    angle=math.radians(yaw)
    c,s=math.cos(angle),math.sin(angle)
    pending=[];footprints=[]
    for name,along,back,turn,scale in items:
        item=dict(name=name,old=name,x=round(x+c*along+s*back,3),y=0,z=round(z-s*along+c*back,3),
                  yaw=(yaw+turn)%360,scale=scale,zone=zone)
        footprint=prop_shape(item)
        if (not space_free(footprint) or any(overlaps(footprint,b) for b in footprints) or
                inside is not None and not all(point_inside(v,inside) for v in footprint)):
            return False
        pending.append(item);footprints.append(footprint)
    props.extend(pending)
    for footprint in footprints:reserve_life(footprint)
    return True

for name,items in east_courts.items():
    if not place_group(name,(0,0,0),items,east_bounds[name]):
        raise ValueError('A work station leaves its courtyard: '+name)
for x,z,w,d in [(66.3,-27.93,19.6,1.55),(89,-27.3,18.6,1.2),(66.2,-57.89,16.6,1.08),(88.7,-59.44,18.6,1.65)]:
    aisle=rectangle(x,z,w,d)
    assert not any(overlaps(prop_shape(p),aisle) for p in props if p['zone'] in east_courts)
for bounds in east_bounds.values():reserve_life(bounds)

if not place_group('Market Hall loading',(-26,-44,270),[('Merchant_Wagon',0,0,0,.75)]):
    raise ValueError('The market loading stop is blocked.')
reserve_life(rectangle(-22.9,-44,1.65,2.2))

if not place_group('Arcane academy observation',(41,99,180),
                   [('Observing_Quadrant',0,0,0,.85)]):
    raise ValueError('The academy observing station is blocked.')

if not place_group('Southwest communal wash',(-41,-80,0),
                   [('Well',0,0,0,.8),('Wash_Tub',2.8,-1.1,0,.86),
                    ('Bucket',4,-1.1,0,.86),('Clothesline',2.8,-2.45,0,.88)]):
    raise ValueError('The residential wash yard is blocked.')

# Two clear aisles run between the facing rows of market stalls.
for x in (-8.15,8.15):
    reserve_life(rectangle(x,-31,1.9,33))
stall_stock = {
    'Produce_Stall': ('Produce_Basket','Vegetable_Crate','Basket_Empty','Produce_Basket'),
    'Bread_Stall': ('Bread_Basket','Bread_Basket','Crate_Closed'),
    'Cloth_Stall': ('Rolled_Fabric','Rolled_Fabric','Crate_Closed'),
    'Flower_Stall': ('Planter','Planter','Bucket'),
    'Fish_Stall': ('Fish_Crate','Fish_Crate','Wash_Tub'),
    'Potion_Stall': ('Pottery_Jars','Pottery_Jars','Crate_Closed'),
    'Book_Stall': ('Crate_Open','Crate_Closed'),
    'Spice_Stall': ('Pottery_Jars','Grain_Sack','Market_Scale'),
    'Butcher_Stall': ('Crate_Open','Wash_Tub','Bucket'),
    'Weapon_Stall': ('Weapon_Rack','Crate_Closed','Crate_Open'),
}
for stall in [p for p in props if p['name'] in stall_stock]:
    model=stall['name'];yaw=stall['yaw']
    width,depth=[max(model_sizes[v][model][i] for v in ('old','new'))*stall['scale'] for i in (0,1)]
    anchor=(stall['x'],stall['z'],yaw)
    for i,name in enumerate(stall_stock[model]):
        scale=.75 if name in ('Planter','Wash_Tub','Weapon_Rack','Market_Scale') else .86
        w,d=[max(model_sizes[v][name][j] for v in ('old','new'))*scale for j in (0,1)]
        edge=width/2+w/2+.18
        back=depth/2+d/2+.18
        # Goods stay by the end of the counter, with stock on the service side.
        display=[(side*edge,front) for side in (-1,1) for front in (-depth*.26,-.15,.4)]
        service=[(along,back+extra) for extra in (0,.7) for along in (-width*.3,width*.3,0,-edge,edge)]
        extra_slots=[(along,offset) for offset in (back+.35,back+1.05,-depth*.28,.2)
                     for along in (edge+.35,-edge-.35,edge+.85,-edge-.85)]
        for along,offset in (display+service if i==0 else service+display)+extra_slots:
            if place_group(model+' goods',anchor,[(name,along,offset,0,scale)],market_outline):break

def furnish_edge(zone,area,items):
    bounds=square_shape(area)
    for yaw in (0,180,90,270):
        angle=math.radians(yaw)
        c,s=math.cos(angle),math.sin(angle)
        width,depth=(area['width'],area['depth']) if yaw%180==0 else (area['depth'],area['width'])
        for side in (-1,1):
            for along in (width*.24,-width*.24,0,width*.36,-width*.36):
                back=side*(depth/2-.9)
                anchor=(area['x']+c*along+s*back,area['z']-s*along+c*back,yaw+(180 if side<0 else 0))
                if place_group(zone,anchor,items,bounds):return True
    return False

# Keep signs at decisions and lamps on the side used by pedestrians.
for x,z in [(4.7,-107),(-4.7,-107),(108.5,-29.6),(108.5,-38.6),
            (4.4,-69),(-4.4,-50),(4.4,-16),(-4.6,17.3),(4.6,17.3),
            (-85,16.7),(-79,16.7),(58.5,16.7),(65.5,16.7),
            (-48.5,42),(33.5,99),(50,99),(78,99),(87.5,99),
            (60,98.2),(64.5,98.2),(-60.7,96.5),(-94.5,89),(5.1,84),(21,71.6)]:
    for dx,dz in [(0,0),(-.8,0),(.8,0),(0,-.8),(0,.8),(-1.6,0),(1.6,0)]:
        if place_group('Gate and street lighting',(x+dx,z+dz,0),[('Streetlamp',0,0,0,.9)]):break
for x,z,yaw in [(-5,-101.7,0),(4.7,-29.5,0),(-4.5,18.3,0),(65.5,17.8,0),
                (58.5,104,0),(-55.3,89.8,0),(-77.7,18.5,0),(55.9,-30,90)]:
    for dx,dz in [(0,0),(-1,0),(1,0),(0,-1),(0,1)]:
        if place_group('Wayfinding',(x+dx,z+dz,yaw),[('Signpost',0,0,0,.85)]):break

for name,x,z,yaw in [('Shop_Sign',-16.9,-38.5,0),
                     ('Shop_Sign',-36.2,-40.5,0),('Noticeboard',-22,-38,90),
                     ('Market_Scale',-14.4,-29.9,0),('Barrel',-19.5,-27.4,0),
                     ('Bucket',-18.9,-29.4,0),('Bench',-15.6,-46.5,0)]:
    for dx,dz in [(0,0),(-1,0),(1,0),(0,-1),(0,1),(-1,1),(1,-1)]:
        if place_group('Market trading and deliveries',(x+dx,z+dz,yaw),[(name,0,0,0,.85)]):break

# Larger cultivation beds belong to the spirit school garden behind the street.
for area in [a for a in activity_areas if a['name'].startswith('SpiritAcademy_Lv5 rest court')]:
    for _ in range(2):
        furnish_edge('Spirit academy cultivation',area,
                     [('Planter',-1.65,0,0,.8),('Planter',0,0,0,.8),('Planter',1.65,0,0,.8),('Bucket',2.95,0,0,.85)])
for x,z,yaw in [(35.3,102.7,0),(54.5,103.15,0),(72.6,103.15,0),(91.1,103.15,0),
                (33.8,40.4,0),(78.4,40.55,0),(-25.5,24.9,0)]:
    place_group('Public entrance notices',(x,z,yaw),[('Noticeboard',0,0,0,.82)])

for area in [a for a in activity_areas if a['use'] in ('cargo','drill')]:
    if area['use']=='drill':
        furnishing=[('Shield_Rack',0,0,0,.84),('Crate_Open',1.9,0,0,.85)]
    elif area['name'].startswith('Windmill'):
        furnishing=[('Grain_Sack',-.65,0,0,.88),('Grain_Sack',.55,0,0,.88),('Basket_Empty',1.68,0,0,.85)]
    elif area['name'].startswith('Blacksmith') or area['name'].startswith('WorkshopHouse'):
        furnishing=[('Crate_Open',-.62,0,0,.85),('Bucket',.62,0,0,.85)]
    else:
        furnishing=[('Crate_Closed',-.7,0,0,.85),('Crate_Open',.7,0,0,.85)]
    for _ in range(3):
        if not furnish_edge(area['name'],area,furnishing):break

for x,z,yaw in [(27.3,-59,0),(38.8,-59,0),(84.7,-97.5,0),(91.4,-97.5,0),
                (29.1,-57.3,0),(39.3,-57.3,0),(54,-75.8,90)]:
    place_group('Bakery and mill supplies',(x,z,yaw),[('Grain_Sack',0,0,0,.85),('Basket_Empty',1.12,0,0,.85)])

# Kitchens and wash areas share supplies across adjacent houses.
wash_centers=[(p['x'],p['z']) for p in props if p['name']=='Wash_Tub']
for p in old_drying:
    x,z,yaw=p['x'],p['z'],p['yaw']
    if any(math.hypot(x-a,z-b)<6.5 for a,b in wash_centers):continue
    laundry=[('Clothesline',0,0,0,p['scale'])]
    placed=False
    for along,back in [(-1,1.3),(1,1.3),(-2.65,0),(2.65,0),(-1,-1.3),(1,-1.3),
                       (-2.15,.85),(2.15,.85),(-2.15,-.85),(2.15,-.85)]:
        if place_group('Shared drying yard',(x,z,yaw),laundry+[('Wash_Tub',along,back,0,.84)]):
            wash_centers.append((x,z));placed=True;break
    if not placed and any(math.hypot(x-a,z-b)<12 for a,b in wash_centers):
        for along,back in [(-1,.72),(1,.72),(-2.46,0),(2.46,0),(-1,-.72),(1,-.72)]:
            if place_group('Drying near the wash yard',(x,z,yaw),laundry+[('Basket_Empty',along,back,0,.83)]):
                wash_centers.append((x,z));break

for garden in gardens:
    x,z,w,d,yaw=(garden['x'],garden['z'],garden['width'],garden['depth'],garden['yaw'])
    angle=math.radians(yaw);c,s=math.cos(angle),math.sin(angle)
    if garden.get('flowers'):
        items=[('Planter',-.88,0,0,.72),('Bucket',.55,0,0,.86)]
    else:
        items=[('Basket_Empty',-.6,0,0,.83),('Bucket',.6,0,0,.84)]
    for along,back,turn in [(0,-d/2-.72,0),(0,d/2+.72,180),(-w/2-.72,0,90),(w/2+.72,0,-90)]:
        if place_group('Garden tools',(x+c*along+s*back,z-s*along+c*back,yaw+turn),items):break

# Finish existing work stations where their working side has room for a companion item.
for p in list(props):
    name,zone=p['name'],p['zone']
    if name=='Wash_Tub' and 'Stall' not in zone:
        companion='Bucket'
    elif name=='Bench' and ('court' in zone.lower() or zone=='Civic courts'):
        companion='Planter'
    elif name=='Crate_Closed' and zone in ('Street life / delivery','Shared courts / cargo'):
        companion='Crate_Open'
    else:
        continue
    if any(q['name']==companion and math.hypot(p['x']-q['x'],p['z']-q['z'])<2.7 for q in props):
        continue
    w=max(model_sizes[v][name][0] for v in ('old','new'))*p['scale']
    size=.75 if companion=='Planter' else .86
    gap=max(model_sizes[v][companion][0] for v in ('old','new'))*size/2
    for along,back in ((w/2+gap+.23,0),(-w/2-gap-.23,0),(w/2+gap+.3,.5),(-w/2-gap-.3,.5)):
        if place_group('Supplies beside '+zone.rsplit('/',1)[-1].strip(),(p['x'],p['z'],p['yaw']),
                       [(companion,along,back,0,size)]):break

# Shop and workshop doorways get recognizable stock at the end of the frontage.
for b in buildings:
    if b['name'] not in ('MerchantHouse','WorkshopHouse','Market_Hall','Tavern','Bakery','Guildhall'):continue
    merchant=b['name'] in ('MerchantHouse','Market_Hall')
    name='Produce_Basket' if merchant else 'Bread_Basket' if b['name']=='Bakery' else 'Pottery_Jars' if b['name']=='Tavern' else 'Crate_Open'
    anchor=(b['x'],b['z'],b['yaw'])
    for side in (-1,1):
        along=side*(b['width']/2-1)
        edge=-b['depth']/2
        for dx,dz in [(along,edge-.74),(along,edge-1.35),(side*(b['width']/2+.84),edge+.9),
                      (side*(b['width']/2+.84),edge+2.1)]:
            if place_group('Shop and workshop stock',anchor,[(name,dx,dz,0,.84)]):
                place_group('Shop and workshop stock',anchor,[('Shop_Sign' if merchant else 'Barrel',dx-side*1.35,dz,0,.83)])
                break

# Residential garden entrances need a reachable container, with gaps between decorated doors.
doorstep=[]
for b in buildings:
    if b['name']!='Fantasy_House':continue
    x,z,yaw=b['x'],b['z'],b['yaw']
    if any(math.hypot(x-a,z-c)<7.5 for a,c in doorstep):continue
    neighbor=any(math.hypot(x-g['x'],z-g['z'])<8 for g in gardens)
    name='Basket_Empty' if neighbor else 'Pottery_Jars'
    for side in (-1,1):
        if any(place_group('Garden and household doorways',(x,z,yaw),[(name,dx,dz,0,.84)])
               for dx,dz in [(side*(b['width']/2-.85),-b['depth']/2-.83),
                             (side*(b['width']/2+.88),-b['depth']/2+1.06)]):
            doorstep.append((x,z));break

# Mark the mouths of the important streets and leave the crossing open.
for name,cx,cz,yaw in [
        ('Signpost',0,-100,0),('Signpost',0,-34,0),('Signpost',0,68,0),('Signpost',0,101.4,0),
        ('Signpost',62,38,0),('Signpost',62,68,0),('Signpost',62,101.4,0),('Signpost',-58,92,0),
        ('Streetlamp',0,-100,0),('Streetlamp',0,-67,0),('Streetlamp',0,38,0),('Streetlamp',0,68,0),
        ('Streetlamp',0,101.4,0),('Streetlamp',62,38,0),('Streetlamp',62,68,0),
        ('Streetlamp',-58,68,0),('Streetlamp',-62,-41,0),('Streetlamp',-62,-58.5,0),
        ('Streetlamp',77.5,-34,0),('Streetlamp',100,-34,0),('Streetlamp',62,14.2,0),
        ('Bollard',0,-100,0),('Bollard',77.5,-34,0),('Bollard',100,-34,0),('Bollard',43,1.8,0)]:
    placed=False
    for side in (1,-1):
        if placed:break
        for along,back in [(3,3),(4.6,3),(3,4.6),(5.5,4.2),(-3,3),(-4.6,3),(-3,4.6),(-5.5,4.2)]:
            x,z=cx+along,cz+side*back
            gap=5 if name=='Streetlamp' else 3
            if any(p['name']==name and math.hypot(x-p['x'],z-p['z'])<gap for p in props):continue
            if place_group('Junction fixtures',(x,z,yaw),[(name,0,0,0,.87)]):
                placed=True;break

# The two schools use different equipment in their grounds and waiting areas.
for area in [a for a in activity_areas if a['name'].startswith('SpiritAcademy_Lv5 rest court')]:
    for _ in range(4):
        if not furnish_edge('Spirit academy cultivation',area,[('Planter',-.78,0,0,.72),('Bucket',.67,0,0,.85)]):
            break

# A street seat needs a clear approach and a reason to stop. The training
# dummies are already part of the warrior academy's enclosed practice court.
props[:] = [p for p in props if p['name'] not in ('Bench','Training_Dummy')]
reset_furnishing_space()
for bounds in east_bounds.values():reserve_life(bounds)
for x in (-8.15,8.15):reserve_life(rectangle(x,-31,1.9,33))
reserve_life(rectangle(-22.9,-44,1.65,2.2))

for zone,x,z,yaw in [('Northwest neighborhood well',-62.8,84.5,270),
                     ('Northeast neighborhood well',106.85,138.2,180),
                     ('Eastern neighborhood well',96.25,28.5,90)]:
    angle=math.radians(yaw)
    drawing_space=rectangle(x+2.19*math.cos(angle),z-2.19*math.sin(angle),1.35,1.45,yaw)
    if not space_free(drawing_space) or not place_group(zone,(x,z,yaw),
                                                       [('Well',0,0,0,.83),('Bucket',1.94,-1.29,0,.87)]):
        raise ValueError('The water-drawing area is blocked: '+zone)
    reserve_life(drawing_space)

seat_positions=[]
def seat(zone,x,z,yaw):
    scale = .88
    if any(math.hypot(x-a,z-b)<5.5 for a,b in seat_positions):return False
    width,depth=[max(model_sizes[v]['Bench'][i] for v in ('old','new'))*scale for i in (0,1)]
    angle=math.radians(yaw)
    step=depth/2+.08+.625
    foot_space=rectangle(x-step*math.sin(angle),z-step*math.cos(angle),width,1.25,yaw)
    footprint=rectangle(x,z,width,depth,yaw,.08)
    if not space_free(footprint) or not space_free(foot_space):return False
    prop('Bench',x,z,yaw,scale,zone)
    seat_positions.append((x,z))
    reserve_life(footprint)
    reserve_life(foot_space)
    return True

for zone,options in [
    ('Museum east shade',[(94.35,61.05,270),(94.1,60.4,270)]),
    ('Museum west shade',[(69.95,61,90),(69.95,59.8,90)]),
    ('Clinic west waiting', [(-64.5,42.9,0),(-65.1,42.8,0)]),
    ('Clinic east waiting', [(-55.5,42.9,0),(-54.9,42.8,0)]),
    ('Library garden seat',[(27.05,49.1,90),(27.05,50.8,90)]),
    ('Library reading court',[(42.7,62.45,0),(44.4,63.1,180),(38.2,62.65,180)]),
    ('Fountain east seat',[(20.6,82,90),(20.8,81.6,90)]),
    ('Fountain north seat',[(14.7,86.35,0),(14.4,86.1,0)]),
    ('Royal garden walk',[(-28.7,136,90),(-28.7,132.8,90),(-28.7,143,90)]),
    ('Tavern side seat',[(-45.85,-60.5,270)]),
    ('Market waiting seat',[(-15.6,-46.5,0),(-17.6,-45.3,270)]),
    ('Market north seat',[(16,-18.2,0)]),
    ('Cathedral approach seat',[(-42.3,20.2,0)]),
    ('Northwest well shade',[(-65.2,87.95,0)]),
    ('Northeast well shade',[(104.1,141.7,0)]),
    ('Eastern street seat',[(96.5,34.1,180)]),
    ('Southwest wash garden',[(-37,-76.6,180)]),
    ('Royal household garden',[(-39.8,104.8,270)]),
    ('Fountain neighborhood garden',[(8.5,95.5,90)]),
]:
    for x,z,yaw in options:
        if seat(zone,x,z,yaw):break

# Keep a single seat on the edge of each smaller rest court. Face its open
# space and prefer a side close to a tree.
for area in activity_areas:
    if area['use']!='rest' or area['name'].startswith(('Museum','Library','Royal_Castle','Tavern')):continue
    x,z,w,d=(area[k] for k in ('x','z','width','depth'))
    spots=[(x+side*(w/2-.55),z+d*offset,90*side)
           for side in (-1,1) for offset in (0,-.27,.27)]
    spots += [(x+w*offset,z+side*(d/2-.55),0 if side>0 else 180)
              for side in (-1,1) for offset in (0,-.27,.27)]
    trees=[p for p in props if p['name'].startswith('Tree_') and math.hypot(x-p['x'],z-p['z'])<10]
    spots.sort(key=lambda s:min((abs(math.hypot(s[0]-p['x'],s[1]-p['z'])-2.7) for p in trees),default=0))
    for xx,zz,yaw in spots:
        if seat(area['name']+' seat',round(xx,3),round(zz,3),yaw):break

assert all(p['name']!='Training_Dummy' for p in props), 'Training dummies belong to the warrior academy prefab.'

frontages=[]
all_prop_shapes=[prop_shape(p) for p in props]
assert not any(overlaps(shape,s) for shape in all_prop_shapes for s in clearance), 'A furnishing blocks a plot, street or entrance.'
assert not any(overlaps(a,b) for i,a in enumerate(all_prop_shapes) for b in all_prop_shapes[i+1:]), 'Furnishings overlap.'
assert not any(p['zone']!='Workers service court' and any(overlaps(shape,s) for s in fixed_structures) for p,shape in zip(props,all_prop_shapes)), 'A furnishing overlaps a built service fixture.'

assert all(reaches_street((p['points'][-1]['x'],p['points'][-1]['z'])) for p in access), 'An entrance no longer reaches the finished paving.'
assert all(a['x']==b['x'] or a['z']==b['z'] for route in roads+access
           for a,b in zip(route['points'],route['points'][1:])), 'A street or approach runs diagonally.'
for building,path in zip(buildings,access):
    along, across = ('x','z') if abs(math.sin(math.radians(building['yaw'])))>.5 else ('z','x')
    start,end = path['points'][:2]
    assert abs(start[across]-building[across])<.001 and start[across]==end[across] and start[along]!=end[along], \
        'An approach leaves the center of a facade: '+path['name']
fountain_base = rectangle(fountain_x,fountain_z,14,14)
assert not any(overlaps(fountain_base,shape) for shape in street_boxes(0)), 'The fountain pedestal overlaps a street.'
road_shapes=[]
for r in roads:
    pieces=[]
    for a,b in zip(r['points'],r['points'][1:]):
        dx,dz=b['x']-a['x'],b['z']-a['z']
        pieces.append(rectangle((a['x']+b['x'])/2,(a['z']+b['z'])/2,r['width']+.04,math.hypot(dx,dz)+.04,math.degrees(math.atan2(dx,dz))))
    road_shapes.append(pieces)
assert not any(overlaps(p,river_shape) for b,p in zip(buildings,plots) if b['name']!='Watermill'), 'A building plot crosses the river.'
crossings={'Royal bridge crossing','Residential bridge crossing','Scholars bridge crossing'}
assert not any(r['name'] not in crossings and any(overlaps(s,river_shape) for s in shapes)
               for r,shapes in zip(roads,road_shapes)), 'A street crosses the river away from a bridge.'
connected={0}
while True:
    more={j for i in connected for j in range(len(road_shapes)) if any(overlaps(a,b) for a in road_shapes[i] for b in road_shapes[j])}
    if more <= connected:break
    connected |= more
assert len(connected)==len(road_shapes), 'Disconnected streets: '+str([roads[i]['name'] for i in range(len(road_shapes)) if i not in connected])
review=ROOT/'Artifacts/CountryComparison'
review.mkdir(parents=True,exist_ok=True)
city_area=abs(sum(a[0]*b[1]-a[1]*b[0] for a,b in zip(WALL,WALL[1:]+WALL[:1])))/2
building_area=sum(b['width']*b['depth'] for b in buildings)
bends=[]
for r in roads:
    for a,b,c in zip(r['points'],r['points'][1:],r['points'][2:]):
        incoming=(b['x']-a['x'],b['z']-a['z']);outgoing=(c['x']-b['x'],c['z']-b['z'])
        cosine=sum(x*y for x,y in zip(incoming,outgoing))/(math.hypot(*incoming)*math.hypot(*outgoing))
        bends.append(math.degrees(math.acos(max(-1,min(1,cosine)))))
minor_bends=sum(1<angle<30 for angle in bends)
assert building_area/city_area>=.4, 'Building coverage must stay at least 40%.'
assert minor_bends<=10, 'Small road bends fragment the building blocks.'
(review/'plan_validation.json').write_text(json.dumps(dict(status='PASS',building_plots=len(buildings),overlapping_building_plots=0,
    building_street_overlaps=0,entrance_paths_crossing_other_plots=0,
    planned_building_area_m2=round(building_area,2),
    city_area_m2=city_area,building_coverage_percent=round(building_area/city_area*100,2),
    road_bends=sum(angle>1 for angle in bends),minor_road_bends=minor_bends,
    diagonal_street_or_approach_segments=0,off_center_approaches=0,
    streets=len(roads),connected_streets=len(roads),entrance_paths=len(access),entrances_reaching_paving=len(access),
    river_building_overlaps=0,streets_crossing_river_away_from_bridges=0,main_river_bridges=len(crossings),
    watchtowers=len(watchtowers),watchtower_building_street_overlaps=0,market_stalls=16,market_stall_types=len(stall_types),
    frontage_paving=len(frontages),new_kitchen_gardens=new_gardens,street_life_groups=len(life_groups),street_life_props=len(props)-life_start,
    furnished_buildings=len({g['building'] for g in life_groups})),indent=2)+'\n')
OUT.mkdir(parents=True,exist_ok=True)
for b in buildings:b.pop('frontageTarget',None)
plan=dict(pavedTown=False,activityAreas=activity_areas,cityHalfSize=112,cityNorth=NORTH,cityOffset=154,riverZ=RIVER_Z,riverWidth=RIVER_WIDTH,wallOutline=[dict(x=x,z=z) for x,z in WALL],watchtowers=watchtowers,roads=roads,accessPaths=access,frontages=frontages,buildings=buildings,props=props,gardens=gardens,
          squares=squares,greens=[],enclosures=enclosures,
          rationale='Stone streets cross earthen squares and connect both gates with the market, bridge and palace. Streets run on the city axes and approaches leave the center of each facade. The northeast academies face one shared street with matching front setbacks. Both model sets share the same plan.')
(OUT/'town_plan.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2)+'\n')
print('TOWN_PLAN',len(buildings),'buildings',len(props),'props',len(roads),'connected streets',len(access),'entrance paths',len(gardens),'rear gardens')
(review/'coherence_plan_validation.json').write_text(json.dumps(dict(status='PASS',relocated_existing_props=relocated,activity_courts=court_count,
    activity_props=len(props)-activity_start,all_props_checked=len(props),prop_plot_street_entrance_overlaps=0,prop_pair_overlaps=0,prop_structure_reserve_overlaps=0),indent=2)+'\n')
