"""
Télécharge les personnages PixelLab (zip « download ») vers Assets/Art/PixelLab/Characters/<name>/.
Usage : python "Tools~/pixellab_download.py" [name...]   (PIXELLAB_TOKEN dans l'environnement)
"""
import io, json, os, sys, urllib.request, zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
IDS = os.path.join(ROOT, 'Tools~', 'pixellab_chars.json')
OUT = os.path.join(ROOT, 'Assets', 'Art', 'PixelLab', 'Characters')
TOKEN = os.environ.get('PIXELLAB_TOKEN', '')


def fetch(url):
    req = urllib.request.Request(url, headers={'Authorization': f'Bearer {TOKEN}'})
    with urllib.request.urlopen(req, timeout=180) as r:
        return r.read()


def main():
    ids = json.load(open(IDS))
    names = sys.argv[1:] or list(ids)
    for name in names:
        data = fetch(f'https://api.pixellab.ai/mcp/characters/{ids[name]}/download')
        dest = os.path.join(OUT, name)
        os.makedirs(dest, exist_ok=True)
        z = zipfile.ZipFile(io.BytesIO(data))
        n = 0
        for info in z.infolist():
            if info.is_dir():
                continue
            target = os.path.join(dest, info.filename.replace('/', os.sep))
            os.makedirs(os.path.dirname(target), exist_ok=True)
            with open(target, 'wb') as f:
                f.write(z.read(info))
            n += 1
        print(name, n, 'fichiers ->', dest)


if __name__ == '__main__':
    main()
