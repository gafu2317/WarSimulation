"""Measure unassigned town space independently of the plan generator."""
import argparse
import collections
import json
import math
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SOIL = {'soil', 'earth', 'dirt'}
LAWN = {'lawn', 'grass', 'garden'}


def polygon(record):
    if record.get('points'):
        return [(p['x'], p['z']) for p in record['points']]
    angle = math.radians(record.get('yaw', 0))
    c, s = math.cos(angle), math.sin(angle)
    w, d = record['width'] / 2, record['depth'] / 2
    return [(record['x'] + c*x + s*z, record['z'] - s*x + c*z)
            for x, z in [(-w, -d), (w, -d), (w, d), (-w, d)]]


def contains(x, z, points):
    result = False
    for a, b in zip(points, points[1:] + points[:1]):
        if (a[1] > z) != (b[1] > z) and x < (b[0]-a[0])*(z-a[1])/(b[1]-a[1])+a[0]:
            result = not result
    return result


def raster(points, bounds):
    x0, z0, x1, z1 = bounds
    return {(x, z)
            for x in range(max(x0, math.floor(min(p[0] for p in points))),
                           min(x1, math.ceil(max(p[0] for p in points))))
            for z in range(max(z0, math.floor(min(p[1] for p in points))),
                           min(z1, math.ceil(max(p[1] for p in points))))
            if contains(x+.5, z+.5, points)}


def raster_records(records, bounds):
    cells = set()
    for record in records:
        cells |= raster(polygon(record), bounds)
    return cells


def raster_paths(paths, bounds):
    records = []
    for path in paths:
        if path.get('outline'):
            records.append(dict(points=path['outline']))
            continue
        for a, b in zip(path['points'], path['points'][1:]):
            dx, dz = b['x']-a['x'], b['z']-a['z']
            if math.hypot(dx, dz) < .001:
                continue
            records.append(dict(x=(a['x']+b['x'])/2, z=(a['z']+b['z'])/2,
                                width=path['width'], depth=math.hypot(dx, dz),
                                yaw=math.degrees(math.atan2(dx, dz))))
    return raster_records(records, bounds)


def components(cells, far):
    remaining = set(cells)
    groups = []
    while remaining:
        first = min(remaining)
        remaining.remove(first)
        pending, group = [first], []
        while pending:
            x, z = pending.pop()
            group.append((x, z))
            for adjacent in [(x-1, z), (x+1, z), (x, z-1), (x, z+1)]:
                if adjacent in remaining:
                    remaining.remove(adjacent)
                    pending.append(adjacent)
        if len(group) >= 20:
            groups.append(dict(estimatedAreaM2=len(group),
                               center=dict(x=round(sum(x+.5 for x, _ in group)/len(group), 2),
                                           z=round(sum(z+.5 for _, z in group)/len(group), 2)),
                               bounds=dict(minX=min(x for x, _ in group), minZ=min(z for _, z in group),
                                           maxX=max(x for x, _ in group)+1, maxZ=max(z for _, z in group)+1),
                               areaOutsideProps5mM2=len(set(group) & far)))
    return sorted(groups, key=lambda group: (-group['estimatedAreaM2'], group['center']['x']))


def prop_coverage(props, bounds, radius=5):
    covered = set()
    for p in props:
        for x in range(max(bounds[0], math.floor(p['x']-radius)), min(bounds[2], math.ceil(p['x']+radius))):
            for z in range(max(bounds[1], math.floor(p['z']-radius)), min(bounds[3], math.ceil(p['z']+radius))):
                if (x+.5-p['x'])**2 + (z+.5-p['z'])**2 <= radius**2:
                    covered.add((x, z))
    return covered


