# 1.1 / 2.4 BLE telemetry protocol v1

Peripheral local name: `GripRehab`. Private service UUID: `8c180000-5f55-4d3c-8ac7-7d25a3818f42`.
Telemetry characteristic: `8c180001-5f55-4d3c-8ac7-7d25a3818f42`, READ | NOTIFY, fixed 20 bytes. Metadata characteristic: `8c180002-5f55-4d3c-8ac7-7d25a3818f42`, READ, fixed 12 bytes. No BLE write control; calibration is an explicit local USB serial operation with streaming stopped.

All integers are little-endian. No native C struct is transmitted. All fields are explicitly serialized.

| Offset | Size | Field |
| --- | --- | --- |
| 0 | 1 | protocol version = 1 |
| 1 | 1 | flags |
| 2 | 1 | shape: 0 unknown, 1 sphere, 2 cylinder, 3 pencil |
| 3 | 1 | reserved = 0 |
| 4 | 4 | uint32 sequence, increments for each generated packet including invalid heartbeat |
| 8 | 4 | uint32 sample millis; for invalid heartbeat, packet generation millis |
| 12 | 4 | int32 raw signed HX711 counts, 0 when no reading exists |
| 16 | 4 | int32 signed milliNewtons, 0 when invalid; flags are authoritative |

Flags: bit 0 VALID, bit 1 CALIBRATED, bit 2 SENSOR_TIMEOUT, bit 3 ADC_SATURATED, bit 4 OVER_RANGE, bit 5 CALIBRATING, bit 6 STREAM_STOPPED; bit 7 reserved. VALID is allowed only with CALIBRATED, a known shape, and no fault/stopped/calibrating flags. Invalid value 0 is never a measured zero.

Metadata: offset 0 uint32 calibration revision; offset 4 uint32 calibration record CRC32; offset 8 uint8 shape; offset 9 uint8 protocol version; offsets 10-11 reserved zero. Read on connect, after a shape/calibration change and before starting a session. Calibration is bound to the installed shape; changing shape invalidates it until that shape is calibrated. Firmware supports one active calibration record, not automatic mechanical shape detection.

10 Hz maximum notification attempt rate; use actual sample timestamps and do not assume every message is a new measurement. Default 10 SPS HX711 conversions may cause occasional repeated values at the 10 Hz heartbeat. A receiver must not integrate the same sample timestamp twice for force endurance. Force is valid for at most 500 ms after its last conversion. Sequence gaps indicate missed generated packets; BLE reconnect resets receiver continuity, not necessarily firmware sequence.

Faults invalidate force immediately when detected. USB commands `stop`, `tare`, `cal <newtons>`, `shape <1|2|3>` prevent valid streaming. Only `start` with valid calibration and a fresh reading permits VALID packets. Reconnect cannot itself restart a stopped stream. No physiological safety-stop or clinical fatigue detector exists in W2 firmware.

Synthetic conformance vector: version 1, flags 3, shape 1, sequence 42, sample time 1000, raw -12345, force 12.345 N:

```text
01 03 01 00 2a 00 00 00 e8 03 00 00 c7 cf ff ff 39 30 00 00
```

Reject unknown version/reserved bits, wrong length, invalid shape and inconsistent VALID flags. Host logs continuity anomalies; drops duplicate and backward/out-of-order packets rather than interpreting a huge unsigned delta as loss. Receiver silence must independently invalidate the last force.


Review update (2026-09-13): the bench monitor buffers at most 128 packets and releases batches only after unchanged calibration metadata is re-read, including a final check for sub-second captures. This adds up to about one second of logging delay and is not the future game transport. Capture changes/errors discard the pending batch and end with a nonzero exit. All hardware events carry source/session/connection/device provenance. Repeated sample timestamps expire force after 500 ms even when packets continue, and conflicting values for one timestamp are rejected. Host `sample_stale` can invalidate `force_n` independently of the raw device flags.

Protocol v1 still has no calibration epoch in each telemetry packet. Periodic metadata checks cannot prove that metadata never changed and returned to its old value between checks. Calibration/shape changes remain prohibited during a capture; a future epoch-bearing protocol is needed for atomic provenance under concurrent reconfiguration. Hardware BLE behavior has not been tested without a radio/device.
