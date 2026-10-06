# Display performance — 6 October 2026

Measured in the Windows Mono release player, Direct3D 11, windowed, using isolated UI fixtures. After 90 warmup frames, each view contributes 300 measured frame intervals. Camera inference and physical BLE are disabled. These are whole-player menu samples, not isolated UI CPU/GPU time or camera latency.

Hardware: Intel Core i7-11800H, NVIDIA GeForce RTX 3060 Laptop GPU, 16,151 MB system memory. Unity 6000.4.4f1. Evidence: `ui-player-checks.json`.

| Resolution | Screen | Mean ms | p95 ms | Mean FPS |
| --- | --- | ---: | ---: | ---: |
| 1280x720 | Home | 2.32 | 3.29 | 430.2 |
| 1280x720 | Appearance | 3.34 | 5.01 | 299.0 |
| 1600x900 | Home | 2.63 | 3.37 | 380.3 |
| 1600x900 | Appearance | 2.83 | 3.63 | 353.0 |

The 60 FPS target is exceeded in these short menu samples only. Live-camera gameplay, other hardware and all routes remain unmeasured. Release GC/batch recorders report unavailable, so their zero placeholders are not measured 0 B GC or zero batches. A Development Player/Profiler capture is required for those budgets. Samples use reduced motion and solid panels and do not measure the moving High background tier. No before-redesign baseline or percentage improvement is claimed.

Contact solver timings from the native gameplay fixture are in player-checks.json. They describe that synthetic workload, not end-to-end tracking latency. Current input freshness remains bounded to 500 ms; this is an invalidation limit, not a latency measurement.
