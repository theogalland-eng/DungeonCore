"""
TAMPONS DE LA CITÉ : chaque scène Tiled d'un pack (extérieur d'un bâtiment avec sa cour, son jardin, ses habitants)
devient un « tampon » posé tel quel sur l'île.

- le sol (pavés, herbe, ombres, détails) est fusionné en une image plate, dessinée sous les personnages ;
- les objets (bâtiment, arbres, statues, étals, habitants...) sont découpés en morceaux (groupes de tuiles voisines),
  chacun trié à ses pieds : le héros passe devant ou derrière comme il faut ;
- les clôtures sont coupées par rangée (un côté de clôture ne recouvre pas ce qui est devant lui) ;
- les tuiles animées (drapeaux, fumée, fontaine, habitants) donnent des bandes d'images ;
- les remparts et la place dallée bleutée qui entourent chaque scène dans les packs sont retirés.

Sortie : Assets/Art/Settlement/Stamps/<id>/ (images) + <id>.json (morceaux, tailles, porte), lus par
Editor/StampImporter.cs. Les images des packs restent hors du dépôt public.

usage : python "Tools~/make_stamps.py" [id ...]
"""
import os, re, sys, json, base64, zlib, struct, math
import xml.etree.ElementTree as ET
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NEW = os.path.join(ROOT, 'Assets', 'imports', 'new')
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Settlement', 'Stamps')
FLIP_H, FLIP_V, FLIP_D = 0x80000000, 0x40000000, 0x20000000


def pack(num, *rel):
    for base in (NEW, os.path.join(NEW, 'to_add'), os.path.dirname(NEW)):
        if not os.path.isdir(base):
            continue
        for d in os.listdir(base):
            if d.startswith('craftpix-net-%s' % num) and not d.endswith('.meta'):
                return os.path.join(base, d, *rel)
    raise FileNotFoundError(num)


# id, carte, options
#   drop     : couches retirées
#   ground   / objects : couches forcées au sol / en objets
#   crop     : (x0, y0, x1, y1) en cases de la carte Tiled (bornes incluses) : le reste est coupé
STAMPS = {
    'manor':   dict(tmx=lambda: pack(653272, 'Tiled_files', 'Exterior.tmx'), drop=['Birds', 'Walls'], margin=1),
    'hero_home': dict(tmx=lambda: pack(654184, 'Tiled_files', 'Exterior.tmx'), drop=['Birds'], margin=None),
    'prison':  dict(tmx=lambda: pack(659586, 'Tiled_files', 'Exterior.tmx'), drop=['Walls3']),
    'library': dict(tmx=lambda: pack(845649, 'Tiled_files', 'Exterior.tmx')),
    'tavern':  dict(tmx=lambda: pack(666104, 'Tiled_files', 'Tavern_exterior.tmx')),
    'forge':   dict(tmx=lambda: pack(741016, 'Tiled_files', 'Blacksmith_house_exterior.tmx')),
    'guild':   dict(tmx=lambda: pack(189780, 'Tiled_files', 'Exterior.tmx')),
    'magetower': dict(tmx=lambda: pack(289481, 'Tiled_files', 'Exterior.tmx'), drop=['Walls']),
    'glass':   dict(tmx=lambda: pack(692491, 'Tiled_files', 'Exterior.tmx')),
    'farm':    dict(tmx=lambda: pack(471853, 'Tiled_files', 'Farm.tmx'), margin=None, feather=28),
    'training': dict(tmx=lambda: pack(626036, 'Tiled_files', 'Training_ground.tmx'), drop=['Tile Layer 33'], margin=None),
    'chapel':  dict(tmx=lambda: pack(160005, 'Tiled_files', 'Ruined_temple_exterior.tmx'), drop=['water', 'water_detailization', 'Discoverers'], ground=['site', 'House_platform', 'Bricks']),
    'int_library': dict(tmx=lambda: pack(845649, 'Tiled_files', 'Interior.tmx'), interior=True),
    'int_prison':  dict(tmx=lambda: pack(659586, 'Tiled_files', 'Interior.tmx'), interior=True),
    'int_manor':   dict(tmx=lambda: pack(653272, 'Tiled_files', 'Interior_1st_floor.tmx'), interior=True, ground=['Ladder']),
    'int_home':    dict(tmx=lambda: pack(654184, 'Tiled_files', 'Interior1.tmx'), interior=True, walls=[], trim=(2, 3)),
    'market':  dict(tmx=lambda: pack(587497, 'Tiled_files', 'Market_square.tmx'), drop=['Walls_top'], margin=None),
}

