"""Regression checks for capture provenance and portable delivery failures."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'firmware/2.4-wireless-streaming/host'))
from monitor import Capture, CaptureChanged
from protocol import Tracker
spec=importlib.util.spec_from_file_location('delivery_checks',ROOT/'Tools/LegacyPhase2/verify_phase2.py')
delivery=importlib.util.module_from_spec(spec);spec.loader.exec_module(delivery)

def packet(seq=0,ms=100,mn=1000):return struct.pack('<BBBBIIii',1,3,1,0,seq,ms,1000,mn)

class CaptureTests(unittest.TestCase):
    def setUp(self):self.meta={'calibration_id':1,'calibration_crc':123,'shape_id':1}
    def test_final_metadata_change_discards_pending(self):
        c=Capture(self.meta);c.accept(packet(),1)
        with self.assertRaises(CaptureChanged):c.confirm(dict(self.meta,calibration_id=2))
        self.assertEqual(c.pending,[])
    def test_confirm_releases_once(self):
        c=Capture(self.meta);c.accept(packet(),1)
        self.assertEqual(len(c.confirm(self.meta)),1);self.assertEqual(c.confirm(self.meta),[])
    def test_valid_force_without_calibration_rejected(self):
        c=Capture(dict(self.meta,calibration_id=0,calibration_crc=0))
        with self.assertRaises(CaptureChanged):c.accept(packet(),1)
    def test_repeated_sample_expires_despite_new_packets(self):
        t=Tracker();t.accept(packet(),1)
        self.assertIsNone(t.accept(packet(seq=1),1.5)['force_n'])
        self.assertFalse(t.stale(1.5))
        self.assertEqual(t.accept(packet(seq=2,ms=200),1.6)['force_n'],1)
    def test_same_timestamp_conflicting_values(self):
        t=Tracker();t.accept(packet(),1)
        with self.assertRaises(ValueError):t.accept(packet(seq=1,mn=999),1.1)
    def test_nonfinite_clock_rejected(self):
        with self.assertRaises(ValueError):Tracker().accept(packet(),float('nan'))

class DeliveryTests(unittest.TestCase):
    def test_failed_run_replaces_old_pass_receipt(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);(root/'delivery').mkdir();(root/'delivery/verification.json').write_text('{"status":"PASS"}')
            failure=type('Result',(),{'returncode':1,'stdout':'','stderr':'compiler failed'})()
            with patch.object(delivery,'ROOT',root),patch.object(sys,'argv',['verify_phase2.py']),patch.object(delivery.subprocess,'run',return_value=failure),contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(delivery.main(),1)
            self.assertEqual(json.loads((root/'delivery/verification.json').read_text())['status'],'FAIL')
    def test_missing_tracked_file_cannot_be_packaged(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)
            with self.assertRaises(ValueError):delivery.write_archive(root,['absent.txt'],{},root/'out.zip')
            self.assertFalse((root/'out.zip').exists())
    def test_archive_contains_exact_bytes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);(root/'sample.txt').write_bytes(b'original\r\nbytes')
            delivery.write_archive(root,['sample.txt'],{'status':'PASS'},root/'out.zip')
            with zipfile.ZipFile(root/'out.zip') as z:self.assertEqual(z.read('sample.txt'),b'original\r\nbytes')
    def test_escape_path_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(ValueError):delivery.write_archive(Path(tmp),['../outside.txt'],{},Path(tmp)/'out.zip')

if __name__=='__main__':unittest.main()
