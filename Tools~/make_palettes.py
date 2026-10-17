"""Palettes du héros pour la personnalisation (cheveux, peau, tenue).

Pour chaque apparence (épéistes 1-9, archer/mage/soigneuse 1-9), on relève les couleurs de chaque catégorie dans
les calques de tête et de corps. Le jeu recolore ensuite les sprites à la volée (HeroLook) : chaque couleur d'une
catégorie prend la teinte choisie en gardant son ombre ou sa lumière.
  - peau  : couleurs proches des carnations du pack ;
  - cheveux : le reste de la tête, seulement pour les paliers 1-3 (au-delà, la tête est sous un casque) ;
  - tenue : étoffes colorées du corps (le métal, l'or des armures lourdes et les contours restent).
Sortie : Assets/Resources/HeroPalettes/<id>.json
Usage : python "Tools~/make_palettes.py"
"""
import colorsys
import importlib.util
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'HeroPalettes')


def load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(ROOT, 'Tools~', name + '.py'))
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


M8 = load('make_8dir')
MR = load('make_roles')


def colors(img):
    im = img.convert('RGBA')
    return {p[:3] for p in im.get_flattened_data() if p[3] > 0}


def is_cloth(c, tier):
    if MR.is_skin(c):
        return False
    h, s, v = colorsys.rgb_to_hsv(c[0] / 255, c[1] / 255, c[2] / 255)
    if v < 0.17:
        return False
    if tier >= 4 and s < 0.28:
        return False
    if tier >= 7 and 0.08 < h < 0.17 and s > 0.5 and v > 0.55:
        return False
    return True


def hexs(cs):
    return ['#%02x%02x%02x' % c for c in sorted(cs)]


def palette(cid, tier):
    ch = M8.CHARACTERS[cid]
    head, body = set(), set()
    for anim in ('Idle', 'Walk', 'Run', 'attack'):
        L = M8.load_layers(ch, anim)
        if 'head' in L:
            head |= colors(L['head'])
        if 'body' in L:
            body |= colors(L['body'])
    skin = {c for c in head | body if MR.is_skin(c) and c not in MR.EYES}
    # la recoloration est globale (par couleur) : une couleur partagée avec le corps (contours...) n'est pas « cheveux »
    hair = {c for c in head - body if not MR.is_skin(c) and colorsys.rgb_to_hsv(*(x / 255 for x in c))[2] >= 0.12} if tier <= 3 else set()
    outfit = {c for c in body if is_cloth(c, tier)} - hair
    return {'hair': hexs(hair), 'skin': hexs(skin), 'outfit': hexs(outfit)}


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    ids = [(f'swordsman_{n}', n) for n in range(1, 10)]
    ids += [(f'adv_{r}_{n}', n) for r in ('archer', 'mage', 'healer') for n in range(1, 10)]
    for cid, tier in ids:
        pal = palette(cid, tier)
        with open(os.path.join(OUT, cid + '.json'), 'w', encoding='utf-8') as f:
            json.dump(pal, f)
        print(cid, {k: len(v) for k, v in pal.items()})
