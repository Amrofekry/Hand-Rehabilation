import importlib.util
from pathlib import Path
import unittest


FILE = Path(__file__).resolve().parents[2] / "ai-engine/4.5-decision-policy/policy.py"
spec = importlib.util.spec_from_file_location("griprehab_policy", FILE)
policy = importlib.util.module_from_spec(spec)
import sys
sys.modules[spec.name] = policy
spec.loader.exec_module(policy)


def observation(at, signs=(), *, valid=True, stable=False, age=0.05):
    return policy.Observation(at, at - age, frozenset(signs), valid, valid, valid, stable)


class AdaptivePolicyTests(unittest.TestCase):
    def test_stable_window_cooldown_and_upper_bound(self):
        p = policy.AdaptivePolicy(policy.DemoPolicySettings(stable_window_s=2, cooldown_s=3, max_level=2))
        self.assertEqual(p.step(observation(1, stable=True)).level, 1)
        self.assertEqual(p.step(observation(3, stable=True)).level, 2)
        self.assertEqual(p.step(observation(4, stable=True)).level, 2)
        self.assertEqual(p.step(observation(10, stable=True)).level, 2)

    def test_one_sign_warns_and_resume_requires_clear_explicitly(self):
        p = policy.AdaptivePolicy(policy.DemoPolicySettings(sign_persistence_s=1))
        p.step(observation(1, [policy.Signal.FORCE]))
        self.assertEqual(p.step(observation(2, [policy.Signal.FORCE])).status, policy.SessionStatus.WARNING)
        with self.assertRaises(ValueError):
            p.resume()
        self.assertEqual(p.step(observation(3)).status, policy.SessionStatus.WARNING)
        self.assertEqual(p.resume().status, policy.SessionStatus.ACTIVE)

    def test_two_independent_signs_latch_stop(self):
        p = policy.AdaptivePolicy(policy.DemoPolicySettings(sign_persistence_s=1))
        signs = [policy.Signal.FORCE, policy.Signal.KINEMATIC]
        p.step(observation(1, signs))
        self.assertEqual(p.step(observation(2, signs)).status, policy.SessionStatus.STOPPED)
        self.assertEqual(p.step(observation(10)).status, policy.SessionStatus.STOPPED)
        with self.assertRaises(ValueError):
            p.resume()

    def test_stale_or_missing_input_never_increases(self):
        p = policy.AdaptivePolicy(policy.DemoPolicySettings(stable_window_s=1))
        p.step(observation(1, stable=True))
        self.assertEqual(p.step(observation(2, stable=True, age=0.5)).level, 1)
        self.assertEqual(p.step(observation(3, stable=True, valid=False)).level, 1)
        self.assertEqual(p.step(observation(4, stable=True)).level, 1)

    def test_duplicate_category_cannot_count_as_two(self):
        p = policy.AdaptivePolicy(policy.DemoPolicySettings(sign_persistence_s=1))
        p.step(observation(1, [policy.Signal.FORCE, policy.Signal.FORCE]))
        self.assertEqual(p.step(observation(2, [policy.Signal.FORCE])).status, policy.SessionStatus.WARNING)

    def test_nonmonotonic_or_future_capture_rejected(self):
        p = policy.AdaptivePolicy()
        p.step(observation(1))
        with self.assertRaises(ValueError):
            p.step(observation(1))
        with self.assertRaises(ValueError):
            p.step(observation(2, age=-1))


if __name__ == "__main__":
    unittest.main()
