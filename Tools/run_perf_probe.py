"""Stage 7 performance, soak and fault-injection runner for the rendered Windows player.

Synthetic input only, isolated acceptance storage, never a physical webcam or device.
  python Tools/run_perf_probe.py                 # 20 s per module at 1920x1080
  python Tools/run_perf_probe.py --seconds 1800  # 30-minute soak per module
  python Tools/run_perf_probe.py --ports-busy    # ports 27817/27818 already taken
"""
from pathlib import Path
import argparse
import json
import socket
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / 'Builds/Windows/GripRehab.exe'


def run(seconds, ports_busy, width=1920, height=1080, solver_off=False):
    started = time.time()
    name = 'perf-probe-ports' if ports_busy else ('perf-soak' if seconds >= 300 else 'perf-probe')
    log = ROOT / f'Logs/{name}.log'
    result_path = ROOT / ('Logs/perf-probe-ports.json' if ports_busy else 'Logs/perf-probe.json')
    command = [str(EXE), '-apartment-perf-probe', '-force-d3d11', '-screen-fullscreen', '0',
               '-screen-width', str(width), '-screen-height', str(height),
               '-perf-seconds', str(seconds), '-logFile', str(log)]
    if ports_busy:
        command.append('-perf-ports-busy')
    if solver_off:
        command.append('-contact-solver-off')  # Stage 8 A/B: the contact solver's own cost
    held = []
    try:
        if ports_busy:
            for port in (27817, 27818):
                s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                s.bind(('127.0.0.1', port))
                held.append(s)
        with subprocess.Popen(command, cwd=ROOT) as process:
            try:
                code = process.wait(timeout=seconds * 4 + 240)
            except subprocess.TimeoutExpired:
                process.terminate()
                raise RuntimeError('Player hung: no exit before the timeout')
    finally:
        for s in held:
            s.close()
    if not result_path.exists() or result_path.stat().st_mtime < started:
        raise RuntimeError('Player did not write fresh probe output; ensure current scripts were built')
    result = json.loads(result_path.read_text(encoding='utf-8-sig'))
    output = log.read_text(errors='replace')
    destination = ROOT / f'Docs/{name}.json'
    destination.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    if code or not result['passed'] or '[REHAB_PERF] PASS' not in output:
        raise RuntimeError(f'Probe failed: exit {code}, {result.get("detail", "")}')
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--seconds', type=int, default=20)
    parser.add_argument('--ports-busy', action='store_true')
    parser.add_argument('--solver-off', action='store_true')
    args = parser.parse_args()
    result = run(args.seconds, args.ports_busy, solver_off=args.solver_off)
    summary = {'passed': result['passed'], 'size': f"{result['width']}x{result['height']}", 'quality': result['quality'],
               'phases': [{k: p[k] for k in ('name', 'fpsMean', 'frameP95Ms', 'fpsP95Floor', 'gcBytesMean', 'gcBytesP95',
                                             'managedGrowthPercent', 'totalGrowthPercent', 'gen0Collections', 'errors', 'iterations')}
                          for p in result['phases']],
               'handLatencyP50Ms': result['handLatencyP50Ms'], 'handLatencyP95Ms': result['handLatencyP95Ms'],
               'malformed': [result['malformedSent'], result['malformedRejected'], result['faultErrors']],
               'ports': [result['visionPortProblem'], result['forcePortStatus']]}
    print(json.dumps(summary, indent=1))


if __name__ == '__main__':
    main()
