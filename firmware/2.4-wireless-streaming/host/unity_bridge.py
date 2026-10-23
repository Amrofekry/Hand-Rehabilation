"""WBS 6.1: checked Phase 2 BLE telemetry to the Unity loopback force input.

The bridge does not invent valid force, perform calibration, or restart firmware.
It reuses the Phase 2 decoder and metadata-confirmed capture buffer.
"""

import argparse
import asyncio
import json
import socket
import struct
import time
import uuid

from monitor import Capture, CaptureChanged
from protocol import METADATA, TELEMETRY, decode_metadata

DESTINATION = ("127.0.0.1", 27818)


class UnitySender:
    def __init__(self, source, port):
        self.source = source
        self.destination = ("127.0.0.1", port)
        self.socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.session_id = str(uuid.uuid4())
        self.connection_id = str(uuid.uuid4())

    def send(self, payload):
        envelope = dict(schemaVersion=1, source=self.source,
                        sessionId=self.session_id, connectionId=self.connection_id)
        envelope.update(payload)
        self.socket.sendto(json.dumps(envelope, separators=(",", ":"), allow_nan=False).encode(),
                           self.destination)

    def status(self, reason):
        self.send(dict(kind="status", reason=reason))

    def sample(self, row, metadata, now):
        age_ms = max(0, int((now - row["received_at"]) * 1000))
        if not row["new_sample"] or row["force_n"] is None or age_ms >= 500:
            return False
        self.send(dict(kind="sample", calibrationRevision=metadata["calibration_id"],
                       calibrationCrc=metadata["calibration_crc"], shapeId=row["shape_id"],
                       sequence=row["sequence"], sampleMs=row["device_ms"],
                       rawCounts=row["raw_counts"],
                       forceMilliNewtons=round(row["force_n"] * 1000),
                       flags=row["flags"], ageMs=age_ms))
        return True

    def close(self):
        self.socket.close()


def relay_confirmed(capture, metadata, sender, now):
    """Only emit samples after a fresh metadata read confirmed the pending batch."""
    sent = 0
    for row in capture.confirm(metadata):
        row["received_at"] = row["host_receive"]
        sent += sender.sample(row, metadata, now)
    return sent


async def scan():
    from bleak import BleakScanner

    devices = await BleakScanner.discover(timeout=5)
    for device in devices:
        print(f"{device.name or '(unnamed)'}  {device.address}")


async def hardware(address, port):
    from bleak import BleakClient

    sender = UnitySender("hardware", port)
    disconnected = asyncio.Event()
    invalid = asyncio.Event()
    sender.status("Connecting to GripRehab BLE")
    try:
        async with BleakClient(address, disconnected_callback=lambda _: disconnected.set()) as client:
            metadata = decode_metadata(await client.read_gatt_char(METADATA))
            capture = Capture(metadata)
            print(f"Connected: shape={metadata['shape_id']} revision={metadata['calibration_id']}", flush=True)

            def notification(_, data):
                if disconnected.is_set() or invalid.is_set():
                    return
                received = time.monotonic()
                try:
                    capture.accept(data, received)
                    capture.pending[-1]["host_receive"] = received
                    if capture.pending[-1]["force_n"] is None:
                        sender.status("Firmware reports invalid force or stale sample")
                except (CaptureChanged, ValueError) as error:
                    capture.pending.clear()
                    sender.status(f"BLE packet rejected: {error}")
                    invalid.set()

            await client.start_notify(TELEMETRY, notification)
            last_confirm = time.monotonic()
            stale_sent = False
            while client.is_connected and not disconnected.is_set() and not invalid.is_set():
                await asyncio.sleep(0.05)
                now = time.monotonic()
                if now - last_confirm >= 0.2:
                    current = decode_metadata(await client.read_gatt_char(METADATA))
                    relay_confirmed(capture, current, sender, time.monotonic())
                    last_confirm = time.monotonic()
                if capture.tracker.stale(now) or (capture.tracker.last_sample_receive is None or
                                                  now - capture.tracker.last_sample_receive >= 0.5):
                    if not stale_sent:
                        sender.status("BLE sample or notifications stale")
                    stale_sent = True
                else:
                    stale_sent = False
            if client.is_connected:
                await client.stop_notify(TELEMETRY)
            if invalid.is_set():
                raise CaptureChanged("BLE capture invalidated; restart bridge after fixing device")
            raise ConnectionError("BLE disconnected; firmware requires USB start after reconnect")
    except (Exception, KeyboardInterrupt) as error:
        sender.status(str(error))
        raise
    finally:
        sender.close()


def demo(seconds, port):
    sender = UnitySender("synthetic", port)
    metadata = dict(calibration_id=1, calibration_crc=1234, shape_id=1)
    capture = Capture(metadata)
    start = time.monotonic()
    sequence = 0
    try:
        while time.monotonic() - start < seconds:
            now = time.monotonic()
            sample_ms = sequence * 100
            packet = struct.pack("<BBBBIIii", 1, 3, 1, 0, sequence, sample_ms, 1000, 18000)
            capture.accept(packet, now)
            capture.pending[-1]["host_receive"] = now
            relay_confirmed(capture, metadata, sender, time.monotonic())
            sequence += 1
            time.sleep(max(0, start + sequence * 0.1 - time.monotonic()))
        sender.status("Synthetic replay ended")
    finally:
        sender.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--address", help="BLE device address from --scan")
    group.add_argument("--scan", action="store_true", help="list nearby BLE addresses")
    group.add_argument("--demo", action="store_true", help="clearly marked synthetic packet replay")
    parser.add_argument("--seconds", type=float, default=5, help="demo duration")
    parser.add_argument("--port", type=int, default=DESTINATION[1])
    args = parser.parse_args()
    if not 0 < args.port < 65536 or args.seconds <= 0:
        parser.error("Invalid port or duration")
    if args.demo:
        demo(args.seconds, args.port)
    elif args.scan:
        asyncio.run(scan())
    else:
        asyncio.run(hardware(args.address, args.port))


if __name__ == "__main__":
    main()
