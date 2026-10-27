"""Unit tests for the hybrid tracker core (no camera, GPU or model): python -m unittest Tools.Vision.hybrid.test_core"""
import unittest

import numpy as np

from Tools.Vision.hybrid.core import (Estimate, HandTracker, OneEuro, bone_length_cv, box_of, fuse, hand_scale, jitter,
                                      reject_jumps)


def hand(cx=320.0, cy=240.0, s=100.0):
    """A plausible open right hand, 21 joints, size about s pixels."""
    base = np.array([[0, 0], [-.25, -.15], [-.4, -.3], [-.5, -.45], [-.6, -.6],
                     [-.2, -.6], [-.22, -.85], [-.23, -1.0], [-.24, -1.12],
                     [0, -.65], [0, -.92], [0, -1.08], [0, -1.2],
                     [.18, -.6], [.2, -.85], [.21, -1.0], [.22, -1.1],
                     [.33, -.5], [.38, -.7], [.4, -.82], [.42, -.92]])
    return base * s + [cx, cy]


class OneEuroTests(unittest.TestCase):
    def test_still_noise_is_reduced(self):
        rng = np.random.default_rng(1); f = OneEuro(); truth = hand(); raw, out = [], []
        for i in range(120):
            x = truth + rng.normal(0, 1.5, truth.shape)
            raw.append(x); out.append(f(x, i / 30, hand_scale(truth)))
        self.assertLess(np.std(np.stack(out[30:]) - truth), 0.5 * np.std(np.stack(raw[30:]) - truth))

    def test_fast_motion_has_little_lag(self):
        f = OneEuro(); lag = []
        for i in range(60):
            x = hand(cx=100 + 20 * i)  # 600 px/s
            y = f(x, i / 30, hand_scale(x)); lag.append(np.abs(y - x).max())
        self.assertLess(max(lag[20:]), 25)  # under a quarter of the hand size behind at 6 hand sizes per second

    def test_bad_time_resets(self):
        f = OneEuro(); f(hand(), 1.0); y = f(hand(cx=400), 0.5)
        self.assertTrue(np.allclose(y, hand(cx=400)))


class FusionTests(unittest.TestCase):
    def test_agreeing_models_average_by_confidence(self):
        a = Estimate(hand(), np.full(21, .9)); b = Estimate(hand(cx=322), np.full(21, .3))
        xy, conf, z, overruled = fuse([a, b])
        self.assertEqual(overruled, 0); self.assertTrue(np.allclose(xy[:, 0], hand()[:, 0] + 0.5))

    def test_disagreeing_joint_takes_the_confident_model(self):
        a = hand(); b = hand(); b[8] += [60, 0]
        ca = np.full(21, .9); cb = np.full(21, .5)
        xy, conf, z, overruled = fuse([Estimate(a, ca), Estimate(b, cb)])
        self.assertEqual(overruled, 1); self.assertTrue(np.allclose(xy[8], a[8])); self.assertLess(conf[8], .9)

    def test_depth_comes_from_the_model_that_has_it(self):
        z = np.linspace(0, -.1, 21)
        xy, conf, out_z, _ = fuse([Estimate(hand(), np.full(21, .9)), Estimate(hand(), np.full(21, .9), z)])
        self.assertTrue(np.allclose(out_z, z))

    def test_no_estimate_fails(self):
        with self.assertRaises(ValueError):
            fuse([])


class OutlierTests(unittest.TestCase):
    def test_single_joint_leap_is_held(self):
        prev = hand(); cur = hand(); cur[12] += [400, 0]
        out, bad = reject_jumps(prev, cur, 1 / 30)
        self.assertEqual(bad, 1); self.assertLess(np.linalg.norm(out[12] - prev[12]), 0.5 * hand_scale(prev))

    def test_whole_hand_move_is_kept(self):
        prev = hand(); cur = hand(cx=420)
        out, bad = reject_jumps(prev, cur, 1 / 30)
        self.assertEqual(bad, 0); self.assertTrue(np.allclose(out, cur))


class TrackerTests(unittest.TestCase):
    def test_sides_stay_with_their_tracks(self):
        t = HandTracker()
        left, right = box_of(hand(cx=450)), box_of(hand(cx=180))
        self.assertEqual(t.assign([right, left], ["Right", "Left"], [.99, .99], 0.0, 640), ["Right", "Left"])
        # MediaPipe briefly mislabels both (a common flicker); the tracks keep their sides.
        moved = [box_of(hand(cx=185)), box_of(hand(cx=445))]
        self.assertEqual(t.assign(moved, ["Left", "Right"], [.7, .7], 1 / 30, 640), ["Right", "Left"])
        self.assertGreaterEqual(t.swaps_prevented, 2)

    def test_lost_track_expires(self):
        t = HandTracker(lost_after_s=.5); t.assign([box_of(hand())], ["Right"], [.99], 0.0, 640)
        t.assign([], [], [], 1.0, 640); self.assertEqual(t.tracks, {})

    def test_one_box_per_side(self):
        t = HandTracker(); sides = t.assign([box_of(hand(cx=100)), box_of(hand(cx=500))], ["Right", "Right"], [.99, .99], 0, 640)
        self.assertEqual(sorted(s for s in sides if s), ["Left", "Right"])


class MetricTests(unittest.TestCase):
    def test_steady_hand_scores_low(self):
        seq = [hand() for _ in range(10)]
        self.assertAlmostEqual(jitter(seq), 0); self.assertAlmostEqual(bone_length_cv(seq), 0)

    def test_shaky_hand_scores_higher(self):
        rng = np.random.default_rng(2); seq = [hand() + rng.normal(0, 3, (21, 2)) for _ in range(20)]
        self.assertGreater(jitter(seq), 0.01); self.assertGreater(bone_length_cv(seq), 0.01)


if __name__ == "__main__":
    unittest.main()
