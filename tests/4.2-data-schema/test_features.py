import importlib.util
from pathlib import Path
import sys
import unittest


FILE = Path(__file__).resolve().parents[2] / "ai-engine/4.2-data-collection/features.py"
spec = importlib.util.spec_from_file_location("griprehab_features", FILE)
features = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = features
spec.loader.exec_module(features)


def point(t, *, session="synthetic-session", source="synthetic_replay", shape=1,
          revision=1, age=0.05, force=12.0, closure=0.5, rom=25.0, hand=True):
    return features.TelemetryPoint(session, t, t - age, source, source != "hardware",
                                   shape, revision, force, closure, rom, hand)


class FeatureTests(unittest.TestCase):
    def test_valid_window_keeps_units_and_synthetic_label(self):
        builder = features.FeatureWindowBuilder(duration_s=1)
        for t in (1, 1.2, 1.4, 1.6, 2.0):
            result = builder.add(point(t))
        self.assertTrue(result.valid)
        self.assertEqual(result.force_mean_n, 12)
        self.assertTrue(result.synthetic)
        self.assertEqual(result.rom_span_deg, 0)

    def test_stale_and_missing_are_not_zero_metrics(self):
        for bad in (point(2.0, age=0.5), point(2.0, force=None)):
            builder = features.FeatureWindowBuilder(duration_s=1)
            for t in (1, 1.2, 1.4, 1.6):
                builder.add(point(t))
            result = builder.add(bad)
            self.assertFalse(result.valid)
            self.assertIsNone(result.force_mean_n)

    def test_shape_or_calibration_change_resets_window(self):
        builder = features.FeatureWindowBuilder(duration_s=1)
        for t in (1, 1.2, 1.4, 1.6, 2.0):
            builder.add(point(t))
        self.assertIsNone(builder.add(point(2.1, revision=2)))
        self.assertIsNone(builder.add(point(2.2, revision=2, shape=2)))

    def test_gap_and_identity_validation(self):
        builder = features.FeatureWindowBuilder(duration_s=1)
        builder.add(point(1))
        builder.add(point(1.2))
        result = builder.add(point(2))
        self.assertFalse(result.valid)
        self.assertEqual(result.reason, "sensor gap")
        with self.assertRaises(ValueError):
            builder.add(point(2))


if __name__ == "__main__":
    unittest.main()
