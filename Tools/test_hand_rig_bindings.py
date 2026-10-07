"""Saved-scene and source contracts; no native SDK, rendering or camera."""
from collections import Counter
from pathlib import Path
import re,unittest
from Tools.scene_layout import composed_blocks
ROOT=Path(__file__).resolve().parents[1]
APT=ROOT/'Assets/Rehab/3.4.1-force-object-interaction/Apartment'

def scene_objects():
    records={}
    for key, block in composed_blocks().items():
        header=re.match(r'--- !u!(\d+) &(-?\d+)\n',block)
        if header:records[int(key)]=(int(header[1]),block)
    return records

class HandRigBindings(unittest.TestCase):
    def test_saved_input_skeleton_has_one_of_each_required_bone(self):
        records=scene_objects()
        sdk=next(body for kind,body in records.values() if 'Assembly-CSharp::GripRehab.RehabVirtualGrasp' in body)
        root_id=int(re.search(r'inputSkeleton: \{fileID: (-?\d+)',sdk)[1])
        names=[];pending=[root_id];seen=set()
        while pending:
            fid=pending.pop();self.assertNotIn(fid,seen);seen.add(fid)
            kind,body=records[fid];self.assertEqual(kind,4)
            go=int(re.search(r'm_GameObject: \{fileID: (-?\d+)',body)[1])
            names.append(re.search(r'(?m)^  m_Name: ([^\r\n]+)',records[go][1])[1])
            children=body.split('  m_Children:',1)[1].split('  m_Father:',1)[0]
            pending += [int(n) for n in re.findall(r'fileID: (-?\d+)',children)]
        counts=Counter(names)
        for side in ['L','R']:
            self.assertEqual(counts['Wrist_'+side],1)
            for finger in ['Thumb','Index','Middle','Ring','Pinky']:
                for joint in ['A','B','C']:self.assertEqual(counts[finger+joint+'_'+side],1)
    def test_saved_pose_drivers_match_channel_order_and_imported_axes(self):
        records=scene_objects()
        drivers=[body for kind,body in records.values() if 'Assembly-CSharp::GripRehab.FingerPoseDriver' in body]
        self.assertEqual(len(drivers),2)
        for body in drivers:
            go=int(re.search(r'm_GameObject: \{fileID: (-?\d+)',body)[1])
            side='L' if 'm_Name: Left hand input' in records[go][1] else 'R'
            joints=body.split('  joints:',1)[1].split('  independentKeyboard:',1)[0]
            ids=[int(n) for n in re.findall(r'fileID: (-?\d+)',joints)]
            self.assertEqual(len(ids),15)
            expected=[f+j+'_'+side for f in ['Index','Middle','Ring','Pinky','Thumb'] for j in ['A','B','C']]
            for fid,name in zip(ids,expected):
                game=int(re.search(r'm_GameObject: \{fileID: (-?\d+)',records[fid][1])[1])
                self.assertIn('  m_Name: '+name+'\n',records[game][1])
            self.assertIn('curlAxis: {x: 0, y: 0, z: 1}',body)
            self.assertIn('curlSign: '+('1' if side=='L' else '-1')+'\n',body)
    def test_project_profile_remains_discoverable_without_vendor_edits(self):
        profile=(ROOT/'Assets/Resources/VG_ControllerProfiles/VG_CP_Rehab.Input.asset').read_text(encoding='utf-8-sig')
        self.assertIn('m_controllerClasses: RehabVGController, Assembly-CSharp',profile)
        self.assertIn('m_sensor: 16',profile)
        self.assertIn('m_fingerControlType: 1',profile)
    def test_sdk_adapter_validates_before_writing_any_bone(self):
        code=(APT/'RehabVGController.cs').read_text(encoding='utf-8')
        self.assertIn('if(!BindingsReady)return false;',code)
        self.assertIn('HandRigRules.TryRotation(source.rotation',code)
        self.assertIn('HandRigRules.Finite(source.position)',code)
        self.assertLess(code.index('poses[i]=Matrix4x4.TRS'),code.index('SetPose(i,poses[i])'))
        self.assertIn('outputs[i].name!=expected',code)
        self.assertIn('ambiguous.Contains(expected)',code)
    def test_ready_checks_return_codes_and_each_contact_joint(self):
        code=(APT/'RehabVirtualGrasp.cs').read_text(encoding='utf-8')
        self.assertIn('!ReadContacts(id,false,rightContacts)||!ReadContacts(id,true,leftContacts)',code)
        self.assertIn('HandRigRules.SdkFinger(i/3),i%3',code)
        self.assertIn('!=VG_ReturnCode.SUCCESS||bones[i]==null',code)
        self.assertIn('if(bones[i]==bones[j])return false;',code)
        self.assertIn('if(!RehabVGController.HasBindings(id))',code)
    def test_contacts_cached_and_unavailable_are_explicit(self):
        code=(APT/'RehabVirtualGrasp.cs').read_text(encoding='utf-8')
        binding=code.split('public void BindContacts',1)[1].split('public void ShowLeft',1)[0]
        self.assertNotIn('new Transform',binding)
        self.assertIn('Ready?(left?leftContacts:rightContacts):unavailableContacts',binding)
    def test_one_selected_mesh_and_initialization_guard(self):
        code=(APT/'RehabVirtualGrasp.cs').read_text(encoding='utf-8')
        self.assertIn('outputMesh.sharedMesh=left?leftOnlyMesh:rightOnlyMesh',code)
        self.assertIn('if(!initialized)return;',code)
        self.assertIn('base.FixedUpdate()',code)
        self.assertIn('nextRegistration=Time.realtimeSinceStartupAsDouble+.5',code)
    def test_invalid_input_never_uses_measured_force_as_joint_truth(self):
        code=(APT/'FingerPoseDriver.cs').read_text(encoding='utf-8')
        self.assertIn('HandRigRules.Unit(ManualJoint[n])',code)
        self.assertIn('HandRigRules.DemoLimit(finger,joint)',code)
        self.assertIn('TrackingUsed = RigReady && dt>0',code)
        self.assertNotIn('LiveForceReceiver',code)
    def test_test_doubles_stay_outside_runtime_assets(self):
        self.assertFalse(any((ROOT/'Assets').rglob('*SdkShim*')))
        self.assertTrue((ROOT/'Tools/Offline/HandRigSdkShim.cs').exists())
    def test_existing_finger_keyboard_contract_is_preserved(self):
        app=(APT/'ApartmentExperience.cs').read_text(encoding='utf-8')
        self.assertIn('ActiveHand.AdjustFinger(ActiveHand.SelectedFinger,bend*Time.deltaTime)',app)
        self.assertIn('Input.GetKeyDown(KeyCode.J)',app)
        self.assertIn('rightHand.ResetFingers();leftHand.ResetFingers()',app)

if __name__=='__main__':unittest.main()
