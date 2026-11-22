"""The hand check report's orientation maths (SYNTHETIC hands): a turned hand reads as that turn."""
import math
import unittest

import hand_check_report as h


def hand(rot):
    # An open right hand in the patient's frame (x right, y up, z toward the camera), fingers up, middle knuckle above the wrist.
    pts = [(0, 0, 0)] + [(-.03 - i * .01, .03 + i * .02, 0) for i in range(4)]
    for x in (-.019, 0, .019, .037):
        pts += [(x, .09 + j * .03, 0) for j in range(4)]
    out = []
    for p in pts:
        q = tuple(sum(rot[i][k] * p[k] for k in range(3)) for i in range(3))
        out.append((q[0], -q[1], -q[2]))  # patient frame -> world landmark convention
    return out


def rot_y(deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return ((c, 0, s), (0, 1, 0), (-s, 0, c))


class HandCheckReportTests(unittest.TestCase):
    def test_turn_angle(self):
        ref = h.palm_basis(hand(((1, 0, 0), (0, 1, 0), (0, 0, 1))))
        for deg in (20, 60, 120):
            b = h.palm_basis(hand(rot_y(deg)))
            self.assertAlmostEqual(h.angle(h.mul(b, h.tr(ref))), deg, delta=.5)

    def test_quaternion_identity(self):
        self.assertAlmostEqual(h.angle(h.quat_matrix(0, 0, 0, 1)), 0, delta=1e-6)
        s = math.sin(math.radians(45))  # 90 degrees about y
        self.assertAlmostEqual(h.angle(h.quat_matrix(0, s, 0, s)), 90, delta=1e-4)


if __name__ == '__main__':
    unittest.main()
