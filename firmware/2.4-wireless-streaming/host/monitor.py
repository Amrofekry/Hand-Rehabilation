"""Bench BLE monitor. Buffer packets until calibration metadata is rechecked."""
import argparse
import asyncio
from datetime import datetime, timezone
import json
import struct
import time
import uuid
from protocol import TELEMETRY, METADATA, Tracker, decode_metadata


def emit(value):
    print(json.dumps(value, allow_nan=False), flush=True)


class CaptureChanged(ValueError):
    pass


class Capture:
    """At most one second of pending samples; reject changed calibration batches."""
    def __init__(self, metadata):
        self.metadata = dict(metadata)
        self.tracker = Tracker()
        self.pending = []

    def accept(self, packet, received):
        row = self.tracker.accept(packet, received)
        if (row['shape_id'] != self.metadata['shape_id'] or row['flags'] & 32 or
                (row['flags'] & 1 and not self.metadata['calibration_id'])):
            raise CaptureChanged('Shape/calibration changed during capture')
        if len(self.pending) >= 128:
            raise CaptureChanged('Pending packet bound exceeded')
        row.update(calibration_id=self.metadata['calibration_id'], calibration_crc=self.metadata['calibration_crc'],
                   host_monotonic_ns=int(received*1e9), host_utc=datetime.now(timezone.utc).isoformat())
        self.pending.append(row)

    def confirm(self, metadata):
        if metadata != self.metadata:
            self.pending.clear()
            raise CaptureChanged('Calibration metadata changed; pending packets discarded')
        rows, self.pending = self.pending, []
        return rows


def demo():
    tracker=Tracker(); session=str(uuid.uuid4()); connection=str(uuid.uuid4())
    for seq,flags,ms,mn in [(0,68,0,0),(1,3,100,5000),(3,3,200,10000),(4,66,300,0)]:
        packet=struct.pack('<BBBBIIii',1,flags,1,0,seq,ms,1000,mn)
        emit(dict(tracker.accept(packet,ms/1000),source='synthetic',session_id=session,connection_id=connection))
    emit(dict(event='silence_check',stale=tracker.stale(.9),source='synthetic',session_id=session,connection_id=connection))


async def hardware(address, seconds):
    from bleak import BleakClient
    session=str(uuid.uuid4()); connection=str(uuid.uuid4()); disconnected=asyncio.Event(); invalid=asyncio.Event()
    loop=asyncio.get_running_loop()
    def log(row):
        emit(dict(row,source='hardware',session_id=session,connection_id=connection,device_id=address))
    def on_disconnect(_): loop.call_soon_threadsafe(disconnected.set)
    async with BleakClient(address,disconnected_callback=on_disconnect) as client:
        info=decode_metadata(await client.read_gatt_char(METADATA)); capture=Capture(info)
        log(dict(event='metadata',**info))
        def notification(_,data):
            if invalid.is_set() or disconnected.is_set(): return
            try: capture.accept(data,time.monotonic())
            except CaptureChanged as error:
                log(dict(event='capture_invalidated',reason=str(error),force_n=None));invalid.set()
            except ValueError as error: log(dict(event='rejected',reason=str(error)))
        async def confirm():
            current=decode_metadata(await client.read_gatt_char(METADATA))
            for row in capture.confirm(current): log(row)
        await client.start_notify(TELEMETRY,notification)
        deadline=time.monotonic()+seconds; stale_announced=False; last_metadata=time.monotonic()
        try:
            while time.monotonic()<deadline and not disconnected.is_set() and not invalid.is_set():
                await asyncio.sleep(.1)
                if capture.tracker.stale(time.monotonic()):
                    if not stale_announced: log(dict(event='stale',force_n=None))
                    stale_announced=True
                else: stale_announced=False
                if time.monotonic()-last_metadata>=1 and client.is_connected:
                    await confirm();last_metadata=time.monotonic()
            if client.is_connected:
                # Quiesce notifications before the final metadata check, including sub-second captures.
                await client.stop_notify(TELEMETRY)
                if not invalid.is_set(): await confirm()
            else:
                invalid.set();log(dict(event='disconnected',force_n=None))
            if invalid.is_set(): raise CaptureChanged('Capture interrupted; pending packets discarded')
            log(dict(event='capture_end',status='complete'))
        except Exception as error:
            capture.pending.clear()
            log(dict(event='capture_end',status='invalid',reason=str(error),force_n=None))
            raise
    # A new invocation starts a new continuity/session envelope; no silent reconnect.


def main():
    p=argparse.ArgumentParser();g=p.add_mutually_exclusive_group(required=True)
    g.add_argument('--demo',action='store_true');g.add_argument('--address')
    p.add_argument('--seconds',type=float,default=30);a=p.parse_args()
    if not 0<a.seconds<=3600:p.error('seconds must be within (0, 3600]')
    if a.demo:demo()
    else:
        try:asyncio.run(hardware(a.address,a.seconds))
        except ImportError:p.exit(2,'Install bleak==3.0.2 for real BLE hardware.\n')
        except Exception as error:p.exit(1,str(error)+'\n')


if __name__=='__main__':main()
