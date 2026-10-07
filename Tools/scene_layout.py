"""Read-only inspection of the authored additive workspace (never writes YAML).

Unity's manifest restores cross-scene references before content activation. For
legacy saved-wiring tests this reconstructs those references in memory only.
Native scene loading is verified separately by Editor/player acceptance tests.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
MAIN = ROOT / 'Assets/Scenes/3.1-Bootstrap.unity'
MANIFEST = ROOT / 'Assets/Resources/SceneComposition.asset'

def blocks(path):
    text = path.read_text(encoding='utf-8-sig')
    return {m[1]: m[0] for m in re.finditer(
        r'(?ms)^--- !u!\d+ &(-?\d+).*?(?=^--- !u!|\Z)', text)}

def paths():
    if not MANIFEST.exists(): return [MAIN]
    return [ROOT / p for p in re.findall(r'^  - path: (.+)$', MANIFEST.read_text(), re.M)]

def links():
    if not MANIFEST.exists(): return []
    result = []
    for m in re.finditer(r'(?ms)^  - ownerId: (\w+)\n(.*?)(?=^  - ownerId:|^  sunLightId:|\Z)', MANIFEST.read_text()):
        item = {'ownerId': m[1]}
        for field in ['property','targetId','ownerComponent','targetComponent']:
            value = re.search(r'^    '+field+r': (.+)$', m[2], re.M)[1].strip("'")
            item[field] = int(value) if field.endswith('Component') else value
        result.append(item)
    return result

def marked_objects(records):
    markers = {}
    for body in records.values():
        match = re.search(r'^  id: (\w+)$', body, re.M)
        if match:
            go = re.search(r'^  m_GameObject: \{fileID: (-?\d+)\}', body, re.M)[1]
            if match[1] in markers: raise ValueError('Duplicate reference marker')
            components = records[go].split('  m_Component:', 1)
            ids = re.findall(r'component: \{fileID: (-?\d+)\}', components[1].split('  m_Layer:',1)[0]) if len(components)>1 else []
            markers[match[1]] = (go, ids)
    return markers

def composed_blocks():
    records = {}
    for number, path in enumerate(paths()):
        local = blocks(path)
        containers = {key for key, body in local.items() if re.search(r'^  m_Name: .+ / content$', body, re.M)}
        generated = {key for key, body in local.items() if key in containers or
            'GripRehab.Composition.SceneReferenceId' in body or
            any('  m_GameObject: {fileID: '+go+'}' in body for go in containers)}
        remap = {key: str(1000000000000000 + number * 10000 + index) for index, key in enumerate(sorted(generated))}
        for key, body in local.items():
            kind = int(re.match(r'--- !u!(\d+)', body)[1])
            if kind in [29,104,157,196,1660057539]: continue  # per-scene settings/root list
            body = re.sub(r'^(--- !u!\d+ &)(-?\d+)', lambda m: m[1]+remap.get(m[2],m[2]), body)
            body = re.sub(r'\{fileID: (-?\d+)\}', lambda m: '{fileID: '+remap.get(m[1],m[1])+'}', body)
            key = remap.get(key, key)
            if key in records: raise ValueError('Conflicting authored object ID across content scenes: '+key)
            records[key] = body
    if not MANIFEST.exists(): return records
    markers = marked_objects(records)
    for link in links():
        owner_go, owner_components = markers[link['ownerId']]
        if owner_components: owner = owner_components[link['ownerComponent']]
        else:
            # A stripped prefab GameObject omits its inherited component list;
            # locate the serialized override component by its unique field.
            field = link['property'].split('.Array.data[')[0]
            candidates = [key for key, body in records.items() if '  m_GameObject: {fileID: '+owner_go+'}' in body and
                re.search(r'^  '+re.escape(field)+':', body,re.M)]
            # Inherited fields serialize in PrefabInstance modifications rather
            # than a local MonoBehaviour block. Native verification covers
            # these links; this adapter does not expand or alter vendor prefabs.
            if not candidates: continue
            if len(candidates)!=1: raise ValueError('Ambiguous prefab field: '+field)
            owner = candidates[0]
        target_go, target_components = markers[link['targetId']]
        if link['targetComponent'] < 0: target = target_go
        elif target_components: target = target_components[link['targetComponent']]
        else:
            # This scene stores only one stripped component on its prefab seat.
            candidates = [key for key, body in records.items() if '  m_GameObject: {fileID: '+target_go+'}' in body and
                'GripRehab.Composition.SceneReferenceId' not in body]
            if not candidates:
                # The unused stripped SeatPoint stub is omitted by Unity after
                # cross-scene references are cleared. Resolve its inherited
                # component from the actual prefab, then use its original ID
                # only to normalize the historical wiring comparison.
                prefab = ROOT/'Assets/Rehab/3.1-environment/Apartment/Prefabs/Apartment02.prefab'
                source_go = re.search(r'm_CorrespondingSourceObject: \{fileID: (\d+), guid: (\w+)', records[target_go])
                if source_go is None or source_go[2] not in Path(str(prefab)+'.meta').read_text(): raise ValueError('Unknown inherited prefab')
                prefab_blocks = blocks(prefab)
                components = prefab_blocks[source_go[1]].split('  m_Component:')[1].split('  m_Layer:')[0]
                source_component = re.findall(r'component: \{fileID: (-?\d+)\}',components)[link['targetComponent']]
                archived = blocks(ROOT/'Assets/SceneBackups/GripRehab-BeforeSceneSplit.unity')
                stubs = [(key,body) for key,body in archived.items() if 'm_CorrespondingSourceObject: {fileID: '+source_component+', guid: '+source_go[2] in body]
                if len(stubs)!=1: raise ValueError('Inherited component stub is ambiguous')
                target, body = stubs[0]; records[target] = body
            elif len(candidates)!=1: raise ValueError('Ambiguous stripped prefab target')
            else: target = candidates[0]
        prop = link['property']
        if '.Array.data[' in prop:
            name, suffix = prop.split('.Array.data[')
            index = int(suffix.rstrip(']'))
            found = re.search(r'(?ms)^  '+re.escape(name)+r':.*?(?=^  [A-Za-z_]|\Z)', records[owner])
            count = [-1]
            def replace(m):
                count[0] += 1
                return '{fileID: '+target+'}' if count[0] == index else m[0]
            section = re.sub(r'\{fileID: -?\d+\}', replace, found[0])
            records[owner] = records[owner][:found.start()] + section + records[owner][found.end():]
        else:
            records[owner], count = re.subn(r'(?m)^(  '+re.escape(prop)+r': )\{fileID: -?\d+\}',
                lambda m: m[1]+'{fileID: '+target+'}', records[owner])
            if count != 1: raise ValueError('Unresolved manifest field: '+prop)
    return records
