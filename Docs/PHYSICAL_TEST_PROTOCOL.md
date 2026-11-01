# Physical test protocol (Stage 18, 2026-11-01)

Hardware readiness without hardware: the software side is ready and tested on SYNTHETIC input. **Every item below is OPEN** until a person runs it on the real device and fills in its template in `Docs/evidence/physical/`. Nothing here is clinical validation; these are engineering bench checks.

| Tool | What it covers | Tested now |
|---|---|---|
| `firmware/2.3-load-cell-calibration/calibration_wizard.py` | Guided shape → tare → known load → start → independent loading/unloading verification → stop → save → status; writes the bench report JSON | SYNTHETIC `--simulate` (10 tests in `tests/2.3-calibration`) |
| `verify_calibration.py` | Error limit `max(1 N, 2 % of reference)`, zero plus at least two loads in both directions | Unit tests |
| Device status panel (sensor mode, top right) | LIVE / STALE / WAITING / RECONNECTED, rate, loss, age, reconnects, calibration revision; battery is not in protocol v1 | 12 offline tests (injected dropouts, stale gaps, reconnects) |
| `Tools/Phase2Bridge/unity_bridge.py --demo` | Replay through the same UDP path as BLE | Bridge unit tests |

## P1 Load-cell calibration (per grip shape: sphere 1, cylinder 2, pencil 3)
1. Mount the reviewed fixture. Power the board and let it settle for 5 min.
2. Run: `python firmware/2.3-load-cell-calibration/calibration_wizard.py --port COM5 --shape 1 --cal-kg 2 --verify-kg 0,1,3 --out Docs/evidence/physical/calibration-shape1.json`
3. Use certified masses only. The wizard converts kg × 9.80665 to N. The verification masses must differ from the calibration mass.
4. **Pass:** the report has `"saved": true` and verification `"passed": true`. Record the mass certificates in the template.
5. Reboot the board and run `status`. The revision and CRC must match the report (persistence).

## P2 Repeatability and drift
- Run zero, then 1 kg, then zero, 10 times. Report the mean and SD at each point.
- Hold 3 kg for 10 min and log the drift each minute.
- Template: `Docs/evidence/physical/repeatability.csv` (`trial,reference_n,measured_n`).

## P3 BLE link
- Start `unity_bridge.py`, then sensor mode in the game. The device status panel must show LIVE.
- Rate ≥ 9 Hz at the 10 SPS jumper (≥ 72 Hz at 80 SPS). Loss < 1 % over 10 min at 1 m. Age < 150 ms.
- Walk to 5 m and back: STALE must show within 0.5 s of loss, and RECONNECTED after recovery. The object is released, never frozen.
- Template: `Docs/evidence/physical/ble-link.md` (screenshots of the panel at each step).

## P4 End-to-end game check
- Squeeze 5, 15 and 30 N on the calibrated grip. The HUD force must be within the P1 limits.
- Cup at 40 N (porcelain) must break; a sponge must squash and recover.
- Template: `Docs/evidence/physical/game-force.md`.

## P5 Camera hand (human feel)
- Run `Docs/HAND_CONTROLS.md` with a real webcam, 3 lighting levels, both hands.
- Record: grabs attempted/succeeded, false grabs, drops, comfort 1–5.
- Template: `Docs/evidence/physical/camera-hand.md`. The SYNTHETIC numbers in `Docs/HAND_CONTROL_REPORT.md` do not replace this.

## P6 Voice and audio
- Every voice command in `Docs/VOICE_COMMANDS.md`, in a quiet room and with background TV. Record recognised/attempted.
- Listening check of all sounds at 3 volume levels.
- Template: `Docs/evidence/physical/voice-audio.md`.
