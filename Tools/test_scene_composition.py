"""Saved-workspace contracts. Native startup/save/build have separate evidence."""
import re
import unittest
from pathlib import Path
from Tools.scene_layout import ROOT, MAIN, MANIFEST, paths, blocks, links, composed_blocks

class SceneCompositionTests(unittest.TestCase):
    def test_six_focused_content_scenes_and_small_bootstrap(self):
        self.assertEqual([p.stem for p in paths()], ['3.1-Apartment', '3.1-Lighting', '3.2-PlayerAndSensors',
            '3.3-VirtualHands', '3.4.1-Interaction', '3.4-Interface'])
        self.assertLess(MAIN.stat().st_size, 10000)
        self.assertEqual(MAIN.read_text().count('GripRehab.Composition.SceneBootstrap'), 1)
        self.assertNotIn('GripRehab.ApartmentExperience', MAIN.read_text())
        self.assertTrue((ROOT/'Assets/Scenes/Archive/GripRehab-BeforeSceneSplit.unity').exists())

    def test_staged_roots_are_inactive_on_disk(self):
        manifest = MANIFEST.read_text()
        for path in paths():
            record = blocks(path)
            containers = [b for b in record.values() if re.search(r'^  m_Name: .+ / content$', b,re.M)]
            self.assertEqual(len(containers), 1, path.name)
            self.assertIn('  m_IsActive: 0\n', containers[0])
            self.assertIn(path.relative_to(ROOT).as_posix(), manifest)

    def test_reference_manifest_and_original_wiring_resolve(self):
        self.assertEqual(len(links()), 26)
        self.assertEqual(len({(l['ownerId'],l['ownerComponent'],l['property']) for l in links()}),26)
        record = composed_blocks()
        experience = next(b for b in record.values() if 'GripRehab.ApartmentExperience' in b)
        for field in ['virtualGrasp','statusText','contextText','forceText','progressFill']:
            self.assertIsNotNone(
                re.search(r'^  '+field+r': \{fileID: [1-9]\d*\}',experience,re.M),field)

    def test_build_includes_all_parts_and_excludes_archive(self):
        saved = (ROOT/'ProjectSettings/EditorBuildSettings.asset').read_text()
        build = re.findall(r'^    path: (.+)$',saved,re.M)
        self.assertEqual(build[0], MAIN.relative_to(ROOT).as_posix())
        for path in paths(): self.assertIn(path.relative_to(ROOT).as_posix(),build)
        self.assertFalse(any('Archive/' in p for p in build))

    def test_reference_binding_precedes_activation(self):
        source = (ROOT/'Assets/Rehab/3.1-environment/Composition/SceneBootstrap.cs').read_text()
        self.assertLess(source.index('composition.Bind();'),source.index('composition.Activate(true);'))
        self.assertIn('LoadSceneMode.Additive',source)

if __name__=='__main__': unittest.main()