def audit(plan):
    wall = [(p['x'], p['z']) for p in plan['wallOutline']]
    bounds = (math.floor(min(x for x, _ in wall)), math.floor(min(z for _, z in wall)),
              math.ceil(max(x for x, _ in wall)), math.ceil(max(z for _, z in wall)))
    city = raster(wall, bounds)
    buildings = raster_records(plan['buildings'], bounds) & city
    river = raster_records([dict(x=0, z=plan['riverZ'], width=bounds[2]-bounds[0], depth=plan['riverWidth'])], bounds) & city
    access = plan.get('accessPaths', [])
    streets = raster_paths(plan['roads'], bounds) & city
    roads = streets | raster_paths(access, bounds)
    frontages = raster_paths(plan.get('frontages', []), bounds) & city
    squares = raster_records(plan.get('squares', []), bounds) & city
    greens = raster_records(plan.get('greens', []), bounds) & city
    enclosed_ground = raster_records(plan.get('enclosures', []), bounds) & city
    gardens = raster_records(plan.get('gardens', []), bounds) & city
    activities = raster_records(plan.get('activityAreas', []), bounds) & city
    soil = raster_records([a for a in plan.get('activityAreas', []) if a.get('surface', '').lower() in SOIL], bounds)
    lawn = raster_records([a for a in plan.get('activityAreas', []) if a.get('surface', '').lower() in LAWN], bounds)
    open_ground = city - buildings - river
    if plan.get('pavedTown', False):
        paving = roads | frontages | squares
    else:
        paving = streets | frontages | raster_paths([a for a in access if a.get('paved')], bounds)
        soil |= squares | enclosed_ground
    unclassified = open_ground - roads - squares - greens - enclosed_ground - gardens - activities
    uncovered = unclassified - frontages
    bare = set() if plan.get('pavedTown', False) else unclassified - frontages
    covered = prop_coverage(plan.get('props', []), bounds)
    unclassified_far, bare_far = unclassified-covered, bare-covered
    exact_area = abs(sum(a[0]*b[1]-a[1]*b[0] for a, b in zip(wall, wall[1:]+wall[:1])))/2
    metrics = dict(exactCityAreaM2=exact_area, sampledCityAreaM2=len(city), buildingAreaM2=len(buildings),
                   openGroundAreaM2=len(open_ground), plannedPavingOutsideBuildingsM2=len((paving & city)-buildings),
                   gardenOutsideBuildingsM2=len((greens | gardens)-buildings),
                   unplannedBareEarthAreaM2=len(bare),
                   unplannedBareEarthPercent=round(100*len(bare)/exact_area, 2),
                   explicitSoilAreaM2=len((soil | gardens) & open_ground),
                   explicitLawnAreaM2=len((lawn | greens) & open_ground),
                   unclassifiedSpaceAreaM2=len(unclassified),
                   unclassifiedSpaceOutsideProps5mM2=len(unclassified_far),
                   unclassifiedSpaceWithoutFrontagePavingM2=len(uncovered),
                   bareEarthOutsideProps5mM2=len(bare_far),
                   props=len(plan.get('props', [])), activityAreas=len(plan.get('activityAreas', [])))
    districts = []
    regions = [('Southwest housing', bounds[0], 0, bounds[1], 4),
               ('Southeast craft', 0, bounds[2], bounds[1], 4),
               ('Northwest civic', bounds[0], 0, 12, 80),
               ('Northwest military', bounds[0], 0, 80, bounds[3]),
               ('Northeast civic', 0, bounds[2], 12, 80),
               ('Northeast royal and college', 0, bounds[2], 80, bounds[3])]
    for name, x0, x1, z0, z1 in regions:
        district = {(x, z) for x, z in city if x0 <= x+.5 < x1 and z0 <= z+.5 < z1}
        districts.append(dict(name=name, unplannedBareEarthAreaM2=len(bare & district),
                              bareEarthOutsideProps5mM2=len(bare_far & district),
                              unclassifiedSpaceAreaM2=len(unclassified & district),
                              unclassifiedSpaceOutsideProps5mM2=len(unclassified_far & district),
                              props=sum(x0 <= p['x'] < x1 and z0 <= p['z'] < z1 for p in plan.get('props', []))))
    zones = collections.defaultdict(lambda: dict(unclassifiedSpaceAreaM2=0, unclassifiedSpaceOutsideProps5mM2=0,
                                                 unplannedBareEarthAreaM2=0, bareEarthOutsideProps5mM2=0, props=0))
    owners = [(b, math.cos(math.radians(b.get('yaw', 0))), math.sin(math.radians(b.get('yaw', 0)))) for b in plan['buildings']]

    def nearest_zone(x, z):
        def distance(owner):
            b, c, s = owner
            dx, dz = x-b['x'], z-b['z']
            return max(abs(c*dx-s*dz)-b['width']/2, 0)**2 + max(abs(s*dx+c*dz)-b['depth']/2, 0)**2
        return min(owners, key=distance)[0]['zone']

    for x, z in unclassified | bare:
        zone = zones[nearest_zone(x+.5, z+.5)]
        zone['unclassifiedSpaceAreaM2'] += (x, z) in unclassified
        zone['unclassifiedSpaceOutsideProps5mM2'] += (x, z) in unclassified_far
        zone['unplannedBareEarthAreaM2'] += (x, z) in bare
        zone['bareEarthOutsideProps5mM2'] += (x, z) in bare_far
    for p in plan.get('props', []):
        zones[nearest_zone(p['x'], p['z'])]['props'] += 1
    report = dict(method=dict(gridStepM=1, sample='cell center, half-open polygon edges',
                              area='Raster estimates in square metres; exact polygon city area reported separately.',
                              unclassified='Outside buildings, water, roads, entrance paths, squares, greens, enclosures, gardens and activityAreas. Frontage paving alone does not assign a use.',
                              bareEarth='Unassigned exposed earth; pavedTown covers the base, while explicitly planned soil and lawn are reported separately.',
                              propRadiusM=5, zones='Nearest building footprint, not its center; decoration within 5m does not prove a functional use.'),
                  pavedTown=bool(plan.get('pavedTown', False)), metrics=metrics, districts=districts,
                  buildingZones=dict(sorted(zones.items())),
                  unplannedBareEarthComponentsOver20M2=components(bare, bare_far),
                  unclassifiedSpaceComponentsOver20M2=components(unclassified, unclassified_far))
    layers = [('Unassigned space', unclassified, '#d99a43'), ('Paving apron only', frontages & unclassified, '#bead83'),
              ('River', river-buildings, '#359bac'),
              ('Road / entrance / square', (roads | squares)-buildings, '#b3b4b0'),
              ('Named activity area', activities & open_ground, '#849b73'),
              ('Enclosed ground', enclosed_ground & open_ground, '#b19369'),
              ('Lawn / kitchen garden', (greens | gardens) & open_ground, '#627d45'),
              ('Building footprint', buildings, '#49545f')]
    return report, bounds, city, layers


