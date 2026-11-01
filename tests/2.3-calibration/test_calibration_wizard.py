import importlib.util
import sys
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'firmware/2.3-load-cell-calibration'))
spec=importlib.util.spec_from_file_location('wizard',ROOT/'firmware/2.3-load-cell-calibration/calibration_wizard.py')
w=importlib.util.module_from_spec(spec); spec.loader.exec_module(w)
G=w.G
class Wizard(unittest.TestCase):
    def go(self,dev,cal=2*G,loads=(0,G,3*G)):
        def load(n): dev.load=n
        return w.run(dev,1,cal,list(loads),lambda t:None,load)
    def test_full_sequence_saves_and_verifies(self):
        r=self.go(w.SimulatedConsole())
        self.assertEqual(r['steps'],['shape','tare','known load','start','verify','save'])
        self.assertTrue(r['verification']['passed']); self.assertTrue(r['saved']); self.assertEqual(r['calibration_id'],1)
        self.assertAlmostEqual(float(r['counts_per_N']),420,delta=2)
    def test_verification_uses_both_directions(self):
        rows=self.go(w.SimulatedConsole())['verification']['rows']
        self.assertEqual({x['direction'] for x in rows},{'loading','unloading'})
    def test_wrong_reference_is_not_saved(self):
        dev=w.SimulatedConsole()
        # the real verification loads are 20 % above what the operator declared
        r=w.run(dev,1,2*G,[0,G,3*G],lambda t:None,lambda n: setattr(dev,'load',n*1.2 if n and n!=2*G else n))
        self.assertFalse(r['verification']['passed']); self.assertFalse(r['saved'])
    def test_cal_before_tare_is_rejected(self):
        dev=w.SimulatedConsole(); w.command(dev,'shape 1',r'OK shape')
        with self.assertRaises(RuntimeError): w.command(dev,'cal 9.8',r'OK collecting known load')
    def test_cli_simulate_exit_zero(self):
        self.assertEqual(w.main(['--simulate']),0)
if __name__=='__main__': unittest.main()
