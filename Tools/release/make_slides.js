// Builds Docs/delivery/GripRehab_Final.pptx: the graduation presentation, from real in-game captures (Logs/) and
// the measured results. Run: NODE_PATH="$(npm root -g)" node Tools/release/make_slides.js <pptx skill scripts dir>
const path = require('path');
const fs = require('fs');
const pptxgen = require('pptxgenjs');
const { applyTheme } = require(path.join(process.argv[2], 'apply_theme.js'));

const ROOT = path.resolve(__dirname, '..', '..');
const OUT = path.join(ROOT, 'Docs', 'delivery', 'GripRehab_Final.pptx');
const img = rel => path.join(ROOT, rel);
const THEME = {
  name: 'GripRehab Teal', headFontFace: 'Cambria', bodyFontFace: 'Calibri',
  colors: { dk1: '13201E', lt1: 'FFFFFF', dk2: '0F3D3A', lt2: 'EEF4F2', accent1: '1F8A72', accent2: '2EC4A0',
            accent3: 'F2A541', accent4: '5B6B67', accent5: 'D7E6E2', accent6: 'C8553D', hlink: '1F8A72', folHlink: '0F3D3A' }
};
const HEX = THEME.colors;

const pres = new pptxgen();
pres.layout = 'LAYOUT_16x9'; // 10 x 5.625 in
pres.title = 'GripRehab final presentation'; pres.author = 'GripRehab capstone team';
pres.theme = { headFontFace: THEME.headFontFace, bodyFontFace: THEME.bodyFontFace };
const C = pres.SchemeColor;

pres.defineSlideMaster({ title: 'DARK_TITLE', background: { color: C.text2 }, objects: [
  { placeholder: { options: { name: 'title', type: 'title', x: .6, y: 1.55, w: 8.8, h: 1.3, fontSize: 40, bold: true, color: C.background1, valign: 'bottom', align: 'left' }, text: '' } },
  { placeholder: { options: { name: 'body', type: 'body', x: .6, y: 2.95, w: 8.8, h: 1.2, fontSize: 18, color: C.accent5, valign: 'top', align: 'left' }, text: '' } } ] });
pres.defineSlideMaster({ title: 'CONTENT', background: { color: C.background1 }, slideNumber: { x: 9.2, y: 5.2, w: .5, h: .3, fontSize: 10, color: C.accent4 }, objects: [
  { placeholder: { options: { name: 'title', type: 'title', x: .5, y: .3, w: 9, h: .75, fontSize: 30, bold: true, color: C.text2, valign: 'middle' }, text: '' } },
  { text: { text: 'GripRehab · v1.0.6', options: { x: .5, y: 5.2, w: 4, h: .3, fontSize: 10, color: C.accent4 } } } ] });

const T = (s, text, o) => s.addText(text, Object.assign({ isTextBox: true, fontSize: 14, color: C.text1, margin: 0 }, o));
const card = (s, x, y, w, h, fill) => s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: .08, fill: { color: fill || C.background2 }, line: { color: fill || C.background2 } });
const pic = (s, file, x, y, w, h) => { if (fs.existsSync(img(file))) s.addImage({ path: img(file), x, y, w, h, rounding: false }); else card(s, x, y, w, h, C.accent5); };
const dot = (s, x, y, label) => { s.addShape(pres.shapes.OVAL, { x, y, w: .42, h: .42, fill: { color: C.accent2 }, line: { color: C.accent2 } }); T(s, label, { x, y, w: .42, h: .42, fontSize: 13, bold: true, color: C.text2, align: 'center', valign: 'middle' }); };

// 1. Title
pres.addSection({ title: 'Opening' });
let s = pres.addSlide({ masterName: 'DARK_TITLE', sectionTitle: 'Opening' });
s.addText('GripRehab', { placeholder: 'title' });
s.addText('Hand-grip rehabilitation in a virtual apartment: camera hand, force sensor, adaptive activities', { placeholder: 'body' });
s.addNotes('Introduce the project in one sentence: everyday tasks in a 3D apartment, controlled by the hand in front of a webcam, with grip force from a sensor or estimated from the camera.');