def draw_map(path, bounds, city, layers, props):
    from PIL import Image, ImageDraw, ImageFont
    scale, pad, legend = 4, 28, 300
    x0, z0, x1, z1 = bounds
    width, height = (x1-x0)*scale, (z1-z0)*scale
    image = Image.new('RGB', (width+pad*2+legend, height+pad*2), '#20272b')
    draw = ImageDraw.Draw(image)

    def paint(cells, color):
        for x, z in cells:
            xx, yy = pad+(x-x0)*scale, pad+(z1-z-1)*scale
            draw.rectangle((xx, yy, xx+scale-1, yy+scale-1), fill=color)

    paint(city, '#b7a17a')
    for _, cells, color in layers:
        paint(cells, color)
    for p in props:
        xx, yy = pad+(p['x']-x0)*scale, pad+(z1-p['z'])*scale
        draw.ellipse((xx-2, yy-2, xx+2, yy+2), fill='#c34b42', outline='#f3d2a1')
    font_path = ROOT/'Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular.ttf'
    font = ImageFont.truetype(str(font_path), 17) if font_path.exists() else ImageFont.load_default()
    lx = width+pad*2+12
    draw.text((lx, pad), 'CITY SPACE AUDIT', fill='#f5f1e5', font=font)
    draw.text((lx, pad+30), 'North up / 1 m grid', fill='#ddd8c8', font=font)
    for i, (label, _, color) in enumerate(layers):
        y = pad+82+i*52
        draw.rectangle((lx, y+3, lx+18, y+21), fill=color)
        draw.text((lx+28, y), label, fill='#eee9dd', font=font)
    draw.text((lx, pad+82+len(layers)*52), 'Red dots: prop centers', fill='#eee9dd', font=font)
    image.save(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', type=Path, default=ROOT/'Assets/Data/CountryComparison/town_plan.json')
    parser.add_argument('--out', type=Path, required=True, help='Directory for audit.json and ground_map.png')
    args = parser.parse_args()
    plan = json.loads(args.plan.read_text())
    report, bounds, city, layers = audit(plan)
    report['plan'] = str(args.plan.resolve())
    args.out.mkdir(parents=True, exist_ok=True)
    (args.out/'audit.json').write_text(json.dumps(report, indent=2)+'\n')
    draw_map(args.out/'ground_map.png', bounds, city, layers, plan.get('props', []))
    print(json.dumps(report['metrics'], indent=2))


if __name__ == '__main__':
    main()
