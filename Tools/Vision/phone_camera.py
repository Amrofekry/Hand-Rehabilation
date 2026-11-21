"""Android phone camera over USB for the hand tracker: adb + scrcpy-server (Apache-2.0) in camera mode.

The phone's camera (back by default, Android 12+) is encoded to H.264 on the phone and streamed raw over an adb
forward socket; OpenCV's FFmpeg backend decodes it. No app is installed on the phone; USB debugging must be on.
Tools live on drive E (E:/GP/codex/.local/tools/scrcpy/...; override with GRIPREHAB_SCRCPY_DIR).

  .venv/Scripts/python.exe Tools/Vision/phone_camera.py --list          # devices and cameras with sizes and fps
  .venv/Scripts/python.exe Tools/Vision/phone_camera.py --probe 5      # open the stream for 5 s, print fps and size

For hand tracking (2026-11-20 plan): highest fps, 1280x720 (MediaPipe works at 192/224 px inside, 4K only adds
delay), the widest back lens that keeps the hand large enough. Focus, exposure and stabilization are the camera's
own automatic modes here; scrcpy's camera mode does not set them.
"""
from __future__ import annotations

import argparse
import os
import random
import re
import socket
import subprocess
import threading
import time
from dataclasses import dataclass, field
from pathlib import Path

SCRCPY_VERSION = "5.0.1"
DEFAULT_DIR = Path(__file__).resolve().parents[3] / ".local" / "tools" / "scrcpy" / f"scrcpy-win64-v{SCRCPY_VERSION}"
DEVICE_JAR = "/data/local/tmp/griprehab-scrcpy-server.jar"
NO_WINDOW = 0x08000000 if os.name == "nt" else 0  # CREATE_NO_WINDOW: no console flashes over the game


def tools_dir() -> Path:
    return Path(os.environ.get("GRIPREHAB_SCRCPY_DIR", str(DEFAULT_DIR)))


@dataclass
class PhoneCameraConfig:
    facing: str = "back"            # back | front | external
    camera_id: str = ""             # overrides facing when set (from --list)
    size: str = "1280x720"          # WxH; "" lets the phone choose
    fps: int = 60
    high_speed: bool = False        # constrained high-speed mode (only listed size/fps pairs)
    zoom: float = 0.0               # 0 = not set; < 1 selects a wider lens on phones that allow it
    bit_rate: int = 8_000_000
    port: int = 27183
    serial: str = ""                # adb device serial when several are connected
    extra: tuple = ()               # more scrcpy-server key=value options (checked), e.g. video_codec_options=...
    scid: int = field(default_factory=lambda: random.randrange(1, 0x7FFFFFFF))

    def validate(self) -> None:
        if self.facing not in ("back", "front", "external"):
            raise ValueError(f"Unknown camera facing: {self.facing}")
        if self.size and not re.fullmatch(r"[1-9]\d{1,4}x[1-9]\d{1,4}", self.size):
            raise ValueError(f"Camera size must be WxH, got {self.size!r}")
        if not 1 <= self.fps <= 480:
            raise ValueError(f"Camera fps out of range: {self.fps}")
        if not 1024 <= self.port <= 65535:
            raise ValueError(f"Port out of range: {self.port}")
        if self.zoom < 0 or self.zoom > 100:
            raise ValueError(f"Zoom out of range: {self.zoom}")
        for option in self.extra:
            if not re.fullmatch(r"[a-z_]{2,40}=[A-Za-z0-9:=.,_-]{1,120}", option):
                raise ValueError(f"Unsafe server option: {option!r}")

    @property
    def socket_name(self) -> str:
        return f"scrcpy_{self.scid:08x}"


def adb_base(cfg: PhoneCameraConfig) -> list[str]:
    cmd = [str(tools_dir() / "adb.exe") if os.name == "nt" else "adb"]
    return cmd + (["-s", cfg.serial] if cfg.serial else [])


