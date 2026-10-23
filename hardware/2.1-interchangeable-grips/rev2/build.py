"""Validate nominal packaging and export the R2 design from one dimension source."""
import argparse
from collections import Counter
import hashlib
import itertools
import json
import math
from pathlib import Path
import re
import subprocess

BASE = Path(__file__).resolve().parent
PARTS = ['tray', 'deck', 'base_plate', 'top_plate', 'fixed_spacer', 'active_spacer',
         'stop_sleeve', 'sphere_bottom', 'sphere_top', 'cylinder_bottom', 'cylinder_top',
         'pencil_bottom', 'pencil_top']


def validate(d):
    require(d['units'] == 'mm', "Validation failed: d['units'] == 'mm'")
    def finite(value):
        if isinstance(value, dict):
            for item in value.values(): finite(item)
        elif isinstance(value, list):
            for item in value: finite(item)
        elif isinstance(value, (int, float)):
            require(math.isfinite(value), 'Nonfinite dimension')
    finite(d)
    # Fixed carrier interfaces in assembly.scad cannot be changed only in JSON.
    require(d['electronics_bay'] == {'min': [-50,-27,3], 'max': [50,27,29]}, 'Carrier envelope mismatch')
    require(d['base_plate'] == {'size': [132,80,4], 'z': 30}, 'Base plate mismatch')
    require(d['top_plate']['size'] == [132,80,4], 'Top plate mismatch')
    require(d['fixed_spacer']['min'] == [15,-6.35,34] and d['fixed_spacer']['size'] == [25,12.7,4], 'Fixed spacer must meet base and fixed mounting region')
    require(d['active_spacer']['min'] == [-40,-6.35,50.7] and d['active_spacer']['size'] == [25,12.7,4], 'Active spacer mounting region mismatch')
    require(d['cell']['min'] == [-40,-6.35,38], 'Cell position mismatch')
    require(d['guides'] == {'x': [-48,48], 'y': [-25,25], 'diameter': 4, 'clearance_hole': 4.6}, 'Guide interface mismatch')
    bay = d['electronics_bay']
    for c in d['components']:
        require(all(s > 0 for s in c['size']), c['id'])
        require(all(bay['min'][i] <= c['min'][i] and c['min'][i]+c['size'][i] <= bay['max'][i] for i in range(3)), c['id'])
    for a, b in itertools.combinations(d['components'], 2):
        overlap = all(max(a['min'][i], b['min'][i]) < min(a['min'][i]+a['size'][i], b['min'][i]+b['size'][i]) for i in range(3))
        require(not overlap, (a['id'], b['id']))
    cell = d['cell']
    require(cell['size'] == [80, 12.7, 12.7], "Validation failed: cell['size'] == [80, 12.7, 12.7]")
    # Drawing dimensions: first hole 5 mm from each end; next 15 mm inward.
    require(cell['active_holes_x'] == [-35, -20], "Validation failed: cell['active_holes_x'] == [-35, -20]")
    require(cell['fixed_holes_x'] == [20, 35], "Validation failed: cell['fixed_holes_x'] == [20, 35]")
    require(cell['active_thread'] == 'M4' and cell['fixed_thread'] == 'M5', "Validation failed: cell['active_thread'] == 'M4' and cell['fixed_thread'] == 'M5'")
    require(abs(d['fixed_spacer']['min'][2]+d['fixed_spacer']['size'][2]-cell['min'][2]) < 1e-6, "Validation failed: abs(d['fixed_spacer']['min'][2]+d['fixed_spacer']['size'][2]-cell['min'][2]) < 1e-6")
    require(abs(cell['min'][2]+cell['size'][2]-d['active_spacer']['min'][2]) < 1e-6, "Validation failed: abs(cell['min'][2]+cell['size'][2]-d['active_spacer']['min'][2]) < 1e-6")
    require(abs(d['active_spacer']['min'][2]+d['active_spacer']['size'][2]-d['top_plate']['z']) < 1e-6, "Validation failed: abs(d['active_spacer']['min'][2]+d['active_spacer']['size'][2]-d['top_plate']['z']) < 1e-6")
    require(0 < d['stop_gap_proposal'] < 1, "Validation failed: 0 < d['stop_gap_proposal'] < 1")
    report = {'revision': d['revision'], 'nominal_envelope_check': 'PASS',
              'component_pairs_checked': len(list(itertools.combinations(d['components'], 2))),
              'drawing_hole_pattern_check': 'PASS', 'physical_fit': 'NOT_PERFORMED',
              'strength_and_stop_setting': 'NOT_VALIDATED', 'component_basis': d['components']}
    return report