// 2. Problem and goal
s = pres.addSlide({ masterName: 'CONTENT', sectionTitle: 'Opening' });
s.addText('Why grip rehabilitation needs a game', { placeholder: 'title' });
[['Repetitive', 'Grip exercises are hundreds of identical squeezes; patients lose motivation and stop.'],
 ['Hard to measure', 'Progress is judged by eye between visits; force, hold and steadiness are rarely recorded.'],
 ['Our approach', 'Real household tasks with the patient\'s own hand on camera, measured force and difficulty that adapts.']]
  .forEach(([h, b], i) => { const x = .5 + i * 3.05; card(s, x, 1.35, 2.85, 3.3); dot(s, x + .25, 1.6, String(i + 1));
    T(s, h, { x: x + .25, y: 2.2, w: 2.4, h: .5, fontSize: 20, bold: true, color: C.text2, fontFace: THEME.headFontFace });
    T(s, b, { x: x + .25, y: 2.75, w: 2.4, h: 1.7, fontSize: 14, valign: 'top' }); });
s.addNotes('Problem, measurement gap, and our answer. No clinical claim: this is a research prototype.');

// 3. Architecture
pres.addSection({ title: 'System' });
s = pres.addSlide({ masterName: 'CONTENT', sectionTitle: 'System' });
s.addText('How the system fits together', { placeholder: 'title' });
const inputs = ['Webcam → MediaPipe', 'Load cell → BLE', 'Voice commands', 'Keyboard / mouse'];
inputs.forEach((t, i) => { card(s, .5, 1.3 + i * .85, 2.3, .65, C.accent5); T(s, t, { x: .6, y: 1.3 + i * .85, w: 2.1, h: .65, valign: 'middle', fontSize: 13, bold: true, color: C.text2 }); });
const core = [['Interaction', 'grab · hold · release · 18 grips'], ['Physics', 'deformation · fracture · cutting'], ['Activities', '6 tasks × 10 levels · scoring']];
core.forEach(([h, b], i) => { const y = 1.3 + i * 1.15; card(s, 3.55, y, 2.6, .95, C.text2);
  T(s, h, { x: 3.7, y: y + .08, w: 2.3, h: .4, fontSize: 15, bold: true, color: C.background1 }); T(s, b, { x: 3.7, y: y + .48, w: 2.3, h: .4, fontSize: 12, color: C.accent5 }); });
[['Product', 'sessions · PDF/CSV export · dashboards'], ['AI', 'fatigue model (ONNX) · safe adaptation']].forEach(([h, b], i) => { const y = 1.5 + i * 1.5; card(s, 6.9, y, 2.6, 1.15);
  T(s, h, { x: 7.05, y: y + .12, w: 2.3, h: .4, fontSize: 15, bold: true, color: C.text2 }); T(s, b, { x: 7.05, y: y + .55, w: 2.3, h: .5, fontSize: 12 }); });
[[2.85, 2.55, .65], [6.2, 2.55, .65]].forEach(([x, y, w]) => s.addShape(pres.shapes.RIGHT_ARROW, { x, y, w, h: .35, fill: { color: C.accent2 }, line: { color: C.accent2 } }));
s.addNotes('Inputs on the left all reach the same interaction code. Physics and activities in the middle. Product data and the AI loop on the right.');

// 4. Camera hand pipeline
s = pres.addSlide({ masterName: 'CONTENT', sectionTitle: 'System' });
s.addText('The camera hand: from pixels to a stable grip', { placeholder: 'title' });
const steps = [['21 landmarks', 'MediaPipe, UDP, no video saved'], ['One Euro filter', 'steady at rest, fast when moving'], ['Grasp score', 'max(fist, pinch); pointing = walk'],
               ['Hysteresis gate', 'close .55 · open .28'], ['Loss grace', '0.6 s keeps the object'], ['Same entry points', 'as keyboard and voice']];
