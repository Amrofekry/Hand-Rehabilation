"""Negative sensitivity checks in Logs only; never launches Unity or replaces Assets."""
from pathlib import Path
import json,subprocess
from datetime import datetime,timezone
root=Path(__file__).resolve().parents[2]
apt=root/'Assets/Rehab/3.4.1-force-object-interaction/Apartment'
# Execute negative mutations in Logs only; no production file is replaced.
editor=Path('C:/Program Files/Unity/Hub/Editor/6000.4.4f1/Editor/Data')
refs=editor/'MonoBleedingEdge/lib/mono/4.5'
base=[str(editor/'NetCoreRuntime/dotnet.exe'),str(editor/'DotNetSdkRoslyn/csc.dll'),'-nologo','-noconfig','-nostdlib','-target:exe']
flags=['-r:'+str(refs/n) for n in ['mscorlib.dll','System.dll','System.Core.dll','System.Numerics.dll','System.Numerics.Vectors.dll']]
output=root/'Logs/item4-negative';output.mkdir(parents=True,exist_ok=True)
policy=(apt/'GraspMotionPolicy.cs').read_text(encoding='utf-8')
shield=(apt/'GraspCollisionShield.cs').read_text(encoding='utf-8')
tests=[]
for name,old,new,which in [
    ('overlap-restored-too-soon','if(!held&&GraspMotionPolicy.Separated(a.min,a.max,b.min,b.max,.015f))','if(!held)','shield'),
    ('teleport-generates-throw','if(!Finite(delta)||delta.sqrMagnitude>maximumStep*maximumStep)return new Vector3();','if(!Finite(delta))return new Vector3();','policy')]:
    mutated=(shield if which=='shield' else policy)
    assert mutated.count(old)==1
    mutated=mutated.replace(old,new)
    source=output/(name+'.cs');source.write_text(mutated,encoding='utf-8')
    exe=output/(name+'.exe')
    production=[source,apt/'GraspMotionPolicy.cs'] if which=='shield' else [source,apt/'GraspCollisionShield.cs']
    command=base+['-out:'+str(exe)]+flags+[
        str(root/'Tools/Offline/TrackingMathShim.cs'),str(root/'Tools/Offline/GraspPhysicsShim.cs'),
        str(root/'Tools/Offline/GraspTests.cs')]+[str(x) for x in production]
    compiled=subprocess.run(command,cwd=root,capture_output=True,text=True,errors='replace',timeout=60)
    (output/(name+'-compile.log')).write_text(compiled.stdout+compiled.stderr,encoding='utf-8')
    if compiled.returncode: raise RuntimeError(compiled.stdout+compiled.stderr)
    result=subprocess.run([str(editor/'MonoBleedingEdge/bin/mono.exe'),str(exe)],cwd=root,capture_output=True,text=True,errors='replace',timeout=30)
    (output/(name+'.log')).write_text(result.stdout+result.stderr,encoding='utf-8')
    if result.returncode==0: raise RuntimeError('Negative mutation unexpectedly passed')
    tests.append(dict(name=name,rejected=True,compileExit=compiled.returncode,testExit=result.returncode,scope='Managed test recorder/math, not native Unity'))
    print(name+': rejected by source harness')
data=dict(utc=datetime.now(timezone.utc).isoformat(),productionModifiedByMutations=False,checks=tests)
(root/'Docs/evidence/item4-negative-checks.json').write_text(json.dumps(data,indent=2),encoding='utf-8')

