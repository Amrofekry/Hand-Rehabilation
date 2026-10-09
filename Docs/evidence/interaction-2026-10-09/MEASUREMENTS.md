# Final synthetic measurements

These are local software fixtures, not real camera, BLE or material-validation results.

## Moving contact

| Object | Hand | Mean ms | p95 ms | Maximum ms | Independent skin-depth maximum mm |
| --- | --- | ---: | ---: | ---: | ---: |
| Silicone ball | Right | 10.08 | 13.67 | 16.48 | 0.545 |
| Porcelain mug | Right | 35.30 | 69.85 | 75.54 | 0.500 |
| Brittle cracker | Right | 8.62 | 13.23 | 17.87 | 0.596 |
| Kitchen sponge | Right | 12.85 | 23.58 | 28.53 | 0.792 |
| Silicone cup | Right | 36.59 | 54.22 | 59.35 | 0.750 |
| Ripe tomato | Right | 10.12 | 12.91 | 15.09 | 0.551 |
| Silicone ball | Left | 10.87 | 14.93 | 17.54 | 0.586 |
| Porcelain mug | Left | 25.22 | 31.45 | 36.16 | 0.500 |
| Brittle cracker | Left | 10.20 | 14.68 | 18.17 | 0.562 |
| Kitchen sponge | Left | 14.35 | 23.34 | 27.30 | 0.713 |
| Silicone cup | Left | 30.78 | 44.94 | 77.90 | 0.743 |
| Ripe tomato | Left | 12.72 | 15.65 | 19.13 | 0.574 |

Maximum reported skin-vertex penetration: 0.799 mm. This is not proof of zero continuous triangle/edge intersections.

## Example right-hand keyboard material responses

Maximum deformation and remaining displacement after the fixed recovery interval; brittle rows fracture, so their remaining displacement is not elastic recovery.

| Object | Maximum mm | Remaining mm | Fractured |
| --- | ---: | ---: | --- |
| Silicone ball | 10.16 | 0.01 | No |
| Porcelain mug | 0.00 | 0.00 | Yes |
| Brittle cracker | 2.16 | 2.16 | Yes |
| Kitchen sponge | 30.00 | 2.87 | No |
| Silicone cup | 18.01 | 0.07 | No |
| Ripe tomato | 23.50 | 6.71 | No |

36 cases / 639 checks passed. Fixture-wide average: 128.56 FPS; contact-loop average: 4.93 ms. The average includes easy/waiting periods. Read moving-contact p95/max above before interpreting this as gameplay performance.

## Captures

Isolated object images: `0` ball, `1` porcelain mug, `2` cracker, `3` sponge, `4` silicone cup, `5` tomato. `before`, `squeeze`, `maximum`, `released`, `cracks`, and `fracture` identify stages. `regression-images` retains the final 1280x720 hand/object and UI regression captures.
