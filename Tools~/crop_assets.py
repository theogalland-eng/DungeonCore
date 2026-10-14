"""
Découpe les objets choisis des packs (via Tools~/segments/*.json) en PNG individuels
et compose quelques sprites (champ cultivé, bandes d'animation).
Sortie : Assets/Art/Settlement/*.png et Assets/Art/Dungeon/*.png
Usage : python "Tools~/crop_assets.py"
"""
import glob, json, os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NEW = os.path.join(ROOT, 'Assets', 'imports', 'new')
SEG = os.path.join(ROOT, 'Tools~', 'segments')
OUT_SET = os.path.join(ROOT, 'Assets', 'Art', 'Settlement')


def sheet(pattern):
    return Image.open(glob.glob(os.path.join(NEW, pattern))[0]).convert('RGBA')


def seg(name):
    d = json.load(open(os.path.join(SEG, name + '.json')))
    src = d['source'] if os.path.exists(d['source']) else os.path.join(NEW, d['source'])
    return Image.open(src).convert('RGBA'), d['boxes']


def crop_box(im, b, pad=0):
    return im.crop((b[0] - pad, b[1] - pad, b[0] + b[2] + pad, b[1] + b[3] + pad))


# nom -> (segments, index)
SINGLES = {
    # Campement
    'tent_white': ('Tents_n_objects', 1), 'tent_white_full': ('Tents_n_objects', 0), 'tent_orange': ('Tents_n_objects', 10),
    'tent_purple': ('Tents_n_objects', 25), 'tent_red': ('Tents_n_objects', 38), 'tent_green': ('Tents_n_objects', 45),
    'tent_round': ('Tents_n_objects', 60),
    'log_h': ('Tents_n_objects', 55), 'log_v': ('Tents_n_objects', 54), 'log_h2': ('Tents_n_objects', 61), 'log_v2': ('Tents_n_objects', 62),
    'camp_crate_crystals': ('Tents_n_objects', 50), 'camp_sacks': ('Tents_n_objects', 51), 'camp_barrel_sack': ('Tents_n_objects', 68),
    'camp_shield_axe': ('Tents_n_objects', 69), 'camp_potions': ('Tents_n_objects', 72),
    # Port / pêche
    'house_a': ('Exterior_objetcs', 0), 'house_b': ('Exterior_objetcs', 1), 'house_a_walls': ('Exterior_objetcs', 6), 'house_b_walls': ('Exterior_objetcs', 7),
    'fish_rack': ('Exterior_objetcs', 9), 'crates_fish': ('Exterior_objetcs', 29), 'fish_crates2': ('Exterior_objetcs', 27), 'fish_net': ('Exterior_objetcs', 30),
    'shark': ('Exterior_objetcs', 23), 'barrel': ('Exterior_objetcs', 11), 'barrel_fish': ('Exterior_objetcs', 24), 'sacks': ('Exterior_objetcs', 44),
    'crate_stack': ('Exterior_objetcs', 8),
    # Marché
    'stall_weapons': ('Objects', 0), 'stall_fruits': ('Objects', 14), 'stall_bread': ('Objects', 24), 'tent_magic': ('Objects', 37),
    'cart_potions': ('Objects', 44), 'market_crates': ('Objects', 3), 'banner_red': ('Objects', 10), 'barrel_big': ('Objects', 46),
    'table_feast': ('Objects', 34),
}

# Bandes d'animation régulières : (motif de fichier, largeur, hauteur d'image) -> copiées telles quelles
STRIPS = {
    'boat_row': ('craftpix-net-885927*/PNG/Boats/Small_boat_v1_1.png', 48, 96),
    'boat_row2': ('craftpix-net-885927*/PNG/Boats/Small_boat_v1_3.png', 48, 96),
    'boat_row3': ('craftpix-net-885927*/PNG/Boats/Small_boat_v1_6.png', 48, 96),
    'boat_sail': ('craftpix-net-885927*/PNG/Boats/Boat2.png', 160, 112),
    'boat_ship': ('craftpix-net-885927*/PNG/Boats/Boat1.png', 176, 128),
}


def main():
    os.makedirs(OUT_SET, exist_ok=True)
    cache = {}
    for name, (sname, idx) in SINGLES.items():
        if sname not in cache:
            cache[sname] = seg(sname)
        im, boxes = cache[sname]
        crop_box(im, boxes[idx]).save(os.path.join(OUT_SET, name + '.png'))

    # Pot de cuisine animé (feu de camp) : les pots seuls de la planche du bivouac
    im, boxes = seg('Bonfire_boiler_lizardman')
    pots = [b for b in boxes if b[2] < 45 and b[3] < 40]
    w = max(b[2] for b in pots); h = max(b[3] for b in pots)
    strip = Image.new('RGBA', (w * len(pots), h))
    for i, b in enumerate(pots):
        c = crop_box(im, b)
        strip.alpha_composite(c, (i * w + (w - c.width) // 2, h - c.height))
    strip.save(os.path.join(OUT_SET, f'campfire_pot_{w}x{h}.png'))

    # Bateaux (bandes régulières)
    for name, (pat, fw, fh) in STRIPS.items():
        sheet(pat).save(os.path.join(OUT_SET, f'{name}_{fw}x{fh}.png'))

    # Ponton : passerelle verticale (2 cases) + plateforme d'amarrage 3x3
    tiles = sheet('craftpix-net-885927*/PNG/Exterior_tiles.png')
    tiles.crop((112, 16, 128, 32)).save(os.path.join(OUT_SET, 'pier_left.png'))
    tiles.crop((128, 16, 144, 32)).save(os.path.join(OUT_SET, 'pier_right.png'))
    tiles.crop((176, 0, 224, 48)).save(os.path.join(OUT_SET, 'pier_platform.png'))
    tiles.crop((16, 16, 32, 32)).save(os.path.join(OUT_SET, 'deck_plank.png'))

    # Champ cultivé : terre labourée + rangées de plants
    plants_im, pb = seg('Plants')
    crops = [crop_box(plants_im, pb[i]) for i in (52, 53, 57, 58) if i < len(pb)]
    field = Image.new('RGBA', (64, 48), (0, 0, 0, 0))
    soil = (112, 82, 56, 255); furrow = (88, 62, 42, 255)
    for y in range(4, 46):
        for x in range(2, 62):
            field.putpixel((x, y), furrow if (y // 4) % 2 == 0 else soil)
    for r, y in enumerate((6, 18, 30)):
        for k, x in enumerate(range(4, 58, 14)):
            c = crops[(r + k) % len(crops)]
            field.alpha_composite(c, (min(64 - c.width, x), min(48 - c.height, y)))
    field.save(os.path.join(OUT_SET, 'field.png'))

    # Mannequin d'entraînement (première image)
    man = sheet('craftpix-net-626036*/PNG/Characters/Mannequins/With_shadow/Attacked_Manequin1_with_shadow.png')
    man.crop((0, 0, 32, 32)).save(os.path.join(OUT_SET, 'dummy.png'))
    print('ok', len(os.listdir(OUT_SET)))


if __name__ == '__main__':
    main()
