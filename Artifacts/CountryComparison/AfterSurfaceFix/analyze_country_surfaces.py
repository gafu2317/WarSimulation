import collections,json,math
from pathlib import Path
import numpy as np
folder=Path('/Users/fukutomi/Unity/WarSimulation/Artifacts/CountryComparison/AfterSurfaceFix')
audit=json.loads((folder/'surface_audit.json').read_text())
faces=audit['ground']
points=np.array([[[f[k]['x'],f[k]['z']] for k in ['a','b','c']] for f in faces])
heights=np.array([f['a']['y'] for f in faces])
lo=points.min(axis=1);hi=points.max(axis=1)
cities=np.array([f['city'] for f in faces])
materials=np.array([f['material'] for f in faces])
def cross(a,b): return float(a[0]*b[1]-a[1]*b[0])
def intersection(a,b):
    subject=list(a)
    sign=1 if cross(b[1]-b[0],b[2]-b[0])>0 else -1
    for p,q in zip(b,np.roll(b,-1,axis=0)):
        if not subject: return []
        output=[]
        prev=subject[-1]; dp=sign*cross(q-p,prev-p)
        for current in subject:
            dc=sign*cross(q-p,current-p)
            if (dc>=-1e-9)!=(dp>=-1e-9):
                output.append(prev+(current-prev)*(dp/(dp-dc)))
            if dc>=-1e-9: output.append(current)
            prev=current;dp=dc
        subject=output
    return subject

def area(poly):
    return abs(sum(cross(p,q) for p,q in zip(poly,poly[1:]+poly[:1])))*.5

def uv_at(i,p):
    f=faces[i]; xyz=points[i]
    tex=np.array([[f[k]['x'],f[k]['y']] for k in ['ua','ub','uc']])
    m=np.column_stack((xyz[1]-xyz[0],xyz[2]-xyz[0]))
    if abs(np.linalg.det(m))<1e-12: return np.zeros(2)
    t=np.linalg.solve(m,p-xyz[0]); return tex[0]+t[0]*(tex[1]-tex[0])+t[1]*(tex[2]-tex[0])
groups={};counts=collections.Counter();candidates=0
for i in range(len(faces)):
    mask=(np.arange(len(faces))>i)&(np.abs(heights-heights[i])<.0042)&(lo[:,0]<hi[i,0]-1e-6)&(hi[:,0]>lo[i,0]+1e-6)&(lo[:,1]<hi[i,1]-1e-6)&(hi[:,1]>lo[i,1]+1e-6)&((cities==cities[i])|(cities=='shared')|(cities[i]=='shared'))
    for j in np.where(mask)[0]:
        candidates+=1
        polygon=intersection(points[i],points[j])
        if len(polygon)<3: continue
        overlap=area(polygon)
        if overlap<.0001: continue
        gap=abs(float(heights[i]-heights[j])); same_material=materials[i]==materials[j]
        uv_diff=float('inf')
        if same_material:
            sample=np.mean(polygon,axis=0)
            delta=np.abs(uv_at(i,sample)-uv_at(j,sample));delta=np.minimum(delta%1,1-delta%1)
            uv_diff=float(max(delta))
        category=('same_surface' if same_material and uv_diff<1e-4 else 'competing_surface') if gap<.00015 else 'near_parallel'
        city=faces[i]['city'] if faces[i]['city']!='shared' else faces[j]['city']
        paths=tuple(sorted([faces[i]['path'],faces[j]['path']]))
        key=(city,category,paths,round(gap,5))
        if key not in groups:
            groups[key]={'city':city,'category':category,'paths':paths,'gap':gap,'area':0,'pairs':0,'materials':list({str(materials[i]),str(materials[j])}),'sample':np.mean(polygon,axis=0).tolist()}
        groups[key]['area']+=overlap;groups[key]['pairs']+=1
        counts[(city,category)]+=1
report={'summary':[{ 'city':k[0],'category':k[1],'trianglePairs':v,'objectPairs':sum(1 for x in groups.values() if (x['city'],x['category'])==k)} for k,v in counts.items()],
        'pairs':sorted(groups.values(),key=lambda g:(g['category']!='competing_surface',-g['area'])),'candidateTrianglePairs':candidates}
(folder/'ground_overlap_analysis.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(report['summary'],ensure_ascii=False))
for category in ['competing_surface','near_parallel']:
    print(category)
    for record in [r for r in report['pairs'] if r['category']==category][:25]: print(json.dumps(record,ensure_ascii=False))
