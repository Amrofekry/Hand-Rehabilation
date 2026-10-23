"""Independent solid probes for the three geometry defects found during review."""
import argparse
from pathlib import Path
import subprocess
BASE=Path(__file__).resolve().parent


def main():
    p=argparse.ArgumentParser();p.add_argument('--openscad',required=True);args=p.parse_args()
    out=BASE.parents[2]/'delivery/cad-review-probes';out.mkdir(parents=True,exist_ok=True)
    # Positions lie inside the former defects, not on ambiguous solid boundaries.
    probes=[('deck_slot','deck()',[-20,22,14.7],False),
            ('deck_support','tray()',[48.5,21,13.5],True),
            ('sphere_usb_left','shell("sphere",false)',[-90,-10,20],False),
            ('sphere_usb_right','shell("sphere",false)',[90,-10,20],False)]
    for name,module,position,solid in probes:
        source=out/(name+'.scad');target=out/(name+'.stl')
        source.write_text(f'include <{BASE.as_posix()}/parameters.scad>\nuse <{BASE.as_posix()}/assembly.scad>\n$fn=40;\nintersection() {{ {module}; translate({position}) cube([0.1,0.1,0.1],center=true); }}\n')
        result=subprocess.run([args.openscad,'-o',str(target),str(source)],capture_output=True,text=True)
        (out/(name+'.log')).write_text(result.stdout+result.stderr)
        if 'WARNING:' in result.stderr or 'ERROR:' in result.stderr:raise RuntimeError(result.stderr)
        empty='Current top level object is empty' in result.stderr
        if solid:
            if result.returncode or empty or not target.exists():raise ValueError(name+' has no support material')
        elif not empty:raise ValueError(name+' is still obstructed')
        print(name,'PASS',flush=True)


if __name__=='__main__':main()
