# 2.4 BLE streaming and receiver

Build `grip_stream/grip_stream.ino` using the root firmware build script. The [protocol](../../docs/1.1-system-architecture/BLE_PROTOCOL.md) specifies UUIDs, 20-byte packets, units and flags. No Wi-Fi stack or Unity transport is implemented in W2; Bluetooth is the selected Gantt transport and live Unity integration is 6.1.

The sketch polls BLE and HX711 independently, attempts telemetry every 100 ms, exposes calibration metadata, and continues invalid heartbeats after an ADC timeout. A BLE disconnect explicitly stops valid streaming and resumes advertising; reconnection requires USB `start` after calibration/fresh-data checks. BLE setup failure leaves the USB bench console available. No patient calibration command is writable over BLE.

## Host monitor

```powershell
python firmware/2.4-wireless-streaming/host/monitor.py --demo
# Only after acquiring hardware and installing bleak==3.0.2 in a chosen Python environment:
python firmware/2.4-wireless-streaming/host/monitor.py --address 'DEVICE_ADDRESS' --seconds 30
```

Demo mode explicitly emits synthetic records. Hardware mode reads calibration metadata, subscribes to packets, adds session/connection IDs and host timestamps, rejects malformed/backward/duplicate packets, reports gaps, and detects 500 ms silence. JSONL can be redirected to a bench log. Do not recalibrate during capture; stop the monitor, recalibrate/save, and begin a new capture/session. Device millis are not automatically synchronized to camera frames; that belongs to 4.2. No participant data is requested by this bench tool.

## Physical acceptance protocol (not yet performed)

1. Calibrate/verify/save the correct shape with USB. Read metadata, issue `start`, and capture 60 seconds of BLE output. Record average and worst packet intervals, unique-sample intervals and packet loss; compare the provisional 10 Hz / 500 ms requirements.
2. Stop USB streaming; force becomes null with STREAM_STOPPED. Starting without a fresh calibrated reading must fail.
3. Disconnect the central; reconnect and confirm advertising returns but valid force does not restart until explicit `start`.
4. Remove ADC power/data using the reviewed bench procedure; verify invalid timeout within 500 ms. Reconnect ADC and check flags. A disconnected bridge with a still-operating ADC may produce plausible raw counts; this cannot be conclusively diagnosed by software alone.
5. Change shape, tare, and apply known loads. Confirm calibration invalidation and metadata revision. Check reboot restore and bad/missing calibration behavior.
6. Preserve actual device/firmware/library revisions, logs and measured timing. No mock stream is physical BLE acceptance evidence.


Review update (2026-09-13): the bench monitor buffers at most 128 packets and releases batches only after unchanged calibration metadata is re-read, including a final check for sub-second captures. This adds up to about one second of logging delay and is not the future game transport. Capture changes/errors discard the pending batch and end with a nonzero exit. All hardware events carry source/session/connection/device provenance. Repeated sample timestamps expire force after 500 ms even when packets continue, and conflicting values for one timestamp are rejected. Host `sample_stale` can invalidate `force_n` independently of the raw device flags.

Protocol v1 still has no calibration epoch in each telemetry packet. Periodic metadata checks cannot prove that metadata never changed and returned to its old value between checks. Calibration/shape changes remain prohibited during a capture; a future epoch-bearing protocol is needed for atomic provenance under concurrent reconfiguration. Hardware BLE behavior has not been tested without a radio/device.

## Unity force bridge (WBS 6.1 software advance)

`host/unity_bridge.py` reuses this folder's protocol decoder and metadata-confirmed capture buffer. It reads live notifications with Bleak and sends checked samples to the Unity player on loopback port 27818. See `../../unity-phase3/docs/BLE_INTEGRATION.md` for installation, launch and acceptance limits. The `--demo` option is synthetic only. Real-hardware acceptance is still pending.
