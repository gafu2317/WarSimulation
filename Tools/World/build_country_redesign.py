"""Place the Country buildings and verify their street connections."""

import argparse
from dataclasses import dataclass
import json
import math
from pathlib import Path

from country_street_life import (
    DOORSTEP_GROUPS, HOUSEHOLD_YARDS, MARKET_STOCK, NEIGHBORHOOD_GROUPS, NEIGHBORHOOD_SPOTS,
    PLANTED_EDGES, PROP_COMPANIONS, SHOPFRONTS, SMALL_YARDS, STREET_YARDS, WAGON_STOPS,
    YARD_COMPANIONS, YARD_ROWS,
)


ROOT = Path(__file__).resolve().parents[2]
BASE_PLAN = ROOT / 'Assets/Data/CountryComparison/town_plan.json'
BRIDGES = ('Royal bridge crossing', 'Residential bridge crossing', 'Scholars bridge crossing')
REPLACEMENTS = {'VelvetTerrace': 'Ballroom', 'LanternSpire': 'MasqueradeSalon'}
HOMES = {'Fantasy_House','MerchantHouse','WorkshopHouse'}


@dataclass(frozen=True)
class Box:
    left: float
    south: float
    right: float
    north: float

    @classmethod
    def around(cls, x, z, width, depth, yaw=0):
        if yaw % 180:
            width, depth = depth, width
        return cls(x-width/2, z-depth/2, x+width/2, z+depth/2)

    def overlaps(self, other, margin=0):
        return (self.left < other.right+margin-1e-7 and other.left < self.right+margin-1e-7
                and self.south < other.north+margin-1e-7 and other.south < self.north+margin-1e-7)

    def contains(self, point):
        x, z = point
        return self.left-1e-7 <= x <= self.right+1e-7 and self.south-1e-7 <= z <= self.north+1e-7

    def inside(self, wall, inset=1.25):
        return all((b[0]-a[0])*(z-a[1])-(b[1]-a[1])*(x-a[0]) >= inset*math.dist(a,b)
                   for x in (self.left,self.right) for z in (self.south,self.north)
                   for a,b in zip(wall,wall[1:]+wall[:1]))


def coords(route):
    return [(p['x'], p['z']) for p in route['points']]


def points(pairs):
    return [dict(x=round(x,4), z=round(z,4)) for x,z in pairs]


