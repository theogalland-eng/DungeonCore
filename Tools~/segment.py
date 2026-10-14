"""
Découpe automatique des planches d'objets : chaque objet (composante opaque, ombre comprise)
devient un rectangle. Sortie : Tools~/segments/<planche>.json + aperçu numéroté.
Usage : python "Tools~/segment.py" <png> [dilatation=2] [preview_dir]
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SEG = os.path.join(ROOT, "Tools~", "segments")


def segment(path, dil=2, min_size=6):
    im = Image.open(path).convert("RGBA")
    a = np.array(im)[:, :, 3] > 8
    mask = ndimage.binary_dilation(a, iterations=dil) if dil > 0 else a
    lab, n = ndimage.label(mask)
    boxes = []
    for sl in ndimage.find_objects(lab):
        y0, y1 = sl[0].start, sl[0].stop
        x0, x1 = sl[1].start, sl[1].stop
        # resserrer sur les pixels réels
        sub = a[y0:y1, x0:x1]
        ys, xs = np.nonzero(sub)
        if len(xs) == 0:
            continue
        x0, x1, y0, y1 = x0 + xs.min(), x0 + xs.max() + 1, y0 + ys.min(), y0 + ys.max() + 1
        if (x1 - x0) < min_size and (y1 - y0) < min_size:
            continue
        boxes.append([int(x0), int(y0), int(x1 - x0), int(y1 - y0)])
    boxes.sort(key=lambda b: (b[1] // 16, b[0]))
    return im, boxes


def main():
    path = sys.argv[1]
    dil = int(sys.argv[2]) if len(sys.argv) > 2 else 2
    prev = sys.argv[3] if len(sys.argv) > 3 else None
    im, boxes = segment(path, dil)
    os.makedirs(SEG, exist_ok=True)
    name = os.path.splitext(os.path.basename(path))[0]
    json.dump({"source": path.replace("\\", "/"), "size": im.size, "boxes": boxes},
              open(os.path.join(SEG, name + ".json"), "w"), indent=0)
    print(name, len(boxes), "objets")
    if prev:
        s = 2 if max(im.size) < 700 else 1
        bg = Image.new("RGBA", im.size, (70, 60, 80, 255))
        bg.alpha_composite(im)
        bg = bg.resize((im.width * s, im.height * s), Image.NEAREST)
        d = ImageDraw.Draw(bg)
        for i, (x, y, w, h) in enumerate(boxes):
            d.rectangle([x * s, y * s, (x + w) * s - 1, (y + h) * s - 1], outline=(255, 255, 0, 255))
            d.text((x * s + 1, y * s), str(i), fill=(0, 255, 255, 255))
        bg.save(os.path.join(prev, "seg_" + name + ".png"))


if __name__ == "__main__":
    main()
