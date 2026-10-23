# 2.2 Concrete from-zero bench sourcing list

**R2 selection update:** the latest mechanical package selects the 100 kg alloy-steel TAL220 configuration matching its manufacturer drawing, retains the Adafruit 2011 battery and 2465 power board, and enlarges the carrier. Use the [R2 package](../2.1-interchangeable-grips/rev2/README.md) and its [specific circuit](REV2_CIRCUIT.md). The Phidgets reference and small R1 enclosure below are historical bench alternatives. Seller availability and real assembly have not been verified.

This list supplies named starting parts rather than asking the user to identify existing hardware. It is a **bench reference set**, not a verified fit BOM for the small enclosure. No purchase was placed. Keep invoices and verify seller authenticity/part revision before ordering.

| Qty | Part | Reason and constraint |
| --- | --- | --- |
| 1 | [Arduino Nano 33 IoT ABX00027](https://docs.arduino.cc/hardware/nano-33-iot/) | Required MCU; 3.3 V GPIO; USB data cable needed. |
| 1 | [SparkFun HX711 BOB-13879](https://www.sparkfun.com/sparkfun-load-cell-amplifier-hx711.html) | Documented separate VCC/VDD rails. |
| 1 | [Phidgets FRC4123_0 single-point 100 kg cell](https://www.phidgets.com/?prodid=1298) | Concrete full-bridge bench candidate: 130 mm length, M6 mounting, rated 2 mV/V output. It exceeds the provisional 80 mm cell bay; revise the CAD after reviewing its mechanical drawing, or source a documented smaller 100 kg cell. Manufacturer page specifies bridge wiring and a 12 V maximum supply; confirm behavior at the actual HX711 excitation voltage during calibration. |
| 1 | [Adafruit PowerBoost 1000C, product 2465](https://learn.adafruit.com/adafruit-powerboost-1000c-load-share-usb-charge-boost) | Documented charger/load sharing/boost reference; output is nominal 5.2 V, not exactly 5.0 V. Charge current up to 1 A. Use output for VIN/analog VCC only; verify actual rails and thermal/current budget. |
| 1 | [Adafruit protected 3.7 V 2000 mAh battery, product 2011](https://www.adafruit.com/product/2011) | Documented protection and JST polarity, 60 x 36 x 7 mm. The manufacturer permits charge at 2 A or less and recommends lower rates; the 1 A charger is below that stated limit, but thermal integration must be checked. This 36 mm width exceeds the current tray's 26 mm inner width; it is a bench reference until the enclosure is redesigned. |
| 1 each | Micro-USB data cable; suitable USB charger for power module; multimeter; current-limited bench supply | Program/test power separately before integrating battery. Use the power module's specified supply capability. |
| as needed | Breadboard for unloaded bring-up; soldered connectors for assembled unit; M6 cell fasteners matching drawing; M3 shell fasteners; insulation and strain relief | Do not transmit grip force through a breadboard, battery, PCB or printed fit-only adapter. |
| lab set | Known masses/reference force fixture, calipers, printer access | Physical verification cannot be substituted by the synthetic CSV or STL closure check. |

## Go/no-go before final grip fabrication

The first reference set proves software and electrical interfaces can be tested with obtainable, documented parts. It does not close the packaging problem. The 130 mm cell and 36 mm-wide battery do not fit the illustrative small core. Choose a larger common core (with larger shapes), a verified compact alternative cell/battery, or a remote-electronics architecture. The last option changes the supplied architecture; do not silently adopt it. This is the principal remaining design decision for fully completing 2.1/2.2, and is recorded in the delivery report.

The diagram's claimed same-core Sphere/Cylinder/Pencil packaging has no source dimensions. A fabricated small load-cell part number would conceal this conflict. Keep the parametric fit meshes explicitly separate from a released, fully populated mechanical design.
