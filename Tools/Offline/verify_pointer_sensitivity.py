"""Isolated negative tests; never replaces production files or launches Unity."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, subprocess

ROOT=Path(__file__).resolve().parents[2]
EDITOR=Path('C:/Program Files/Unity/Hub/Editor/6000.4.4f1/Editor/Data')
OUT=ROOT/'Logs/offline/pointer-negative'
GAME=ROOT/'Assets/Rehab/3.4-game-modules'
sources={name:(GAME/name).read_text(encoding='utf-8') for name in ['RehabPointerPolicy.cs','RehabDwellGate.cs']}
cases=['tip-instead-of-palm-cursor','stationary-navigation-rearmed','held-control-repeats']
results=[]
refs=EDITOR/'MonoBleedingEdge/lib/mono/4.5'
flags=['-r:'+str(refs/name) for name in ['mscorlib.dll','System.dll','System.Core.dll','System.Numerics.dll','System.Numerics.Vectors.dll']]
for name in cases:
    folder=OUT/name;folder.mkdir(parents=True,exist_ok=True)
    mutated=dict(sources)
    if name==cases[0]:
        code=mutated['RehabPointerPolicy.cs']
        needle='var palm=(points[0]+points[5]+points[9]+points[13]+points[17])*.2f;'
        assert needle in code
        code=code.replace(needle,'var palm=points[8];')
        mutated['RehabPointerPolicy.cs']=code
    elif name==cases[1]:
        code=mutated['RehabPointerPolicy.cs'];needle='if (!Waiting) return true;'
        assert code.count(needle)==1
        mutated['RehabPointerPolicy.cs']=code.replace(needle,'if (true) return true;')
    else:
        code=mutated['RehabDwellGate.cs'];needle='if (Latched) return false;'
        assert code.count(needle)==1
        mutated['RehabDwellGate.cs']=code.replace(needle,'// negative fixture: omit the latch guard')
    paths=[]
    for filename,code in mutated.items():
        path=folder/filename;path.write_text(code,encoding='utf-8');paths.append(path)
    exe=folder/'negative.exe'
    command=[EDITOR/'NetCoreRuntime/dotnet.exe',EDITOR/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib',
        '-target:exe','-out:'+str(exe),*flags,ROOT/'Tools/Offline/TrackingMathShim.cs',ROOT/'Tools/Offline/PointerTests.cs',
        ROOT/'Assets/Rehab/3.2-vision/HandTracking.cs',*paths]
    compiled=subprocess.run([str(x) for x in command],cwd=ROOT,capture_output=True,text=True,timeout=60)
    (folder/'compile.log').write_text(compiled.stdout+compiled.stderr,encoding='utf-8')
    if compiled.returncode:raise RuntimeError(compiled.stdout+compiled.stderr)
    result=subprocess.run([str(EDITOR/'MonoBleedingEdge/bin/mono.exe'),str(exe)],cwd=ROOT,capture_output=True,text=True,timeout=30)
    (folder/'tests.log').write_text(result.stdout+result.stderr,encoding='utf-8')
    if result.returncode==0:raise RuntimeError('Mutation was not caught: '+name)
    results.append(dict(name=name,compiled=True,rejected=True,exit=result.returncode,summary=result.stdout.splitlines()[-1]))
    print(name+': rejected')
OUT.mkdir(parents=True,exist_ok=True)
(OUT/'checks.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),checks=results,
    productionReplacedByMutations=False,sourceHashes={name:hashlib.sha256((GAME/name).read_bytes()).hexdigest() for name in sources}),indent=2),encoding='utf-8')
