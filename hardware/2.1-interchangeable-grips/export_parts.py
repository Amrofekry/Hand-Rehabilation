import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
import re

def check_meshes(out, write_report=False):
    checks=[]
    for target in sorted(out.glob('*.stl')):
        points=[tuple(map(float,p)) for p in re.findall(r'vertex\s+([-\d.e+]+)\s+([-\d.e+]+)\s+([-\d.e+]+)',target.read_text())]
        if not points or len(points)%3: raise ValueError('Invalid ASCII STL: '+str(target))
        edges=Counter()
        for i in range(0,len(points),3):
            a,b,c=points[i:i+3]
            for x,y in ((a,b),(b,c),(c,a)): edges[tuple(sorted((x,y)))]+=1
        bad=sum(n!=2 for n in edges.values())
        checks.append(dict(part=target.stem,triangles=len(points)//3,nonmanifold_edges=bad,closed_mesh=bad==0))
    if len(checks)!=9 or not all(x['closed_mesh'] for x in checks): raise ValueError('Mesh closure check failed')
    manifest=json.loads((out/'manifest.json').read_text())
    if len(manifest)!=9 or {x['part'] for x in manifest}!={x['part'] for x in checks}:
        raise ValueError('Manifest must cover every mesh exactly once')
    for item in manifest:
        if hashlib.sha256((out/(item['part']+'.stl')).read_bytes()).hexdigest()!=item['sha256']:
            raise ValueError('Mesh hash mismatch: '+item['part'])
    if write_report:
        (out/'mesh_check.json').write_text(json.dumps(checks,indent=2)+'\n')
    elif json.loads((out/'mesh_check.json').read_text())!=checks:
        raise ValueError('Stored mesh report does not match geometry')
    print('PASS: nine closed meshes, every undirected edge has two incident triangles, hashes match')

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--openscad',default='openscad'); ap.add_argument('--verify-only',action='store_true'); args=ap.parse_args()
    base=Path(__file__).resolve().parent; out=base/'exports'; out.mkdir(exist_ok=True)
    if args.verify_only:
        check_meshes(out); return
    parts=['tray','lid','platen']+[s+'_'+h for s in ['sphere','cylinder','pencil'] for h in ['top','bottom']]
    def render(part):
        target=out/(part+'.stl')
        r=subprocess.run([args.openscad,'-o',str(target),'-D',f'part="{part}"',str(base/'grip_core.scad')],capture_output=True,text=True)
        if r.returncode or not target.exists() or target.stat().st_size<100 or 'ERROR:' in r.stderr:
            raise RuntimeError(part+': '+r.stderr)
        print(part, target.stat().st_size,flush=True)
        return dict(part=part,bytes=target.stat().st_size,sha256=hashlib.sha256(target.read_bytes()).hexdigest())
    # Separate processes and output files; bound concurrency to limit memory use.
    with ThreadPoolExecutor(max_workers=3) as pool:
        records=list(pool.map(render,parts))
    (out/'manifest.json').write_text(json.dumps(records,indent=2)+'\n')
    check_meshes(out, write_report=True)
if __name__=='__main__': main()