def parameters(d):
    values = dict(base_size=d['base_plate']['size'], base_z=d['base_plate']['z'],
                  top_z=d['top_plate']['z'], active_x=d['cell']['active_holes_x'],
                  fixed_x=d['cell']['fixed_holes_x'], guide_x=d['guides']['x'],
                  guide_y=d['guides']['y'], guide_d=d['guides']['diameter'],
                  guide_clear=d['guides']['clearance_hole'], stop_gap=d['stop_gap_proposal'],
                  cell_min=d['cell']['min'], cell_size=d['cell']['size'],
                  fixed_min=d['fixed_spacer']['min'], fixed_size=d['fixed_spacer']['size'],
                  active_min=d['active_spacer']['min'], active_size=d['active_spacer']['size'],
                  sphere_d=d['shells']['sphere_diameter'], cylinder_l=d['shells']['cylinder_length'],
                  cylinder_d=d['shells']['cylinder_diameter'], pencil_l=d['shells']['pencil_length'],
                  pencil_d=d['shells']['pencil_across_corners'],
                  component_boxes=[[c['min'], c['size']] for c in d['components']])
    return '// Generated from layout.json by build.py. Do not edit.\n'+''.join(f'{key} = {json.dumps(value)};\n' for key, value in values.items())


def mesh(path):
    points = [tuple(map(float, p)) for p in re.findall(r'vertex\s+([-\d.e+]+)\s+([-\d.e+]+)\s+([-\d.e+]+)', path.read_text())]
    require(points and len(points) % 3 == 0, path)
    edges = Counter()
    parents = {}
    def find(p):
        parents.setdefault(p, p)
        while parents[p] != p:
            parents[p] = parents[parents[p]]
            p = parents[p]
        return p
    for i in range(0, len(points), 3):
        a, b, c = points[i:i+3]
        require(len({a, b, c}) == 3, path)
        ab=[b[j]-a[j] for j in range(3)]; ac=[c[j]-a[j] for j in range(3)]
        cross=[ab[1]*ac[2]-ab[2]*ac[1],ab[2]*ac[0]-ab[0]*ac[2],ab[0]*ac[1]-ab[1]*ac[0]]
        require(all(math.isfinite(v) for p in (a,b,c) for v in p) and any(v != 0 for v in cross), 'Nonfinite or zero-area triangle')
        for u, v in [(a,b), (b,c), (c,a)]:
            edges[tuple(sorted((u,v)))]+=1
            parents[find(u)] = find(v)
    require(all(count == 2 for count in edges.values()), path)
    connected = len({find(p) for p in points})
    require(connected == 1, (path, 'disconnected bodies', connected))
    return {'part': path.stem, 'triangles': len(points)//3, 'edge_closure': 'PASS', 'connected_bodies': connected,
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    parser = argparse.ArgumentParser()
    mode=parser.add_mutually_exclusive_group()
    mode.add_argument('--openscad')
    mode.add_argument('--verify-only', action='store_true')
    args = parser.parse_args()
    d = json.loads((BASE/'layout.json').read_text())
    report = validate(d)
    generated = parameters(d)
    if args.verify_only:
        require((BASE/'parameters.scad').read_text() == generated, 'Regenerate parameters')
    else:
        (BASE/'parameters.scad').write_text(generated)
    out = BASE/'exports'; out.mkdir(exist_ok=True)
    if args.openscad:
        for part in PARTS:
            result = subprocess.run([args.openscad, '-o', str(out/(part+'.stl')), '-D', f'part="{part}"', str(BASE/'assembly.scad')], capture_output=True, text=True)
            if result.returncode or 'ERROR:' in result.stderr: raise RuntimeError(part+': '+result.stderr)
            print(part, 'exported', flush=True)
        for part in ['base_plate', 'top_plate', 'fixed_spacer', 'active_spacer']:
            subprocess.run([args.openscad, '-o', str(out/(part+'.dxf')), '-D', f'part="{part}"', '-D', 'flat=true', str(BASE/'assembly.scad')], check=True, capture_output=True)
    if args.openscad or args.verify_only:
        report['meshes'] = [mesh(out/(part+'.stl')) for part in PARTS]
        report['source_sha256'] = {name: hashlib.sha256((BASE/name).read_text(encoding='utf-8').encode()).hexdigest()
                                   for name in ['assembly.scad','layout.json','parameters.scad']}
        report['dxf_sha256'] = {name: hashlib.sha256((out/(name+'.dxf')).read_bytes()).hexdigest()
                               for name in ['base_plate','top_plate','fixed_spacer','active_spacer']}
        if args.verify_only:
            previous = json.loads((out/'verification.json').read_text())
            require(report == previous, 'Export hash or verification mismatch')
        else:
            (out/'verification.json').write_text(json.dumps(report, indent=2)+'\n')
    print('PASS: nominal packaging, drawing hole pattern'+(', 13 closed meshes' if 'meshes' in report else ''), flush=True)


if __name__ == '__main__': main()
