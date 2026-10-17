"""Personnages en 8 directions, recomposés à partir des calques (Parts) des packs craftpix.

Les packs ne dessinent que 4 directions (bas, gauche, droite, haut). Chaque personnage y est découpé en calques
(ombre, épée dans le dos, corps, tête, épée, traînée...). Une diagonale se recompose en associant deux directions :
  SE / SW = corps (et arme) de face + tête de profil   → trois-quarts face
  NE / NW = corps (et arme) de profil + tête de dos    → trois-quarts dos
La tête est recalée sur le cou (bas-centre de la tête d'origine). Blessure et mort gardent le profil.
Une roulade est fabriquée en plus (rotations exactes de 90°, sans perte de pixels).

Sortie : Assets/Art/Characters8/<id>/<id>_<État>.png — 8 lignes (S, SE, E, NE, N, NW, W, SW), images de 64 px.
Usage : python "Tools~/make_8dir.py" [id...]      (sans argument : tous)
"""
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
IMP = os.path.join(ROOT, 'Assets', 'imports')
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Characters8')
F = 64
DIRS = ['S', 'SE', 'E', 'NE', 'N', 'NW', 'W', 'SW']
SRC_ROW = {'S': 0, 'W': 1, 'E': 2, 'N': 3}           # ordre des lignes craftpix : bas, gauche, droite, haut
# direction → (corps et arme, tête)
RECIPE = {'S': ('S', 'S'), 'SE': ('S', 'E'), 'E': ('E', 'E'), 'NE': ('E', 'N'),
          'N': ('N', 'N'), 'NW': ('W', 'N'), 'W': ('W', 'W'), 'SW': ('S', 'W')}
SIDE_ONLY = {'S': 'S', 'SE': 'E', 'E': 'E', 'NE': 'E', 'N': 'N', 'NW': 'W', 'W': 'W', 'SW': 'W'}
# ordre d'empilement des calques (du fond vers l'avant)
ORDER = ['shadow', 'sword_back', 'body', 'head', 'sword', 'sword_arm', 'swing', 'aoe', 'red']
# états du jeu → nom d'animation dans les fichiers
STATES = {'Idle': 'Idle', 'Walk': 'Walk', 'Run': 'Run', 'Attack': 'attack', 'WalkAttack': 'Walk_Attack',
          'RunAttack': 'Run_Attack', 'Hurt': 'Hurt', 'Death': 'Death'}
ANIM_NAMES = sorted(set(STATES.values()), key=len, reverse=True)


def swordsman(n):
    if n <= 3:
        base = os.path.join(IMP, 'craftpix-net-180537-free-swordsman-1-3-level-pixel-top-down-sprite-character', 'PNG', f'Swordsman_lvl{n}')
        prefix = f'Swordsman_lvl{n}_'
    else:
        pack = 'craftpix-net-934911-swordsman-level-4-6-pixel-character-top-down-sprite-pack' if n <= 6 \
            else 'craftpix-net-299679-swordsman-7-9-level-pixel-top-down-sprite-character-pack'
        base = os.path.join(IMP, 'new', pack, 'PNG', f'Swordsman_lvl{n}')
        prefix = f'lvl{n}_'
    return {'id': f'swordsman_{n}', 'parts': os.path.join(base, 'Parts'), 'prefix': prefix,
            'full': os.path.join(base, 'With_shadow')}


CHARACTERS = {f'swordsman_{n}': swordsman(n) for n in range(1, 10)}


def role(name, n):
    """Archer / mage / soigneuse du palier n (calques produits par Tools~/make_roles.py)."""
    return {'id': f'adv_{name}_{n}', 'parts': os.path.join(IMP, 'Roles', 'Parts', f'{name}_{n}'), 'prefix': f'{name}_{n}_', 'full': None}


for _r in ('archer', 'mage', 'healer'):
    for _n in range(1, 10):
        CHARACTERS[f'adv_{_r}_{_n}'] = role(_r, _n)
    CHARACTERS[f'adv_{_r}'] = role(_r, 2)          # rôle des expéditions (palier 2)


# ── Monstres : familles du mode Noyau (3 stades), lignes bas/haut/gauche/droite ──
MONSTERS = {'rat': 'Rat', 'slime': 'Slime', 'goblin': 'Goblin', 'orc': 'Orc', 'skeleton': 'Skeleton', 'mushroom': 'Mushroom',
            'zombie': 'Zombie', 'gnoll': 'Gnoll', 'imp': 'Imp', 'lizard': 'Lizardman', 'ghost': 'Ghost', 'lich': 'Lich',
            'vampire': 'Vampires', 'demon': 'Demon', 'golem': 'Golem'}