def server_args(cfg: PhoneCameraConfig, *, listing: str = "") -> list[str]:
    """scrcpy-server key=value options: raw H.264 camera stream on the forward tunnel, no audio or control."""
    cfg.validate()
    args = [f"scid={cfg.scid:08x}", "log_level=info", "tunnel_forward=true", "audio=false", "control=false",
            "cleanup=true"]
    if listing:
        return args + ["video=false", f"{listing}=true"]
    args += ["raw_stream=true", "video_source=camera", "video_codec=h264", f"video_bit_rate={cfg.bit_rate}"]
    args += [f"camera_id={cfg.camera_id}"] if cfg.camera_id else [f"camera_facing={cfg.facing}"]
    if cfg.size:
        args.append(f"camera_size={cfg.size}")
    args.append(f"camera_fps={cfg.fps}")
    if cfg.high_speed:
        args.append("camera_high_speed=true")
    if cfg.zoom > 0:
        args.append(f"camera_zoom={cfg.zoom:g}")
    return args + list(cfg.extra)


def shell_command(cfg: PhoneCameraConfig, *, listing: str = "") -> list[str]:
    return adb_base(cfg) + ["shell", f"CLASSPATH={DEVICE_JAR}", "app_process", "/", "com.genymobile.scrcpy.Server",
                            SCRCPY_VERSION] + server_args(cfg, listing=listing)


def parse_devices(text: str) -> list[dict]:
    """`adb devices -l` lines -> [{'serial', 'state', 'model'}] (state: device | unauthorized | offline)."""
    out = []
    for line in text.splitlines()[1:]:
        parts = line.split()
        if len(parts) < 2 or parts[0].startswith("*"):
            continue
        model = next((p.split(":", 1)[1] for p in parts[2:] if p.startswith("model:")), "")
        out.append({"serial": parts[0], "state": parts[1], "model": model})
    return out


def _run(cmd: list[str], timeout: float = 20) -> subprocess.CompletedProcess:
    return subprocess.run(cmd, capture_output=True, text=True, timeout=timeout, creationflags=NO_WINDOW)


def devices(cfg: PhoneCameraConfig | None = None) -> list[dict]:
    cfg = cfg or PhoneCameraConfig()
    return parse_devices(_run(adb_base(PhoneCameraConfig()) + ["devices", "-l"]).stdout)


def ready_device(cfg: PhoneCameraConfig) -> str:
    """The serial to use, or a plain-language reason it cannot be used."""
    found = devices(cfg)
    if cfg.serial:
        found = [d for d in found if d["serial"] == cfg.serial]
    if not found:
        raise RuntimeError("No phone found over USB. Connect it with a data cable and turn on USB debugging "
                           "(Settings > Developer options).")
    usable = [d for d in found if d["state"] == "device"]
    if not usable:
        raise RuntimeError("The phone is connected but not allowed yet: unlock it and accept 'Allow USB debugging'.")
    return usable[0]["serial"]


def push_server(cfg: PhoneCameraConfig) -> None:
    jar = tools_dir() / "scrcpy-server"
    if not jar.is_file():
        raise RuntimeError(f"scrcpy-server missing: {jar}")
    result = _run(adb_base(cfg) + ["push", str(jar), DEVICE_JAR], timeout=60)
    if result.returncode != 0:
        raise RuntimeError("Could not copy the camera server to the phone: " + (result.stderr or result.stdout).strip())


def list_cameras(cfg: PhoneCameraConfig, sizes: bool = False) -> str:
    cfg.serial = ready_device(cfg)
    push_server(cfg)
    result = _run(shell_command(cfg, listing="list_camera_sizes" if sizes else "list_cameras"), timeout=60)
    return (result.stdout + result.stderr).strip()


