"""
Effets visuels (packs craftpix de Assets/imports/effects) -> bandes d'animation régulières
Assets/Resources/FX/<nom>_<L>x<H>.png (chargées à l'exécution par FxLibrary).
Chaque effet : images triées par numéro, rognées sur l'enveloppe commune, réduites si trop grandes.
Usage : python "Tools~/crop_fx.py"
"""
import glob, os, re
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FX = os.path.join(ROOT, 'Assets', 'imports', 'effects')
NEW = os.path.join(ROOT, 'Assets', 'imports', 'new')
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'FX')

# nom : (dossier ou planche, taille max en pixels, [images à garder])
SETS = {
    'fireball': ('craftpix-net-819691*/PNG/Fireball', 48, None),
    'explosion': ('craftpix-671189-pixel-art*/PNG/Explosion', 96, None),
    'heal': ('craftpix-net-207889*/Life Recovery/PNG', 72, None),
    'revival': ('craftpix-net-207889*/Revival/PNG', 80, None),
    'buff': ('craftpix-net-207889*/Strength Buff/PNG', 72, None),
    'debuff': ('craftpix-net-207889*/Debuff/PNG', 72, None),
    'mana': ('craftpix-net-207889*/Mana Recovery/PNG', 72, None),
    'smoke_rise': ('craftpix-net-526500*/PNG/Rising_smoke', 64, None),
    'smoke_column': ('craftpix-net-526500*/PNG/Cycled_smoke_long', 129, None),
    'smoke_ring': ('craftpix-net-526500*/PNG/Smoke_ring/Smoke_ring1', 64, None),
    'smoke_circle': ('craftpix-net-526500*/PNG/Circle_smoke', 80, None),
    'smoke_ghost': ('craftpix-net-526500*/PNG/Smoke_ghost', 80, None),
    'smoke_skull': ('craftpix-net-526500*/PNG/Smoke_scull', 80, None),
    'magma_geyser': ('craftpix-net-819691*/PNG/Magma_geyser', 96, None),
    'lava_drop': ('craftpix-net-819691*/PNG/Lava_drop', 64, None),
    'small_fire': ('craftpix-net-819691*/PNG/Small_fire', 32, None),
    'fire_pillar': ('craftpix-net-819691*/PNG/Fire_pillar', 143, None),
    'lightning': ('craftpix-671189-pixel-art*/PNG/Lightning1', 96, None),
    'shine': ('craftpix-781160*/PNG/light', 64, None),
    'ice': ('craftpix-781160*/PNG/ice', 80, None),
    'tesla': ('craftpix-781160*/PNG/tesla_ball', 64, None),
}


def num(f):
    m = re.findall(r'(\d+)', os.path.basename(f))
    return int(m[-1]) if m else 0


def build(name, pattern, maxsize):
    d = glob.glob(os.path.join(FX, pattern))
    if not d:
        print('!!', name, 'introuvable')
        return
    files = sorted(glob.glob(os.path.join(d[0], '*.png')), key=num)
    frames = [Image.open(f).convert('RGBA') for f in files]
    # enveloppe commune
    box = None
    for im in frames:
        b = im.getbbox()
        if not b:
            continue
        box = b if box is None else (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
    frames = [im.crop(box) for im in frames if im.getbbox()]
    w, h = frames[0].size
    s = min(1.0, maxsize / max(w, h))
    if s < 1.0:
        w2, h2 = max(1, round(w * s)), max(1, round(h * s))
        # grosses images « lisses » : réduction de qualité ; pixel art : plus proche voisin
        method = Image.LANCZOS if max(w, h) > 300 else Image.NEAREST
        frames = [im.resize((w2, h2), method) for im in frames]
        w, h = w2, h2
    strip = Image.new('RGBA', (w * len(frames), h), (0, 0, 0, 0))
    for i, im in enumerate(frames):
        strip.alpha_composite(im, (i * w, 0))
    for old in glob.glob(os.path.join(OUT, name + '_*x*.png')):
        os.remove(old)
        if os.path.exists(old + '.meta'):
            os.remove(old + '.meta')
    strip.save(os.path.join(OUT, f'{name}_{w}x{h}.png'))
    print(f'{name}: {len(frames)} images {w}x{h}')


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, (pattern, mx, _) in SETS.items():
        build(name, pattern, mx)
    arrow = glob.glob(os.path.join(NEW, 'craftpix-net-626036*', 'PNG', 'Characters', 'Archers', 'Arrow_single.png'))
    if arrow:
        im = Image.open(arrow[0]).convert('RGBA')
        im = im.crop(im.getbbox())
        im.save(os.path.join(OUT, 'arrow.png'))
        print('arrow', im.size)


if __name__ == '__main__':
    main()
