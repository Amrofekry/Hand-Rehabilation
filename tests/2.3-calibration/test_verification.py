import importlib.util
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('verification',ROOT/'firmware/2.3-load-cell-calibration/verify_calibration.py')
module=importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
class Verification(unittest.TestCase):
    def rows(self): return [dict(reference_n=str(n),measured_n=str(n+.1),direction=d) for d in ('loading','unloading') for n in (0,10,20)]
    def test_pass(self): self.assertTrue(module.evaluate(self.rows())['passed'])
    def test_error_fails(self):
        rows=self.rows(); rows[1]['measured_n']='15'; self.assertFalse(module.evaluate(rows)['passed'])
    def test_nonfinite(self):
        rows=self.rows(); rows[0]['measured_n']='nan'
        with self.assertRaises(ValueError): module.evaluate(rows)
    def test_coverage(self):
        with self.assertRaises(ValueError): module.evaluate(self.rows()[:3])
    def test_tolerance(self):
        with self.assertRaises(ValueError): module.evaluate(self.rows(),absolute=-1)
if __name__=='__main__': unittest.main()
