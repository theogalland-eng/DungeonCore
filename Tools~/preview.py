"""Aperçu agrandi de segments choisis : python preview.py <segments_json_name> <ids ex: 1,2,5-9> <out.png> [scale]"""
import json, os, sys
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def parse(ids):
    out = []
    for part in ids.split(','):
        if '-' in part:
            a, b = part.split('-')
            out += list(range(int(a), int(b) + 1))
        elif part:
            out.append(int(part))
    return out


def main():
    name, ids, out = sys.argv[1], parse(sys.argv[2]), sys.argv[3]
    scale = int(sys.argv[4]) if len(sys.argv) > 4 else 3
    d = json.load(open(os.path.join(ROOT, 'Tools~', 'segments', name + '.json')))
    im = Image.open(os.path.join(ROOT, 'Assets', 'imports', 'new', d['source']) if not os.path.exists(d['source']) else d['source']).convert('RGBA')
    crops = [(i, im.crop((b[0], b[1], b[0] + b[2], b[1] + b[3]))) for i, b in enumerate(d['boxes']) if i in ids]
    W = sum(c.width * scale + 10 for _, c in crops)
    H = max(c.height * scale for _, c in crops) + 14
    o = Image.new('RGBA', (W, H), (70, 60, 80, 255))
    dr = ImageDraw.Draw(o)
    x = 0
    for i, c in crops:
        o.alpha_composite(c.resize((c.width * scale, c.height * scale), Image.NEAREST), (x, 14))
        dr.text((x, 0), f'{i}:{c.width}x{c.height}', fill=(255, 255, 0, 255))
        x += c.width * scale + 10
    o.save(out)


if __name__ == '__main__':
    main()
