"""Hand Lab replay: play a recorded real-hand session back into the game on the same timeline.

  ai-engine/.venv-ai/Scripts/python.exe Tools/hand_replay.py Logs/hand-lab/<session> [--out DIR] [--image-size]
Starts the built player in Hand Lab replay mode (no webcam), waits until it is ready, then sends every recorded hand
packet to UDP 27817 with its original spacing. The game writes a fresh game.csv; Tools/hand_lab_report.py compares
it with the original. --image-size adds the recorded frame size to each packet (as the fixed bridge sends it).
"""
from pathlib import Path
import argparse
import json
import socket
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / 'Builds/Windows/GripRehab.exe'


def packets(session: Path, image_size: bool):
    for line in (session / 'bridge.jsonl').read_text(encoding='utf-8').splitlines():
        record = json.loads(line)
        if record.get('kind') != 'hand':
            continue
        payload = record['payload']
        if image_size and record.get('width') and record.get('height'):
            payload = dict(payload, imageWidth=record['width'], imageHeight=record['height'])
        yield record['payload']['captureSeconds'], payload


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('session', type=Path)
    parser.add_argument('--out', type=Path)
    parser.add_argument('--image-size', action='store_true')
    args = parser.parse_args()
    session = args.session if args.session.is_absolute() else ROOT / args.session
    out = args.out or session / ('replay-' + time.strftime('%Y%m%d-%H%M%S'))
    out = out if out.is_absolute() else ROOT / out
    out.mkdir(parents=True, exist_ok=True)
    recorded = list(packets(session, args.image_size))
    if not recorded:
        raise SystemExit('No hand packets in ' + str(session / 'bridge.jsonl'))
    command = [str(EXE), '-apartment-hand-lab', '-hand-lab-replay', '-hand-lab-out=' + str(out), '-force-d3d11',
               '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900', '-logFile', str(out / 'player.log')]
    with subprocess.Popen(command, cwd=ROOT) as player, socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
        deadline = time.monotonic() + 120
        while not (out / 'ready').exists():
            if time.monotonic() > deadline or player.poll() is not None:
                player.kill()
                raise SystemExit('The player did not reach the Hand Lab start')
            time.sleep(.05)
        start, first = time.monotonic(), recorded[0][0]
        for captured, payload in recorded:
            wait = start + (captured - first) - time.monotonic()
            if wait > 0:
                time.sleep(wait)
            payload = dict(payload, captureSeconds=time.monotonic())
            sock.sendto(json.dumps(payload, separators=(',', ':')).encode('utf-8'), ('127.0.0.1', 27817))
        code = player.wait(timeout=180)
    print(json.dumps({'replay': str(out), 'packets': len(recorded), 'exit': code}))


if __name__ == '__main__':
    main()
