"""Mutate isolated source copies only; never launch Unity or the native SDK."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[2]
APT=ROOT/'Assets/Rehab/3.4.1-force-object-interaction/Apartment'
EDITOR=Path('C:/Program Files/Unity/Hub/Editor/6000.4.4f1/Editor/Data')
OUT=ROOT/'Logs/offline/hand-rig-negative'
sources={name:(APT/name).read_text(encoding='utf-8') for name in ['FingerPoseDriver.cs','HandRigRules.cs','RehabVGController.cs','RehabVirtualGrasp.cs']}
cases=['wrong-contact-channel-order','unvalidated-delta-time','mesh-registration-without-input','partial-invalid-sdk-frame']
refs=EDITOR/'MonoBleedingEdge/lib/mono/4.5'
flags=['-r:'+str(refs/n) for n in ['mscorlib.dll','System.dll','System.Core.dll','System.Numerics.dll','System.Numerics.Vectors.dll']]
checks=[]
def change(files,name,old,new):
    assert files[name].count(old)==1,(name,old)
    files[name]=files[name].replace(old,new)
for case in cases:
    folder=OUT/case;folder.mkdir(parents=True,exist_ok=True);copies=dict(sources)
    if case==cases[0]:change(copies,'HandRigRules.cs','return (channel+1)%5;','return channel;')
    elif case==cases[1]:change(copies,'HandRigRules.cs','float.IsFinite(dt)&&dt>0?Math.Min(.1f,dt):0','Math.Min(.1f,dt)')
    elif case==cases[2]:change(copies,'RehabVirtualGrasp.cs','if(!RehabVGController.HasBindings(id)){Pending("Both project input controller bindings are required; verify VG_CP_Rehab.Input");return;}','// isolated negative: omit controller binding guard')
    else:
        change(copies,'RehabVGController.cs','poses[i]=Matrix4x4.TRS(source.position,rotation,Vector3.one);','poses[i]=Matrix4x4.TRS(source.position,rotation,Vector3.one);SetPose(i,poses[i]);')
        change(copies,'RehabVGController.cs','{SetPose(i,poses[i]);WrittenThisFrame++;Matches++;}','{WrittenThisFrame++;Matches++;}')
    paths=[]
    for name,code in copies.items():
        path=folder/name;path.write_text(code,encoding='utf-8');paths.append(path)
    exe=folder/'negative.exe'
    command=[EDITOR/'NetCoreRuntime/dotnet.exe',EDITOR/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib',
        '-target:exe','-define:HAND_RIG_TESTS','-out:'+str(exe),*flags,ROOT/'Tools/Offline/TrackingMathShim.cs',
        ROOT/'Tools/Offline/HandRigSdkShim.cs',ROOT/'Tools/Offline/HandRigTests.cs',ROOT/'Assets/Rehab/3.2-vision/HandTracking.cs',*paths]
    compiled=subprocess.run([str(x) for x in command],cwd=ROOT,capture_output=True,text=True,timeout=60)
    (folder/'compile.log').write_text(compiled.stdout+compiled.stderr,encoding='utf-8')
    if compiled.returncode:raise RuntimeError(compiled.stdout+compiled.stderr)
    run=subprocess.run([str(EDITOR/'MonoBleedingEdge/bin/mono.exe'),str(exe)],cwd=ROOT,capture_output=True,text=True,timeout=30)
    (folder/'tests.log').write_text(run.stdout+run.stderr,encoding='utf-8')
    if run.returncode==0:raise RuntimeError('Mutation escaped tests: '+case)
    checks.append(dict(name=case,compiled=True,rejected=True,exit=run.returncode,summary=run.stdout.splitlines()[-1]));print(case+': rejected')
(OUT/'checks.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),checks=checks,productionReplacedByMutations=False,
    sourceHashes={name:hashlib.sha256((APT/name).read_bytes()).hexdigest() for name in sources}),indent=2),encoding='utf-8')
