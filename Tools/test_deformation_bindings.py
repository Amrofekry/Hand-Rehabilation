from pathlib import Path
import unittest,re
ROOT=Path(__file__).resolve().parents[1]
APT='Assets/Rehab/3.4.1-force-object-interaction/Apartment'
def source(relative):
    return (ROOT/relative).read_text(encoding='utf-8-sig')
class DeformationBindings(unittest.TestCase):
    def setUp(self):
        self.obj=source(APT+'/SqueezableObject.cs')
        self.visual=source(APT+'/ObjectFractureVisuals.cs')
    def test_confirmed_contact_not_callback_cache_drives_pressure(self):
        self.assertIn('TryProbeContact(boundCapsules[i],out contactPoints[i],out contactNormals[i])',self.obj)
        self.assertIn('Physics.ComputePenetration(',self.obj)
        self.assertIn('boundProbes[i].SetForce(force/ContactCount)',self.obj)
        self.assertNotIn('probe.Contact==this',self.obj)
        self.assertIn('foreach(var probe in boundProbes)probe?.SetForce(0)',self.obj)
    def test_solver_uses_world_metres_and_dynamic_clone(self):
        self.assertIn('mesh=Instantiate(filter.sharedMesh)',self.obj)
        self.assertIn('mesh.MarkDynamic()',self.obj)
        self.assertIn('worldRest[i]=visual.TransformPoint(rest[i])',self.obj)
        self.assertIn('visual.InverseTransformVector(delta)',self.obj)
        self.assertIn('mesh.SetVertices(deformation.Vertices)',self.obj)
        self.assertIn('ContactDeformation.SafeDt(dt)',self.obj)
    def test_force_without_contact_cannot_deform_or_damage(self):
        self.assertIn('if(ContactCount>0&&force>0&&dt>0)',self.obj)
        self.assertIn('GripDamage(ContactCount>0?force:0,dt)',self.obj)
        self.assertIn('ApplyContacts(null,0,dt)',self.obj)
    def test_material_profiles_have_valid_distinct_responses(self):
        profiles=sorted((ROOT/APT/'Profiles').glob('*.asset'))
        self.assertEqual(len(profiles),6)
        for path in profiles:
            fields=dict(re.findall(r'(?m)^  ([A-Za-z]\w*): ([^\r\n]+)',path.read_text(encoding='utf-8-sig')))
            self.assertGreater(float(fields['fullCompressionNewtons']),0,path.name)
            self.assertGreater(float(fields['deformationRadiusMeters']),0,path.name)
            self.assertGreater(float(fields['recoveryPerSecond']),0,path.name)
            self.assertGreaterEqual(float(fields['maxDepthMeters']),0,path.name)
            if 'maxVertices' in fields:self.assertLessEqual(int(fields['maxVertices']),4096,path.name)
        mug=(ROOT/APT/'Profiles/04_Porcelain_mug.asset').read_text(encoding='utf-8-sig')
        self.assertIn('deformable: 0',mug);self.assertIn('maxDepthMeters: 0',mug);self.assertIn('breakable: 1',mug)
        tomato=(ROOT/APT/'Profiles/06_Ripe_tomato.asset').read_text(encoding='utf-8-sig')
        self.assertIn('plasticity: 0.25',tomato)
    def test_camera_peak_reaches_brittle_threshold_without_claiming_force(self):
        settings=source('Assets/Rehab/3.1-environment/Apartment/ApartmentSettings.asset')
        peak=float(re.search(r'cameraVisualMaxNewtons: ([\d.]+)',settings)[1])
        self.assertEqual(peak,80)
        for path in (ROOT/APT/'Profiles').glob('*.asset'):
            text=path.read_text(encoding='utf-8-sig')
            if 'breakable: 1' in text:self.assertGreater(peak,float(re.search(r'sustainedForceNewtons: ([\d.]+)',text)[1]))
        self.assertIn('No measured force; open hand to release',source(APT+'/ApartmentExperience.cs'))
    def test_inactive_fragments_are_cached_then_activated(self):
        self.assertIn('fragments.GetComponentsInChildren<Rigidbody>(true)',self.obj)
        self.assertIn('fragments.GetComponentsInChildren<Collider>(true)',self.obj)
        self.assertIn('fractureVisuals.Root',self.obj)
        self.assertIn('fragments.SetActive(true)',self.obj)
        self.assertIn('FracturePartitioner.Split(vertices,triangles,12)',self.visual)
        self.assertNotIn('CreatePrimitive',self.visual)
    def test_fracture_preserves_player_shield_and_limits_scatter(self):
        self.assertIn('collisionShield.Protect(shardColliders,playerColliders)',self.obj)
        self.assertIn('inherited+away*.12f+Vector3.up*.03f,1.5f',self.obj)
        self.assertNotIn('ForceMode.Impulse',self.obj)
        self.assertIn('shardColliders[i],shardColliders[j],true',self.obj)
    def test_damage_seams_and_reset_are_wired(self):
        self.assertIn('fractureVisuals?.SetDamage(DamageValue,IsBroken)',self.obj)
        self.assertIn('fractureVisuals?.SetDamage(0,false)',self.obj)
        self.assertIn('deformation.Reset()',self.obj)
        self.assertIn('MaxDisplacementMeters=0',self.obj)
        self.assertIn('objectColliders[i].enabled=colliderEnabled[i]',self.obj)
        self.assertIn('fractureVisuals?.Dispose()',self.obj)
        self.assertIn('value>=.25f',self.visual)
    def test_native_acceptance_checks_actual_geometry_and_fragments(self):
        tests=source(APT+'/ApartmentAcceptance.cs')
        self.assertIn('ball.HasProbeContact(p,out _,out _)',tests)
        self.assertIn('mug.FragmentsVisible&&mug.FragmentCount>1',tests)
        self.assertIn('!cracker.FragmentsVisible&&cracker.DamageValue==0&&cracker.MaxDisplacementMeters==0',tests)
if __name__=='__main__':unittest.main()
