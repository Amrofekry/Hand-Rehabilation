"""WBS 2.3 / Stage 18 guided calibration over the bench console (calibration.ino at 115200).

Steps: shape -> tare -> known load -> start -> independent verification loads (loading and unloading)
-> stop -> save -> status. Verification uses verify_calibration.evaluate, so the calibration point alone
is never called validation. The bench report records shape, revision, offset, counts/N and CRC.

--simulate runs the same steps against a software model of the console (SYNTHETIC; no hardware, no
physical claim). With a real board: --port COM5 (needs pyserial). Not clinical or physical certification.
"""
import argparse
import json
import math
import random
import re
import sys
import time
import zlib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from verify_calibration import evaluate  # noqa: E402

G = 9.80665
RESULT_NAMES = {0: 'none', 1: 'tared', 2: 'calibrated', 3: 'unstable', 4: 'timeout', 5: 'bad reference', 6: 'needs tare', 7: 'saturated'}


class SimulatedConsole:
    """Software model of BenchConsole: same commands and replies, a linear cell with noise. SYNTHETIC."""

    def __init__(self, counts_per_n=420.0, offset=8150, noise=6.0, seed=18):
        self.k, self.zero, self.noise, self.rng = counts_per_n, offset, noise, random.Random(seed)
        self.load = 0.0; self.shape = 0; self.offset = None; self.cpn = None; self.revision = 0; self.crc = 0
        self.running = False; self.saved = None; self.result = 0; self.lines = []; self.t = 0

    def raw(self): return int(round(self.zero + self.k * self.load + self.rng.gauss(0, self.noise)))

    def write(self, cmd):
        cmd = cmd.strip(); out = self.lines.append
        if cmd == 'status':
            out(f'STATUS shape={self.shape} calibrated={int(self.cpn is not None)} revision={self.revision} offset={self.offset or 0} '
                f'counts_per_N={(self.cpn or 0):.6f} crc={self.crc:X} running={int(self.running)}')
        elif cmd.startswith('shape '):
            s = int(cmd[6:]) if cmd[6:].isdigit() else 0
            if not 1 <= s <= 3: out('ERR shape 1..3'); return
            if s != self.shape: self.cpn = None
            self.shape = s; out('OK shape; recalibrate if changed')
        elif cmd == 'tare':
            if not self.shape: out('ERR select shape / collection busy'); return
            out('OK collecting tare'); self.offset = round(sum(self.raw() for _ in range(20)) / 20); self.cpn = None; self.result = 1; out('RESULT 1')
        elif cmd.startswith('cal '):
            try: n = float(cmd[4:])
            except ValueError: out('ERR cal NEWTONS'); return
            if self.offset is None or not (n > 0) or not math.isfinite(n): out('ERR reference / tare / busy'); return
            out('OK collecting known load'); mean = sum(self.raw() for _ in range(20)) / 20
            self.cpn = (mean - self.offset) / n; self.revision += 1
            self.crc = zlib.crc32(f'{self.shape},{self.revision},{self.offset},{self.cpn:.6f}'.encode()); self.result = 2; out('RESULT 2')
        elif cmd == 'start':
            ok = self.cpn is not None; self.running = ok; out('OK started' if ok else 'ERR need fresh calibrated sample')
        elif cmd == 'stop': self.running = False; out('OK stopped')
        elif cmd == 'save':
            if self.running or self.cpn is None: out('ERR stop and calibrate before save'); return
            self.saved = (self.shape, self.revision, self.offset, self.cpn, self.crc); out('OK saved and verified')
        else: out('ERR unknown command; help')

    def sample(self):
        self.t += 100; r = self.raw(); valid = self.running and self.cpn
        return f'DATA,{self.t},{r},{(r - self.offset) / self.cpn:.3f},{3 if valid else 0},{self.shape}' if valid else f'DATA,{self.t},{r},NA,0,{self.shape}'

    def readline(self, timeout):
        if self.lines: return self.lines.pop(0)
        return self.sample()


class SerialConsole:
    def __init__(self, port, baud=115200):
        try: import serial  # pyserial, only for real hardware
        except ImportError: raise SystemExit('pyserial is needed for --port: pip install pyserial')
        self.s = serial.Serial(port, baud, timeout=.2); time.sleep(2); self.s.reset_input_buffer()

    def write(self, cmd): self.s.write((cmd + '\n').encode())

    def readline(self, timeout):
        end = time.time() + timeout
        while time.time() < end:
            line = self.s.readline().decode(errors='replace').strip()
            if line: return line
        return ''


def command(dev, cmd, expect, timeout=8):
    """Send one command; return the first reply line matching expect (regex), fail on ERR or timeout."""
    dev.write(cmd); end = time.time() + timeout
    while time.time() < end:
        line = dev.readline(timeout)
        if line.startswith('DATA,'): continue
        if line.startswith('ERR'): raise RuntimeError(f'{cmd}: {line}')
        if re.match(expect, line): return line
    raise RuntimeError(f'{cmd}: no reply matching {expect}')