def rotate(x, z, yaw):
    return ((x,z),(z,-x),(-x,-z),(-z,x))[(yaw//90)%4]


def segment(a, b, width):
    ax, az = a
    bx, bz = b
    assert (ax == bx) != (az == bz), (a,b)
    if ax == bx:
        return Box(ax-width/2,min(az,bz),ax+width/2,max(az,bz))
    return Box(min(ax,bx),az-width/2,max(ax,bx),az+width/2)


def route_boxes(route):
    centers = coords(route)
    return [segment(a,b,route['width']) for a,b in zip(centers,centers[1:])]


def rectangle(record):
    return Box.around(record['x'],record['z'],record['width'],record['depth'],record.get('yaw',0))


class Layout:
    def __init__(self, baseline, dimensions):
        self.dimensions = dimensions
        self.wall = coords({'points':baseline['wallOutline']})
        self.water = Box.around(0,baseline['riverZ'],224,baseline['riverWidth'])
        self.plan = {k:baseline[k] for k in ('cityHalfSize','cityNorth','cityOffset','riverZ','riverWidth','wallOutline','watchtowers')}
        self.plan.update(pavedTown=False,roads=[],accessPaths=[],frontages=[],buildings=[],props=[],
                         gardens=[],squares=[],greens=[],enclosures=[],activityAreas=[],
                         amenityPaths=[],secondaryAccessPaths=[],
                         gates=[dict(x=0,z=-112,yaw=0,width=9),dict(x=112,z=-34,yaw=90,width=9)])
        self.towers = [rectangle(tower) for tower in self.plan['watchtowers']]
        self.streets = {}
        self.street_shapes = {}
        self.building_shapes = []
        self.entrances = []
        self.props = []
        self.courts = []
        self.amenities = []
        self.protected = self.towers+[Box.around(35.5,2.4,10,7),Box.around(0,-111,11,4),Box.around(110.4,-34,5.5,11)]

    def road(self, name, width, centers, *, kind='district', housing=False):
        route = dict(name=name,width=width,points=points(centers),avenue=kind=='avenue',
                     frontage=housing,kind=kind)
        shapes = route_boxes(route)
        assert not any(s.overlaps(self.water) for s in shapes) or name in BRIDGES, name
        assert not any(s.overlaps(t) for s in shapes for t in self.towers), (name,'watchtower')
        assert name in {'South gate threshold','Merchants gate road'} or all(s.inside(self.wall,.55) for s in shapes), (name,'walls')
        assert name not in self.streets
        self.streets[name] = route
        self.street_shapes[name] = shapes
        self.plan['roads'].append(route)

    def size(self, name):
        model = self.dimensions[name]
        return tuple(b-a for a,b in zip(model['low'],model['high']))

    def building(self, name, x, z, street, zone, *, yaw=0, scale=1, housing=False, width=3.4, label=None):
        source = REPLACEMENTS.get(name,name)
        model = self.dimensions[source]
        w,d,_ = self.size(source)
        record = dict(name=name,x=round(x,4),z=round(z,4),width=round(w*scale,4),depth=round(d*scale,4),
                      yaw=yaw,zone=zone,street=street,scale=scale)
        if source != name:
            record['modernModel'] = source
        if label:
            record['label'] = label
        shape = rectangle(record)
        try:
            assert shape.inside(self.wall), 'walls'
            assert name == 'Watermill' or not shape.overlaps(self.water,.5), 'river'
            assert not any(shape.overlaps(b,.65) for b in self.building_shapes), 'another building'
            assert not any(shape.overlaps(t,.2) for t in self.towers), 'watchtower'
            crossed = next((k for k,boxes in self.street_shapes.items()
                            if any(shape.overlaps(s,.20) for s in boxes)), None)
            assert crossed is None, crossed
            assert not housing or not any(shape.overlaps(c,.24) for c in self.courts+self.protected+self.amenities), 'reserved land'
            forward = rotate(0,-1,yaw)
            along = 0 if forward[0] else 1
            side = forward[along]
            assert not any(shape.overlaps(p,.12) for p in self.entrances), 'blocks another approach'
            paths,path_shapes = [],[]
            for kind,span,suffix in [('entry',width,' entrance'),('secondaryEntry',1.75,' foyer door')]:
                if kind not in model:
                    continue
                entry = model[kind]
                ex,ez = ((entry[i]-(model['low'][i]+model['high'][i])/2)*scale for i in (0,1))
                dx,dz = rotate(ex,ez,yaw)
                door = (round(x+dx,4),round(z+dz,4))
                possible = []
                for a,b in zip(coords(self.streets[street]),coords(self.streets[street])[1:]):
                    if a[along] != b[along]:
                        continue
                    if not min(a[1-along],b[1-along])+span/2 <= door[1-along] <= max(a[1-along],b[1-along])-span/2:
                        continue
                    distance = side*(a[along]-door[along])
                    if distance > .01:
                        target = list(door)
                        target[along] = round(a[along],4)
                        possible.append((distance,tuple(target)))
                assert possible, 'no street opposite opening'
                distance,target = min(possible)
                assert distance <= (8 if housing else 18), f'{distance:.1f} m to street'
                path = dict(name=name+suffix,width=span,paved=not housing,points=points([door,target]))
                path_shape = route_boxes(path)[0]
                assert not any(path_shape.overlaps(b,.07) for b in self.building_shapes+self.amenities), 'approach crosses plot'
                assert not path_shape.overlaps(self.water) and not any(path_shape.overlaps(t,.15) for t in self.towers), 'approach blocked'
                paths.append(path)
                path_shapes.append(path_shape)
            assert paths, 'missing entrance'
        except AssertionError as error:
            if housing:
                return False
            raise ValueError(f'{name} at {(x,z)}: {error}') from error
        self.building_shapes.append(shape)
        self.entrances.extend(path_shapes)
        self.plan['buildings'].append(record)
        self.plan['accessPaths'].append(paths[0])
        self.plan['secondaryAccessPaths'].extend(paths[1:])
        return True

    def court(self, name, x, z, width, depth):
        court = dict(name=name,x=x,z=z,width=width,depth=depth)
        shape = rectangle(court)
        assert shape.inside(self.wall) and not shape.overlaps(self.water), name
        assert not any(shape.overlaps(b) for b in self.building_shapes), name
        self.courts.append(shape)
        self.plan['squares'].append(court)
        return shape

    def free(self, shape, clearance=.24):
        return (shape.inside(self.wall) and not shape.overlaps(self.water,.35)
                and not any(shape.overlaps(b,clearance) for b in self.building_shapes+self.streets_flat+self.entrances+
                            self.protected+self.props+self.amenities))

    def prop(self, name, x, z, zone, *, yaw=0, scale=1, inside=None, clearance=.24):
        x,z = round(x,3),round(z,3)
        w,d,h = self.size(name)
        footprint_scale = scale
        if name.startswith('Tree_'):
            footprint_scale *= {'Tree_Shade':9,'Tree_Street':7,'Tree_AlleyCypress':8}[name]/h
        shape = Box.around(x,z,w*footprint_scale,d*footprint_scale,yaw)
        if inside and not (inside.contains((shape.left,shape.south)) and inside.contains((shape.right,shape.north))):
            return False
        if not self.free(shape,clearance):
            return False
        self.props.append(shape)
        record = dict(name=name,old=name,x=x,z=z,y=0,scale=scale,yaw=yaw,zone=zone)
        if clearance != .24:
            record['clearance'] = clearance
        self.plan['props'].append(record)
        return True

    def row(self, names, x, z, yaw, zone, scale, *, inside=None, gap=.26):
        widths = [self.size(name)[0]*scale for name in names]
        at = -(sum(widths)+gap*(len(names)-1))/2
        first = len(self.props)
        for name,width in zip(names,widths):
            dx,dz = rotate(at+width/2,0,yaw)
            if not self.prop(name,x+dx,z+dz,zone,yaw=yaw,scale=scale,inside=inside,clearance=min(.24,gap-.02)):
                del self.props[first:]
                del self.plan['props'][first:]
                return False
            at += width+gap
        return True

    def finish_streets(self):
        self.streets_flat = [s for shapes in self.street_shapes.values() for s in shapes]
        connected = {'South gate high street'}
        while True:
            hit = {name for name,shapes in self.street_shapes.items() if name not in connected
                   and any(a.overlaps(b,.015) for a in shapes for k in connected for b in self.street_shapes[k])}
            if not hit:
                break
            connected.update(hit)
        missing = self.streets.keys()-connected
        assert not missing, f'Disconnected streets: {sorted(missing)}'

    def align_facade(self,name,street,station,yaw,setback):
        normal = rotate(0,-1,yaw)
        axis = 0 if normal[0] else 1
        route = self.streets[street]
        centers = coords(route)
        line, = [a[axis] for a,b in zip(centers,centers[1:]) if a[axis]==b[axis]
                 and min(a[1-axis],b[1-axis])<station<max(a[1-axis],b[1-axis])]
        _,depth,_ = self.size(REPLACEMENTS.get(name,name))
        across = line-normal[axis]*(route['width']/2+setback+depth/2)
        return (across,station) if axis==0 else (station,across)

    def pave_frontages(self):
        for b,entry in zip(self.plan['buildings'],self.plan['accessPaths']):
            dx,dz = rotate(0,-1,b['yaw'])
            x,z = coords(entry)[-1]
            half_street = self.streets[b['street']]['width']/2
            half_depth = b['depth']/2
            edge = (x-dx*half_street if dx else b['x'],z-dz*half_street if dz else b['z'])
            facade = b['x']+dx*half_depth,b['z']+dz*half_depth
            position_gap = (edge[0]-facade[0])*dx+(edge[1]-facade[1])*dz
            if not .2<=position_gap<=1.65:
                continue
            # Extend a few centimetres into the street so adjacent paving shares an edge.
            route = dict(name=b['name']+' frontage',width=b['width']-.22,
                         points=points([(facade[0]+.01*dx,facade[1]+.01*dz),
                                        (edge[0]+.025*dx,edge[1]+.025*dz)]))
            frontage = route_boxes(route)[0]
            if not any(frontage.overlaps(shape) for shape in self.building_shapes):
                self.plan['frontages'].append(route)

    def house_rows(self):
        count = 0
        for route in self.plan['roads']:
            if not route['frontage']:
                continue
            for a,b in zip(coords(route),coords(route)[1:]):
                horizontal = a[1] == b[1]
                lo,hi = sorted((a[0],b[0]) if horizontal else (a[1],b[1]))
                fixed = a[1] if horizontal else a[0]
                for side in (1,-1):
                    cursor = lo+1.5
                    while cursor < hi-5:
                        first = ('MerchantHouse' if 'Market' in route['name'] else
                                 'WorkshopHouse' if 'Freight' in route['name'] else 'Fantasy_House')
                        primary = (first if count%4==0 or first=='Fantasy_House' else 'Fantasy_House')
                        base = (1.06,.98,1.10,.91,1.02)[count%5]
                        for asset,scale in ((primary,base),('Fantasy_House',.93),('Fantasy_House',.84)):
                            w,d,_ = self.size(asset)
                            along = cursor+w*scale/2
                            if along+w*scale/2 > hi-1.25:
                                continue
                            setback = (.56,.75,.66,.84)[count%4]
                            across = fixed+side*(route['width']/2+d*scale/2+setback)
                            x,z = (along,across) if horizontal else (across,along)
                            yaw = (0 if side==1 else 180) if horizontal else (90 if side==1 else 270)
                            if self.building(asset,x,z,route['name'],'Residential',yaw=yaw,scale=scale,width=1.45,housing=True):
                                count += 1
                                cursor += w*scale+(1.4 if count%5==0 else .88)
                                break
                        else:
                            cursor += .8
        return count

    def validate(self, baseline, houses):
        names = {b['name'] for b in self.plan['buildings']}
        missing = {b['name'] for b in baseline['buildings']} - names
        assert not missing, f'Missing placed model types: {missing}'
        required = {b['name'] for b in baseline['buildings'] if b['name'] not in HOMES}
        assert all(sum(r['name']==name for r in self.plan['buildings'])==1 for name in required | {'Plaza'})
        assert len(self.plan['accessPaths']) == len(self.plan['buildings'])
        for b,path in zip(self.plan['buildings'],self.plan['accessPaths']):
            assert any(s.contains(coords(path)[-1]) for s in self.street_shapes[b['street']]), b['name']
            assert b['name'] not in required or b['scale']==1, b['name']
        prop_names = {p['name'] for p in self.plan['props']}
        assert prop_names >= {'Bench','Well'} and any(name.startswith('Tree_') for name in prop_names)
        self.plan['counts'] = dict(modelTypes=len(names),plots=len(self.plan['buildings']),houses=houses,
                                   streets=len(self.streets),accessPaths=len(self.entrances),
                                   props=len(self.props),propTypes=len(prop_names),
                                   wells=sum(p['name']=='Well' for p in self.plan['props']),
                                   benches=sum(p['name']=='Bench' for p in self.plan['props']),
                                   trees=sum(p['name'].startswith(('Tree_','NaturalTree_')) for p in self.plan['props']))


def roads(layout):
    road = layout.road
    road('South gate threshold',9,[(0,-113),(0,-106)],kind='avenue')
    road('South gate high street',12,[(0,-106),(0,-34)],kind='avenue',housing=True)
    road('Market crossing',12,[(0,-34),(0,2)],kind='avenue')
    road('Royal bridge crossing',12,[(0,2),(0,15)],kind='avenue')
    road('Royal ascent',12,[(0,15),(0,104)],kind='avenue',housing=True)
    road('Merchants gate road',9,[(0,-34),(113,-34)],kind='avenue',housing=True)
    road('Market west street',6.4,[(-109.5,-34),(0,-34)],housing=True)
    road('Southern river quay',3.5,[(-109.5,2),(107,2)],housing=True)
    road('Northern river quay',4,[(-106,15),(-82,15),(-82,23),(-54,23),(-54,15),(107,15)])
    road('Residential bridge crossing',4.5,[(-82,2),(-82,15.5)],kind='bridge')
    road('Scholars bridge crossing',5.4,[(62,2),(62,15)],kind='bridge')

    road('Civic west street',5.4,[(-101,25.5),(0,25.5)],housing=True)
    road('Bellfounders lane',4.8,[(-101,73.5),(0,73.5)],housing=True)
    road('Western civic lane',3.6,[(-101,15),(-101,153)],kind='lane',housing=True)
    road('West residential street',3.7,[(-101,107),(-29.5,107)],kind='lane',housing=True)
    road('Observatory lane',3.8,[(-101,124),(-46.9,124)],kind='lane',housing=True)
    road('Royal household lane',4.2,[(-46.9,73.5),(-46.9,156.5)],housing=True)
    road('Northwestern lane',3.6,[(-101,153),(-65,153),(-65,156.5),(69.3,156.5)],kind='lane',housing=True)
    road('Churchyard lane',3.2,[(-77,25.5),(-77,73.5)],kind='lane',housing=True)
    road('Bath lane',3,[(-101,65),(-77,65)],kind='lane',housing=True)
    road('Bell lane',3.2,[(-41,25.5),(-41,73.5)],kind='lane',housing=True)
    road('Palace west lane',3.4,[(-29.5,107),(-29.5,156.5)],kind='lane',housing=True)

    road('East river street',5.4,[(0,22.5),(108,22.5)],housing=True)
    road('East terrace',5.8,[(0,59.9),(108,59.9)],housing=True)
    road('Academy street',6.4,[(-9,103.9),(108,103.9)],housing=True)
    road('Scholars street',5.4,[(62,15),(62,103.9)],housing=True)
    road('Eastern academy lane',3.2,[(108,15),(108,123.5)],kind='lane',housing=True)
    road('East gardens lane',3.2,[(108,88),(38,88),(38,103.9)],kind='lane',housing=True)
    road('Academy garden lane',2.8,[(69.3,103.9),(69.3,156.5)],kind='lane')
    road('Library lane',3.2,[(69.3,123.5),(108,123.5)],kind='lane',housing=True)
    road('Academy rear lane',3.2,[(69.3,152),(101,152)],kind='lane',housing=True)

    road('Arena approach',4.8,[(-84.7,-107),(0,-107)],housing=True)
    road('West tournament lane',3.4,[(-96.7,-91.4),(-96.7,-34)],kind='lane',housing=True)
    road('Western market lane',4.2,[(-48,2),(-48,-107)],housing=True)
    road('West houses street',3.6,[(-96.7,-49.2),(0,-49.2)],kind='lane',housing=True)
    road('Arena north alley',3.2,[(-96.7,-63),(-48,-63)],kind='lane',housing=True)
    road('Market quay service lane',3.2,[(-109.5,-34),(-109.5,2)],kind='lane',housing=True)
    road('South east street',5.2,[(0,-76),(105,-76)],housing=True)
    road('South garden lane',3.2,[(55.5,-34),(55.5,-101.5)],kind='lane',housing=True)
    road('East gate lane',4.6,[(74,-34),(74,-101.5)],housing=True)
    road('Eastern roof lane',3.6,[(106,-76),(106,2)],kind='lane',housing=True)
    road('South freight lane',3.2,[(0,-101.5),(94,-101.5)],kind='lane',housing=True)
    layout.finish_streets()


def facilities(layout):
    layout.building('Royal_Castle',0,131,'Academy street','Royal precinct',width=10,label='王城')
    layout.building('Watermill',-67,14.9,'Northern river quay','Mill quay',yaw=90,width=2.3,label='水車小屋')
    rows = [
        ('Church','Civic west street',-25,0,1.15,'Cathedral close',3.68,'教会'),
        ('Clinic','East river street',24,0,.95,'Civic quarter',2.88,'診療所'),
        ('Plaza','Royal ascent',79,90,.85,'Civic quarter',4.32,'噴水広場'),
        ('Library','Library lane',87,0,.92,'Civic quarter',3.36,'図書館'),
        ('Museum','Merchants gate road',89,0,1.15,'Civic quarter',3.52,'美術館'),
        ('Observatory','Observatory lane',-81,0,.95,'Civic quarter',2.88,'天文台'),
        ('WarriorAcademy_Lv5','Bellfounders lane',-76,0,1.15,'Academy quarter',1.58,'戦士学校'),
        ('ArcaneAcademy_Lv5','East terrace',89.9,0,1.05,'Academy quarter',3.84,'魔法学校'),
        ('SpiritAcademy_Lv5','Academy street',49.5,0,1.15,'Academy quarter',3.84,'精霊使役学校'),
        ('Arena','Arena approach',-74.5,0,1.58,'Tournament quarter',4.4,'闘技場'),
        ('Guardhouse','Arena approach',-24,0,.95,'Gate ward',3.2,'衛兵所'),
        ('Tavern','Market west street',-65,0,1.05,'Entertainment quarter',2.72,'酒場'),
        ('LanternSpire','East river street',92.6,0,1.05,'Entertainment quarter',2.88,'仮面のサロン'),
        ('CrimsonRowhouse','Merchants gate road',24,0,1.05,'Entertainment quarter',3.68,'演芸館'),
        ('VeiledCourtyard','South east street',38,0,.95,'Entertainment quarter',3.2,'中庭館'),
        ('Casino','South east street',91,0,1.1,'Entertainment quarter',2.88,'カジノ'),
        ('VelvetTerrace','Bellfounders lane',-30,0,1.05,'Entertainment quarter',3.52,'舞踏館'),
        ('Blacksmith','West houses street',-28,180,1.0,'Craft and freight quarter',3.2,'鍛冶場'),
        ('Chapel','Civic west street',-91,0,.80,'Western residential',2,'礼拝堂'),
        ('Bathhouse','Churchyard lane',53,270,.8,'Civic quarter',2.6,'浴場'),
        ('Barracks','Bellfounders lane',-55,180,.8,'Military quarter',2.8,'兵舎'),
        ('Windmill','Northwestern lane',-58.16,180,.95,'Military quarter',2,'風車'),
        ('Guildhall','South gate high street',-57,90,.85,'Market quarter',1.8,'ギルド会館'),
        ('Bakery','Market west street',-85.9,0,.85,'Market quarter',2,'パン屋'),
        ('Granary','Market west street',-99.65,0,.85,'Market quarter',2.8,'穀物庫'),
        ('Market_Hall','Market west street',-34.6,0,.95,'Market quarter',1.8,'市場会館'),
        ('Warehouse','Merchants gate road',52,0,.95,'Craft and freight quarter',3.4,'倉庫'),
        ('Stable','South east street',94,180,.75,'Craft and freight quarter',2.8,'厩舎'),
        ('WorkshopHouse','Market west street',-86.9,180,.95,'Craft and freight quarter',2,'工房'),
    ]
    for name,street,station,yaw,setback,zone,width,label in rows:
        x,z = layout.align_facade(name,street,station,yaw,setback)
        layout.building(name,x,z,street,zone,yaw=yaw,width=width,label=label)


def furnish_market(layout, court):
    stalls = ('Produce_Stall','Bread_Stall','Cloth_Stall','Flower_Stall','Fish_Stall',
              'Potion_Stall','Book_Stall','Spice_Stall','Butcher_Stall','Weapon_Stall')
    rows = (-43.5,-39.3,-27.8,-23.2,-18.6,-14)
    for x in (-18.24,-12.76):
        aisle = dict(name='Market shopping aisle',width=2.4,points=points([(x,court.south),(x,court.north)]))
        layout.plan['amenityPaths'].append(aisle)
        layout.protected.extend(route_boxes(aisle))
    market_stalls = []
    for i,z in enumerate(rows):
        for j,(x,yaw) in enumerate(((-21.8,270),(-9.6,90))):
            name = stalls[(2*i+j)%len(stalls)]
            assert layout.prop(name,x,z,'Market square',yaw=yaw,scale=.86,inside=court), (name,x,z)
            market_stalls.append(layout.plan['props'][-1])
    layout.prop('Noticeboard',-19.9,-45.3,'Market square',scale=.76,inside=court)
    for stall in market_stalls:
        w,d,_ = layout.size(stall['name'])
        for i,name in enumerate(MARKET_STOCK[stall['name']]):
            a,b,_ = layout.size(name)
            a,b = a*.82,b*.82
            front,back = (-d*.28,d*.43)
            edge = .43*w+a/2+.27
            end = .43*d+b/2+.27
            slots = [(sx*(edge+extra),depth) for extra in (0,.36,.72) for sx in (-1,1)
                     for depth in (front,0,back,end)]
            if i:
                slots = [(along,end+extra) for extra in (0,.34) for along in (0,-w*.3,w*.3)]+slots
            for u,v in slots:
                dx,dz = rotate(u,v,stall['yaw'])
                if layout.prop(name,stall['x']+dx,stall['z']+dz,'Market stock',yaw=stall['yaw'],scale=.82,inside=court):
                    break
    for name,x,z,yaw,size in (
        ('Handcart',-15.52,-23.6,0,.88),('Crate_Closed',-15.43,-20.64,0,.83),
        ('Crate_Open',-16.26,-15.55,0,.83),('Produce_Basket',-14.89,-15.49,0,.88),
        ('Market_Scale',-15.66,-16.58,0,.83),('Barrel',-16.13,-43.2,0,.93),
        ('Crate_Closed',-14.91,-43.03,0,.85),('Grain_Sack',-15.42,-44.46,0,.90),
    ):
        assert layout.prop(name,x,z,'Market loading and weighing',yaw=yaw,scale=size,inside=court),name


def roadside_sites(route,anchor,width,depth,radius):
    candidates = []
    for a,b in zip(coords(route),coords(route)[1:]):
        horizontal = a[1] == b[1]
        axis = 0 if horizontal else 1
        lo,hi = sorted((a[axis],b[axis]))
        for station in range(math.ceil(lo+width/2),math.floor(hi-width/2)+1):
            roadside = (station,a[1]) if horizontal else (a[0],station)
            for yaw in (0,180) if horizontal else (90,270):
                dx,dz = rotate(0,route['width']/2+depth/2+.16,yaw)
                x,z = roadside[0]+dx,roadside[1]+dz
                distance = math.dist(anchor,(x,z))
                if distance <= radius:
                    candidates.append((distance,x,z,yaw,roadside))
    return sorted(candidates)


def furnish_courts(layout):
    for street,anchor,label,use in STREET_YARDS:
        route = layout.streets[street]
        depth,width = (6.9,8.4) if use=='freight' else (5.8,8.0)
        for _,x,z,yaw,roadside in roadside_sites(route,anchor,width,depth,19):
            shape = Box.around(x,z,width,depth,yaw)
            if not layout.free(shape,.13) or any(shape.overlaps(c,.18) for c in layout.courts):
                continue
            first = len(layout.props)
            rows = YARD_ROWS[use]
            offsets = (-.78,2.04) if use=='freight' else (-.24,1.68)
            for names,offset in zip(rows,offsets):
                dx,dz = rotate(0,offset,yaw)
                if not layout.row(names,x+dx,z+dz,yaw,label,.85,inside=shape,gap=.37):
                    del layout.props[first:]
                    del layout.plan['props'][first:]
                    break
            else:
                area = dict(name=label,x=x,z=z,width=shape.right-shape.left,depth=shape.north-shape.south,use=use)
                layout.plan['greens' if use=='garden' else 'squares'].append(area)
                layout.plan['activityAreas'].append(area)
                dx,dz = rotate(0,-depth/2+.45,yaw)
                path = dict(name=label,width=1.6,points=points([roadside,(x+dx,z+dz)]))
                layout.plan['amenityPaths'].append(path)
                layout.protected.extend(route_boxes(path))
                layout.amenities.append(shape)
                break


def furnish_planted_edges(layout):
    patterns = (
        ('Tree_Street','Bench','Planter'),
        ('Flower_Border','Flower_Border','Bucket'),
        ('Planter','Bench','Planter'),
        ('Flower_Border','Tree_Street','Flower_Border'),
    )
    for i,(street,anchor) in enumerate(PLANTED_EDGES):
        route = layout.streets[street]
        width,depth = (6.9,3.28) if i%4!=2 else (5.3,3.0)
        names = patterns[i%4]
        for _,x,z,yaw,street_center in roadside_sites(route,anchor,width,depth,7.2):
            shape = Box.around(x,z,width,depth,yaw)
            if not layout.free(shape,.09) or any(shape.overlaps(c,.10) for c in layout.courts):
                continue
            dx,dz = rotate(0,.16 if 'Tree_Street' in names else .53,yaw)
            if not layout.row(names,x+dx,z+dz,yaw,'Planted street edge',.53,inside=shape,gap=.62):
                continue
            dx,dz = rotate(0,-depth/2+.28,yaw)
            path = dict(name='Planted street edge',width=1.25,points=points([street_center,(x+dx,z+dz)]))
            layout.plan['amenityPaths'].append(path)
            layout.protected.extend(route_boxes(path))
            area = dict(name='Planted street edge',x=x,z=z,width=shape.right-shape.left,depth=shape.north-shape.south,use='rest')
            layout.plan['greens'].append(area)
            layout.plan['activityAreas'].append(area)
            layout.amenities.append(shape)
            break


def furnish_doorsteps(layout):
    details = ('Pottery_Jars','Basket_Empty','Bucket','Crate_Open','Barrel','Planter')
    for i,b in enumerate(layout.plan['buildings']):
        asset = b['name']
        house = not b.get('label')
        names = SHOPFRONTS.get(asset,(details[i%len(details)],) if house else ())
        for j,name in enumerate(names):
            scale = .74 if house else .86
            w,d,_ = layout.size(name)
            sign = 1 if (i+j)%2==0 else -1
            slots = [(side*b['width']*fraction,-b['depth']/2-d*scale/2-gap)
                     for gap in (.12,.43,.8,1.25) for side in (sign,-sign) for fraction in (.31,.43,.19)]
            slots.extend((side*(b['width']/2+w*scale/2+.18),-b['depth']/2+back)
                         for side in (sign,-sign) for back in (1.1,2.5,4.0))
            for u,v in slots:
                dx,dz = rotate(u,v,b['yaw'])
                if layout.prop(name,b['x']+dx,b['z']+dz,
                               'Doorstep details' if house else f'{asset} forecourt',
                               yaw=b['yaw'],scale=scale,clearance=.10):
                    break


def furnish_households(layout, recipes=HOUSEHOLD_YARDS):
    for i,(b,entry) in enumerate(zip(layout.plan['buildings'],layout.plan['accessPaths'])):
        if b.get('label'):
            continue
        zone,names = recipes[i%len(recipes)]
        yaw,x,z = b['yaw'],b['x'],b['z']
        road_x,road_z = coords(entry)[-1]
        _,edge = rotate(road_x-x,road_z-z,-yaw)
        edge += layout.streets[b['street']]['width']/2
        half = b['depth']/2
        for side in ((-1,1) if i%2==0 else (1,-1)):
            u = side*(b['width']/2+.43)
            dx,dz = rotate(u,edge+.032,yaw)
            start = x+dx,z+dz
            dx,dz = rotate(u,half+1.54,yaw)
            stop = x+dx,z+dz
            path = segment(start,stop,.76)
            if not layout.free(path,.02):
                continue
            layout.protected.append(path)
            for at in (1.35,2.38,3.30):
                dx,dz = rotate(-side*.24,half+at,yaw)
                if layout.row(names,x+dx,z+dz,yaw,zone,.76,gap=.26):
                    dx,dz = rotate(u,edge-.025,yaw)
                    layout.plan['amenityPaths'].append(dict(name=zone,width=.76,gravel=True,
                                                            points=points([(x+dx,z+dz),stop])))
                    break
            else:
                layout.protected.pop()
                continue
            break


def furnish_lane_edges(layout):
    signs = {'Churchyard lane','Observatory lane','Academy street','Library lane',
             'Market west street','Arena approach','South east street'}
    for route in layout.plan['roads']:
        if route['name'] in BRIDGES:
            continue
        for a,b in zip(coords(route),coords(route)[1:]):
            horizontal = a[1] == b[1]
            axis = 0 if horizontal else 1
            lo,hi = sorted((a[axis],b[axis]))
            for i,station in enumerate(range(math.ceil(lo+7),math.floor(hi-5),22)):
                edge = route['width']/2
                name = 'Signpost' if i==0 and route['name'] in signs else 'Streetlamp'
                side = 1 if (i+axis)%2 else -1
                slots = ((station+delta,a[1-axis]+s*(edge+extra),s)
                         for extra in (.57,1.05,1.56) for delta in (0,1.6,-1.4,3,-3) for s in (side,-side))
                for along,across,s in slots:
                    x,z = (along,across) if horizontal else (across,along)
                    yaw = (0 if s==1 else 180) if horizontal else (90 if s==1 else 270)
                    if any(p['name'] in {'Signpost','Streetlamp'} and math.hypot(x-p['x'],z-p['z'])<8.5
                           for p in layout.plan['props']):
                        continue
                    if not layout.prop(name,x,z,'Lane lights and wayfinding',yaw=yaw,scale=.87,clearance=.15):
                        continue
                    if i%3==1:
                        dx,dz = rotate(1.0,.29,yaw)
                        layout.prop('Planter',x+dx,z+dz,'Lane lights and wayfinding',yaw=yaw,scale=.71,clearance=.15)
                    break


def rest_pocket(layout, street, at, label, *, well=False, radius=39):
    route = layout.streets[street]
    depth = 4.7 if well else 4.2
    width = 7 if well else 6.5
    candidates = []
    for a,b in zip(coords(route),coords(route)[1:]):
        horizontal = a[1] == b[1]
        axis = 0 if horizontal else 1
        near,far = sorted((a[axis],b[axis]))
        fixed = a[1-axis]
        for along in range(math.ceil(near+width/2),math.floor(far-width/2)+1):
            for side in (1,-1):
                base = (along,fixed) if horizontal else (fixed,along)
                x,z = base
                normal = (0,side) if horizontal else (side,0)
                center = (x+normal[0]*(route['width']/2+depth/2+.12),
                          z+normal[1]*(route['width']/2+depth/2+.12))
                candidates.append((math.dist(at,center),horizontal,side,base,normal,center))
    for distance,horizontal,side,(x,z),(nx,nz),(cx,cz) in sorted(candidates):
        if distance > radius:
            return False
        yaw = (0 if side==1 else 180) if horizontal else (90 if side==1 else 270)
        cross = Box.around(cx,cz,width,depth,yaw)
        if not layout.free(cross,.08) or any(cross.overlaps(b,.12) for b in layout.courts):
            continue
        edge = route['width']/2

        def stamp(name,lateral,outward,scale):
            px = x+nx*(edge+outward)+(lateral if horizontal else 0)
            pz = z+nz*(edge+outward)+(0 if horizontal else lateral)
            assert layout.prop(name,px,pz,label,yaw=yaw,scale=scale,inside=cross),(label,name)

        if well:
            stamp('Well',-.55,2.63,.86)
            stamp('Bucket',1.34,3.34,.82)
            stamp('Bench',2.32,1.91,.88)
        else:
            stamp('Tree_Street',-1.45,2.48,.60)
            stamp('Bench',1.34,1.82,.87)
            stamp('Flower_Border',1.65,3.23,.63)
        layout.amenities.append(cross)
        layout.plan['greens' if not well else 'squares'].append(
            dict(name=label,x=cx,z=cz,width=width if horizontal else depth,depth=depth if horizontal else width))
        layout.plan['activityAreas'].append(dict(name=label,x=cx,z=cz,width=cross.right-cross.left,
                                                 depth=cross.north-cross.south,use='well' if well else 'rest'))
        centerline = (x+nx*(edge+.86),z+nz*(edge+.86))
        layout.plan['amenityPaths'].append(dict(name=label,width=2.0,points=points([(x,z),centerline])))
        return True
    return False


def furnish_streets(layout):
    for street,at,label in [
        ('East river street',(42,24),'Clinic waiting'),
        ('Royal ascent',(10,96),'Fountain square seats'),
        ('Academy street',(74,109),'Academy entrance seats'),
        ('Observatory lane',(-61,124),'Observatory garden seats'),
        ('Academy rear lane',(77,155),'Reading terrace seats'),
        ('Merchants gate road',(66,-32),'East gate seats'),
        ('Arena north alley',(-58,-62),'Arena lane seats'),
        ('West residential street',(-81,107),'West neighborhood seats'),
        ('West houses street',(-50,-50),'South neighborhood seats'),
        ('Northern river quay',(-28,13),'Riverside seats'),
    ]:
        rest_pocket(layout,street,at,label)

    for i,z in enumerate((-98,-85,-73,-61,-48,-20,-7,19,31,44,57,70,83,95)):
        for side in (-1,1):
            if i%3!=1:
                layout.prop('Tree_AlleyCypress',side*(8.4+i%2),z,'Royal avenue planting',scale=.72+(i%3)*.05)
            if i%2==0:
                layout.prop('Streetlamp',side*7.05,z+2.5,'Royal avenue lights',scale=.9)

    for x in (-48,-37,-17,13,24,35,47,76,87,99):
        layout.prop('Tree_AlleyCypress',x,18.38,'North quay planting',scale=.69)

    for x,z in ((-83,113),(-54,83),(56,89),(96,155),(-34,145),(-91,-66)):
        for dx,dz in ((0,0),(3,0),(-3,0),(0,3),(0,-3),(5,2),(-5,2),(4,-3),(-4,-3),(7,0),(-7,0)):
            if layout.prop('Tree_Shade',x+dx,z+dz,'Neighborhood shade',scale=.66):
                break

    for b in [p for p in layout.plan['buildings'] if not p.get('label')][::2]:
        dx,dz = rotate(0,b['depth']/2+2.5,b['yaw'])
        x,z = (b['x']+dx,b['z']+dz)
        if not layout.prop('Tree_Street',x,z,'Residential gardens',scale=.52):
            layout.prop('Planter',x,z,'Residential gardens',scale=.60)

    for name,x,z,yaw,scale in [
        ('Streetlamp',-22,-30,0,.92),('Streetlamp',-9,-30,0,.92),('Signpost',-8,-38,0,.83),
        ('Streetlamp',52,-73,0,.9),('Streetlamp',78,-70,0,.9),('Signpost',8,-72,0,.82),
        ('Streetlamp',62,108.2,0,.9),('Streetlamp',93,107.7,0,.88),('Signpost',8,98.8,0,.85),
        ('Crate_Closed',42.3,-25,0,.62),('Barrel',42.3,-23,0,.68),
        ('Water_Trough',101.7,-78.65,0,.72),('Crate_Closed',-44,-55,0,.60),
    ]:
        layout.prop(name,x,z,'District furnishings',yaw=yaw,scale=scale)


def add_court(layout, label, kind, shape, roadside, opening, *, green=False):
    area = dict(name=label,x=(shape.left+shape.right)/2,z=(shape.south+shape.north)/2,
                width=shape.right-shape.left,depth=shape.north-shape.south,use=kind)
    layout.plan['greens' if green else 'squares'].append(area)
    layout.plan['activityAreas'].append(area)
    path = dict(name=label,width=1.45,points=points([roadside,opening]))
    layout.plan['amenityPaths'].append(path)
    layout.protected.extend(route_boxes(path))
    layout.amenities.append(shape)


def furnish_wagon_stops(layout):
    for street,at,asset in WAGON_STOPS:
        label = 'Covered wagon stop' if asset=='Covered_Wagon' else 'Freight wagon stop'
        route = layout.streets[street]
        scale = .85
        w,d,_ = layout.size(asset)
        width,depth = d*scale+1.36,w*scale+1.52
        for _,x,z,yaw,roadside in roadside_sites(route,at,width,depth,18):
            shape = Box.around(x,z,width,depth,yaw)
            if not layout.free(shape,.13) or any(shape.overlaps(c,.18) for c in layout.courts):
                continue
            dx,dz = rotate(-.47,.47,yaw)
            if not layout.prop(asset,x+dx,z+dz,label,yaw=(yaw+90)%360,scale=scale,inside=shape):
                continue
            for name,v in (('Crate_Closed',.78),('Grain_Sack',-.16)):
                dx,dz = rotate(width/2-.59,v,yaw)
                layout.prop(name,x+dx,z+dz,label,yaw=yaw,scale=.81,inside=shape,clearance=.15)
            dx,dz = rotate(0,-depth/2+.65,yaw)
            add_court(layout,label,'freight',shape,roadside,(x+dx,z+dz))
            break


def furnish_neighborhood_groups(layout):
    for street,at,label,kind in NEIGHBORHOOD_SPOTS:
        route = layout.streets[street]
        rows = NEIGHBORHOOD_GROUPS[kind]
        for groups,scale in ((rows,.96),(rows,.84),(rows[:1],.88)):
            spans = [[layout.size(name)[:2] for name in names] for names in groups]
            gap = .36
            width = max(sum(w for w,_ in row)*scale+(len(row)-1)*gap for row in spans)+.64
            depths = [max(d for _,d in row)*scale for row in spans]
            depth = 1.38+sum(depths)+.74*(len(groups)-1)+.36
            for _,x,z,yaw,roadside in roadside_sites(route,at,width,depth,12.5):
                shape = Box.around(x,z,width,depth,yaw)
                if not layout.free(shape,.13) or any(shape.overlaps(c,.14) for c in layout.courts):
                    continue
                first = len(layout.props)
                back = 1.38-depth/2
                for names,row_depth in zip(groups,depths):
                    dx,dz = rotate(0,back+row_depth/2,yaw)
                    if not layout.row(names,x+dx,z+dz,yaw,label,scale,inside=shape,gap=gap):
                        del layout.props[first:]
                        del layout.plan['props'][first:]
                        break
                    back += row_depth+.74
                else:
                    dx,dz = rotate(0,.76-depth/2,yaw)
                    add_court(layout,label,kind,shape,roadside,(x+dx,z+dz),green=kind in {'garden','herbs','fountain'})
                    break
            else:
                continue
            break


def furnish_yard_companions(layout):
    for path in tuple(layout.plan['amenityPaths']):
        names = YARD_COMPANIONS.get(path['name'])
        if not names:
            continue
        a,b = coords(path)
        length = math.dist(a,b)
        fx,fz = (round((v-u)/length) for u,v in zip(a,b))
        yaw = {(0,1):0,(1,0):90,(0,-1):180,(-1,0):270}[fx,fz]
        for distance,u in ((1.15,0),(1.34,-2.1),(1.34,2.1),(2.22,0),(2.22,-2.1),(2.22,2.1)):
            dx,dz = rotate(u,distance,yaw)
            if layout.row(names,b[0]+dx,b[1]+dz,yaw,path['name']+' supplies',.87,gap=.30):
                break


def furnish_doorstep_groups(layout):
    for i,b in enumerate(layout.plan['buildings']):
        if b.get('label'):
            recipes = SHOPFRONTS.get(b['name'],('Planter','Bench','Street_Brazier'))
            names = (*recipes,'Noticeboard') if b['zone'] in {'Academy quarter','Civic quarter'} else recipes
        else:
            names = DOORSTEP_GROUPS[i%len(DOORSTEP_GROUPS)]
        for j,name in enumerate(names):
            scale = .83+.04*((i+j)%3)
            w,d,_ = layout.size(name)
            sign = (1,-1)[i%2]
            half_w,half_d = b['width']/2,b['depth']/2
            slots = [(side*half_w*f,-half_d-d*scale/2-gap)
                     for gap in (.13,.50,.84) for side in (sign,-sign) for f in (.77,.90,.54,.33)]
            slots.extend((side*(half_w+w*scale/2+.15),-half_d+back)
                         for side in (sign,-sign) for back in (.46,1.78,3.20,4.74))
            slots.extend((side*(half_w+w*scale/2+.82),-half_d+back)
                         for side in (sign,-sign) for back in (.54,2.02,3.47,5.15))
            for u,v in slots:
                dx,dz = rotate(u,v,b['yaw'])
                if layout.prop(name,b['x']+dx,b['z']+dz,'Doorway and yard supplies',yaw=b['yaw'],
                               scale=scale,clearance=.12):
                    break


def furnish_prop_companions(layout, first):
    additions = layout.plan['props'][first:]
    for p in additions:
        if p['zone'] not in {'Doorway and yard supplies','Extra market goods'}:
            continue
        names = PROP_COMPANIONS.get(p['name'],())
        w,d,_ = (v*p['scale'] for v in layout.size(p['name']))
        for i,name in enumerate(names):
            scale = .86+i*.032
            width,depth,_ = (v*scale for v in layout.size(name))
            side = (-1,1)[i%2]
            gap = .21
            across = w/2+width/2+gap
            back = d/2+depth/2+gap
            slots = ((side*across,0),(-side*across,0),(.17,back),(-.17,-back),
                     (side*across,back*.64),(-side*across,back*.64))
            for u,v in slots:
                dx,dz = rotate(u,v,p['yaw'])
                if layout.prop(name,p['x']+dx,p['z']+dz,p['zone'],yaw=p['yaw'],scale=scale,clearance=.16):
                    break


def enrich_street_life(layout, market):
    first = len(layout.props)
    layout.protected.extend(shape for route in layout.plan['amenityPaths'] for shape in route_boxes(route))
    for street,at,label in (
        ('Western civic lane',(-106,65),'West ward well'),
        ('Royal household lane',(-44,147),'Northwest ward well'),
        ('East river street',(36,25),'Clinic ward well'),
        ('Library lane',(102,126),'Library ward well'),
        ('Eastern academy lane',(110,112),'Eastern academy well'),
        ('South freight lane',(27,-104),'South gate ward well'),
        ('Eastern roof lane',(108,-51),'East wall well'),
    ):
        rest_pocket(layout,street,at,label,well=True,radius=17)
    furnish_wagon_stops(layout)
    furnish_neighborhood_groups(layout)
    for i,(x,z) in enumerate(((-107,20),(-107,54),(-107,109),(-104,139),(-79,150),
                              (-30,128),(74,142),(101,136),(108,77),(108,46),(108,-55),
                              (58,-106),(37,-107),(-104,-63),(-102,-46),(-32,18),(47,19),(97,19))):
        name = ('NaturalTree_03','NaturalTree_05','NaturalTree_07','NaturalTree_10')[i%4]
        for dx,dz in ((0,0),(0,2),(0,-2),(2,0),(-2,0),(1.5,3),(1.5,-3)):
            if layout.prop(name,x+dx,z+dz,'District shade trees',yaw=(i%4)*90,scale=.66):
                break
    for name,x,z,yaw in (
        ('Cloth_Display',-23.04,-16.45,90),('Shop_Sign',-20.1,-45.4,0),
        ('Bollard',-17.0,-45.58,0),('Bollard',-14.02,-45.58,0),
        ('Vegetable_Crate',-14.81,-26.23,0),('Bread_Basket',-16.28,-26.25,0),
        ('Rope_Coil',-15.7,-25.9,0),('Crate_Open',-16.18,-18.7,0),
        ('Crate_Closed',-14.75,-18.67,0),('Produce_Basket',-16.0,-39.2,0),
        ('Sack_Stack',-15.04,-38.18,0),('Outdoor_Table',-15.58,-40.9,0),
        ('Market_Scale',-15.54,-12.75,0),('Three_Legged_Stool',-16.49,-13.51,0),
    ):
        layout.prop(name,x,z,'Extra market goods',yaw=yaw,scale=.84,inside=market,clearance=.14)
    furnish_yard_companions(layout)
    furnish_households(layout,SMALL_YARDS)
    furnish_doorstep_groups(layout)
    furnish_prop_companions(layout,first)


def build(baseline, dimensions):
    layout = Layout(baseline,dimensions)
    roads(layout)
    facilities(layout)
    market = layout.court('Market square',-15.2,-28.6,18.4,36.0)
    layout.court('Royal forecourt',0,102.1,20.8,10.8)
    assert layout.building('MerchantHouse',-33,-42.3,'Market west street','Market quarter',yaw=180,scale=.89,housing=True,width=1.6)
    for street,at,label in [
        ('West residential street',(-79,111),'West neighborhood well'),
        ('West houses street',(-73,-52),'Southern neighborhood well'),
        ('East gardens lane',(72,90),'East neighborhood well'),
        ('South east street',(64,-78),'East gate well'),
    ]:
        assert rest_pocket(layout,street,at,label,well=True), label
    houses = 1+layout.house_rows()
    furnish_market(layout,market)
    furnish_courts(layout)
    furnish_planted_edges(layout)
    furnish_streets(layout)
    furnish_households(layout)
    furnish_doorsteps(layout)
    furnish_lane_edges(layout)
    enrich_street_life(layout,market)
    layout.pave_frontages()
    layout.validate(baseline,houses)
    return layout,houses


def update_footprints(plan, dimensions, baseline, *, keep_frontage=False):
    layout = Layout(plan,dimensions)
    for route in plan['roads']:
        layout.road(route['name'],route['width'],coords(route),kind=route['kind'],housing=route['frontage'])
    layout.finish_streets()
    for building,entry in zip(plan['buildings'],plan['accessPaths'],strict=True):
        model = building.get('modernModel',building['name'])
        new_width,new_depth = (v * building['scale'] for v in layout.size(model)[:2])
        path_width = round(entry['width']*new_width/building['width'],3)
        x,z = building['x'],building['z']
        if keep_frontage:
            dx,dz = rotate(0,(new_depth-building['depth'])/2,building['yaw'])
            x,z = x+dx,z+dz
        placed = layout.building(building['name'],x,z,building['street'],building['zone'],
                                 yaw=building['yaw'],scale=building['scale'],housing=not entry['paved'],
                                 width=path_width,label=building.get('label'))
        if not placed:
            raise ValueError(f"Invalid existing house placement: {building['x']}, {building['z']}")
    for area in plan['activityAreas']:
        shape = rectangle(area)
        if any(shape.overlaps(part,.08) for part in layout.building_shapes+layout.entrances):
            raise ValueError(f"The revised frontage crosses the {area['name']} court")
    for path in plan['amenityPaths']:
        if any(box.overlaps(shape) for box in route_boxes(path) for shape in layout.building_shapes):
            raise ValueError(f"The revised building blocks {path['name']}")
    for prop in plan['props']:
        if not layout.prop(prop['name'],prop['x'],prop['z'],prop['zone'],
                           yaw=prop['yaw'],scale=prop['scale'],clearance=prop.get('clearance',.24)):
            raise ValueError(f"Furnishing blocks the revised layout: {prop['name']} at {prop['x']}, {prop['z']}")
    layout.validate(baseline,plan['counts']['houses'])
    layout.pave_frontages()
    return {**plan, **{key:layout.plan[key] for key in ('buildings','accessPaths','secondaryAccessPaths','frontages','counts')}}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('dimensions',type=Path)
    parser.add_argument('output',type=Path)
    parser.add_argument('--from-plan',type=Path,help='Re-measure buildings and entrances at their current placements')
    parser.add_argument('--keep-frontage',action='store_true',help='Keep the street-facing plot edge when a model changes depth')
    args = parser.parse_args()
    baseline = json.loads(BASE_PLAN.read_text())
    dimensions = json.loads(args.dimensions.read_text())
    if args.from_plan:
        plan = update_footprints(json.loads(args.from_plan.read_text()),dimensions,baseline,keep_frontage=args.keep_frontage)
    else:
        plan = build(baseline,dimensions)[0].plan
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(plan,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(plan['counts']))


if __name__ == '__main__':
    main()