steps.forEach(([h, b], i) => { const col = i % 3, row = Math.floor(i / 3); const x = .5 + col * 3.05, y = 1.3 + row * 1.85;
  card(s, x, y, 2.85, 1.6); dot(s, x + .2, y + .2, String(i + 1));
  T(s, h, { x: x + .75, y: y + .2, w: 2, h: .45, fontSize: 16, bold: true, color: C.text2, valign: 'middle' });
  T(s, b, { x: x + .2, y: y + .8, w: 2.5, h: .7, fontSize: 13, valign: 'top' }); });
s.addNotes('Explain each step. The key ideas: filter only what is shown, keep rehab measurements raw, and never drop the object on one noisy frame.');

// 5. Results chart
pres.addSection({ title: 'Results' });
s = pres.addSlide({ masterName: 'CONTENT', sectionTitle: 'Results' });
s.addText('Camera hand: measured before and after', { placeholder: 'title' });
s.addChart(pres.charts.BAR, [{ name: 'Original', labels: ['Rest jitter (mm)', 'Sweep error (mm)', 'Settle time (ms)'], values: [17.5, 17.2, 62] },
                            { name: 'Final', labels: ['Rest jitter (mm)', 'Sweep error (mm)', 'Settle time (ms)'], values: [10.4, 11.6, 28] }], {
  x: .5, y: 1.2, w: 5.6, h: 3.8, barDir: 'col', barGrouping: 'clustered', chartColors: [HEX.accent4, HEX.accent1],
  showValue: true, dataLabelPosition: 'outEnd', dataLabelFormatCode: '0.0', dataLabelFontSize: 11, dataLabelFontFace: '+mn-lt', dataLabelColor: HEX.dk1,
  showLegend: true, legendPos: 'b', legendFontSize: 11, legendFontFace: '+mn-lt', catAxisLabelFontSize: 11, catAxisLabelFontFace: '+mn-lt',
  catAxisLabelColor: HEX.accent4, valAxisLabelColor: HEX.accent4, valAxisLabelFontSize: 10, valGridLine: { color: 'D7E6E2', size: .5 }, catGridLine: { style: 'none' },
  showTitle: true, title: 'Lower is better (worst final run shown)', titleFontSize: 12, titleColor: HEX.dk2, titleFontFace: '+mn-lt' });
[['Held through a 0.7 s tracking gap', 'no → yes'], ['Camera grab in an activity', 'no → yes'], ['Pinch grab of small items', 'no → yes'], ['False grab while pointing', 'none, before and after']]
  .forEach(([h, b], i) => { const y = 1.25 + i * .9; card(s, 6.4, y, 3.1, .75);
    T(s, h, { x: 6.55, y: y + .06, w: 2.85, h: .35, fontSize: 12, bold: true, color: C.text2 }); T(s, b, { x: 6.55, y: y + .38, w: 2.85, h: .32, fontSize: 12, color: C.accent1, bold: true }); });
T(s, 'SYNTHETIC: 30 fps hand frames with σ 0.004 landmark noise, no person. Real-hand feel is still a human check.', { x: .5, y: 4.95, w: 8.6, h: .25, fontSize: 10, italic: true, color: C.accent4 });
s.addNotes('Numbers come from Tools/run_hand_benchmark.py; final values are the worst of two recorded runs (9.9-10.4 mm, 9.9-11.6 mm, 20-28 ms). Say clearly these are synthetic frames.');

// 6. Activities grid
s = pres.addSlide({ masterName: 'CONTENT', sectionTitle: 'Results' });
s.addText('Six everyday activities, ten levels each', { placeholder: 'title' });
[['activity-1-bookshelf-during', 'Organise the bookshelf'], ['activity-2-coffeetable-during', 'Set the coffee table'], ['activity-3-storagebox-during', 'Pack the storage box'],
 ['activity-4-simplemeal-during', 'Cook a simple meal'], ['activity-5-cutting-during', 'Cut the vegetables'], ['activity-6-prepareandserve-during', 'Prepare and serve']]
  .forEach(([f, label], i) => { const col = i % 3, row = Math.floor(i / 3); const x = .5 + col * 3.05, y = 1.2 + row * 1.95;
    pic(s, 'Logs/activity-shots/' + f + '.png', x, y, 2.85, 1.6); T(s, label, { x, y: y + 1.62, w: 2.85, h: .28, fontSize: 12, bold: true, color: C.text2 }); });
