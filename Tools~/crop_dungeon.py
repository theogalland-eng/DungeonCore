"""
Pièges et décors du donjon (packs craftpix de Assets/imports/new) :
  - bandes d'animation régulières  -> Assets/Art/Dungeon/<nom>_<L>x<H>.png (découpées en grille par Unity)
  - groupes d'images segmentées     -> bandes alignées en bas
  - objets isolés                    -> PNG simples
Usage : python "Tools~/crop_dungeon.py"
"""
import glob, json, os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NEW = os.path.join(ROOT, 'Assets', 'imports', 'new')
SEG = os.path.join(ROOT, 'Tools~', 'segments')
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Dungeon')


def sheet(pattern):
    return Image.open(glob.glob(os.path.join(NEW, pattern))[0]).convert('RGBA')


def seg(name):
    d = json.load(open(os.path.join(SEG, name + '.json')))
    src = d['source'] if os.path.exists(d['source']) else os.path.join(NEW, d['source'])
    return Image.open(src).convert('RGBA'), d['boxes']


# nom : (planche, largeur image, hauteur image, vertical ?)
GRIDS = {
    'trap_spikes': ('craftpix-net-298079*/PNG/Spikes.png', 48, 64, False),
    'trap_column': ('craftpix-net-298079*/PNG/column_trap.png', 48, 64, False),
    'trap_ghost': ('craftpix-net-298079*/PNG/ghost_trap.png', 64, 64, False),
    'trap_bomb': ('craftpix-net-665895*/PNG/Bomb.png', 48, 48, False),
    'trap_guillotine': ('craftpix-net-665895*/PNG/Guillotine.png', 48, 48, False),
    'trap_blades': ('craftpix-net-665895*/PNG/Rotating_blades.png', 48, 48, True),
    'statue_fire': ('craftpix-net-298079*/PNG/Statue_fire.png', 80, 80, False),
    'fountain': ('craftpix-net-125640*/PNG/fountain_animation.png', 48, 64, False),
}

# groupes d'images segmentées (une variante, plusieurs images)
GROUPS = {
    'brazier': ('torches', [0, 5, 10, 15, 20, 25]),
    'torch_wall': ('torches', [3, 8, 13, 18, 23, 28]),
    'candelabra': ('candles', [0, 9, 18, 27, 36]),
}

SINGLES = {
    'sarcophagus': ('coffins', 0), 'coffin_skeleton': ('coffins', 2), 'sarcophagus_open': ('coffins', 14),
    'coffin_wood': ('coffins', 29), 'coffin_wood_open': ('coffins', 33),
    'gargoyle': ('other_objects', 0), 'statue_knight': ('other_objects', 3), 'chest_gold_pile': ('other_objects', 6),
    'hanging_skeleton': ('other_objects', 21), 'skull_pile': ('other_objects', 35), 'bones': ('other_objects', 25),
    'chest_royal': ('Treasure_Objects', 3), 'chest_legend': ('Treasure_Objects', 9),
    'gold_pile': ('Treasure_Objects', 45), 'silver_pile': ('Treasure_Objects', 48),
    'web': ('web', 0),
}


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, (pat, fw, fh, vertical) in GRIDS.items():
        im = sheet(pat)
        if vertical:
            n = im.height // fh
            strip = Image.new('RGBA', (fw * n, fh))
            for i in range(n):
                strip.paste(im.crop((0, i * fh, fw, (i + 1) * fh)), (i * fw, 0))
            im = strip
        else:
            im = im.crop((0, 0, (im.width // fw) * fw, fh))   # première rangée seulement
        im.save(os.path.join(OUT, f'{name}_{fw}x{fh}.png'))

    cache = {}
    for name, (sname, ids) in GROUPS.items():
        if sname not in cache:
            cache[sname] = seg(sname)
        im, boxes = cache[sname]
        crops = [im.crop((b[0], b[1], b[0] + b[2], b[1] + b[3])) for b in (boxes[i] for i in ids)]
        w = max(c.width for c in crops); h = max(c.height for c in crops)
        strip = Image.new('RGBA', (w * len(crops), h))
        for i, c in enumerate(crops):
            strip.alpha_composite(c, (i * w + (w - c.width) // 2, h - c.height))
        strip.save(os.path.join(OUT, f'{name}_{w}x{h}.png'))

    for name, (sname, idx) in SINGLES.items():
        if sname not in cache:
            cache[sname] = seg(sname)
        im, boxes = cache[sname]
        b = boxes[idx]
        im.crop((b[0], b[1], b[0] + b[2], b[1] + b[3])).save(os.path.join(OUT, name + '.png'))

    # Souches (arbres abattus par les colons)
    stumps = os.path.join(ROOT, 'Assets', 'Art', 'Settlement')
    for i, f in enumerate(['Broken_tree1', 'Broken_tree2', 'Broken_tree5']):
        p = glob.glob(os.path.join(ROOT, 'Assets', 'imports', 'craftpix-net-385863*', 'PNG', 'Assets_separately', 'Trees_shadow', f + '.png'))
        if p:
            Image.open(p[0]).save(os.path.join(stumps, f'stump_{i}.png'))
    print('ok', len(os.listdir(OUT)))


if __name__ == '__main__':
    main()
