from pathlib import Path
from datetime import datetime,timezone
import json,subprocess,hashlib
project=Path(__file__).resolve().parents[2];task=project
out=project/'Logs/offline/deformation-negative';out.mkdir(parents=True,exist_ok=True)
apt=project/'Assets/Rehab/3.4.1-force-object-interaction/Apartment'
editor=Path('C:/Program Files/Unity/Hub/Editor/6000.4.4f1/Editor/Data');refs=editor/'MonoBleedingEdge/lib/mono/4.5'
flags=['-r:'+str(refs/n) for n in ['mscorlib.dll','System.dll','System.Core.dll','System.Numerics.dll','System.Numerics.Vectors.dll']]
policy=(apt/'ContactDeformation.cs').read_text(encoding='utf-8')
part=(apt/'FracturePartitioner.cs').read_text(encoding='utf-8')
baseline={'ContactDeformation.cs':policy,'FracturePartitioner.cs':part}
cases=[
 ('dent-pushed-outward','ContactDeformation.cs','normal*(-depth)','normal*(depth)'),
 ('final-recovery-upload-lost','ContactDeformation.cs','if((next-Vertices[i]).sqrMagnitude>0)','if((next-Vertices[i]).sqrMagnitude>0&&(next-goal).sqrMagnitude>0)'),
 ('missing-fracture-triangle','FracturePartitioner.cs','for(int i=0;i<centroids.Length;i++){\n   float best=', 'for(int i=0;i<centroids.Length-1;i++){\n   float best=')
]
checks=[]
for name,which,old,new in cases:
    folder=out/name;folder.mkdir(parents=True,exist_ok=True)
    sources=[]
    for filename,content in baseline.items():
        if filename==which:
            assert content.count(old)==1
            content=content.replace(old,new)
        path=folder/filename;path.write_text(content,encoding='utf-8');sources.append(path)
    exe=folder/'negative.exe'
    command=[editor/'NetCoreRuntime/dotnet.exe',editor/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib','-target:exe','-out:'+str(exe),*flags,
             project/'Tools/Offline/TrackingMathShim.cs',project/'Tools/Offline/DeformationTests.cs',apt/'GraspMotionPolicy.cs',*sources]
    compiled=subprocess.run([str(x) for x in command],cwd=task,capture_output=True,text=True,errors='replace',timeout=60)
    (folder/'compile.log').write_text(compiled.stdout+compiled.stderr,encoding='utf-8')
    if compiled.returncode:raise RuntimeError(compiled.stdout+compiled.stderr)
    result=subprocess.run([str(editor/'MonoBleedingEdge/bin/mono.exe'),str(exe)],cwd=task,capture_output=True,text=True,errors='replace',timeout=30)
    (folder/'test.log').write_text(result.stdout+result.stderr,encoding='utf-8')
    if result.returncode==0:raise RuntimeError('Negative mutation unexpectedly passed: '+name)
    checks.append(dict(name=name,compiled=True,rejected=True,exit=result.returncode))
    print(name+': rejected')
(out/'checks.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(),checks=checks,productionReplacedByMutations=False,sourceHashes={name:hashlib.sha256((apt/name).read_bytes()).hexdigest() for name in baseline}),indent=2),encoding='utf-8')
