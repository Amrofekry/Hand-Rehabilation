import unittest
from types import SimpleNamespace

from mediapipe_bridge import make_payload
from replay_vision import hand_points


class VisionBridgeContractTest(unittest.TestCase):
    def test_two_hands_have_distinct_labels_and_21_finite_points(self):
        for left, center in ((True, 0.34), (False, 0.66)):
            points = hand_points(center, 0.5, left)
            self.assertEqual(len(points), 21)
            self.assertTrue(all(0 <= p["x"] <= 1 and 0 <= p["y"] <= 1 for p in points))
            category = SimpleNamespace(category_name="Left" if left else "Right", score=0.96)
            payload = make_payload(
                [SimpleNamespace(**p) for p in points], category, 12.5,
                17 if left else 18, "test", False
            )
            self.assertEqual(payload["handedness"], category.category_name)
            self.assertEqual(payload["schemaVersion"], 1)
            self.assertEqual(payload["ageMs"], 0)
            self.assertEqual(len(payload["landmarks"]), 21)

    def test_label_swap_and_invalid_label(self):
        point = SimpleNamespace(x=0.5, y=0.5, z=0.0)
        hand = [point] * 21
        right = SimpleNamespace(category_name="Right", score=0.8)
        self.assertEqual(make_payload(hand, right, 1.0, 1, "test", True)["handedness"], "Left")
        with self.assertRaises(ValueError):
            make_payload(hand, SimpleNamespace(category_name="Unknown", score=0.8),
                         1.0, 1, "test", False)


if __name__ == "__main__":
    unittest.main()
