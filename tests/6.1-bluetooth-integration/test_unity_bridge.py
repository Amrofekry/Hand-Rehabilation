from pathlib import Path
import socket
import struct
import sys
import time
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "firmware/2.4-wireless-streaming/host"))
from monitor import Capture, CaptureChanged
from unity_bridge import UnitySender, relay_confirmed


class BridgeContract(unittest.TestCase):
    def setUp(self):
        self.metadata = dict(calibration_id=7, calibration_crc=1234, shape_id=1)
        self.capture = Capture(self.metadata)
        self.receiver = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.receiver.bind(("127.0.0.1", 0))
        self.receiver.settimeout(.2)
        self.sender = UnitySender("synthetic", self.receiver.getsockname()[1])

    def tearDown(self):
        self.sender.close()
        self.receiver.close()

    def accept(self, sequence, sample_ms, flags=3, force=18000, received=None):
        received = time.monotonic() if received is None else received
        packet = struct.pack("<BBBBIIii", 1, flags, 1, 0, sequence, sample_ms, 1000, force)
        self.capture.accept(packet, received)
        self.capture.pending[-1]["host_receive"] = received

    def test_fresh_calibrated_sample_reaches_loopback(self):
        self.accept(1, 100)
        self.assertEqual(relay_confirmed(self.capture, self.metadata, self.sender, time.monotonic()), 1)
        import json
        frame = json.loads(self.receiver.recvfrom(2048)[0])
        self.assertEqual((frame["kind"], frame["source"], frame["forceMilliNewtons"]),
                         ("sample", "synthetic", 18000))
        self.assertEqual(frame["calibrationRevision"], 7)

    def test_repeated_timestamp_does_not_relay(self):
        self.accept(1, 100)
        relay_confirmed(self.capture, self.metadata, self.sender, time.monotonic())
        self.receiver.recvfrom(2048)
        self.accept(2, 100)
        self.assertEqual(relay_confirmed(self.capture, self.metadata, self.sender, time.monotonic()), 0)
        with self.assertRaises(socket.timeout): self.receiver.recvfrom(2048)

    def test_expired_or_invalid_force_does_not_relay(self):
        self.accept(1, 100, received=time.monotonic() - .6)
        self.assertEqual(relay_confirmed(self.capture, self.metadata, self.sender, time.monotonic()), 0)
        self.accept(2, 200, flags=68, force=0)
        self.assertEqual(relay_confirmed(self.capture, self.metadata, self.sender, time.monotonic()), 0)

    def test_metadata_change_discards_pending(self):
        self.accept(1, 100)
        with self.assertRaises(CaptureChanged):
            relay_confirmed(self.capture, dict(self.metadata, calibration_id=8),
                            self.sender, time.monotonic())
        self.assertEqual(self.capture.pending, [])
        with self.assertRaises(socket.timeout): self.receiver.recvfrom(2048)


if __name__ == "__main__": unittest.main()