class PhoneCamera:
    """cv2.VideoCapture-like: isOpened(), read() -> (ok, frame BGR), release(). Starts and stops the phone side."""

    def __init__(self, cfg: PhoneCameraConfig):
        self.cfg = cfg
        self.server: subprocess.Popen | None = None
        self.capture = None
        self.log: list[str] = []

    def open(self, timeout: float = 15) -> "PhoneCamera":
        import cv2
        cfg = self.cfg
        cfg.serial = ready_device(cfg)
        push_server(cfg)
        forward = _run(adb_base(cfg) + ["forward", f"tcp:{cfg.port}", f"localabstract:{cfg.socket_name}"])
        if forward.returncode != 0:
            raise RuntimeError("adb forward failed: " + (forward.stderr or forward.stdout).strip())
        self.server = subprocess.Popen(shell_command(cfg), stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
                                       creationflags=NO_WINDOW)
        threading.Thread(target=self._drain, daemon=True).start()
        # Raw H.264 with FFmpeg's buffering off: decode each frame as it arrives.
        os.environ["OPENCV_FFMPEG_CAPTURE_OPTIONS"] = "f;h264|fflags;nobuffer|flags;low_delay|probesize;32768|analyzeduration;0|threads;1"
        # adb forward accepts a connection even before the phone side listens (then closes it), so try a few times.
        deadline = time.monotonic() + timeout
        while True:
            time.sleep(.5)
            if self.server.poll() is not None:
                raise RuntimeError("The phone camera server stopped: " + self._reason())
            if any("ERROR" in line or "Exception" in line for line in self.log):
                self.release()
                raise RuntimeError("The phone camera reported: " + " | ".join(self.log[-5:]))
            self.capture = cv2.VideoCapture(f"tcp://127.0.0.1:{cfg.port}", cv2.CAP_FFMPEG)
            if self.capture.isOpened():
                return self
            self.capture.release()
            self.capture = None
            if time.monotonic() > deadline:
                self.release()
                raise RuntimeError("Could not decode the phone camera stream. " + " | ".join(self.log[-5:]))

    def _reason(self) -> str:
        """The exception line itself (not the stack below it), with the usual cause in plain words."""
        lines = [l.strip() for l in self.log if "Exception" in l or "ERROR" in l or "Error" in l]
        text = " | ".join(lines[:3]) or " | ".join(self.log[-3:])
        if "CAMERA_DISABLED" in text or "disabled" in text.lower() or "connectDevice" in " ".join(self.log):
            text += " (keep the phone unlocked with its screen on: Android blocks the camera for background use)"
        return text

    def _drain(self) -> None:
        for line in self.server.stdout:  # keep the pipe empty; keep the last lines for messages
            self.log.append(line.rstrip())
            del self.log[:-200]

    def isOpened(self) -> bool:
        return self.capture is not None and self.capture.isOpened()

    def read(self):
        return self.capture.read() if self.capture is not None else (False, None)

    def get(self, prop):
        return self.capture.get(prop) if self.capture is not None else 0

    def set(self, prop, value):  # sizes and fps are set on the phone side
        return False

    def release(self) -> None:
        if self.capture is not None:
            self.capture.release()
            self.capture = None
        if self.server is not None and self.server.poll() is None:
            self.server.terminate()
            try:
                self.server.wait(timeout=3)
            except subprocess.TimeoutExpired:
                self.server.kill()
        self.server = None
        try:
            _run(adb_base(self.cfg) + ["forward", "--remove", f"tcp:{self.cfg.port}"], timeout=5)
        except Exception:
            pass


class IPWebcamCamera:
    """The free 'IP Webcam' Android app (Pavel Khlebovich) over the same USB cable: adb forwards its local HTTP port and
    OpenCV reads its MJPEG stream. The app keeps its own camera settings (stabilization, focus, exposure, white
    balance, fps, size); settings() lists them and set() changes one through its HTTP API. cv2.VideoCapture-like."""

    def __init__(self, port: int = 8080, serial: str = ""):
        if not 1024 <= port <= 65535:
            raise ValueError(f"Port out of range: {port}")
        self.cfg = PhoneCameraConfig(port=port, serial=serial)
        self.capture = None

    @property
    def base(self) -> str:
        return f"http://127.0.0.1:{self.cfg.port}"

    def forward(self) -> None:
        self.cfg.serial = ready_device(self.cfg)
        result = _run(adb_base(self.cfg) + ["forward", f"tcp:{self.cfg.port}", f"tcp:{self.cfg.port}"])
        if result.returncode != 0:
            raise RuntimeError("adb forward failed: " + (result.stderr or result.stdout).strip())

    def _get(self, path: str, timeout: float = 3) -> bytes:
        import urllib.request
        with urllib.request.urlopen(self.base + path, timeout=timeout) as response:  # loopback only (adb forward)
            return response.read()

    def settings(self) -> dict:
        import json
        return json.loads(self._get("/status.json?show_avail=1"))

    def set(self, name: str, value: str) -> str:
        if not re.fullmatch(r"[a-z_]{2,40}", name) or not re.fullmatch(r"[A-Za-z0-9_.x-]{1,40}", str(value)):
            raise ValueError(f"Unsafe setting {name}={value}")
        return self._get(f"/settings/{name}?set={value}").decode("utf-8", "replace")

    def open(self, timeout: float = 10) -> "IPWebcamCamera":
        import cv2
        self.forward()
        try:
            self._get("/status.json", timeout=timeout)
        except Exception:
            raise RuntimeError("IP Webcam is not answering: open the app on the phone and tap 'Start server'.")
        os.environ["OPENCV_FFMPEG_CAPTURE_OPTIONS"] = "fflags;nobuffer|flags;low_delay|threads;1"
        self.capture = cv2.VideoCapture(self.base + "/video", cv2.CAP_FFMPEG)
        if not self.capture.isOpened():
            raise RuntimeError("Could not read the IP Webcam video stream.")
        return self

    def isOpened(self) -> bool:
        return self.capture is not None and self.capture.isOpened()

    def read(self):
        return self.capture.read() if self.capture is not None else (False, None)

    def get(self, prop):
        return self.capture.get(prop) if self.capture is not None else 0

    def set_prop(self, prop, value):
        return False

    def release(self) -> None:
        if self.capture is not None:
            self.capture.release()
            self.capture = None
        try:
            _run(adb_base(self.cfg) + ["forward", "--remove", f"tcp:{self.cfg.port}"], timeout=5)
        except Exception:
            pass


