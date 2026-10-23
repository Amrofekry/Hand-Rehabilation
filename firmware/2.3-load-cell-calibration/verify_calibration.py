"""WBS 2.3 numeric bench-report validation; not clinical or physical certification."""
import argparse
import csv
import json
import math

def evaluate(rows, absolute=1.0, relative=0.02):
    if not math.isfinite(absolute) or absolute<=0 or not math.isfinite(relative) or relative<0:
        raise ValueError('Invalid tolerances')
    values=[]
    for i,row in enumerate(rows,2):
        ref=float(row['reference_n']); measured=float(row['measured_n']); direction=row['direction']
        if not math.isfinite(ref) or not math.isfinite(measured) or ref<0 or ref>980.665:
            raise ValueError(f'Invalid force at row {i}')
        if direction not in ('loading','unloading'): raise ValueError(f'Invalid direction at row {i}')
        error=abs(measured-ref); limit=max(absolute,relative*ref)
        values.append(dict(reference_n=ref,measured_n=measured,direction=direction,error_n=error,limit_n=limit,passed=error<=limit))
    for direction in ('loading','unloading'):
        refs={x['reference_n'] for x in values if x['direction']==direction}
        if 0 not in refs or len(refs)<3: raise ValueError('Require zero and at least two distinct nonzero forces in both directions')
    return dict(passed=all(v['passed'] for v in values),absolute_tolerance_n=absolute,relative_tolerance=relative,rows=values)

def main():
    p=argparse.ArgumentParser(); p.add_argument('csv'); p.add_argument('--absolute',type=float,default=1); p.add_argument('--relative',type=float,default=.02)
    a=p.parse_args()
    try:
        with open(a.csv,newline='',encoding='utf-8') as f: result=evaluate(list(csv.DictReader(f)),a.absolute,a.relative)
    except (ValueError,KeyError,OSError) as e: p.exit(2,f'Invalid report: {e}\n')
    print(json.dumps(result,indent=2)); raise SystemExit(0 if result['passed'] else 1)
if __name__=='__main__': main()
