"""Deterministic mailbox tests; no camera, raw recordings or MediaPipe dependency."""
import threading
import time
import unittest
from Tools.Vision.mediapipe_bridge import LatestCameraFrame

class Camera:
    def __init__(self):
        self.n=0;self.released=False;self.read_thread=None;self.release_thread=None
    def read(self):
        self.read_thread=threading.get_ident();time.sleep(.002);self.n+=1
        return True, self.n
    def release(self):
        self.released=True;self.release_thread=threading.get_ident()

class LatestCameraTests(unittest.TestCase):
    def test_slow_inference_drops_old_frames_and_shutdown_owns_release(self):
        camera=Camera();reader=LatestCameraFrame(camera)
        try:
            ok,first,t0=reader.read();self.assertTrue(ok)
            time.sleep(.06)
            ok,latest,t1=reader.read();self.assertTrue(ok)
            self.assertGreater(latest,first+5)
            self.assertGreater(t1,t0)
            self.assertLess(time.monotonic()-t1,.05)
        finally:reader.close()
        self.assertTrue(camera.released)
        self.assertFalse(reader.thread.is_alive())
        self.assertEqual(camera.read_thread,camera.release_thread)

    def test_stalled_camera_times_out_without_duplicate_frames(self):
        class Stalled(Camera):
            def read(self):return False,None
        reader=LatestCameraFrame(Stalled())
        try:self.assertFalse(reader.read(timeout=.025)[0])
        finally:reader.close()

if __name__=='__main__':unittest.main()