INTERIOR_GROUND = re.compile(r'floor|carpet|wall|window|tile layer', re.I)
GROUND = re.compile(r'floor|ground|grass|street|brick|plate|spot|detail|shadow|carpet|road|straw|water|bed|hill|walls_grass|walls_sand|platform|crack|fish$', re.I)
CHARACTER = re.compile(r'drinker|eater|girl|guard|archer|fighter|trainer|aristocrat|servant|trader|lute|flutist|chief|librarian|reader|visitor|smoke_guy|cat_|bird|animal|chicken|cow|pig|bandit|old_man|sleeping|prisoner|npc|character|blacksmith|glassblower|player', re.I)
LIGHT = re.compile(r'flag|smoke|sign|banner|window|candle|ivy|cornice|arrow', re.I)
FENCE = re.compile(r'fence|border|railing', re.I)


# ─────────────────────────────────────────────
# LECTURE DES CARTES
# ─────────────────────────────────────────────

class Tileset:
    def __init__(self, first, node, base):
        self.first = first
        self.name = node.get('name') or ''
        self.tw, self.th = int(node.get('tilewidth')), int(node.get('tileheight'))
        self.count, self.cols = int(node.get('tilecount')), int(node.get('columns'))
        img = node.find('image')
        self.src = os.path.normpath(os.path.join(base, img.get('source'))) if img is not None else None
        self.image_name = os.path.basename(self.src) if self.src else ''
        self.im = Image.open(self.src).convert('RGBA') if self.src and os.path.exists(self.src) else None
        self.anims = {}
        for t in node.findall('tile'):
            a = t.find('animation')
            if a is not None:
                self.anims[int(t.get('id'))] = [(int(f.get('tileid')), int(f.get('duration'))) for f in a.findall('frame')]
        self.cache = {}

    def tile(self, local, flags=0):
        key = (local, flags)
        if key in self.cache:
            return self.cache[key]
        im = None
        if self.im is not None and 0 <= local < self.count:
            x, y = (local % self.cols) * self.tw, (local // self.cols) * self.th
            im = self.im.crop((x, y, x + self.tw, y + self.th))
            if flags & FLIP_D: im = im.transpose(Image.TRANSPOSE)
            if flags & FLIP_H: im = im.transpose(Image.FLIP_LEFT_RIGHT)
            if flags & FLIP_V: im = im.transpose(Image.FLIP_TOP_BOTTOM)
        self.cache[key] = im
        return im

    def street(self):
        n = self.image_name.lower()
        return 'street' in n or 'crack' in n


def load_map(path):
    root = ET.parse(path).getroot()
    tw, th = int(root.get('tilewidth')), int(root.get('tileheight'))
    base = os.path.dirname(path)
    tilesets = []
    for ts in root.findall('tileset'):
        first = int(ts.get('firstgid'))
        if ts.get('source'):
            p = os.path.join(base, ts.get('source'))
            node, b = ET.parse(p).getroot(), os.path.dirname(p)
        else:
            node, b = ts, base
        tilesets.append(Tileset(first, node, b))
    tilesets.sort(key=lambda t: t.first)
    layers = []

    def walk(node, visible=True):
        for ch in node:
            vis = visible and ch.get('visible', '1') != '0'
            if ch.tag == 'group':
                walk(ch, vis)
            elif ch.tag == 'layer' and vis:
                cells = {}
                data = ch.find('data')
                for c in (data.findall('chunk') or [data]):
                    cx, cy, cw = int(c.get('x', 0)), int(c.get('y', 0)), int(c.get('width', ch.get('width')))
                    if data.get('encoding') == 'base64':
                        raw = base64.b64decode((c.text or '').strip())
                        if data.get('compression') in ('zlib', 'gzip'):
                            raw = zlib.decompress(raw, 15 + (32 if data.get('compression') == 'gzip' else 0))
                        vals = struct.unpack('<%dI' % (len(raw) // 4), raw)
                    else:
                        vals = [int(v) for v in re.split(r'[,\s]+', (c.text or '').strip()) if v]
                    for i, g in enumerate(vals):
                        if g:
                            cells[(cx + i % cw, cy + i // cw)] = g
                layers.append((ch.get('name') or '', cells))
    walk(root)
    return tw, th, tilesets, layers


def resolve(tilesets, gid):
    g = gid & 0x1FFFFFFF
    ts = None
    for t in tilesets:
        if t.first <= g:
            ts = t
    if ts is None:
        return None, 0, 0
    return ts, g - ts.first, gid & 0xE0000000


def bluish(im):
    px = [p for p in (im.get_flattened_data() if hasattr(im, 'get_flattened_data') else im.getdata()) if p[3] > 40]
    if not px:
        return False
    n = sum(1 for r, g, b, a in px if b > r + 6 and b >= g - 2)
    return n > 0.3 * len(px)


# ─────────────────────────────────────────────
# DÉCOUPE
# ─────────────────────────────────────────────

class Cell:
    __slots__ = ('x', 'y', 'order', 'ts', 'local', 'flags', 'layer')

    def __init__(self, x, y, order, ts, local, flags, layer):
        self.x, self.y, self.order, self.ts, self.local, self.flags, self.layer = x, y, order, ts, local, flags, layer

    def frames(self):
        return self.ts.anims.get(self.local)

    def image(self, k=0):
        fr = self.frames()
        local = fr[k % len(fr)][0] if fr else self.local
        return self.ts.tile(local, self.flags)


def build(sid, opt):
    path = opt['tmx']()
    tw, th, tilesets, layers = load_map(path)
    drop = set(opt.get('drop', []))
    force_ground, force_obj = set(opt.get('ground', [])), set(opt.get('objects', []))
    crop = opt.get('crop')
    ground, objs = [], []
    for order, (name, cells) in enumerate(layers):
        if name in drop:
            continue
        interior = opt.get('interior', False)
        is_ground = (name in force_ground) or (name not in force_obj and (INTERIOR_GROUND.search(name) if interior
                                                                            else GROUND.search(name) and not FENCE.search(name)))
        for (x, y), gid in cells.items():
            if crop and not (crop[0] <= x <= crop[2] and crop[1] <= y <= crop[3]):
                continue
            ts, local, flags = resolve(tilesets, gid)
            if ts is None or ts.im is None:
                continue
            c = Cell(x, y, order, ts, local, flags, name)
            im = c.image()
            if im is None or im.getbbox() is None:
                continue
            if ts.street() and bluish(im):
                continue            # remparts et place dallée des packs
            (ground if is_ground else objs).append(c)
    if not ground and not objs:
        raise RuntimeError('vide : ' + sid)

    # bornes (px) : les grandes tuiles sont ancrées en bas à gauche de leur case
    def rect(c):
        im = c.image()
        return c.x * tw, (c.y + 1) * th - im.height, c.x * tw + im.width, (c.y + 1) * th
    xs0, ys0, xs1, ys1 = zip(*[rect(c) for c in ground + objs])
    X0, Y0, X1, Y1 = min(xs0), min(ys0), max(xs1), max(ys1)
    W, H = X1 - X0, Y1 - Y0

    out = os.path.join(OUT, sid)
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith('.png'):
            os.remove(os.path.join(out, f))

    # sol : une image plate
    g = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    for c in sorted(ground, key=lambda c: c.order):
        r = rect(c)
        g.alpha_composite(c.image(), (r[0] - X0, r[1] - Y0))

    # objets : 1) groupes par couche (4 voisins ; clôtures : seulement sur la rangée) ;
    # 2) un groupe se fond dans un plus grand qu'il recouvre sans descendre plus bas que lui et sans en déborder
    #    sur les côtés (fenêtres, toit, enseigne, lierre sur un mur) ; ce qui est devant (tables, habitants,
    #    caisses à côté) reste à part et se trie à ses propres pieds.
    def cells_of(c):
        im = c.image()
        wc, hc = max(1, im.width // tw), max(1, im.height // th)
        return [(c.x + dx, c.y - dy) for dx in range(wc) for dy in range(hc)]
    by_layer = {}
    for i, c in enumerate(objs):
        by_layer[(c.order, c.x, c.y)] = i
    parent = list(range(len(objs)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i
    for i, c in enumerate(objs):
        fence = bool(FENCE.search(c.layer))
        for dx, dy in ((1, 0), (0, 1)) if not fence else ((1, 0),):
            j = by_layer.get((c.order, c.x + dx, c.y + dy))
            if j is not None:
                a_, b_ = find(i), find(j)
                if a_ != b_:
                    parent[a_] = b_
    comps = {}
    for i in range(len(objs)):
        comps.setdefault(find(i), []).append(objs[i])
    comps = list(comps.values())
    if opt.get('interior'):
        # tapis et dallages posés sur une couche d'objets : grands rectangles pleins et bas → avec le sol
        flat = []
        for cl in comps:
            occ = set(p for c in cl for p in cells_of(c))
            xs = [p[0] for p in occ]; ys = [p[1] for p in occ]
            w, h = max(xs) - min(xs) + 1, max(ys) - min(ys) + 1
            if w >= 5 and h <= 5 and len(occ) >= 0.85 * w * h:
                flat.append(cl)
        for cl in flat:
            comps.remove(cl)
            for c in sorted(cl, key=lambda c: c.order):
                r = rect(c)
                g.alpha_composite(c.image(), (r[0] - X0, r[1] - Y0))
    info = []
    for cl in comps:
        occ = set(p for c in cl for p in cells_of(c))
        xs = [p[0] for p in occ]
        info.append(dict(cells=cl, occ=occ, x0=min(xs), x1=max(xs), base=max(c.y for c in cl),
                         fence=all(FENCE.search(c.layer) for c in cl)))
    owner = list(range(len(info)))

    def top(i):
        while owner[i] != i:
            i = owner[i]
        return i
    order = sorted(range(len(info)), key=lambda i: -len(info[i]['occ']))
    for ai in sorted(range(len(info)), key=lambda i: len(info[i]['occ'])):
        A = info[ai]
        if A['fence']:
            continue
        for bi in order:
            if bi == ai or len(info[bi]['occ']) <= len(A['occ']) or info[bi]['fence']:
                continue
            B = info[bi]
            if A['base'] <= B['base'] and A['x0'] >= B['x0'] - 1 and A['x1'] <= B['x1'] + 1 and A['occ'] & B['occ']:
                owner[ai] = bi
                break
    merged = {}
    for i in range(len(info)):
        merged.setdefault(top(i), []).extend(info[i]['cells'])
    groups = [(info[k]['fence'], v) for k, v in merged.items()]

    pieces = []
    door = None
    biggest = 0
    for i, (fence, cells) in enumerate(sorted(groups, key=lambda g: min(c.y for c in g[1]))):
        rs = [rect(c) for c in cells]
        x0, y0 = min(r[0] for r in rs), min(r[1] for r in rs)
        x1, y1 = max(r[2] for r in rs), max(r[3] for r in rs)
        anim = [c.frames() for c in cells if c.frames()]
        n = max((len(a) for a in anim), default=1)
        n = min(n, 24)
        dur = anim[0][0][1] if anim else 0
        strip = Image.new('RGBA', ((x1 - x0) * n, y1 - y0), (0, 0, 0, 0))
        for k in range(n):
            for c in sorted(cells, key=lambda c: c.order):
                im = c.image(k)
                if im is None:
                    continue
                r = rect(c)
                strip.alpha_composite(im, (k * (x1 - x0) + r[0] - x0, r[1] - y0))
        if strip.getbbox() is None:
            continue
        names = ' '.join(sorted({c.layer for c in cells} | {c.ts.image_name for c in cells}))
        if CHARACTER.search(names) and not re.search(r'house|wall|roof|tower|tent|stall|prison|tree', names, re.I):
            kind = 'character'
        elif fence:
            kind = 'fence'
        elif re.search(r'tree', names, re.I) and (x1 - x0) <= 4 * tw:
            kind = 'tree'
        elif LIGHT.search(names) and (x1 - x0) * (y1 - y0) <= 4 * tw * th:
            kind = 'detail'
        else:
            kind = 'prop'
        wcell, hcell = (x1 - x0) / tw, (y1 - y0) / th
        if kind == 'prop' and wcell >= 4 and hcell >= 4:
            kind = 'building'
        fname = 'p%d' % len(pieces) + ('_%dx%d' % (x1 - x0, y1 - y0) if n > 1 else '') + '.png'
        strip.save(os.path.join(out, fname))
        piece = dict(file=fname, x=x0 - X0, y=Y1 - y1, w=x1 - x0, h=y1 - y0, frames=n, names=names,
                     fps=round(1000.0 / dur, 2) if dur else 0, kind=kind,
                     solid=kind in ('building', 'prop', 'fence', 'tree') and hcell >= 1.5 or kind == 'fence')
        pieces.append(piece)
        if kind == 'building' and wcell * hcell > biggest and re.search(r'house|wall|roof|tower|prison|tent', names, re.I):
            biggest = wcell * hcell
            doors = [c for c in cells if 'door' in c.ts.image_name.lower() or 'door' in c.layer.lower()]
            if doors:
                d = max(doors, key=lambda c: c.y)
                door = ((d.x + 0.5) * tw - X0, Y1 - (d.y + 1) * th)
            else:
                door = ((x0 + x1) / 2 - X0, Y1 - y1)
    if door is None:
        door = (W / 2, 0)
    # le sol ne déborde que d'un peu autour des objets (la place dallée des packs n'est pas recopiée)
    margin = None if opt.get('interior') else opt.get('margin', 2)
    if margin is not None:
        keep = Image.new('L', (W, H), 0)
        from PIL import ImageDraw
        dr = ImageDraw.Draw(keep)
        for p in pieces:
            if p['kind'] == 'character':
                continue
            hh = p['h'] * (0.6 if p['kind'] == 'building' else 1.0)   # pas de dallage derrière les toits
            x0, y0 = p['x'] - margin * tw, H - p['y'] - hh - margin * th
            dr.rectangle([x0, y0, p['x'] + p['w'] + margin * tw - 1, H - p['y'] + margin * th - 1], fill=255)
        alpha = g.getchannel('A')
        from PIL import ImageChops
        g.putalpha(ImageChops.multiply(alpha, keep))
    feather(g, 0 if opt.get('interior') else opt.get('feather', 18))
    g.save(os.path.join(out, 'ground.png'))
    solids = []
    if opt.get('interior'):
        # pièce : on marche sur les cases de sol qui ne portent pas de mur ; le reste (dans le cadre) est solide.
        # Porte : la case praticable la plus basse, au plus près du milieu.
        floor_cells, wall_cells = set(), set()
        for c in ground:
            if c.layer in opt.get('walls', ['Walls']):
                wall_cells.add((c.x, c.y))          # faces des murs (les hauts et les cloisons décoratives restent franchissables)
            elif not re.search(r'wall|window', c.layer, re.I):
                floor_cells.add((c.x, c.y))
        walk = {(x, y) for x, y in floor_cells - wall_cells if X0 // tw < x < (X0 + W) // tw - 1}
        if opt.get('trim') and walk:
            top, bot = opt['trim']   # rangées de sol sous le mur du fond / sous le mur de façade
            ymin, ymax = min(y for x, y in walk), max(y for x, y in walk)
            walk = {(x, y) for x, y in walk if ymin + top <= y <= ymax - bot}
        cx0, cy0 = X0 // tw, Y0 // th
        cw, ch = W // tw, H // th
        mid = cx0 + cw / 2.0
        low = max(y for x, y in walk)
        dx, dy = min(((x, y) for x, y in walk if y >= low - 1), key=lambda c: (abs(c[0] + 0.5 - mid), -c[1]))
        # seule la partie reliée à la porte compte (pas d'îlots de sol hors de la pièce)
        reach, todo = {(dx, dy)}, [(dx, dy)]
        while todo:
            x, y = todo.pop()
            for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if n in walk and n not in reach:
                    reach.add(n); todo.append(n)
        walk = reach
        # le passage de la porte descend jusqu'au bas de la carte (on sort par le bas)
        gap = [x for x in range(dx - 2, dx + 3) if (x, dy) in walk]
        for x in gap:
            for y in range(dy + 1, min(dy + 4, cy0 + ch)):   # le seuil : trois cases sous la porte, pour sortir
                walk.add((x, y))
        door = ((dx + 0.5) * tw - X0, Y1 - (dy + 1) * th)
        # rectangles solides : rangées de cases non praticables, fusionnées verticalement
        runs = {}
        for y in range(cy0, cy0 + ch):
            x = cx0
            while x < cx0 + cw:
                if (x, y) in walk:
                    x += 1
                    continue
                x1 = x
                while x1 < cx0 + cw and (x1, y) not in walk:
                    x1 += 1
                runs.setdefault((x, x1), []).append(y)
                x = x1
        for (xa, xb), ys in runs.items():
            ys.sort()
            start = prev = ys[0]
            for y in ys[1:] + [None]:
                if y is not None and y == prev + 1:
                    prev = y
                    continue
                # cases [xa, xb) x [start, prev] → pixels, origine en bas à gauche, y vers le haut
                solids.append([xa * tw - X0, Y1 - (prev + 1) * th, (xb - xa) * tw, (prev - start + 1) * th])
                if y is not None:
                    start = prev = y
    manifest = dict(id=sid, w=W, h=H, tile=tw, door=[round(door[0], 1), round(door[1], 1)], pieces=pieces, solids=[v for r in solids for v in r])
    with open(os.path.join(out, sid + '.json'), 'w', encoding='utf-8') as f:
        json.dump(manifest, f, indent=1)
    kinds = {}
    for p in pieces:
        kinds[p['kind']] = kinds.get(p['kind'], 0) + 1
    print('%-10s %4dx%-4d px  %2d morceaux %s  porte %s' % (sid, W, H, len(pieces), kinds, manifest['door']))
    return manifest


def feather(img, width):
    """Bords du sol fondus dans le terrain de l'île : tramage ordonné (pixel art) qui s'éclaircit vers le bord,
    sur un bord légèrement bruité — plus de rectangle net autour des scènes."""
    if not width:
        return
    import numpy as np
    from scipy import ndimage
    a = np.array(img.getchannel('A'))
    inside = a > 0
    dist = ndimage.distance_transform_edt(np.pad(inside, 1))[1:-1, 1:-1]   # le bord de l'image compte comme un bord
    h, w = a.shape
    rng = np.random.default_rng(7)
    wob = ndimage.gaussian_filter(rng.random((h, w)), 6)
    wob = (wob - wob.min()) / (wob.max() - wob.min() + 1e-9)
    bayer = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0
    thr = np.tile(bayer, (h // 4 + 1, w // 4 + 1))[:h, :w]
    t = np.clip((dist - wob * width * 1.1) / (width * 0.5), 0, 1)
    keep = inside & (t > thr)
    a = np.where(keep, a, 0).astype(np.uint8)
    img.putalpha(Image.fromarray(a))


def preview(sid, scale=2):
    """Aperçu : sol + morceaux recomposés (contrôle visuel)."""
    out = os.path.join(OUT, sid)
    m = json.load(open(os.path.join(out, sid + '.json'), encoding='utf-8'))
    im = Image.open(os.path.join(out, 'ground.png')).copy()
    for p in sorted(m['pieces'], key=lambda p: -p['y']):
        s = Image.open(os.path.join(out, p['file'])).crop((0, 0, p['w'], p['h']))
        im.alpha_composite(s, (p['x'], m['h'] - p['y'] - p['h']))
    return im.resize((im.width * scale, im.height * scale), Image.NEAREST)


if __name__ == '__main__':
    ids = [a for a in sys.argv[1:] if not a.startswith('--')] or list(STAMPS)
    for sid in ids:
        build(sid, STAMPS[sid])
    if '--preview' in sys.argv:
        tmp = os.path.join(ROOT, 'Temp', 'stamps')
        os.makedirs(tmp, exist_ok=True)
        for sid in ids:
            preview(sid).save(os.path.join(tmp, sid + '.png'))
