import unittest
import json
from types import SimpleNamespace

if __package__:
    from .mediapipe_bridge import HandednessFilter, make_payload
    from .replay_vision import hand_points
else:
    from mediapipe_bridge import HandednessFilter, make_payload
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

    def test_handedness_filter_ignores_short_flips_of_one_hand(self):
        f = HandednessFilter(frames=6, forget=0.5)
        labels = ["Right"] * 10 + ["Left"] * 3 + ["Right"] * 5
        out = [f.update(l, i / 30) for i, l in enumerate(labels)]
        self.assertEqual(set(out), {"Right"})
        # A label that holds for six frames is taken.
        out = [f.update("Left", (20 + i) / 30) for i in range(6)]
        self.assertEqual(out[-1], "Left")
        self.assertEqual(out[0], "Right")

    def test_handedness_filter_passes_two_hands_and_a_new_hand(self):
        f = HandednessFilter()
        f.update("Right", 0.0)
        self.assertEqual(f.update("Left", 0.03, hands=2), "Left")
        f.update("Right", 0.06)
        self.assertEqual(f.update("Left", 2.0), "Left")  # the hand was gone: a new hand keeps its own label

    def test_label_swap_and_invalid_label(self):
        point = SimpleNamespace(x=0.5, y=0.5, z=0.0)
        hand = [point] * 21
        right = SimpleNamespace(category_name="Right", score=0.8)
        self.assertEqual(make_payload(hand, right, 1.0, 1, "test", True)["handedness"], "Left")
        with self.assertRaises(ValueError):
            make_payload(hand, SimpleNamespace(category_name="Unknown", score=0.8),
                         1.0, 1, "test", False)

    def test_world_pose_and_image_dimensions_are_optional_v1_extensions(self):
        image = [SimpleNamespace(x=.4, y=.5, z=-.02)] * 21
        world = [SimpleNamespace(x=.01, y=-.02, z=.03)] * 21
        category = SimpleNamespace(category_name="Left", score=.95)
        payload = make_payload(image, category, 2.0, 3, "test", False, 12.5,
                               world_hand=world, image_width=640, image_height=480)
        self.assertEqual(payload['schemaVersion'], 1)
        self.assertEqual((payload['imageWidth'], payload['imageHeight']), (640, 480))
        self.assertEqual(payload['worldLandmarks'][0], {'x':.01, 'y':-.02, 'z':.03})
        self.assertLess(len(json.dumps(payload).encode('utf-8')), 8192)
        legacy = make_payload(image, category, 2.0, 3, "test", False)
        self.assertNotIn('worldLandmarks', legacy)
        self.assertNotIn('imageWidth', legacy)

    def test_invalid_extensions_cannot_poison_unity_pose(self):
        points = [SimpleNamespace(x=.01, y=.02, z=.03)] * 21
        category = SimpleNamespace(category_name="Right", score=.95)
        for kwargs in ({'world_hand':points[:20]},
                       {'world_hand':[SimpleNamespace(x=float('nan'),y=0,z=0)]*21},
                       {'image_width':640}, {'image_width':-1,'image_height':480}):
            with self.subTest(kwargs=list(kwargs)):
                with self.assertRaises(ValueError):
                    make_payload(points, category, 2.0, 3, 'test', False, **kwargs)
        with self.assertRaises(ValueError):
            make_payload(points, category, 2.0, 3, 'test', False, float('nan'))


if __name__ == "__main__":
    unittest.main()