ROW_DULR = {'S': 0, 'N': 1, 'W': 2, 'E': 3}
SIDE_FAMILIES = {'Rat', 'Golem'}
MON_ANIMS = ['walk_attack', 'run_attack', 'attack', 'idle', 'rwalk', 'walk', 'run', 'hurt', 'death']
MON_STATES = {'Idle': 'idle', 'Walk': 'walk', 'Run': 'run', 'Attack': 'attack', 'WalkAttack': 'walk_attack',
              'RunAttack': 'run_attack', 'Hurt': 'hurt', 'Death': 'death'}


def find_parts(folder, n):
    want = f'{folder}{n}'.lower()
    for dirpath, dirs, files in os.walk(IMP):
        if '__MACOSX' in dirpath or 'craftpix' not in dirpath:
            continue
        if os.path.basename(dirpath).lower() == want and 'Parts' in dirs:
            return os.path.join(dirpath, 'Parts')
    return None


for _id, _folder in MONSTERS.items():
    for _n in (1, 2, 3):
        _p = find_parts(_folder, _n)
        if _p:
            CHARACTERS[f'{_id}_{_n}'] = {'id': f'{_id}_{_n}', 'parts': _p, 'prefix': None, 'full': None,
                                        'rows': ROW_DULR, 'family': _folder}


def mon_part(name):
    """Nom de calque normalisé : head, shadow, body, back (arme dans le dos) ou fx (devant)."""
    p = ''.join(ch for ch in name.lower() if not ch.isdigit()).strip('_- ')
    if not p:
        return None
    if 'head' in p or p == 'cup':
        return 'head'
    if 'shadow' in p:
        return 'shadow'
    if p == 'body':
        return 'body'
    if p == 'back':
        return 'body_alt'
    if 'back' in p:
        return 'back'
    return 'fx_' + p


def load_monster_layers(ch, anim):
    layers = {}
    fam = ch['family'].lower()
    for f in os.listdir(ch['parts']):
        if not f.endswith('.png'):
            continue
        n = f[:-4].lower()
        # préfixe de famille (« rat1_ », « gnoll_ »...) facultatif
        k = 0
        if n.startswith(fam):
            k = len(fam)
            while k < len(n) and n[k].isdigit():
                k += 1
            if k < len(n) and n[k] == '_':
                k += 1
        rest = n[k:]
        a = next((x for x in MON_ANIMS if rest.startswith(x)), None)
        if a is None:
            continue
        if a == 'rwalk':
            a = 'walk'
        if a != anim:
            continue
        part = mon_part(rest[len(a):])
        if part is None:
            continue
        img = Image.open(os.path.join(ch['parts'], f)).convert('RGBA')
        if part in layers:
            base = layers[part].copy()
            base.alpha_composite(img)
            img = base
        layers[part] = img
    if 'body' not in layers and 'body_alt' in layers:
        layers['body'] = layers.pop('body_alt')
    layers.pop('body_alt', None)
    return layers


# ─────────────────────────────────────────────
# Lecture des calques
# ─────────────────────────────────────────────

def load_layers(ch, anim):
    """{nom de calque: image} pour une animation (les variantes « normal » sans effet sont ignorées)."""
    layers = {}
    for f in os.listdir(ch['parts']):
        if not f.endswith('.png') or not f.startswith(ch['prefix']):
            continue
        rest = f[len(ch['prefix']):-4]
        a = next((x for x in ANIM_NAMES if rest.startswith(x + '_')), None)
        if a != anim:
            continue
        part = rest[len(a) + 1:]
        if part.startswith('normal') or part.startswith('nornal'):
            continue
        layers[part] = Image.open(os.path.join(ch['parts'], f)).convert('RGBA')
    return layers


def cell(img, d, col):
    r = SRC_ROW[d]
    if img is None or (col + 1) * F > img.width or (r + 1) * F > img.height:
        return None
    return img.crop((col * F, r * F, (col + 1) * F, (r + 1) * F))


