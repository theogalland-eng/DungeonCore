"""
Genere les atlas de murs auto-connectes (AutoWallTile) de DungeonCore.
Usage : python "Tools~/make_walls.py" [preview_dir]
Sortie : Assets/Art/Generated/Walls/walls_<theme>.png  (128 variantes 16x16, 16 colonnes x 8 lignes)

Masque (bit = voisin mur) : 1 N, 2 E, 4 S, 8 W, 16 SS (2 cases au sud), 32 SW, 64 SE.
Chaque case est composee de 4 quarts (8x8) pris dans des "bandes" d'un bloc source :
  T  = bord superieur du dessus du mur      D  = dessus (interieur)
  BT = bas du dessus + debut de la face      F  = face (16 lignes, base comprise)
  colonnes : L (bord gauche), IL/IR (interieur, periode 16), R (bord droit)
"""
import glob, os, sys
from PIL import Image, ImageEnhance

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
IMP = os.path.join(ROOT, "Assets", "imports")
OUT = os.path.join(ROOT, "Assets", "Art", "Generated", "Walls")


def load(pattern):
    return Image.open(glob.glob(os.path.join(IMP, pattern))[0]).convert("RGBA")


class Style:
    """Bloc source : image + position (y) des bandes et (x) des colonnes."""
    def __init__(self, img, bands, cols):
        self.img, self.bands, self.cols = img, bands, cols

    def quad(self, band, col, half=0):
        y = self.bands[band] + half * 8
        x = self.cols[col]
        return self.img.crop((x, y, x + 8, y + 8))


def donjon():
    im = load("craftpix-net-169442*/PNG/walls_floor.png")
    return Style(im, {"T": 0, "D": 16, "BT": 36, "F": 62}, {"L": 0, "IL": 16, "IR": 24, "R": 40})


def catacomb():
    im = load("new/craftpix-net-298079*/PNG/walls_floor.png")
    return Style(im, {"T": 0, "D": 16, "BT": 36, "F": 58}, {"L": 0, "IL": 16, "IR": 24, "R": 40})


def temple():
    src = load("craftpix-net-160005*/PNG/Walls_floor.png")
    can = Image.new("RGBA", (48, 40))

    def put(box, dst):
        can.paste(src.crop(box), dst)
    # colonnes du pilier : L 12..20, I 20..28, R 28..36
    for y0, dy in ((12, 0), (16, 8), (31, 16)):
        put((12, y0, 20, y0 + 8), (0, dy))
        put((20, y0, 28, y0 + 8), (8, dy))
        put((20, y0, 28, y0 + 8), (16, dy))
        put((28, y0, 36, y0 + 8), (40, dy))
    # face : frise + briques du grand mur (periode 16 a partir de x=20), bords = pilier
    for x_dst, x_src in ((8, 20), (16, 28)):
        put((x_src, 101, x_src + 8, 113), (x_dst, 24))
        put((x_src, 138, x_src + 8, 142), (x_dst, 36))
    put((12, 37, 20, 49), (0, 24)); put((12, 74, 20, 78), (0, 36))
    put((28, 37, 36, 49), (40, 24)); put((28, 74, 36, 78), (40, 36))
    return Style(can, {"T": 0, "D": 8, "BT": 16, "F": 24}, {"L": 0, "IL": 8, "IR": 16, "R": 40})


