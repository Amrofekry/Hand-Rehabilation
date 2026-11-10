> 12 October 2026: restored the earlier VirtualGrasp prototype for Unity 2022.3.0f1 at the user's request. Later hand/contact/soft-body evidence below is historical. See [restoration report](ROLLBACK_2026_10_12.md) and [current 23 tasks](SOFTWARE_TASKS_23.md).

# GripRehab task list

> 2026-10-10 original reference import: interaction item 4 is reopened. The hand/arm and contact/grip engine have been replaced by the actual PhysicsHandsPlayground dependency. Historical DONE labels below describe prior releases, not acceptance of this new engine. See [REFERENCE_HANDS_INTEGRATION.md](REFERENCE_HANDS_INTEGRATION.md) and its measured report. No other task or future feature was added.

This file keeps the original repair task numbers 1-11. For the newly requested **full graduation software scope**, see [SOFTWARE_GRADUATION_PLAN.md](SOFTWARE_GRADUATION_PLAN.md): source review item 1 is complete; its new items 2-21 cover the remaining architecture work. Graduation item 2 source is now integrated; native acceptance remains pending as detailed in [JOURNEY_ITEM_2.md](JOURNEY_ITEM_2.md). A completed repair release does not mean those expanded requirements are implemented.

Updated 2026-10-06. Keep the original task numbers for follow-up requests. Original WBS IDs and dates are unchanged.

The overnight follow-up automation was deleted on 2026-10-06 at the user's request. Continue authorized work in the active conversation only; do not create or resume scheduled follow-ups. Those closeout hashes describe the historical repair release. Graduation item 2 has subsequent source changes; the existing Windows build has not been rebuilt for them.

## Completion pass

1. **DONE:** preserved the existing checkout, reviewed dependencies/references and ran offline compilation, metadata, managed regressions and 69 Python tests.
2. **DONE:** verified the authored scene, accessibility contrast and gallery assets in Unity 6000.4.4f1.
3. **DONE:** three native Play/Stop cycles, 168 runtime checks per cycle and all three UDP sockets released; repeated successfully after the font repair.
4. **DONE:** rebuilt Windows and tested ordinary, explicit synthetic Phase 2 and rendered acceptance modes: 168 / 169 / 168 checks. Test profiles were isolated.
5. **DONE:** repaired native TMP fallback initialization; gallery now passes 87 checks at three resolutions, including repeated scene load/cleanup. Main-menu/Appearance tests pass 60 checks at each of two resolutions. Native visual screenshots and scene edit/save/reopen/build preservation were reviewed.
6. **DONE:** reconciled current delivery reports, performance measurements, input instructions and remaining external trials. Latest evidence: [CLOSEOUT_2026_10_06.md](CLOSEOUT_2026_10_06.md).

These are software completion states, not live-human or physical-device validation. No CAD, Arabic interface, voice commands or additional research-product feature expansion was added.

| No. | Task | Current status |
| --- | --- | --- |
| 1 | Both-hand MediaPipe translation, rotation and palm/back orientation | Software and native synthetic regression complete; live trial is item 9 |
| 2 | Whole-hand pointer and five-second menu dwell | Native transport-to-button activation tested, including left hand and brief tracking flicker; human usability is item 9 |
| 3 | Keyboard grip and individual finger/joint controls; Space must not submit menus | Software/native regression complete |
| 4 | Grab, G summon, release and player collision isolation | Native physics regression complete; live camera usability is item 9 |
| 5 | Local object deformation and material-dependent breakage | Native mesh/contact/fracture regression complete; research approximations remain, with future tuning from real use |
| 6 | Imported VirtualGrasp rig, selected right/left hand and finger poses | Native SDK initialization and both-hand joint/contact tests complete; human pose fidelity is item 9 |
| 7 | English voice commands | **FUTURE WORK, NOT IMPLEMENTED.** No active speech worker, microphone menu controls or voice shortcuts |
| 8 | Editor Play/Stop, Windows build, player and UI regressions | **COMPLETE for current software**, evidence dated 6 October |
| 9 | Supervised right/left live-camera, palm-pointer, dwell and grasp trial | **PENDING USER AVAILABILITY**; include occlusion, recentering and end-to-end latency |
| 10 | Real Nano 33 IoT/HX711 calibration, BLE and load-cell trial | **PENDING HARDWARE**; synthetic replay cannot close this item |
| 11 | Supplied HandMate design across existing menus, HUD and controls | **COMPLETE for current English presentation**; native font fault repaired, themes/screenshots reviewed |

Current input: mouse, keyboard or MediaPipe whole-hand pointer. Interface: English. Graduation item 2 now integrates the local patient/doctor account routes around the current demos; their new native acceptance is pending. The original repair checklist above retains its historical scope. Full dashboards, AI and remaining module requirements stay open in the graduation list.
