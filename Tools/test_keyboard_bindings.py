"""Static integration contracts, not a replacement for Unity Input/EventSystem QA."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
APARTMENT = ROOT / 'Assets/Rehab/3.4.1-force-object-interaction/Apartment'


class KeyboardBindingTests(unittest.TestCase):
    def test_submit_keeps_enter_and_excludes_gameplay_keys(self):
        text = (ROOT / 'ProjectSettings/InputManager.asset').read_text(encoding='utf-8-sig')
        axes = re.findall(r'(?ms)^\s*- serializedVersion: 3\s*\n\s*m_Name: Submit\s*\n.*?(?=^\s*- serializedVersion: 3|\Z)', text)
        self.assertTrue(axes, 'Menu Submit mapping is required')
        # Horizontal whitespace only: an empty binding must not consume the
        # following positiveButton line and hide its actual Enter/Space value.
        bindings = re.findall(r'(?m)^[ \t]*(?:positiveButton|altPositiveButton|negativeButton|altNegativeButton):[ \t]*([^\r\n]*)', '\n'.join(axes))
        self.assertTrue({'enter', 'return'} & {b.strip() for b in bindings})
        self.assertFalse({'space', 'e', 'g', 'q', 'b', 'r', 'f', 'c', 'tab'} & {b.strip().lower() for b in bindings})

    def test_pause_keys_have_one_owner_in_apartment(self):
        choices = (APARTMENT / 'ApartmentChoices.cs').read_text(encoding='utf-8-sig')
        player = (APARTMENT / 'ApartmentPlayer.cs').read_text(encoding='utf-8-sig')
        self.assertNotIn('Input.GetKeyDown(KeyCode.Tab)', choices)
        self.assertIn('RehabKeyboardGate.TogglePause(', player)
        self.assertIn('Input.GetKeyDown(KeyCode.Tab)', player)
        self.assertIn('Input.GetKeyDown(KeyCode.Escape)', player)
        self.assertIn('[DefaultExecutionOrder(-80)]', player)

    def test_shortcuts_are_inside_context_guard(self):
        source = (APARTMENT / 'ApartmentExperience.cs').read_text(encoding='utf-8-sig')
        start = source.index('if(gameplayInput){')
        depth = 0
        for i in range(source.index('{', start), len(source)):
            if source[i] == '{': depth += 1
            elif source[i] == '}':
                depth -= 1
                if depth == 0:
                    block = source[start:i+1]
                    break
        for key in ('Q', 'R', 'F', 'E', 'G', 'C'):
            self.assertIn(f'Input.GetKeyDown(KeyCode.{key})', block)
        self.assertIn('if(gameplayInput&&Input.GetKeyDown(KeyCode.B))', source)
        self.assertIn('UpdateDemoHandMovement()', block)

    def test_player_context_blocks_ui_focus_and_disabled_component(self):
        source = (APARTMENT / 'ApartmentPlayer.cs').read_text(encoding='utf-8-sig')
        for condition in ('!Paused', 'InputFocused', '!_shell.BlocksGameplay', '!RehabAppShell.TextEntryHasFocus()', 'isActiveAndEnabled||_acceptanceInput'):
            self.assertIn(condition, source)
        self.assertIn('if(!GameplayInputAllowed)return;', source)
        self.assertIn('OnApplicationFocus(bool focused)', source)
        self.assertIn('if(!focused)ResetGameplayInput()', source)
        self.assertIn('if(Paused!=paused)_keyboard.Block(Time.frameCount)', source)

    def test_demo_force_uses_shared_hold_gate_and_both_shift_keys(self):
        source = (APARTMENT / 'ApartmentExperience.cs').read_text(encoding='utf-8-sig')
        self.assertIn('RehabKeyboardGate.DemoForce(true,gameplayInput,player.DemoSqueezing', source)
        self.assertIn('KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)', source)
        self.assertIn('KeyCode.KeypadPlus', source)
        self.assertIn('KeyCode.KeypadMinus', source)
        self.assertIn('player?.ResetGameplayInput()', source)

    def test_demo_pose_does_not_use_lingering_camera_frames(self):
        source = (APARTMENT / 'ApartmentExperience.cs').read_text(encoding='utf-8-sig')
        self.assertIn('var rightTracked=CameraMode?cameraTracked&&!LeftSelected:Hardware&&', source)
        self.assertIn('var leftTracked=CameraMode?cameraTracked&&LeftSelected:Hardware&&', source)

    def test_keyboard_pickup_checks_pause_before_releasing(self):
        source = (APARTMENT / 'ApartmentExperience.cs').read_text(encoding='utf-8-sig')
        action = source.split('public bool TryKeyboardAction(){', 1)[1].split('public bool TryGrab(', 1)[0]
        self.assertLess(action.index('if(player.Paused)return false;'), action.index('if(Held!=null)'))
        self.assertIn('if(CameraMode)SetHardware(false);', action)

    def test_g_preserves_camera_mode_and_requires_pose_geometry(self):
        source = (APARTMENT / 'ApartmentExperience.cs').read_text(encoding='utf-8-sig')
        action = source.split('public bool TryCameraSummonSelectedObject(){', 1)[1].split('public bool TryKeyboardAction(){', 1)[0]
        self.assertIn('if(!CameraMode||player.Paused', action)
        self.assertIn('!HandKinematics.HasPoseGeometry(frame)', action)
        self.assertNotIn('SetHardware(', action)

    def test_menu_navigation_is_cleared_during_gameplay(self):
        source = (ROOT / 'Assets/Rehab/3.4-game-modules/RehabAppShell.cs').read_text(encoding='utf-8-sig')
        self.assertIn('events.sendNavigationEvents = navigation;', source)
        self.assertIn('events.SetSelectedGameObject(null);', source)
        self.assertIn('Application.isFocused && !TextEntryHasFocus()', source)
        self.assertIn('UnityEngine.UI.InputField', source)
        self.assertIn('TMPro.TMP_InputField', source)


if __name__ == '__main__':
    unittest.main()