def cave():
    """Roche naturelle : texture pavee sombre, levre claire, face ombree."""
    snow = load("snow-expansion.png")
    tex = snow.crop((304, 128, 320, 144))
    top = Image.new("RGBA", (48, 40))
    for x in range(0, 48, 16):
        for y in range(0, 40, 16):
            top.paste(tex, (x, y))
    face = ImageEnhance.Color(top).enhance(0.65)
    top = ImageEnhance.Brightness(ImageEnhance.Color(top).enhance(0.5)).enhance(0.55)
    can = Image.new("RGBA", (48, 40))
    can.paste(top.crop((0, 0, 48, 24)), (0, 0))       # T, D, (haut de BT)
    can.paste(face.crop((0, 0, 48, 16)), (0, 24))     # F
    px = can.load()
    OUT_C = (12, 11, 16, 255)

    def shade(x, y, f):
        r, g, b, a = px[x, y]
        px[x, y] = (min(255, int(r * f)), min(255, int(g * f)), min(255, int(b * f)), a)

    # face : degrade vertical + stries
    for y in range(24, 40):
        f = 1.15 - (y - 24) / 16 * 0.55
        for x in range(48):
            shade(x, y, f * (0.85 if (x * 7 + y // 3) % 11 == 0 else 1.0))
    # BT : bas du dessus (16..20) puis levre (20..21) puis debut de face (21..24)
    for x in range(48):
        for y in range(21, 24):
            px[x, y] = px[x, y + 8]
        shade(x, 20, 2.6)
        shade(x, 21, 1.5)
        shade(x, 19, 0.7)
    # bord superieur (T) : contour + levre claire
    for x in range(48):
        px[x, 0] = OUT_C
        shade(x, 1, 1.9); shade(x, 2, 1.45)
    # bords gauche/droit (toutes bandes)
    for y in range(40):
        px[0, y] = OUT_C; px[47, y] = OUT_C
        if y > 0:
            shade(1, y, 1.8); shade(2, y, 1.35)
            shade(46, y, 1.6); shade(45, y, 1.25)
    # base de la face : ombre au pied
    for x in range(48):
        px[x, 39] = OUT_C
        shade(x, 38, 0.6)
    return Style(can, {"T": 0, "D": 8, "BT": 16, "F": 24}, {"L": 0, "IL": 8, "IR": 16, "R": 40})


def compose(st, m):
    n, e, s, w, ss, sw, se = [(m >> i) & 1 for i in range(7)]
    img = Image.new("RGBA", (16, 16))
    if not s:
        # face du mur (le sud est ouvert)
        up = ("F", 0) if n else ("T", 0)
        lo = ("F", 1)
        left = "IL" if (w and not sw) else "L"      # la face continue si le voisin est aussi une face
        right = "IR" if (e and not se) else "R"
        lu, ru = (left, right) if n else ("IL" if w else "L", "IR" if e else "R")
    else:
        up = ("D", 0) if n else ("T", 0)
        lo = ("BT", 0) if not ss else ("D", 0)
        left = lu = "IL" if w else "L"
        right = ru = "IR" if e else "R"
    img.paste(st.quad(up[0], lu, up[1]), (0, 0))
    img.paste(st.quad(up[0], ru, up[1]), (8, 0))
    img.paste(st.quad(lo[0], left, lo[1]), (0, 8))
    img.paste(st.quad(lo[0], right, lo[1]), (8, 8))
    return img


def tint(img, rgb):
    r, g, b, a = img.split()
    r = r.point(lambda v: min(255, int(v * rgb[0])))
    g = g.point(lambda v: min(255, int(v * rgb[1])))
    b = b.point(lambda v: min(255, int(v * rgb[2])))
    return Image.merge("RGBA", (r, g, b, a))


THEMES = {
    # id : (style, teinte)
    "volcanic": ("cave", (0.66, 0.6, 0.6)),
    "lava": ("cave", (1.0, 0.52, 0.4)),
    "aqua": ("temple", (0.62, 0.8, 1.0)),
    "ice": ("cave", (0.8, 0.95, 1.15)),
    "fungal": ("cave", (0.62, 0.82, 0.55)),
    "crystal": ("temple", (0.82, 0.66, 1.05)),
    "crypt": ("donjon", (0.82, 0.92, 0.84)),
    "fortress": ("donjon", (1.0, 1.0, 1.0)),
    "catacomb": ("catacomb", (1.1, 1.0, 0.95)),
}

SAMPLE = [
    "##################",
    "##################",
    "###.......########",
    "###.......####.###",
    "###..........#.###",
    "###.......##...###",
    "######.#######.###",
    "######.#.......###",
    "#####...........##",
    "#####.....#.....##",
    "#####...........##",
    "##################",
    "##################",
]


def preview(atlas, path):
    H, W = len(SAMPLE), len(SAMPLE[0])
    img = Image.new("RGBA", (W * 16, H * 16), (70, 66, 76, 255))

    def wall(x, y):
        if x < 0 or y < 0 or x >= W or y >= H:
            return True
        return SAMPLE[y][x] == "#"
    for y in range(H):
        for x in range(W):
            if not wall(x, y):
                continue
            m = (wall(x, y - 1) << 0) | (wall(x + 1, y) << 1) | (wall(x, y + 1) << 2) | (wall(x - 1, y) << 3) \
                | (wall(x, y + 2) << 4) | (wall(x - 1, y + 1) << 5) | (wall(x + 1, y + 1) << 6)
            t = atlas.crop(((m % 16) * 16, (m // 16) * 16, (m % 16) * 16 + 16, (m // 16) * 16 + 16))
            img.alpha_composite(t, (x * 16, y * 16))
    img.resize((img.width * 3, img.height * 3), Image.NEAREST).save(path)


def main():
    styles = {"donjon": donjon(), "temple": temple(), "cave": cave(), "catacomb": catacomb()}
    os.makedirs(OUT, exist_ok=True)
    preview_dir = sys.argv[1] if len(sys.argv) > 1 else None
    for tid, (sname, rgb) in THEMES.items():
        atlas = Image.new("RGBA", (256, 128))
        for m in range(128):
            atlas.paste(compose(styles[sname], m), ((m % 16) * 16, (m // 16) * 16))
        atlas = tint(atlas, rgb)
        atlas.save(os.path.join(OUT, f"walls_{tid}.png"))
        if preview_dir:
            preview(atlas, os.path.join(preview_dir, f"prev_{tid}.png"))
    print("ok", len(THEMES))


if __name__ == "__main__":
    main()
