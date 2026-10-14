"""
Butin des intrus et présentoirs à trophées (pack craftpix « treasure 32x32 objects and icons ») :
  - chaque grande icône (17 px) devient Assets/Resources/Loot/loot_<id>.png (chargée à l'exécution)
  - table / vitrine vides -> Assets/Resources/Loot/trophy_table.png, trophy_shelf.png
Les icônes sont rangées par groupes de 48 px (grande icône à +23 px) sur des lignes de 32 px.
Usage : python "Tools~/crop_loot.py"
"""
import glob, os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACK = glob.glob(os.path.join(ROOT, 'Assets', 'imports', 'new', 'craftpix-net-522456*', 'PNG_n_tiled'))[0]
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Loot')

# id : (ligne, colonne) de la grande icône
LOOT = {
    'copper_coins': (2, 0), 'pouch': (2, 5), 'sack': (2, 7), 'iron_key': (0, 0),
    'silver_coins': (1, 5), 'copper_bar': (3, 4), 'backpack': (2, 8), 'silver_ring': (4, 8),
    'gold_coins': (1, 0), 'merc_sword': (6, 4), 'mage_staff': (6, 3), 'mana_potion': (5, 6), 'silver_bar': (3, 2), 'bronze_crown': (4, 2),
    'emerald_blade': (6, 5), 'silver_crown': (4, 1), 'ruby_ring': (4, 7), 'bravery_medal': (5, 4), 'silver_chalice': (5, 7),
    'emerald_amulet': (5, 1), 'gold_bar': (3, 0),
    'blessed_sword': (6, 7), 'hero_crown': (4, 0), 'sun_shield': (6, 9), 'cursed_scepter': (6, 6), 'priestess_wand': (6, 8),
    'tourney_cup': (6, 0),
}


def trim(im):
    bb = im.getbbox()
    return im.crop(bb) if bb else im


def main():
    os.makedirs(OUT, exist_ok=True)
    icons = Image.open(os.path.join(PACK, 'Icons.png')).convert('RGBA')
    for lid, (row, col) in LOOT.items():
        x, y = 21 + col * 48, 5 + row * 32
        tile = trim(icons.crop((x, y, x + 22, y + 22)))
        # icône carrée 20x20 centrée (même taille à l'écran pour toutes)
        sq = Image.new('RGBA', (20, 20), (0, 0, 0, 0))
        sq.alpha_composite(tile, ((20 - tile.width) // 2, (20 - tile.height) // 2))
        sq.save(os.path.join(OUT, f'loot_{lid}.png'))
    furn = Image.open(os.path.join(PACK, 'Funiture.png')).convert('RGBA')
    trim(furn.crop((52, 8, 93, 34))).save(os.path.join(OUT, 'trophy_table.png'))
    trim(furn.crop((0, 4, 48, 58))).save(os.path.join(OUT, 'trophy_shelf.png'))
    print(len(LOOT), 'objets de butin ->', OUT)


if __name__ == '__main__':
    main()