s.addNotes('All six pass the automated activity probe at levels 2 and 8 (12 of 12, four clean runs on 2026-11-01). Scoring: placement accuracy in mm, errors, failed grabs and time.');

// 7. Physics
s = pres.addSlide({ masterName: 'CONTENT', sectionTitle: 'Results' });
s.addText('Objects that respond to grip force', { placeholder: 'title' });
pic(s, 'Docs/screenshots/player-1280x720/vg-ball-deformed.png', .5, 1.25, 4.4, 2.48);
pic(s, 'Docs/screenshots/player-1280x720/vg-mug-fractured.png', 5.1, 1.25, 4.4, 2.48);
T(s, 'Soft objects squash and recover (spring-damper per vertex)', { x: .5, y: 3.8, w: 4.4, h: .5, fontSize: 13, bold: true, color: C.text2 });
T(s, 'Brittle objects crack, then break past their limit (porcelain 40 N)', { x: 5.1, y: 3.8, w: 4.4, h: .5, fontSize: 13, bold: true, color: C.text2 });
T(s, '18 grip types · 6 materials · contact probes pass at 3 orientations each', { x: .5, y: 4.45, w: 9, h: .35, fontSize: 13, color: C.accent1, bold: true });
s.addNotes('The camera force curve keeps an ordinary hold between 8 and 25 N, so a cup no longer breaks by accident; a deliberate hard squeeze still can.');

// 8. AI and safety
s = pres.addSlide({ masterName: 'CONTENT', sectionTitle: 'System' });
s.addText('AI adapts difficulty, safety rules decide', { placeholder: 'title' });
[['Fatigue model', 'Trained on open EMG data (15 healthy people), exported to ONNX and run in-game with Sentis. Weak labels: early vs late task.'],
 ['Adaptive difficulty', 'Suggests the next level from performance and fatigue signals, within small steps.'],
 ['Safety policy', 'Caps every change and stops on warning signs. The AI only proposes; the rules have the last word.']]
  .forEach(([h, b], i) => { const y = 1.25 + i * 1.2; dot(s, .55, y + .1, String(i + 1));
    T(s, h, { x: 1.2, y, w: 3.5, h: .45, fontSize: 17, bold: true, color: C.text2 }); T(s, b, { x: 1.2, y: y + .45, w: 8.2, h: .6, fontSize: 13 }); });
s.addNotes('Be explicit: the model separates the start of a task from its end; it does not prove biological fatigue.');

// 9. Hardware
s = pres.addSlide({ masterName: 'CONTENT', sectionTitle: 'System' });
s.addText('Hardware ready for its first bench test', { placeholder: 'title' });
['Shape', 'Tare', 'Known load', 'Verify ↑↓', 'Save + CRC'].forEach((t, i) => { const x = .5 + i * 1.85;
  card(s, x, 1.35, 1.6, .8, i === 4 ? C.accent2 : C.accent5); T(s, t, { x, y: 1.35, w: 1.6, h: .8, fontSize: 14, bold: true, color: C.text2, align: 'center', valign: 'middle' });
  if (i < 4) s.addShape(pres.shapes.RIGHT_ARROW, { x: x + 1.62, y: 1.62, w: .21, h: .26, fill: { color: C.accent1 }, line: { color: C.accent1 } }); });
T(s, 'Calibration wizard drives the Arduino bench console step by step; verification uses independent loads, loading and unloading.', { x: .5, y: 2.35, w: 9, h: .6, fontSize: 13 });
card(s, .5, 3.1, 9, 1.55);
T(s, 'Device status panel (sensor mode)', { x: .7, y: 3.2, w: 6, h: .4, fontSize: 15, bold: true, color: C.text2 });
T(s, 'LIVE / STALE / RECONNECTED · packet rate · loss % · sample age · calibration revision. Tested with injected dropouts, stale gaps and reconnects. Physical bench trials: OPEN (protocol written).', { x: .7, y: 3.6, w: 8.6, h: .95, fontSize: 13, valign: 'top' });
s.addNotes('Arduino Nano 33 IoT, HX711, load cell, BLE to a Python bridge, UDP to Unity. Nothing physical is claimed yet.');

