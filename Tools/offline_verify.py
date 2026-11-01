"""Compile against cached real Unity references, inspect metadata, run pure C# tests.
Never launches Unity/Hub, writes an Editor gate request, or uses desktop automation.
Cached references are prerequisites, not proof of a fresh Unity import/build.
"""
from pathlib import Path
from datetime import datetime, timezone
import argparse, hashlib, json, re, subprocess, sys, uuid

ROOT=Path(__file__).resolve().parents[1]
def editor_data():
    # Follow the project's pinned Editor instead of a hard-coded release.
    version=re.search(r'm_EditorVersion:\s*(\S+)',(ROOT/'ProjectSettings/ProjectVersion.txt').read_text(encoding='utf-8'))[1]
    return Path('C:/Program Files/Unity/Hub/Editor')/version/'Editor/Data'
EDITOR=editor_data()
OUTPUT=ROOT/'Logs/offline'

def run(command, log):
    result=subprocess.run([str(x) for x in command],cwd=ROOT,capture_output=True,text=True,errors='replace',timeout=180)
    log.write_text(result.stdout+result.stderr,encoding='utf-8')
    if result.returncode: raise RuntimeError(f'{log.name}: exit {result.returncode}\n{(result.stdout+result.stderr)[-5000:]}')
    return result

def metadata():
    guid_paths={}
    for meta in (ROOT/'Assets').rglob('*.meta'):
        match=re.search(r'^guid:\s*([0-9a-f]{32})',meta.read_text(encoding='utf-8-sig'),re.M)
        if not match or match[1] in guid_paths: raise ValueError(f'Missing/duplicate GUID: {meta}')
        guid_paths[match[1]]=meta
    for directory in ['Assets/_Project','Assets/Product','Assets/ProductIntegration','Assets/UI','Assets/UIBridge','Assets/ThirdParty']:
        folder=ROOT/directory
        if not folder.exists(): continue
        for path in [folder,*folder.rglob('*')]:
            if path.name.endswith('.meta'): continue
            if not Path(str(path)+'.meta').exists(): raise ValueError(f'Missing metadata: {path.relative_to(ROOT)}')
    # Package script GUIDs may resolve from PackageCache; do not invent missing owned references.
    package_guids=set()
    for meta in [*(ROOT/'Library/PackageCache').rglob('*.meta'), *(ROOT/'Packages').rglob('*.meta')]:
        match=re.search(r'^guid:\s*([0-9a-f]{32})',meta.read_text(encoding='utf-8-sig',errors='replace'),re.M)
        if match: package_guids.add(match[1])
    for folder in ['Assets/Scenes','Assets/UI','Assets/_Project']:
        for path in (ROOT/folder).rglob('*'):
            if path.suffix not in ['.unity','.prefab','.asset']: continue
            for guid in re.findall(r'm_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-f]{32})',path.read_text(encoding='utf-8-sig')):
                if guid not in guid_paths and guid not in package_guids: raise ValueError(f'Missing script: {path}: {guid}')
    return len(guid_paths)

def asmdefs():
    definitions={}
    guid_names={}
    for root in [ROOT/'Assets',ROOT/'Library/PackageCache',ROOT/'Packages']:
        for path in root.rglob('*.asmdef'):
            definition=json.loads(path.read_text(encoding='utf-8-sig'))
            definitions[definition['name']]=definition
            meta=Path(str(path)+'.meta')
            if meta.exists():
                match=re.search(r'^guid:\s*([0-9a-f]{32})',meta.read_text(encoding='utf-8-sig'),re.M)
                if match: guid_names[match[1]]=definition['name']
    external={p.stem for p in (ROOT/'Library/ScriptAssemblies').glob('*.dll')}
    external.update({'Unity.ugui','Unity.TextMeshPro'})
    visited,active=set(),set()
    def visit(name):
        if name in active: raise ValueError('Cyclic assembly dependency: '+name)
        if name in visited: return
        active.add(name)
        for reference in definitions[name].get('references',[]):
            if reference.startswith('GUID:'): reference=guid_names.get(reference[5:],reference)
            if reference not in definitions:
                if reference not in external: raise ValueError(f'{name}: unresolved reference {reference}')
            else: visit(reference)
        active.remove(name);visited.add(name)
    # Vendor optional integrations (Leap, Steam, etc.) are gated by their own
    # defineConstraints; audit the project's dependency closure, not disabled SDK samples.
    for name in definitions:
        if name.startswith('GripRehab.'): visit(name)
    return len(visited)

