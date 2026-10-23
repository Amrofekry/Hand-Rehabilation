from pathlib import Path
import struct
import sys
import unittest
sys.path.insert(0,str(Path(__file__).resolve().parents[2]/'firmware/2.4-wireless-streaming/host'))
from protocol import decode,decode_metadata,Tracker

def packet(seq=42,ms=1000,flags=3,mn=12345): return struct.pack('<BBBBIIii',1,flags,1,0,seq,ms,-12345,mn)
class Protocol(unittest.TestCase):
    def test_golden(self):
        data=bytes.fromhex('010301002a000000e8030000c7cfffff39300000')
        self.assertEqual(decode(data)['force_n'],12.345); self.assertEqual(decode(data)['raw_counts'],-12345)
    def test_length_version_reserved(self):
        for data in (b'',packet()+b'0',bytes([2])+packet()[1:],packet()[:3]+b'\x01'+packet()[4:]):
            with self.assertRaises(ValueError): decode(data)
    def test_faults(self):
        self.assertIsNone(decode(packet(flags=68,mn=0))['force_n'])
        for flags in (1,7,65,131):
            with self.assertRaises(ValueError): decode(packet(flags=flags))
        with self.assertRaises(ValueError): decode(packet(flags=4))
    def test_sequence_loss_and_duplicates(self):
        t=Tracker(); t.accept(packet(seq=1),1); row=t.accept(packet(seq=4),1.1)
        self.assertEqual(row['dropped_packets'],2); self.assertFalse(row['new_sample'])
        for seq in (4,3):
            with self.assertRaises(ValueError): t.accept(packet(seq=seq),1.2)
    def test_rollover(self):
        t=Tracker(); t.accept(packet(seq=0xffffffff,ms=0xfffffff0),0)
        self.assertTrue(t.accept(packet(seq=0,ms=10),.1)['new_sample'])
    def test_stale_and_reconnect(self):
        t=Tracker(); self.assertTrue(t.stale(0)); t.accept(packet(),1)
        self.assertFalse(t.stale(1.49)); self.assertTrue(t.stale(1.5))
        self.assertEqual(Tracker().accept(packet(seq=0),2)['dropped_packets'],0)
    def test_backward_clock(self):
        t=Tracker(); t.accept(packet(seq=1,ms=100),1)
        with self.assertRaises(ValueError): t.accept(packet(seq=2,ms=99),2)
    def test_metadata(self):
        self.assertEqual(decode_metadata(struct.pack('<IIBBH',7,1234,2,1,0))['calibration_id'],7)
        with self.assertRaises(ValueError): decode_metadata(b'')
if __name__=='__main__': unittest.main()
