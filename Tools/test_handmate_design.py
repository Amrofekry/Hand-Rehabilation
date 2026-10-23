"""Saved UI presentation contracts; these are not native rendering tests."""
import hashlib
import json
import re
import unittest
from pathlib import Path
from Tools.scene_layout import composed_blocks, paths

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / 'Assets/Scenes/GripRehab.unity'

def blocks(path):
    text = path.read_text(encoding='utf-8-sig')
    return {re.match(r'--- !u!\d+ &(-?\d+)', block)[1]: block
            for block in re.findall(r'^--- !u!.*?(?=^--- !u!|\Z)', text, re.M | re.S)}

def field(block, name):
    found = re.search(r'^  ' + re.escape(name) + r': (.*)$', block, re.M)
    return found[1] if found else None

def rgba(text):
    return tuple(float(x) for x in re.findall(r'[rgba]: ([\d.eE+\-]+)', text))

def contrast(a, b):
    def lum(c):
        values = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055)**2.4 for v in c[:3]]
        return sum(v * w for v, w in zip(values, [.2126, .7152, .0722]))
    x, y = sorted([lum(a), lum(b)])
    return (y + .05) / (x + .05)

class HandMateDesignTests(unittest.TestCase):
    def test_source_art_is_exact_and_only_design_was_imported(self):
        manifest = json.loads((ROOT/'Docs/evidence/handmate-design-import.json').read_text())
        self.assertEqual(len(manifest['assets']), 12)
        for item in manifest['assets']:
            self.assertRegex(item['path'], r'^Assets/UI/HandMate/(Images|Fonts)/')
            self.assertEqual(hashlib.sha256((ROOT/item['path']).read_bytes()).hexdigest(), item['sha256'])
        self.assertFalse(any((ROOT/'Assets/UI/HandMate').rglob('*.cs')))

    def test_scene_logic_and_protected_assets_are_unchanged(self):
        before = json.loads((ROOT/'Docs/evidence/handmate-protected-baseline.json').read_text())
        # The immutable design-only baseline is retained. Later authorized
        # account integration has a narrow, versioned review of six source files.
        # Vendor assets, scene logic and every other protected file stay pinned.
        review = json.loads((ROOT/'Docs/evidence/journey-item2-2026-10-06/protected-source-review.json').read_text())
        changes = review['changes']
        scene_review = json.loads((ROOT/'Docs/evidence/scene-organization-2026-10-07/protected-source-review.json').read_text())['changes']
        flat_review = json.loads((ROOT/'Docs/evidence/flat-scene-folder-2026-10-07/protected-source-review.json').read_text())['changes']
        merge_review = json.loads((ROOT/'Docs/evidence/single-scene-2026-10-07/protected-source-review.json').read_text())['changes']
        self.assertEqual(set(merge_review), set(flat_review))
        self.assertEqual(set(flat_review), {'Assets/Editor/ApartmentAuthoring.cs',
            'Assets/Editor/GripUiGalleryAuthoring.cs',
            'Assets/Editor/RehabVerification.cs', 'Assets/Editor/RehabGateRunner.cs', 'Assets/Rehab/3.4-game-modules/RehabApp.cs'})
        self.assertEqual(set(scene_review), {'Assets/Editor/ApartmentAuthoring.cs', 'Assets/Editor/RehabEditorQA.cs',
            'Assets/Editor/RehabGateRunner.cs', 'Assets/Editor/RehabVerification.cs',
            'Assets/Rehab/3.4.1-force-object-interaction/Apartment/ApartmentAcceptance.cs'})
        self.assertEqual(set(changes), {
            'Assets/Editor/RehabGateRunner.cs', 'Assets/Product/ProductRepository.cs',
            'Assets/ProductIntegration/ResearchPortalHost.cs', 'Assets/ProductIntegration/ResearchSessionRuntime.cs',
            'Assets/Rehab/3.4-game-modules/RehabAppShell.cs',
            'Assets/Rehab/3.4.1-force-object-interaction/Apartment/ApartmentExperience.cs'})
        rollback = json.loads((ROOT/'Docs/evidence/rollback-2026-10-12/protected-source-review.json').read_text())['changes']
        self.assertEqual(set(rollback), {'Assets/Rehab/3.4-game-modules/RehabAppShell.cs', 'Assets/Editor/VirtualGraspAuthoring.cs.meta', 'Assets/Rehab/3.4.1-force-object-interaction/Apartment/ApartmentExperience.cs', 'Assets/Editor/ApartmentVisualPolish.cs', 'Assets/com.gleechi.unity.virtualgrasp/Samples/Tutorials/TutorialCallbacks.cs', 'Assets/com.gleechi.unity.virtualgrasp/Lib/VirtualGrasp.dll', 'Assets/Editor/ApartmentAuthoring.cs', 'Assets/Editor/ApartmentOptionsAuthoring.cs', 'Assets/Editor/ApartmentVisualDiagnostics.cs', 'Assets/Rehab/3.4.1-force-object-interaction/Apartment/RehabVirtualGrasp.cs.meta', 'Assets/Editor/ApartmentMaintenance.cs', 'Assets/Editor/VirtualGraspProjectSettings.cs.meta', 'Assets/Rehab/3.2-vision/HandTracking.cs', 'Assets/Editor/ApartmentCandleBalance.cs', 'Assets/Rehab/3.4.1-force-object-interaction/Apartment/ApartmentAcceptance.cs', 'Assets/Editor/VisionCameraAuthoring.cs', 'Assets/com.gleechi.unity.virtualgrasp/Samples/Onboarding/Scripts/SimpleImpact.cs', 'Assets/Rehab/3.4.1-force-object-interaction/Apartment/SqueezableObject.cs'} )
        ai_review=json.loads((ROOT/'Docs/evidence/ai-2026-10-22/protected-source-review.json').read_text(encoding='utf-8'))['changes']
        self.assertEqual(set(ai_review), {
            'Assets/Product/CapabilityTrial.cs','Assets/Product/CatalogValidation.cs','Assets/Product/ProductModels.cs','Assets/Product/ResearchExport.cs','Assets/Product/ResearchGuidance.cs',
            'Assets/ProductIntegration/ResearchGuidanceSettings.cs','Assets/ProductIntegration/ResearchPortalHost.cs','Assets/ProductIntegration/ResearchSessionRuntime.cs',
            'Assets/ProductIntegration/WindowsProductStore.cs','Assets/Resources/ResearchGuidance.asset','Assets/Settings/PC_Renderer.asset','Assets/Settings/PC_RPAsset.asset',
            'Assets/com.gleechi.unity.virtualgrasp/Samples/Tutorials/1_ProjectSetup/0-ContentSummary.asset',
            'Assets/com.gleechi.unity.virtualgrasp/Samples/Tutorials/2_ObjectBaking/0-ContentSummary.asset',
            'Assets/com.gleechi.unity.virtualgrasp/Samples/Tutorials/3_FingerTracking/0-ContentSummary.asset'})
        for path, expected in before.items():
            with self.subTest(path=path):
                if path in changes:
                    self.assertEqual(changes[path]['originalSha256'], expected)
                    self.assertTrue(changes[path]['reason'])
                    expected = changes[path]['currentSha256']
                if path in scene_review:
                    self.assertEqual(scene_review[path]['previousSha256'], expected)
                    self.assertTrue(scene_review[path]['reason'])
                    expected = scene_review[path]['currentSha256']
                if path in flat_review:
                    self.assertEqual(flat_review[path]['previousSha256'], expected)
                    self.assertTrue(flat_review[path]['reason'])
                    expected = flat_review[path]['currentSha256']
                if path in merge_review:
                    self.assertEqual(merge_review[path]['previousSha256'], expected)
                    self.assertTrue(merge_review[path]['reason'])
                    expected = merge_review[path]['currentSha256']
                if path in rollback:
                    self.assertEqual(rollback[path]['previousSha256'], expected)
                    self.assertTrue(rollback[path]['reason'])
                    expected = rollback[path]['currentSha256']
                if path in ai_review:
                    self.assertEqual(ai_review[path]['previousSha256'],expected)
                    self.assertTrue(ai_review[path]['reason'])
                    expected=ai_review[path]['currentSha256']
                self.assertEqual(hashlib.sha256((ROOT/path).read_bytes()).hexdigest(), expected)
        logic = json.loads((ROOT/'Docs/evidence/handmate-scene-logic-baseline.json').read_text())
        current = composed_blocks()
        for key, expected in logic.items():
            self.assertEqual(hashlib.sha256(current[key].encode()).hexdigest(), expected, key)

    def test_all_three_palettes_have_readable_foregrounds(self):
        for name in ['Dark', 'Light', 'HighContrast']:
            text = (ROOT/f'Assets/UI/Themes/{name}.asset').read_text()
            colors = {k: rgba(field(text, k)) for k in
                      ['textOnDark','textOnLight','textOnPrimary','textOnSecondary',
                       'darkSurface','surface','primary','secondary','backdropArtworkTint']}
            for fg, bg in [('textOnDark','darkSurface'),('textOnLight','surface'),
                           ('textOnPrimary','primary'),('textOnSecondary','secondary')]:
                self.assertGreaterEqual(contrast(colors[fg], colors[bg]), 4.5, (name,fg,bg))
            if name == 'Dark':
                # Maximum possible RGB under a multiplicatively tinted photo
                # is its tint. Even a white photo pixel must support white text.
                self.assertGreaterEqual(contrast(colors['textOnDark'], colors['backdropArtworkTint']), 4.5)
            for key in ['latinFont','headingFont']:
                self.assertRegex(field(text,key), r'fileID: 12800000, guid: [0-9a-f]{32}')

    def test_new_art_does_not_block_buttons_or_leave_parent_bounds(self):
        current = composed_blocks()
        for num in range(1,4):
            base = 90000000000 + num * 10
            go, rect, _, image, tag = [current[str(base+i)] for i in range(5)]
            self.assertIn(f'm_Name: HandMate module {num} illustration', go)
            self.assertEqual(field(image,'m_RaycastTarget'), '0')
            self.assertEqual(field(image,'m_PreserveAspect'), '1')
            self.assertEqual(field(tag,'excludeFromTheme'), '1')
            parent = re.search(r'fileID: (-?\d+)', field(rect,'m_Father'))[1]
            self.assertIn('  - {fileID: '+str(base+1)+'}', current[parent])
            size = [float(v) for v in re.findall(r'[xy]: ([\d.\-]+)',field(current[parent],'m_SizeDelta'))]
            self.assertGreaterEqual(size[0]/2, 43+250/2)
            self.assertGreaterEqual(size[1]/2, 138+120/2)

    def test_scene_ids_and_saved_ui_references_resolve(self):
        all_guids=set()
        for folder in [ROOT/'Assets', ROOT/'Library/PackageCache']:
            for path in folder.rglob('*.meta'):
                match=re.search(r'^guid: ([0-9a-f]{32})',path.read_text(encoding='utf-8-sig',errors='replace'),re.M)
                if match:all_guids.add(match[1])
        for path in [SCENE, *paths(), ROOT/'Assets/UI/GripUiGallery.unity',*(ROOT/'Assets/UI/Components').glob('*.prefab')]:
            text=path.read_text();ids=re.findall(r'^--- !u!\d+ &(-?\d+)',text,re.M)
            self.assertEqual(len(ids),len(set(ids)),path.name)
            for guid in re.findall(r'guid: ([0-9a-f]{32})',text):
                if guid.startswith('0000000000000000'):continue
                self.assertIn(guid,all_guids,(path.name,guid))

    def test_dropdown_arrows_and_slider_handles_remain_visible(self):
        current=composed_blocks()
        objects={key:field(b,'m_Name') for key,b in current.items() if b.startswith('--- !u!1 ')}
        found=0
        for block in current.values():
            if field(block,'m_Color') is None:continue
            go=re.search(r'fileID: (-?\d+)',field(block,'m_GameObject'))[1]
            if objects.get(go) not in ['Arrow','Handle']:continue
            self.assertGreater(contrast(rgba(field(block,'m_Color')),rgba('{r: .102, g: .133, b: .188, a: 1}')),4.5)
            found+=1
        self.assertGreaterEqual(found,6)

if __name__ == '__main__':
    unittest.main()
