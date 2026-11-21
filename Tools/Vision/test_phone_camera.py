import threading
import time
import unittest

try:
    from . import phone_camera as pc
except ImportError:
    import phone_camera as pc


class PhoneCameraCommandTest(unittest.TestCase):
    def test_server_command_streams_raw_h264_from_the_back_camera(self):
        cfg = pc.PhoneCameraConfig(scid=0x1234)
        cmd = pc.shell_command(cfg)
        self.assertIn("com.genymobile.scrcpy.Server", cmd)
        self.assertEqual(cmd[cmd.index("com.genymobile.scrcpy.Server") + 1], pc.SCRCPY_VERSION)
        for opt in ("scid=00001234", "tunnel_forward=true", "audio=false", "control=false", "raw_stream=true",
                    "video_source=camera", "video_codec=h264", "camera_facing=back", "camera_size=1280x720", "camera_fps=60"):
            self.assertIn(opt, cmd)
        self.assertEqual(cfg.socket_name, "scrcpy_00001234")
        self.assertFalse(any(o.startswith("camera_high_speed") or o.startswith("camera_zoom") for o in cmd))

    def test_options_camera_id_high_speed_zoom(self):
        cmd = pc.server_args(pc.PhoneCameraConfig(camera_id="2", size="1920x1080", fps=120, high_speed=True, zoom=.6))
        self.assertIn("camera_id=2", cmd)
        self.assertNotIn("camera_facing=back", cmd)
        self.assertIn("camera_high_speed=true", cmd)
        self.assertIn("camera_zoom=0.6", cmd)
        self.assertIn("camera_fps=120", cmd)

    def test_listing_has_no_video(self):
        cmd = pc.server_args(pc.PhoneCameraConfig(), listing="list_cameras")
        self.assertIn("list_cameras=true", cmd)
        self.assertIn("video=false", cmd)
        self.assertNotIn("raw_stream=true", cmd)

    def test_bad_values_never_reach_the_phone_shell(self):
        for bad in (dict(size="1280x720; reboot"), dict(facing="side"), dict(fps=0), dict(port=80), dict(zoom=-1)):
            with self.assertRaises(ValueError):
                pc.server_args(pc.PhoneCameraConfig(**bad))

    def test_parse_devices(self):
        text = ("List of devices attached\n"
                "R58M12345AB    device usb:1-1 product:a52 model:SM_A525F device:a52q transport_id:1\n"
                "XYZ987 unauthorized usb:1-2 transport_id:2\n\n")
        found = pc.parse_devices(text)
        self.assertEqual(found[0], {"serial": "R58M12345AB", "state": "device", "model": "SM_A525F"})
        self.assertEqual(found[1]["state"], "unauthorized")
        self.assertEqual(pc.parse_devices("List of devices attached\n* daemon started successfully\n"), [])


class FakeCapture:
    def __init__(self, frames, period=.005, fail_first=0):
        self.frames, self.period, self.fail_first, self.i = frames, period, fail_first, 0

    def read(self):
        time.sleep(self.period)
        if self.fail_first > 0:
            self.fail_first -= 1
            return False, None
        if self.i >= self.frames:
            time.sleep(.05)
            return False, None
        self.i += 1
        return True, self.i


class LatestFrameReaderTest(unittest.TestCase):
    def test_only_the_newest_frame_is_handed_out(self):
        reader = pc.LatestFrameReader(FakeCapture(40, period=.002))
        time.sleep(.2)  # a slow consumer: many frames arrive meanwhile
        ok, frame, captured = reader.read()
        reader.stop()
        self.assertTrue(ok)
        self.assertEqual(frame, 40)
        self.assertGreater(reader.dropped, 30)
        self.assertGreater(captured, 0)

    def test_same_frame_is_never_returned_twice_and_a_stall_times_out(self):
        reader = pc.LatestFrameReader(FakeCapture(3, period=.001, fail_first=2))
        seen = []
        for _ in range(5):
            ok, frame, _ = reader.read(timeout=.3)
            if ok:
                seen.append(frame)
        reader.stop()
        self.assertEqual(len(seen), len(set(seen)))
        self.assertGreaterEqual(reader.failures, 2)
        self.assertEqual(seen[-1], 3)


if __name__ == "__main__":
    unittest.main()
