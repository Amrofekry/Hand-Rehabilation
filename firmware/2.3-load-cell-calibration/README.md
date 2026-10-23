# 2.3 HX711 acquisition and calibration

`calibration/calibration.ino` is a USB bench sketch for Arduino Nano 33 IoT. `libraries/RehabCore` owns the hardware-independent calibration/measurement logic and shared USB console. Pins: DOUT D2, SCK D3, channel A gain 128. Default board RATE jumper is 10 SPS. Acquisition checks readiness rather than waiting for absent hardware.

The project-owned `HX711Ready.h` clocks exactly 24 data bits plus one gain-select pulse after readiness checks, preserves interrupt state and sign-extends the result. It does not call the third-party blocking `read()` method. GPIO timing still requires scope/logic-analyzer verification on hardware; the host test verifies pulse count, signed decoding and readiness-loss behavior.

## First calibration, serial monitor at 115200 with newline

1. Secure a reviewed cell fixture, power it, allow readings to settle, and enter `status` or `help`. Boot never waits for a USB terminal. Output `DATA,time_ms,raw_counts,force_N_or_NA,flags,shape` is a bench stream.
2. Enter `shape 1` for sphere, `shape 2` for cylinder or `shape 3` for pencil. This is an operator declaration, not automatic shape detection. Changing shape invalidates the active calibration.
3. Remove external force and enter `tare`. Twenty stable readings are collected without blocking the main loop. Wait for `RESULT 1` (TARED). Timeout is five seconds; count spread above 100 is rejected. Those are configurable engineering settings in `RehabCore.h`, not measured noise performance.
4. Apply a known reference force within the fixture's reviewed load rating. Convert a mass to force with `N = mass_kg * 9.80665`; for example, a **synthetic arithmetic example** of 1 kg is 9.80665 N. Enter `cal 9.80665` only if that is the actual known force. Calibration uses another twenty readings and permits reversed bridge polarity. Wait for `RESULT 2` (CALIBRATED_OK).
5. `start` enables calibrated force output only with a fresh valid reading. Verify independent reference forces and unloading using `verify_calibration.py`; do not reuse only the calibration point and call that validation.
6. `stop`, then `save` commits the shape-bound record with version, revision and CRC. Repeated save of an unchanged record does not rewrite flash. Readback must say `OK saved and verified`.
7. Reboot. The calibration is restored if the record passes all checks, but streaming is stopped. Confirm `status`, then verify reference readings again. Record shape, revision, offset, counts/N and CRC in the bench report.

`tare` invalidates the prior calibration and requires a new known-load calibration. Only one shape calibration is stored. Swapping the physical grip requires selecting that shape and calibrating the whole assembly. Do not use an old shape's calibration just because it fits the same core.

Result codes: 0 none, 1 tared, 2 calibrated, 3 unstable, 4 timeout, 5 bad reference, 6 needs tare, 7 saturated. Rejected operations never enable streaming. Maximum absolute reported force is bounded by the nominal 100 kg sensor capacity; this is not a mechanical or clinical permission to apply that load.

## Verification and evidence

`verify_calibration.py <csv>` reads `reference_n,measured_n,direction` rows and fails nonfinite/missing values, inadequate reference coverage, or errors above `max(1 N, 2% of reference)` by default. These provisional engineering thresholds can be explicitly overridden and must be recorded. Loading and unloading at multiple points, repeated zero, drift over time and all three grip assemblies require physical evidence beyond this numeric check.

The included `synthetic_verification.csv` is a software fixture only. Physical calibration measurements, repeatability/drift and reboot persistence checks have not been performed without hardware.