def foot(img):
    bb = img.getbbox() if img is not None else None
    return ((bb[0] + bb[2]) // 2, bb[3]) if bb else None


def row_frames(img, d):
    """Nombre d'images non vides d'une direction (le dos n'a parfois que 4 images de repos contre 12)."""
    n = 0
    while True:
        c = cell(img, d, n)
        if c is None or not c.getbbox():
            return n
        n += 1


def compose(layers, body_d, head_d, col, with_shadow=True):
    out = Image.new('RGBA', (F, F))
    order = ['shadow', 'back'] + ORDER[1:] + sorted(p for p in layers if p not in ORDER and p != 'back')
    for part in order:
        if part not in layers or (part == 'shadow' and not with_shadow):
            continue
        if part == 'head' and head_d != body_d:
            # la tête boucle sur ses propres images si sa direction en a moins que le corps
            hn = max(1, row_frames(layers['head'], head_d))
            ref, head = cell(layers['head'], body_d, col), cell(layers['head'], head_d, col % hn)
            a, b = foot(ref), foot(head)
            if head is None:
                continue
            if a and b:
                tmp = Image.new('RGBA', (F, F))
                tmp.alpha_composite(head, (max(0, a[0] - b[0]), max(0, a[1] - b[1])))
                head = tmp
            out.alpha_composite(head)
            continue
        c = cell(layers[part], body_d, col)
        if c is not None:
            out.alpha_composite(c)
    return out


def frame_count(layers):
    body = layers.get('body') or next(iter(layers.values()))
    n = body.width // F
    while n > 0 and not any(cell(body, d, n - 1) is not None and cell(body, d, n - 1).getbbox() for d in SRC_ROW):
        n -= 1
    return n


def sheet_8(layers, side_only=False):
    n = frame_count(layers)
    rows = []
    for d in DIRS:
        b, h = (SIDE_ONLY[d], SIDE_ONLY[d]) if side_only else RECIPE[d]
        rows.append([compose(layers, b, h, c) for c in range(n)])
    return rows


# ─────────────────────────────────────────────
# Roulade : accroupi → 3 rotations exactes de 90° → accroupi
# ─────────────────────────────────────────────

def roll_rows(layers):
    rows = []
    for d in DIRS:
        b, h = RECIPE[d]
        body = compose(layers, b, h, 0, with_shadow=False)
        shadow = cell(layers.get('shadow'), b, 0)
        bb = body.getbbox()
        if bb is None:
            rows.append([body] * 5)
            continue
        cx, cy = (bb[0] + bb[2]) // 2, (bb[1] + bb[3]) // 2
        fx, fy = (bb[0] + bb[2]) // 2, bb[3]
        piece = body.crop(bb)
        clockwise = d in ('E', 'SE', 'NE', 'S')
        frames = []
        for step in range(5):
            out = Image.new('RGBA', (F, F))
            if shadow is not None:
                out.alpha_composite(shadow)
            if step in (0, 4):
                # accroupi : écrasé de 20 % vers les pieds
                h2 = max(1, int(piece.height * 0.8))
                sq = piece.resize((piece.width, h2), Image.NEAREST)
                out.alpha_composite(sq, (fx - piece.width // 2, fy - h2))
            else:
                k = step if clockwise else 4 - step
                # ramassé en boule : le corps est tassé avant de tourner
                ball = piece.resize((piece.width, max(1, int(piece.height * 0.62))), Image.NEAREST)
                rot = ball.transpose([None, Image.ROTATE_270, Image.ROTATE_180, Image.ROTATE_90][k])
                # en boule, le corps reste posé au sol : bas de l'image sur les pieds
                out.alpha_composite(rot, (cx - rot.width // 2, fy - rot.height))
            frames.append(out)
        rows.append(frames)
    return rows


# ─────────────────────────────────────────────
# Sortie
# ─────────────────────────────────────────────

# ─────────────────────────────────────────────
# Gestes : accroupi (fouiller, prier, ramasser) et bras tendu (ouvrir, boire, parler)
# ─────────────────────────────────────────────

def squash(frame, k):
    """Le personnage plie les genoux : tassé verticalement, posé sur ses pieds (l'ombre ne bouge pas)."""
    bb = frame.getbbox()
    if bb is None:
        return frame
    piece = frame.crop(bb)
    h2 = max(1, int(round(piece.height * k)))
    out = Image.new('RGBA', (F, F))
    out.alpha_composite(piece.resize((piece.width, h2), Image.NEAREST), (bb[0], bb[3] - h2))
    return out


def crouch_rows(idle_rows):
    return [[squash(r[0], k) for k in (0.92, 0.86, 0.84, 0.86)] for r in idle_rows]


def use_rows(attack_rows):
    return [[r[i] for i in (0, 1, 1, 1, 0)] if len(r) >= 2 else r for r in attack_rows]


def save(rows, path):
    n = max(len(r) for r in rows)
    sheet = Image.new('RGBA', (n * F, len(rows) * F))
    for r, frames in enumerate(rows):
        for c, fr in enumerate(frames):
            sheet.alpha_composite(fr, (c * F, r * F))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sheet.save(path)


def check(ch, layers, anim):
    """Écart entre la recomposition de face et la planche complète du pack (contrôle de l'ordre des calques)."""
    if not ch['full']:
        return None
    fname = f"{ch['prefix']}{anim}_with_shadow.png"
    path = os.path.join(ch['full'], fname)
    if not os.path.exists(path):
        return None
    full = Image.open(path).convert('RGBA')
    diff = total = 0
    for c in range(min(3, frame_count(layers))):
        a = compose(layers, 'S', 'S', c).load()
        b = cell(full, 'S', c)
        if b is None:
            continue
        b = b.load()
        for y in range(F):
            for x in range(F):
                if a[x, y][3] > 0 or b[x, y][3] > 0:
                    total += 1
                    if a[x, y] != b[x, y]:
                        diff += 1
    return diff / max(1, total)


def full_rows(ch, anim):
    if not ch['full']:
        return None
    """Blessure et mort : planche complète du pack, les diagonales reprennent le profil (certains packs n'ont pas
    tous les calques de ces animations)."""
    path = os.path.join(ch['full'], f"{ch['prefix']}{anim}_with_shadow.png")
    if not os.path.exists(path):
        return None
    full = Image.open(path).convert('RGBA')
    n = full.width // F
    return [[cell(full, SIDE_ONLY[d], c) for c in range(n)] for d in DIRS]


def build(cid):
    global F, SRC_ROW
    ch = CHARACTERS[cid]
    SRC_ROW = ch.get('rows', {'S': 0, 'W': 1, 'E': 2, 'N': 3})
    if ch.get('family'):
        return build_monster(cid, ch)
    F = 64
    report = []
    for state, anim in STATES.items():
        if state in ('Hurt', 'Death'):
            rows = full_rows(ch, anim) if ch['full'] else None
            if rows is None and not ch['full']:
                layers = load_layers(ch, anim)
                rows = sheet_8(layers, side_only=True) if layers else None
            if rows is None:
                report.append(f'{state}:absent')
                continue
            save(rows, os.path.join(OUT, cid, f'{cid}_{state}.png'))
            report.append(f'{state}:{len(rows[0])}')
            continue
        layers = load_layers(ch, anim)
        if not layers:
            report.append(f'{state}:absent')
            continue
        rows = sheet_8(layers)
        save(rows, os.path.join(OUT, cid, f'{cid}_{state}.png'))
        err = check(ch, layers, anim)
        report.append(f'{state}:{len(rows[0])}' + (f'({err:.0%})' if err is not None and err > 0.02 else ''))
        if state == 'Idle':
            save(roll_rows(layers), os.path.join(OUT, cid, f'{cid}_Roll.png'))
            save(crouch_rows(rows), os.path.join(OUT, cid, f'{cid}_Crouch.png'))
        if state == 'Attack':
            save(use_rows(rows), os.path.join(OUT, cid, f'{cid}_Use.png'))
    print(cid, ' '.join(report))


def build_monster(cid, ch):
    global F
    report = []
    for state, anim in MON_STATES.items():
        layers = load_monster_layers(ch, anim)
        if 'body' not in layers:
            continue
        F = layers['body'].height // 4
        # quadrupèdes et têtes fondues dans le corps : les diagonales reprennent le profil
        has_head = 'head' in layers and ch['family'] not in SIDE_FAMILIES
        rows = sheet_8(layers, side_only=state in ('Hurt', 'Death') or not has_head)
        save(rows, os.path.join(OUT, cid, f'{cid}_{state}.png'))
        report.append(f'{state}:{len(rows[0])}' + ('' if has_head else '(profil)'))
    F = 64
    print(cid, f'F={F}', ' '.join(report))


if __name__ == '__main__':
    ids = sys.argv[1:] or list(CHARACTERS)
    for cid in ids:
        build(cid)
