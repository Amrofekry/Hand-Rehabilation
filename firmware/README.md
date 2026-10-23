# W2 firmware and bench tools

Both sketches target **Arduino Nano 33 IoT**, FQBN `arduino:samd:nano_33_iot`. The calibration sketch is USB-only; the streaming sketch adds BLE and keeps the same USB calibration console. Both boot headlessly, initially stopped, and require explicit start. Actual board flashing is not performed until a board is available.

## Reproducible setup and compile

Install [Arduino CLI](https://arduino.github.io/arduino-cli/1.3/installation/) and Python 3.10+; for host C++ tests use a working C++11 compiler. This workspace has an ignored project-local toolchain. `setup.ps1` initializes paths relative to the current clone, installs exact Arduino dependencies, and never flashes hardware.

```powershell
./firmware/setup.ps1 -ArduinoCli arduino-cli
./firmware/build.ps1 -ArduinoCli arduino-cli
python firmware/run_tests.py
python firmware/2.4-wireless-streaming/host/monitor.py --demo
```

In this workspace, the CLI path is `firmware/.tools/arduino-cli/arduino-cli.exe`. The installed host compiler is Clang with LLD, so the test runner selects `clang++ -fuse-ld=lld`. If G++ is selected instead, it must have a working linker. Build outputs and downloaded dependencies are ignored; no absolute user paths are committed in a configuration file.

After a device is available: use `arduino-cli board list --config-file firmware/arduino-cli.yaml`, then the Arduino IDE or CLI to upload the selected sketch to the explicitly identified COM port. Disconnect unverified external battery/boost power before USB programming. Use [2.3 calibration instructions](2.3-load-cell-calibration/README.md), then [2.4 BLE bench protocol](2.4-wireless-streaming/README.md).

The firmware persists calibration only after `save`; reflashing may erase that record. Always verify `status` and known reference readings after flashing. Firmware source does not contain a fabricated calibration constant, patient identity, fatigue model, or clinical exercise limit.