def ensure_owned_meta():
    for relative in ['Assets/Product','Assets/ProductIntegration','Assets/UI','Assets/UIBridge']:
        folder=ROOT/relative
        for path in [folder,*folder.rglob('*')]:
            if path.name.endswith('.meta'): continue
            meta=Path(str(path)+'.meta')
            if not meta.exists():
                guid=uuid.uuid5(uuid.NAMESPACE_URL,'GripRehab/'+path.relative_to(ROOT).as_posix()).hex
                meta.write_text('fileFormatVersion: 2\nguid: '+guid+'\n'+('folderAsset: yes\n' if path.is_dir() else ''),encoding='utf-8')

def compile_profile(profile):
    cache=ROOT/'Library/Bee/artifacts'/('1900b0aE.dag' if profile=='editor' else '1900b0aP.dag')
    out=OUTPUT/profile;out.mkdir(parents=True,exist_ok=True)
    jobs=[('GripRehab.Product','GripRehab.Core',['Assets/Product']),
          ('GripRehab.Core','GripRehab.Core',['Assets/_Project/Core']),
          ('GripRehab.UI','GripRehab.UI',['Assets/UI']),
          ('Assembly-CSharp','Assembly-CSharp',None)]
    if profile=='editor':jobs.append(('Assembly-CSharp-Editor','Assembly-CSharp-Editor',None))
    built={}
    for name,template,folders in jobs:
        response=cache/(template+'.rsp')
        if not response.exists(): raise FileNotFoundError('Cached compiler references missing: '+str(response))
        lines=response.read_text(encoding='utf-8-sig').splitlines()
        flags=[];sources=[]
        for line in lines:
            stripped=line.strip().strip('"')
            if stripped.endswith('.cs'): sources.append(stripped);continue
            if line.startswith(('-out:','-refout:','-analyzer:','/analyzer:','/additionalfile:')): continue
            if line.startswith('-r:'):
                referenced=Path(line[3:].strip('"').replace('\\','/')).name.replace('.ref.dll','.dll')
                if referenced in built:line='-r:"'+str(built[referenced])+'"'
            flags.append(line)
        if folders:
            sources=[str(path.relative_to(ROOT)).replace('\\','/') for folder in folders for path in (ROOT/folder).rglob('*.cs') if 'Editor' not in path.parts]
        elif name=='Assembly-CSharp':
            # New default-assembly scripts participate without needing Editor import.
            for path in (ROOT/'Assets').rglob('*.cs'):
                if 'Editor' in path.parts: continue
                if any(list(parent.glob('*.asmdef')) for parent in [path.parent,*path.parents] if parent!=ROOT and ROOT in parent.parents): continue
                sources.append(path.relative_to(ROOT).as_posix())
        else:sources+=['Assets/Editor/'+p.name for p in (ROOT/'Assets/Editor').glob('*.cs')]
        # Cached response files can name scripts removed since the last Editor import.
        sources=sorted({s for s in sources if (ROOT/s).is_file()})
        for dependency in {'GripRehab.UI':['GripRehab.Product'],'Assembly-CSharp':['GripRehab.Product'],'Assembly-CSharp-Editor':['GripRehab.Product']}.get(name,[]):
            flags.append('-r:"'+str(out/(dependency+'.dll'))+'"')
        flags.append('-out:"'+str(out/(name+'.dll'))+'"')
        rsp=out/(name+'.rsp');rsp.write_text('\n'.join(flags+['"'+x+'"' for x in sources]),encoding='utf-8')
        run([EDITOR/'NetCoreRuntime/dotnet.exe',EDITOR/'DotNetSdkRoslyn/csc.dll','@'+str(rsp)],out/(name+'.log'))
        built[name+'.dll']=out/(name+'.dll')
    return {name:hashlib.sha256(path.read_bytes()).hexdigest() for name,path in built.items()}

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--meta',action='store_true');parser.add_argument('--compile',action='store_true');parser.add_argument('--tests',action='store_true');args=parser.parse_args()
    OUTPUT.mkdir(parents=True,exist_ok=True)
    evidence=dict(utc=datetime.now(timezone.utc).isoformat(),scope='Offline compiler/metadata/domain checks only',checks=[],runtimeVerification='Not run by this tool; see separate Editor and Windows player evidence')
    try:
        if args.meta:ensure_owned_meta()
        evidence['checks'].append(dict(name='metadata',passed=True,count=metadata()))
        evidence['checks'].append(dict(name='assembly-graph',passed=True,count=asmdefs()))
        if args.compile:
            for profile in ['editor','player']:evidence['checks'].append(dict(name='compile-'+profile,passed=True,assemblies=compile_profile(profile)))
        if args.tests:
            tests=OUTPUT/'ProductTests.exe';refs=EDITOR/'MonoBleedingEdge/lib/mono/4.5'
            # Execute the same pure source against Mono's BCL; a netstandard
            # reference-only Unity assembly cannot be executed by itself.
            reference_flags=['-r:'+str(refs/name) for name in ['mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll','System.Runtime.Serialization.dll']]
            run([EDITOR/'NetCoreRuntime/dotnet.exe',EDITOR/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib','-target:exe','-out:'+str(tests),*reference_flags,
                 *sorted((ROOT/'Assets/Product').glob('*.cs')),'Tools/Offline/ProductTests.cs','Assets/Rehab/3.4-game-modules/WindowsProfileProtection.cs',
                 'Assets/Rehab/3.4-game-modules/RehabDwellGate.cs','Assets/Rehab/3.4.2-cognitive-performance/SequenceMemoryTask.cs',
                 'Assets/Rehab/3.4.2-cognitive-performance/SpatialAssemblyTask.cs','Assets/Rehab/3.4.2-cognitive-performance/AssemblyBoard.cs'],OUTPUT/'tests-compile.log')
            result=run([EDITOR/'MonoBleedingEdge/bin/mono.exe',tests],OUTPUT/'product-tests.log')
            evidence['checks'].append(dict(name='product-domain-tests',passed=True,result=result.stdout.strip()))
            tracking=OUTPUT/'TrackingTests.exe'
            run([EDITOR/'NetCoreRuntime/dotnet.exe',EDITOR/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib','-target:exe','-out:'+str(tracking),
                 *reference_flags,'-r:'+str(refs/'System.Numerics.dll'),'-r:'+str(refs/'System.Numerics.Vectors.dll'),
                 'Tools/Offline/TrackingMathShim.cs','Tools/Offline/TrackingTests.cs',
                 'Assets/Rehab/3.4.3-visuomotor-coordination/VisuomotorTasks.cs',
                 'Assets/Rehab/3.2-vision/HandTracking.cs',
                 'Assets/Rehab/3.4.1-force-object-interaction/Apartment/FingerPoseDriver.cs','Assets/Rehab/3.4.1-force-object-interaction/Apartment/GripCatalog.cs',
                 'Assets/Rehab/3.4.1-force-object-interaction/Apartment/HandRigRules.cs'],OUTPUT/'tracking-tests-compile.log')
            result=run([EDITOR/'MonoBleedingEdge/bin/mono.exe',tracking],OUTPUT/'tracking-tests.log')
            evidence['checks'].append(dict(name='tracking-source-tests',passed=True,result=result.stdout.strip(),
                scope='Production pose source with managed math/Transform substitutes; no native Unity, SDK or camera'))
            keyboard=OUTPUT/'KeyboardTests.exe'
            run([EDITOR/'NetCoreRuntime/dotnet.exe',EDITOR/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib','-target:exe','-out:'+str(keyboard),
                 *reference_flags,'Tools/Offline/KeyboardTests.cs',
                 'Assets/Rehab/3.4-game-modules/RehabKeyboardGate.cs'],OUTPUT/'keyboard-tests-compile.log')
            result=run([EDITOR/'MonoBleedingEdge/bin/mono.exe',keyboard],OUTPUT/'keyboard-tests.log')
            evidence['checks'].append(dict(name='keyboard-policy-tests',passed=True,result=result.stdout.strip(),
                scope='Production keyboard gate with fabricated frame/key states; no Unity Input/EventSystem or OS input'))
            voice=OUTPUT/'VoiceTests.exe'
            run([EDITOR/'NetCoreRuntime/dotnet.exe',EDITOR/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib','-target:exe','-out:'+str(voice),
                 *reference_flags,'Tools/Offline/VoiceTests.cs',
                 'Assets/Rehab/3.4-game-modules/VoiceCommandRouter.cs'],OUTPUT/'voice-tests-compile.log')
            result=run([EDITOR/'MonoBleedingEdge/bin/mono.exe',voice],OUTPUT/'voice-tests.log')
            evidence['checks'].append(dict(name='voice-router-tests',passed=True,result=result.stdout.strip(),
                scope='Production voice router and spelling buffer with text transcripts; no microphone, Windows speech or Unity'))
            loco=OUTPUT/'HandLocomotionTests.exe'
            run([EDITOR/'NetCoreRuntime/dotnet.exe',EDITOR/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib','-target:exe','-out:'+str(loco),
                 *reference_flags,'-r:'+str(refs/'System.Numerics.dll'),'-r:'+str(refs/'System.Numerics.Vectors.dll'),'Tools/Offline/HandLocomotionTests.cs',
                 'Assets/Rehab/3.2-vision/HandLocomotionPolicy.cs','Tools/Offline/TrackingMathShim.cs','Assets/Rehab/3.4.1-force-object-interaction/Apartment/HandRigRules.cs'],OUTPUT/'hand-locomotion-tests-compile.log')
            result=run([EDITOR/'MonoBleedingEdge/bin/mono.exe',loco],OUTPUT/'hand-locomotion-tests.log')
            evidence['checks'].append(dict(name='hand-locomotion-tests',passed=True,result=result.stdout.strip(),
                scope='Production hand look/walk policy with fabricated palm positions; no camera or Unity'))
            health=OUTPUT/'DeviceHealthTests.exe'
            run([EDITOR/'NetCoreRuntime/dotnet.exe',EDITOR/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib','-target:exe','-out:'+str(health),
                 *reference_flags,'Tools/Offline/DeviceHealthTests.cs','Assets/Rehab/6.1-bluetooth-integration/DeviceHealth.cs'],OUTPUT/'device-health-tests-compile.log')
            result=run([EDITOR/'MonoBleedingEdge/bin/mono.exe',health],OUTPUT/'device-health-tests.log')
            evidence['checks'].append(dict(name='device-health-tests',passed=True,result=result.stdout.strip(),
                scope='Production device-status logic with injected packets, dropouts, stale gaps and reconnects; no BLE or Unity'))
            grasp=OUTPUT/'GraspTests.exe'
            run([EDITOR/'NetCoreRuntime/dotnet.exe',EDITOR/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib','-target:exe','-out:'+str(grasp),
                 *reference_flags,'-r:'+str(refs/'System.Numerics.dll'),'-r:'+str(refs/'System.Numerics.Vectors.dll'),
                 'Tools/Offline/TrackingMathShim.cs','Tools/Offline/GraspPhysicsShim.cs','Tools/Offline/GraspTests.cs',
                 'Assets/Rehab/3.4.1-force-object-interaction/Apartment/GraspMotionPolicy.cs',
                 'Assets/Rehab/3.4.1-force-object-interaction/Apartment/GraspCollisionShield.cs',
                 'Assets/Rehab/3.4.1-force-object-interaction/Apartment/ContactGeometry.cs','Assets/Rehab/3.4.1-force-object-interaction/Apartment/GripCatalog.cs'],OUTPUT/'grasp-tests-compile.log')
            result=run([EDITOR/'MonoBleedingEdge/bin/mono.exe',grasp],OUTPUT/'grasp-tests.log')
            evidence['checks'].append(dict(name='grasp-source-tests',passed=True,result=result.stdout.strip(),
                scope='Production motion policy and pair lifecycle with managed math/bounds/pair recorder; no native PhysX or scene'))
            deformation=OUTPUT/'DeformationTests.exe'
            run([EDITOR/'NetCoreRuntime/dotnet.exe',EDITOR/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib','-target:exe','-out:'+str(deformation),
                 *reference_flags,'-r:'+str(refs/'System.Numerics.dll'),'-r:'+str(refs/'System.Numerics.Vectors.dll'),
                 'Tools/Offline/TrackingMathShim.cs','Tools/Offline/DeformationTests.cs',
                 'Assets/Rehab/3.4.1-force-object-interaction/Apartment/GraspMotionPolicy.cs',
                 'Assets/Rehab/3.4.1-force-object-interaction/Apartment/ContactDeformation.cs',
                 'Assets/Rehab/3.4.1-force-object-interaction/Apartment/FracturePartitioner.cs','Assets/Rehab/3.4.1-force-object-interaction/Apartment/DeformationMaterial.cs','Assets/Rehab/3.4.1-force-object-interaction/Apartment/ActivityCore.cs','Assets/Rehab/3.4.1-force-object-interaction/Apartment/PlaneCutter.cs'],OUTPUT/'deformation-tests-compile.log')
            result=run([EDITOR/'MonoBleedingEdge/bin/mono.exe',deformation],OUTPUT/'deformation-tests.log')
            evidence['checks'].append(dict(name='deformation-fracture-source-tests',passed=True,result=result.stdout.strip(),
                scope='Production kernel/state/damage/partition source with managed math; no rendering or native physics'))
            rig=OUTPUT/'HandRigTests.exe'
            run([EDITOR/'NetCoreRuntime/dotnet.exe',EDITOR/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib','-target:exe','-define:HAND_RIG_TESTS','-out:'+str(rig),
                 *reference_flags,'-r:'+str(refs/'System.Numerics.dll'),'-r:'+str(refs/'System.Numerics.Vectors.dll'),
                 'Tools/Offline/TrackingMathShim.cs','Tools/Offline/HandRigSdkShim.cs','Tools/Offline/HandRigTests.cs',
                 'Assets/Rehab/3.2-vision/HandTracking.cs',
                 'Assets/Rehab/3.4.1-force-object-interaction/Apartment/HandRigRules.cs',
                 'Assets/Rehab/3.4.1-force-object-interaction/Apartment/FingerPoseDriver.cs','Assets/Rehab/3.4.1-force-object-interaction/Apartment/GripCatalog.cs',
                 'Assets/Rehab/3.4.1-force-object-interaction/Apartment/RehabVGController.cs',
                 'Assets/Rehab/3.4.1-force-object-interaction/Apartment/RehabVirtualGrasp.cs'],OUTPUT/'hand-rig-tests-compile.log')
            result=run([EDITOR/'MonoBleedingEdge/bin/mono.exe',rig],OUTPUT/'hand-rig-tests.log')
            evidence['checks'].append(dict(name='hand-rig-source-tests',passed=True,result=result.stdout.strip(),
                scope='Production pose driver/SDK adapters with managed math and SDK doubles; no native VirtualGrasp or rendering'))
            result=run([sys.executable,'-m','unittest','Tools.test_hand_rig_bindings'],OUTPUT/'hand-rig-bindings.log')
            evidence['checks'].append(dict(name='hand-rig-static-bindings',passed=True,result=(result.stdout+result.stderr).strip(),
                scope='Saved-scene/source wiring contracts; no native SDK'))
            pointer=OUTPUT/'PointerTests.exe'
            run([EDITOR/'NetCoreRuntime/dotnet.exe',EDITOR/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib','-target:exe','-out:'+str(pointer),
                 *reference_flags,'-r:'+str(refs/'System.Numerics.dll'),'-r:'+str(refs/'System.Numerics.Vectors.dll'),
                 'Tools/Offline/TrackingMathShim.cs','Tools/Offline/PointerTests.cs',
                 'Assets/Rehab/3.2-vision/HandTracking.cs','Assets/Rehab/3.4-game-modules/RehabDwellGate.cs',
                 'Assets/Rehab/3.4-game-modules/RehabPointerPolicy.cs'],OUTPUT/'pointer-tests-compile.log')
            result=run([EDITOR/'MonoBleedingEdge/bin/mono.exe',pointer],OUTPUT/'pointer-tests.log')
            evidence['checks'].append(dict(name='pointer-source-tests',passed=True,result=result.stdout.strip(),
                scope='Production whole-hand/palm-coordinate/rearm/dwell source with managed math and fabricated landmarks; no native uGUI or camera'))
            result=run([sys.executable,'-m','unittest','Tools.test_pointer_bindings'],OUTPUT/'pointer-bindings.log')
            evidence['checks'].append(dict(name='pointer-static-bindings',passed=True,result=(result.stdout+result.stderr).strip(),
                scope='Static runtime integration contracts; no native uGUI'))
            result=run([sys.executable,'-m','unittest','Tools.test_deformation_bindings'],OUTPUT/'deformation-bindings.log')
            evidence['checks'].append(dict(name='deformation-static-bindings',passed=True,result=(result.stdout+result.stderr).strip(),
                scope='Static integration contracts; no native Unity'))
            result=run([sys.executable,'-m','unittest','Tools.test_grasp_bindings'],OUTPUT/'grasp-binding-tests.log')
            evidence['checks'].append(dict(name='grasp-static-bindings',passed=True,result=(result.stdout+result.stderr).strip(),
                scope='Static source integration contracts; not runtime verification'))
            result=run([sys.executable,'-m','unittest','Tools.test_keyboard_bindings'],OUTPUT/'keyboard-binding-tests.log')
            evidence['checks'].append(dict(name='keyboard-static-bindings',passed=True,result=(result.stdout+result.stderr).strip(),
                scope='Static integration contracts; not native runtime verification'))
        evidence['passed']=True
    except Exception as error:evidence['passed']=False;evidence['error']=str(error)
    (ROOT/'Docs/evidence/files-only-checks.json').write_text(json.dumps(evidence,indent=2),encoding='utf-8')
    print(json.dumps(evidence,indent=2));return 0 if evidence['passed'] else 1

if __name__=='__main__':sys.exit(main())