def wait_result(dev, want, timeout=8):
    end = time.time() + timeout
    while time.time() < end:
        line = dev.readline(timeout)
        if line.startswith('RESULT '):
            code = int(line[7:])
            if code != want: raise RuntimeError(f'result {code} ({RESULT_NAMES.get(code, "?")}), expected {want} ({RESULT_NAMES[want]})')
            return
    raise RuntimeError('no RESULT line (timeout)')


def mean_force(dev, n=10, timeout=8):
    vals = []; end = time.time() + timeout
    while len(vals) < n and time.time() < end:
        line = dev.readline(timeout)
        if line.startswith('DATA,'):
            parts = line.split(',')
            if len(parts) >= 5 and parts[3] != 'NA' and int(parts[4]) & 1: vals.append(float(parts[3]))
    if len(vals) < n: raise RuntimeError('not enough valid calibrated samples')
    return sum(vals) / len(vals)


def parse_status(line):
    return {k: v for k, v in re.findall(r'(\w+)=(\S+)', line)}


def run(dev, shape, cal_newtons, verify_loads, prompt, set_load=None):
    """The guided sequence. prompt(text) asks the operator; set_load(N) moves the simulated load (None on hardware)."""
    def place(n, text):
        if set_load: set_load(n)
        prompt(text)
    report = dict(steps=[])
    command(dev, f'shape {shape}', r'OK shape'); report['steps'].append('shape')
    place(0, 'Remove all load from the grip, keep it still, then press Enter (tare).')
    command(dev, 'tare', r'OK collecting tare'); wait_result(dev, 1); report['steps'].append('tare')
    place(cal_newtons, f'Apply the known reference force {cal_newtons:.3f} N (mass kg x {G}), then press Enter.')
    command(dev, f'cal {cal_newtons}', r'OK collecting known load'); wait_result(dev, 2); report['steps'].append('known load')
    command(dev, 'start', r'OK started'); report['steps'].append('start')
    rows = []
    for direction, loads in (('loading', sorted(verify_loads)), ('unloading', sorted(verify_loads, reverse=True))):
        for n in loads:
            place(n, f'{direction.upper()}: apply {n:.3f} N (an independent reference, not the calibration load), then press Enter.')
            rows.append(dict(reference_n=n, measured_n=round(mean_force(dev), 3), direction=direction))
    report['verification'] = evaluate(rows); report['steps'].append('verify')
    command(dev, 'stop', r'OK stopped')
    if report['verification']['passed']:
        command(dev, 'save', r'OK saved and verified'); report['steps'].append('save')
    status = parse_status(command(dev, 'status', r'STATUS '))
    saved = 'save' in report['steps']
    # Only a saved calibration gets an id in the report; a failed one is not what the board restores after reboot.
    report.update(shape=int(status.get('shape', 0)), saved=saved, calibration_id=int(status.get('revision', 0)) if saved else None,
                  offset=status.get('offset') if saved else None, counts_per_N=status.get('counts_per_N') if saved else None, crc=status.get('crc') if saved else None)
    return report


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    src = p.add_mutually_exclusive_group(required=True)
    src.add_argument('--port', help='serial port of the bench board, e.g. COM5')
    src.add_argument('--simulate', action='store_true', help='SYNTHETIC software console (no hardware)')
    p.add_argument('--shape', type=int, default=1, choices=(1, 2, 3))
    p.add_argument('--cal-kg', type=float, default=2.0, help='known calibration mass, kg')
    p.add_argument('--verify-kg', default='0,1,3', help='independent verification masses, kg (must include 0)')
    p.add_argument('--out', help='bench report JSON path')
    a = p.parse_args(argv)
    verify = [float(x) * G for x in a.verify_kg.split(',')]
    if a.simulate:
        dev = SimulatedConsole(); mode = 'SYNTHETIC simulated console; not a physical calibration'
        def set_load(n): dev.load = n
        prompt = print
    else:
        dev = SerialConsole(a.port); mode = 'PHYSICAL bench; operator-reported reference loads'; set_load = None
        prompt = lambda t: input(t + ' ')
    try: report = run(dev, a.shape, a.cal_kg * G, verify, prompt, set_load)
    except RuntimeError as e: print(f'Calibration stopped: {e}'); return 2
    report.update(mode=mode, utc=time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()))
    text = json.dumps(report, indent=2)
    if a.out: Path(a.out).parent.mkdir(parents=True, exist_ok=True); Path(a.out).write_text(text, encoding='utf-8')
    print(text)
    return 0 if report['verification']['passed'] and report['saved'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