// 10. Quality
s = pres.addSlide({ masterName: 'CONTENT', sectionTitle: 'Results' });
s.addText('How we know it works', { placeholder: 'title' });
[['16', 'player test suites green'], ['67', 'offline hand + device tests'], ['0 / 0 / 0', 'console errors / warnings / logs'], ['32 + 88', 'pytest + Python checks']]
  .forEach(([n, l], i) => { const x = .5 + i * 2.3; card(s, x, 1.35, 2.1, 1.9);
    T(s, n, { x, y: 1.5, w: 2.1, h: .9, fontSize: 36, bold: true, color: C.accent1, align: 'center', fontFace: THEME.headFontFace });
    T(s, l, { x: x + .15, y: 2.45, w: 1.8, h: .7, fontSize: 13, align: 'center', color: C.text2 }); });
T(s, 'Installer tested: per-user install, first-run check passed with the real webcam (640×480), clean uninstall.', { x: .5, y: 3.6, w: 9, h: .5, fontSize: 14, bold: true, color: C.text2 });
T(s, 'Every result is labelled SYNTHETIC, by-eye or physical. No clinical validation is claimed.', { x: .5, y: 4.15, w: 9, h: .5, fontSize: 13, italic: true, color: C.accent4 });
s.addNotes('Automated checks run the built Windows player with synthetic inputs over the real ports; protected files are hash-pinned with reviewed changes.');

// 11. Limits and next steps
pres.addSection({ title: 'Closing' });
s = pres.addSlide({ masterName: 'CONTENT', sectionTitle: 'Closing' });
s.addText('Honest limits and what comes next', { placeholder: 'title' });
card(s, .5, 1.25, 4.35, 2.55); card(s, 5.15, 1.25, 4.35, 2.55, C.accent5);
T(s, 'Limits', { x: .7, y: 1.35, w: 4, h: .45, fontSize: 18, bold: true, color: C.text2 });
T(s, [{ text: 'Webcam depth is estimated from palm size', options: { bullet: true, breakLine: true } }, { text: 'Camera force is a visual estimate', options: { bullet: true, breakLine: true } },
      { text: 'No patient or physical bench data yet', options: { bullet: true, breakLine: true } }, { text: 'Fatigue labels are weak (healthy subjects)', options: { bullet: true } }],
  { x: .7, y: 1.85, w: 4, h: 1.8, fontSize: 14, paraSpaceAfter: 8, valign: 'top' });
T(s, 'Next', { x: 5.35, y: 1.35, w: 4, h: .45, fontSize: 18, bold: true, color: C.text2 });
T(s, [{ text: 'Bench calibration and BLE trials (O6)', options: { bullet: true, breakLine: true } }, { text: 'Real-webcam latency benchmark', options: { bullet: true, breakLine: true } },
      { text: 'Depth camera support (O5)', options: { bullet: true, breakLine: true } }, { text: 'Clinical pilot design with approvals (O7)', options: { bullet: true } }],
  { x: 5.35, y: 1.85, w: 4, h: 1.8, fontSize: 14, paraSpaceAfter: 8, valign: 'top' });
T(s, 'Optional stages: Docs/OPTIONAL_PLAN.md · research ranking: Docs/STAGE17_RESEARCH.md', { x: .5, y: 4.1, w: 9, h: .4, fontSize: 13, color: C.accent4, italic: true });
s.addNotes('Optional stages are in Docs/OPTIONAL_PLAN.md; research ranking in Docs/STAGE17_RESEARCH.md.');

// 12. Close
s = pres.addSlide({ masterName: 'DARK_TITLE', sectionTitle: 'Closing' });
s.addText('Thank you', { placeholder: 'title' });
s.addText('Questions? Live demo: camera hand in the apartment', { placeholder: 'body' });
s.addNotes('Have the installed build open in camera mode; recalibrate with C before the demo.');

fs.mkdirSync(path.dirname(OUT), { recursive: true });
pres.writeFile({ fileName: OUT }).then(() => applyTheme(OUT, THEME)).then(() => console.log('wrote', OUT));
