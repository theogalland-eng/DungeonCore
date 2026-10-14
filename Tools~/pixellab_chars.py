"""
Personnages PixelLab (mage, soigneuse, archer) : création 4 directions, animations, téléchargement.
Usage (PIXELLAB_TOKEN dans l'environnement) :
  python "Tools~/pixellab_chars.py" create        -> lance la création des personnages manquants
  python "Tools~/pixellab_chars.py" status        -> état + animations disponibles
  python "Tools~/pixellab_chars.py" animate <name> <template|v3:description> <dirs,séparées,virgule> [nom]
  python "Tools~/pixellab_chars.py" download      -> Assets/Art/PixelLab/Characters/<name>/<anim>/<dir>_<i>.png
Les identifiants sont stockés dans Tools~/pixellab_chars.json.
"""
import base64, json, os, re, sys, urllib.request
sys.path.insert(0, os.path.dirname(__file__))
import pixellab as pl

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
IDS = os.path.join(ROOT, 'Tools~', 'pixellab_chars.json')
OUT = os.path.join(ROOT, 'Assets', 'Art', 'PixelLab', 'Characters')

CHARS = {
    'mage': 'young human battle mage adventurer, deep blue hooded robe with golden trim, holding a wooden staff topped with a glowing blue crystal, brown leather boots',
    'healer': 'young human priestess healer adventurer, white and gold robes with a light hood, holding a short silver staff with a glowing golden sun emblem',
    'archer': 'human ranger archer adventurer, green hooded tunic over leather armor, holding a wooden longbow, quiver of arrows on the back',
}


def call(sid, tool, args):
    _, msgs = pl.post({"jsonrpc": "2.0", "id": 9, "method": "tools/call", "params": {"name": tool, "arguments": args}}, sid)
    texts = []
    for m in msgs:
        if 'error' in m:
            texts.append('ERROR ' + json.dumps(m['error']))
        for c in m.get('result', {}).get('content', []):
            if c.get('type') == 'text':
                texts.append(c['text'])
    return '\n'.join(texts)


def load():
    return json.load(open(IDS)) if os.path.exists(IDS) else {}


def save(d):
    json.dump(d, open(IDS, 'w'), indent=1)


def main():
    cmd = sys.argv[1]
    sid = pl.session()
    ids = load()
    if cmd == 'create':
        for name, desc in CHARS.items():
            if name in ids:
                continue
            t = call(sid, 'create_character', {
                'description': desc, 'name': name, 'mode': 'standard', 'n_directions': 4, 'size': 48,
                'view': 'low top-down', 'outline': 'single color black outline', 'shading': 'medium shading', 'detail': 'medium detail'})
            print(name, t[:400])
            m = re.search(r'([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})', t)
            if m:
                ids[name] = m.group(1)
                save(ids)
    elif cmd == 'status':
        for name, cid in ids.items():
            print('=====', name, cid)
            print(call(sid, 'get_character', {'character_id': cid})[:3000])
    elif cmd == 'animate':
        name, what, dirs = sys.argv[2], sys.argv[3], sys.argv[4].split(',')
        args = {'character_id': ids[name], 'directions': dirs}
        if what.startswith('v3:'):
            args['action_description'] = what[3:]
            args['mode'] = 'v3'
        else:
            args['template_animation_id'] = what
        if len(sys.argv) > 5:
            args['animation_name'] = sys.argv[5]
        print(call(sid, 'animate_character', args)[:1500])
    elif cmd == 'raw':
        print(call(sid, sys.argv[2], json.loads(sys.argv[3]))[:6000])


if __name__ == '__main__':
    main()
