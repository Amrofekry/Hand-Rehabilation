"""Reject fabrication-input mistakes independently of OpenSCAD export success."""
import copy
import importlib.util
import json
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('cad_build',ROOT/'hardware/2.1-interchangeable-grips/rev2/build.py')
cad=importlib.util.module_from_spec(spec);spec.loader.exec_module(cad)


class Layout(unittest.TestCase):
    def setUp(self):self.d=json.loads((cad.BASE/'layout.json').read_text())
    def test_nominal(self):self.assertEqual(cad.validate(self.d)['nominal_envelope_check'],'PASS')
    def test_battery_too_wide(self):
        self.d['components'][0]['size'][1]=60
        with self.assertRaises(ValueError):cad.validate(self.d)
    def test_overlapping_boards(self):
        self.d['components'][2]['min']=copy.copy(self.d['components'][1]['min'])
        with self.assertRaises(ValueError):cad.validate(self.d)
    def test_wrong_hole_spacing(self):
        self.d['cell']['active_holes_x']=[-35,-25]
        with self.assertRaises(ValueError):cad.validate(self.d)
    def test_missing_force_transfer_contact(self):
        self.d['top_plate']['z']+=1
        with self.assertRaises(ValueError):cad.validate(self.d)


    def test_spacer_lift_from_base_rejected(self):
        self.d['fixed_spacer']['min'][2]+=1
        self.d['cell']['min'][2]+=1
        self.d['active_spacer']['min'][2]+=1
        self.d['top_plate']['z']+=1
        with self.assertRaises(ValueError):cad.validate(self.d)
    def test_nonfinite_dimension_rejected(self):
        self.d['shells']['sphere_diameter']=float('nan')
        with self.assertRaises(ValueError):cad.validate(self.d)
    def test_unsupported_plate_size_rejected(self):
        self.d['top_plate']['size'][0]=200
        with self.assertRaises(ValueError):cad.validate(self.d)


if __name__=='__main__':unittest.main()
