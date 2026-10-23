# 2.2-R2 Circuit integration for the compact TAL220

This supplements the original pin map for the [R2 mechanical configuration](../2.1-interchangeable-grips/rev2/README.md). Nano D2/D3 and the firmware remain unchanged. Current SparkFun naming is **SEN-13879**; earlier project notes called it BOB-13879. Confirm the split-supply v11 schematic rather than relying on the prefix alone.

## Excitation decision

The TAL220 family specifies 5–10 V excitation. The stock SparkFun board's 20 kΩ / 8.2 kΩ regulator divider implies about **4.30 V AVDD/E+**, using its stated equation. That is below the cell's specified excitation range. Applying 5.2 V to the board's VCC does not make its E+ output 5.2 V. Sources: [cell drawing](https://cdn.sparkfun.com/datasheets/Sensors/ForceFlex/TAL220M4M5Update.pdf), [SparkFun v11 schematic](https://cdn.sparkfun.com/assets/f/5/5/b/c/SparkFun_HX711_Load_Cell.pdf).

For R2 bench bring-up, use **external bridge excitation from the measured 5.2 V boost rail**, with the board's E+ left unconnected. This avoids altering the board regulator. Connect:

| Connection | Destination |
| --- | --- |
| PowerBoost output + | Nano VIN, HX711 VCC, TAL220 red excitation+ |
| PowerBoost output ground | Nano GND, HX711 GND, TAL220 black excitation− |
| Nano 3V3 | HX711 VDD only |
| HX711 DAT | Nano D2 |
| HX711 SCK | Nano D3 |
| TAL220 green signal+ | HX711 A+ |
| TAL220 white signal− | HX711 A− |
| HX711 E+ / RED excitation output | **No connection**; insulate it; never tie it to boost output |

Use the load cell's actual lead documentation if a supplied unit differs. Keep bridge leads twisted and away from the switching converter. The battery connects to the PowerBoost JST socket with verified polarity. The board's EN-to-GND switch disables boost output; charge through its own USB connector. Disconnect boost from Nano VIN during Nano USB bring-up until the actual power paths have been checked. Charge on the bench outside the loaded grip.

This arrangement loses cancellation of excitation variation against the ADC's reference. Calibration absorbs the static ratio, but boost ripple/drift and charge-state changes can affect force accuracy. Log and compare unloaded and known-load readings across battery states; reject the arrangement if it cannot meet the declared bench tolerances. A future precision revision can use a reviewed common excitation/reference supply; do not silently alter the stock board to achieve it.

## Checks before connection and loading

Measure the boost under load and require 5.0–5.4 V for this prototype setup. Verify HX711 AVDD separately and require the measured signal common mode `(A+ + A−)/2` to lie within `1.2 V … AVDD−1.3 V`. At nominal 5.2 V bridge excitation it should be near 2.6 V; measure rather than assume. The HX711 data sheet also bounds differential input by `±0.5*AVDD/128`; nominal 4.3 V AVDD gives about ±16.8 mV, above the cell's approximately 5.2 mV rated output. These are circuit calculations, not measured results. [HX711 electrical specifications](https://cdn.sparkfun.com/datasheets/Sensors/ForceFlex/hx711_english.pdf)

Verify Nano VDD/DAT are 3.3 V logic. Record rail limits, actual values, current, ripple, thermal rise, cable strain relief and polarity in `bench_report.csv`. PowerBoost 1000C requires a battery connected for its intended load-sharing operation; use the selected protected 2000 mAh battery, whose documented charge limit accommodates its 1 A charger. [PowerBoost guide](https://learn.adafruit.com/adafruit-powerboost-1000c-load-share-usb-charge-boost), [battery source](https://www.adafruit.com/product/2011)

The existing Phidgets reference remains useful for a separate bench, but is not the R2 fabrication selection. No components have been ordered. Invoice collection, assembled rail verification, known-load calibration, stop tuning and battery/radio testing remain physical acceptance work.
