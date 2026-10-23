# Firmware dependency manifest

Verified build environment on 2026-09-12:

| Dependency | Version | Role |
| --- | --- | --- |
| Arduino CLI | 1.5.2-rc.1, fef6e48df | Official download resolved to this release candidate; record it explicitly |
| arduino:samd | 1.8.14 | Nano 33 IoT platform |
| ARM GCC | 7-2017q4 | Toolchain installed by SAMD core |
| HX711 Arduino Library (bogde) | 0.7.5 reviewed | Reference only; read() contains an unbounded readiness wait, so runtime uses the tested bounded HX711Ready driver |
| FlashStorage | 1.0.0 | SAMD calibration persistence |
| ArduinoBLE | 2.1.0 | BLE peripheral |
| Arduino_SpiNINA | 0.0.2 | ArduinoBLE dependency |
| Clang | 21.1.1 + LLD | Native C++ tests |
| OpenSCAD | 2021.01 portable | CAD mesh generation |
| Bleak | 3.0.2 API reviewed; not installed/tested against a radio | Optional Python hardware monitor |

Upstream sources: [ArduinoBLE](https://github.com/arduino-libraries/ArduinoBLE), [HX711](https://github.com/bogde/HX711), [FlashStorage](https://github.com/cmaglie/FlashStorage), [Bleak client API](https://bleak.readthedocs.io/en/latest/api/client.html). Read each upstream license before redistributing a binary. Download caches are excluded from Git. The two HX711 packages downloaded during evaluation are not linked into the final sketches. Runtime GPIO timing is in the project-owned HX711Ready driver, avoiding the readiness-check/read race and any unbounded acquisition wait.
