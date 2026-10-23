"""Render an exploded cylinder illustration from the exported STL geometry.

Requires Pillow; no geometry, dimensional or strength validation is implied.
"""
from pathlib import Path
import math
import re
from PIL import Image, ImageDraw, ImageFont

BASE = Path(__file__).resolve().parent


def project(point):
    x, y, z = point
    a = math.radians(32)
    u = x * math.cos(a) - y * math.sin(a)
    v = x * math.sin(a) + y * math.cos(a)
    return u, v * .52 - z * .85, v * .85 + z * .52


def main():
    faces = []
    parts = [('cylinder_bottom', 0, (66, 139, 184)),
             ('tray', 8, (89, 104, 120)),
             ('lid', 34, (164, 180, 194)),
             ('cylinder_top', 74, (79, 164, 206))]
    for name, offset, color in parts:
        source = (BASE / 'exports' / (name + '.stl')).read_text()
        vertices = [tuple(map(float, p)) for p in re.findall(
            r'vertex\s+([-\d.e+]+)\s+([-\d.e+]+)\s+([-\d.e+]+)', source)]
        for i in range(0, len(vertices), 3):
            tri = [project((x, y, z + offset)) for x, y, z in vertices[i:i+3]]
            a, b, c = vertices[i:i+3]
            ab = [b[j] - a[j] for j in range(3)]
            ac = [c[j] - a[j] for j in range(3)]
            n = (ab[1]*ac[2]-ab[2]*ac[1], ab[2]*ac[0]-ab[0]*ac[2], ab[0]*ac[1]-ab[1]*ac[0])
            length = math.sqrt(sum(t*t for t in n)) or 1
            light = .65 + .35 * abs((n[0]*.25-n[1]*.35+n[2]*.90)/length)
            faces.append((sum(p[2] for p in tri)/3, tri, tuple(min(255, int(t*light)) for t in color)))
    points = [p for _, tri, _ in faces for p in tri]
    xmin, xmax = min(p[0] for p in points), max(p[0] for p in points)
    ymin, ymax = min(p[1] for p in points), max(p[1] for p in points)
    scale = min(760/(xmax-xmin), 750/(ymax-ymin))
    img = Image.new('RGB', (1200, 1080), '#f1f5f9')
    draw = ImageDraw.Draw(img)
    for _, tri, color in sorted(faces, key=lambda f: f[0]):
        draw.polygon([(600+(x-(xmin+xmax)/2)*scale, 160+(y-ymin)*scale) for x, y, _ in tri], fill=color)
    font_path = Path('C:/Windows/Fonts/segoeui.ttf')
    def font(size):
        return ImageFont.truetype(str(font_path), size) if font_path.exists() else ImageFont.load_default()
    draw.text((48, 30), '2.1  Universal grip | exploded fit prototype', font=font(32), fill='#16324f')
    draw.text((48, 82), 'Cylinder shell / lid / tray / cylinder shell', font=font(22), fill='#475569')
    draw.text((48, 966), 'Illustrative separation; units in the CAD source are millimetres.', font=font(22), fill='#475569')
    draw.text((48, 1004), 'No fitted electronics or load cell. Not a load-rated design.', font=font(23), fill='#9a3412')
    target = BASE / 'assembly-preview.png'
    img.save(target)
    print(target)


if __name__ == '__main__':
    main()