class LatestFrameReader:
    """Reads a live source on its own thread and hands out only the newest frame (older ones are dropped), so the
    tracker never works on a frame that waited in a queue. read() -> (ok, frame, captured monotonic seconds)."""

    def __init__(self, capture):
        self.capture = capture
        self._lock = threading.Condition()
        self._frame = None
        self._time = 0.0
        self._seq = 0
        self._taken = 0
        self._ok = True
        self.dropped = 0
        self.failures = 0
        self._thread = threading.Thread(target=self._loop, daemon=True)
        self._thread.start()

    def _loop(self) -> None:
        while self._ok:
            ok, frame = self.capture.read()
            now = time.monotonic()
            if not ok:  # a stalled camera: retry without spinning a core; read() times out meanwhile
                self.failures += 1
                time.sleep(.02)
                continue
            with self._lock:
                if self._seq > self._taken:
                    self.dropped += 1
                self._frame, self._time, self._seq = frame, now, self._seq + 1
                self._lock.notify_all()

    def read(self, timeout: float = 1.0):
        with self._lock:
            if not self._lock.wait_for(lambda: self._seq > self._taken or not self._ok, timeout):
                return False, None, 0.0
            if self._seq == self._taken:
                return False, None, 0.0
            self._taken = self._seq
            return True, self._frame, self._time

    def stop(self) -> None:
        self._ok = False
        self._thread.join(timeout=2)  # never release the source while this thread is still reading it


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--list", action="store_true", help="List devices, cameras and their sizes and frame rates")
    parser.add_argument("--probe", type=float, default=0, help="Open the camera stream for N seconds and report fps")
    parser.add_argument("--facing", default="back")
    parser.add_argument("--size", default="1280x720")
    parser.add_argument("--fps", type=int, default=60)
    parser.add_argument("--high-speed", action="store_true")
    parser.add_argument("--zoom", type=float, default=0)
    parser.add_argument("--camera-id", default="", help="Camera id from --list (overrides --facing)")
    args = parser.parse_args()
    cfg = PhoneCameraConfig(facing=args.facing, camera_id=args.camera_id, size=args.size, fps=args.fps, high_speed=args.high_speed, zoom=args.zoom)
    if args.list:
        print("devices:", devices(cfg))
        print(list_cameras(cfg))
        print(list_cameras(cfg, sizes=True))
    if args.probe > 0:
        cam = PhoneCamera(cfg).open()
        reader = LatestFrameReader(cam)
        frames, shape, start = 0, None, time.monotonic()
        while time.monotonic() - start < args.probe:
            ok, frame, _ = reader.read()
            if ok:
                frames += 1
                shape = frame.shape
        reader.stop()
        cam.release()
        span = time.monotonic() - start
        print(chr(10).join(cam.log[-12:]))
        print(f"PHONE_PROBE frames={frames} fps={frames / span:.1f} size={shape[1] if shape else 0}x{shape[0] if shape else 0} dropped={reader.dropped}")


if __name__ == "__main__":
    main()
