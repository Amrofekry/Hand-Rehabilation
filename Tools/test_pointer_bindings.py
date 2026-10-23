"""Static integration contracts; never presented as native uGUI verification."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
class PointerBindings(unittest.TestCase):
    def setUp(self):
        self.pointer=(ROOT/'Assets/Rehab/3.4-game-modules/RehabDwellPointer.cs').read_text(encoding='utf-8')
    def test_runtime_uses_tested_gesture_and_hand_selection(self):
        self.assertIn('RehabPointerPolicy.IsPointing(frame)', self.pointer)
        self.assertIn('RehabPointerPolicy.ChooseFrame(right, rightValid, left, leftValid, preferredLeft)', self.pointer)
    def test_late_update_order_and_single_component(self):
        self.assertIn('[DefaultExecutionOrder(600)]', self.pointer)
        self.assertIn('[DisallowMultipleComponent]', self.pointer)
        shell=(ROOT/'Assets/Rehab/3.4-game-modules/RehabAppShell.cs').read_text(encoding='utf-8')
        self.assertIn('GetComponent<RehabDwellPointer>() ?? gameObject.AddComponent<RehabDwellPointer>()', shell)
    def test_unfocused_app_cannot_dwell_or_launch_camera(self):
        guard=self.pointer.index('if (!Application.isFocused) { Hide(); status.enabled = false; return; }')
        self.assertLess(guard,self.pointer.index('cameraLauncher.EnsureStarted()'))
        self.assertLess(guard,self.pointer.index('dwell.Step('))
    def test_hand_change_clears_cursor_smoothing_and_progress(self):
        change=self.pointer.split('if (lastLeft.HasValue && lastLeft.Value != left)',1)[1].split('lastLeft = left;',1)[0]
        for token in ['dwell.Reset()', 'rearm.Reset()', 'hasPosition = false', 'SetTarget(null)']:
            self.assertIn(token,change)
    def test_tracking_loss_hides_and_resets(self):
        missing=self.pointer.split('if (frame == null)',1)[1].split('bool pointing',1)[0]
        self.assertIn('Hide(); return;',missing)
        hide=self.pointer.split('void Hide()',1)[1].split('void SetTarget',1)[0]
        for token in ['dwell.Reset()', 'rearm.Reset()', 'lastLeft = null', 'hasPosition = false']:
            self.assertIn(token,hide)
    def test_settings_and_dt_use_finite_bounded_policy(self):
        for token in ['RehabPointerPolicy.SafeDt(Time.unscaledDeltaTime)', 'RehabPointerPolicy.Duration(dwellSeconds)',
                      'RehabPointerPolicy.Grace(gestureGraceSeconds)', 'RehabPointerPolicy.Smooth(']:
            self.assertIn(token,self.pointer)
    def test_frontmost_hit_blocks_controls_below_it(self):
        self.assertIn('var hit = hits[0].gameObject;',self.pointer)
        self.assertIn('blockedByControl = next != null;',self.pointer)
        self.assertIn('next == null && !blockedByControl',self.pointer)
        self.assertIn('!next.IsActive() || !next.IsInteractable()',self.pointer)
        self.assertIn('SameSurface(target, hits)',self.pointer)
    def test_empty_raycast_cannot_retain_an_old_button(self):
        self.assertIn('if (results.Count == 0) return false;',self.pointer)
        self.assertNotIn('if (results.Count == 0) return true;',self.pointer)
    def test_uses_left_pointer_click_handler_once(self):
        self.assertIn('pointerEvent.button = PointerEventData.InputButton.Left;',self.pointer)
        self.assertEqual(self.pointer.count('ExecuteEvents.pointerClickHandler'),1)
        self.assertNotIn('button.onClick.Invoke()',self.pointer)
        self.assertNotIn('toggle.isOn =',self.pointer)
    def test_callbacks_cannot_bypass_deliberate_rearm(self):
        self.assertIn('pointing && deliberate',self.pointer)
        self.assertLess(self.pointer.index('rearm.Activated(position)'),self.pointer.index('ExecuteEvents.pointerClickHandler'))
    def test_new_event_system_and_resolution_reset(self):
        for token in ['eventSystem != EventSystem.current', 'new PointerEventData(eventSystem)',
                      'screenWidth != Screen.width || screenHeight != Screen.height', 'pointerEvent.Reset()']:
            self.assertIn(token,self.pointer)
    def test_reload_rebuilds_missing_event_even_with_same_event_system(self):
        self.assertIn('eventSystem != EventSystem.current || pointerEvent == null',self.pointer)
        self.assertIn('[NonSerialized] PointerEventData pointerEvent',self.pointer)
        self.assertIn('[NonSerialized] EventSystem eventSystem',self.pointer)
    def test_overlay_cannot_block_raycast(self):
        self.assertNotIn('typeof(GraphicRaycaster)',self.pointer)
        self.assertEqual(self.pointer.count('raycastTarget = false'),5)
    def test_keyboard_and_force_are_not_used_to_select(self):
        for token in ['Input.GetKey', 'GetMouseButton', 'LiveForceReceiver', 'ForceValid']:
            self.assertNotIn(token,self.pointer)
    def test_accessibility_adapter_reads_actual_pointer_and_bounds_values(self):
        source=(ROOT/'Assets/UIBridge/GripDwellSettingsAdapter.cs').read_text(encoding='utf-8')
        self.assertEqual(source.count('shell.GetComponent<RehabDwellPointer>()'),2)
        self.assertIn('Mathf.Clamp(RehabPointerPolicy.Duration(value), 2, 8)',source)
    def test_supported_controls_share_click_path(self):
        self.assertIn('next is TMPro.TMP_Dropdown',self.pointer)
        self.assertIn('next is TMPro.TMP_InputField',self.pointer)
        self.assertIn('next is Dropdown || next is Toggle',self.pointer)
    def test_disabled_shell_cannot_drive_ui(self):
        self.assertIn('!shell.isActiveAndEnabled',self.pointer)
        self.assertIn('if (pointing && trace != null',self.pointer)
    def test_disable_releases_only_menu_owned_camera(self):
        disable=self.pointer.split('void OnDisable()',1)[1].split('void OnDestroy()',1)[0]
        self.assertIn('startedForUi && cameraLauncher != null',disable)
        self.assertIn('!shell.experience.CameraMode',disable)
        self.assertIn('cameraLauncher.StopWorker()',disable)
        self.assertIn('startedForUi = false',disable)

if __name__=='__main__': unittest.main()

class WholeHandBindings(unittest.TestCase):
    def test_no_index_gesture_gate_and_palm_cursor(self):
        pointer=(ROOT/'Assets/Rehab/3.4-game-modules/RehabDwellPointer.cs').read_text()
        policy=(ROOT/'Assets/Rehab/3.4-game-modules/RehabPointerPolicy.cs').read_text()
        self.assertIn('RehabPointerPolicy.PalmScreenPoint(frame, Screen.width, Screen.height)',pointer)
        self.assertNotIn('frame.landmarks[8]',pointer)
        self.assertNotIn('curled >=',policy)
        self.assertIn('HasTrackedHand(LandmarkFrame frame) => HandKinematics.IsUsable(frame)',policy)
