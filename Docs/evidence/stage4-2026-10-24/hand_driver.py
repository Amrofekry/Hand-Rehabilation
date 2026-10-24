"""Synthetic hand for by-eye checks: sends 21-landmark frames to the game's loopback port.

Usage: python hand_driver.py LEFT TOP WIDTH HEIGHT "x,y,seconds;x,y,seconds;..."
Points are desktop screenshot coordinates; the hand glides between them over the given seconds.
Synthetic input only: no camera and no person.
"""
import json, socket, sys, time

left, top, width, height = map(float, sys.argv[1:5])
waypoints = [tuple(map(float, p.split(','))) for p in sys.argv[5].split(';') if p]
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
seq = int(time.time() * 1000)

def frame(x, y):
    global seq
    seq += 1
    pts = [[x, y, 0.0] for _ in range(21)]
    pts[5] = [x - .02, y + .08, 0]; pts[6] = [x - .015, y + .055, 0]; pts[7] = [x - .008, y + .025, 0]; pts[8] = [x, y, 0]
    for j in (9, 13, 17):
        pts[j] = [x + .02, y + .06, 0]; pts[j + 1] = [x + .02, y + .025, 0]; pts[j + 2] = [x + .02, y + .04, 0]; pts[j + 3] = [x + .02, y + .05, 0]
    palm = [sum(pts[i][k] for i in (0, 5, 9, 13, 17)) / 5 for k in range(3)]
    shift = [x - palm[0], y - palm[1], 0]
    pts = [[p[0] + shift[0], p[1] + shift[1], 0] for p in pts]
    return json.dumps({"schemaVersion": 1, "handedness": "Right", "source": "mediapipe-acceptance", "confidence": .95,
                       "ageMs": 1, "sequence": seq, "captureSeconds": seq / 1000.0,
                       "landmarks": [{"x": p[0], "y": p[1], "z": p[2]} for p in pts]})

def send(sx, sy):
    x = (sx - left) / width; y = (sy - top) / height
    sock.sendto(frame(x, y).encode(), ("127.0.0.1", 27817))

prev = waypoints[0]
for point in waypoints:
    duration = max(point[2], 1 / 30)
    steps = max(1, int(duration * 30))
    for i in range(1, steps + 1):
        t = i / steps
        send(prev[0] + (point[0] - prev[0]) * t, prev[1] + (point[1] - prev[1]) * t)
        time.sleep(1 / 30)
    prev = point
print("done", seq)
