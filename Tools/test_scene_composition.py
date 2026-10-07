"""Single-scene saved-wiring contracts; native reload/build evidence is separate."""
import re
import unittest
from Tools.scene_layout import ROOT, MAIN, MANIFEST, paths, composed_blocks

class SceneCompositionTests(unittest.TestCase):
    def test_one_working_scene_and_no_subfolders(self):
        folder=ROOT/'Assets/Scenes'
        self.assertEqual(sorted(p.name for p in folder.iterdir()), ['GripRehab.unity','GripRehab.unity.meta'])
        self.assertEqual(paths(),[MAIN])
        self.assertFalse(MANIFEST.exists())

    def test_gameplay_is_authored_and_has_no_additive_bootstrap(self):
        source=MAIN.read_text()
        self.assertIn('GripRehab.ApartmentExperience',source)
        self.assertNotIn('GripRehab.Composition.SceneBootstrap',source)
        self.assertNotIn('GripRehab.Composition.SceneReferenceId',source)
        self.assertNotRegex(source,re.compile(r'^  m_Name: .+ / content$',re.M))

    def test_gameplay_references_are_saved_directly(self):
        record=composed_blocks()
        experience=next(b for b in record.values() if 'GripRehab.ApartmentExperience' in b)
        for field in ['virtualGrasp','statusText','contextText','forceText','progressFill']:
            value=re.search(r'^  '+field+r': \{fileID: ([1-9]\d*)\}',experience,re.M)
            self.assertIsNotNone(value,field)
            self.assertIn(value[1],record,field)

    def test_build_starts_in_single_scene_and_excludes_retired_parts(self):
        build=re.findall(r'^    path: (.+)$',(ROOT/'ProjectSettings/EditorBuildSettings.asset').read_text(),re.M)
        self.assertEqual(build[0], 'Assets/Scenes/GripRehab.unity')
        self.assertEqual([p for p in build if p.startswith('Assets/Scenes/')],['Assets/Scenes/GripRehab.unity'])
        self.assertFalse(any('SceneBackups/' in p or '3.1-Bootstrap' in p for p in build))

    def test_scene_guid_and_recovery_copy_preserved(self):
        self.assertIn('guid: 0d609e8f6ef162e45a37a4c467a3d5bf',MAIN.with_suffix('.unity.meta').read_text())
        self.assertTrue((ROOT/'Assets/SceneBackups/GripRehab-BeforeSceneSplit.unity').exists())
        self.assertTrue((ROOT/'Docs/evidence/single-scene-2026-10-07/before-merge.zip').exists())

if __name__=='__main__': unittest.main()
