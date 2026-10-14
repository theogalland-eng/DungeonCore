"""
Soumet les objets manquants de pixellab_assets.json (create_map_object, 1 génération chacun),
attend leur fin et enregistre les PNG dans Assets/Art/PixelLab/<name>.png.
Usage : python "Tools~/pixellab_fetch.py"   (PIXELLAB_TOKEN dans l'environnement)
"""
import base64, json, os, re, sys, time, urllib.request
sys.path.insert(0, os.path.dirname(__file__))
import pixellab as pl

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "PixelLab")
JOBS = os.path.join(ROOT, "Tools~", "pixellab_ids.json")


def call(sid, tool, args):
    _, msgs = pl.post({"jsonrpc": "2.0", "id": 9, "method": "tools/call", "params": {"name": tool, "arguments": args}}, sid)
    texts, images = [], []
    for m in msgs:
        for c in m.get("result", {}).get("content", []):
            if c.get("type") == "text":
                texts.append(c["text"])
            elif c.get("type") == "image":
                images.append(c.get("data", ""))
    return "\n".join(texts), images


def main():
    os.makedirs(OUT, exist_ok=True)
    items = json.load(open(os.path.join(ROOT, "Tools~", "pixellab_assets.json")))
    ids = json.load(open(JOBS)) if os.path.exists(JOBS) else {}
    sid = pl.session()
    # 1. soumission (avec pauses si limite de débit)
    for it in items:
        if it["name"] in ids or os.path.exists(os.path.join(OUT, it["name"] + ".png")):
            continue
        args = {"description": it["description"], "width": it["width"], "height": it["height"], "view": "high top-down",
                "outline": "single color outline", "shading": "medium shading", "detail": "medium detail"}
        for attempt in range(12):
            text, _ = call(sid, "create_map_object", args)
            m = re.search(r"id: ([0-9a-f-]{36})", text)
            if m:
                ids[it["name"]] = m.group(1)
                print("submitted", it["name"], m.group(1), flush=True)
                json.dump(ids, open(JOBS, "w"), indent=1)
                break
            print("wait", it["name"], text.strip()[:80], flush=True)
            time.sleep(30)
    # 2. récupération
    pending = {k: v for k, v in ids.items() if not os.path.exists(os.path.join(OUT, k + ".png"))}
    for _ in range(40):
        if not pending:
            break
        for name, oid in list(pending.items()):
            text, images = call(sid, "get_map_object", {"object_id": oid})
            if "failed" in text.lower() and "status" in text.lower():
                print("FAILED", name, text[:200], flush=True)
                pending.pop(name)
                continue
            url = re.search(r"(https?://\S+\.png)", text)
            data = None
            if images:
                data = base64.b64decode(images[0])
            elif url and "completed" in text.lower():
                data = urllib.request.urlopen(url.group(1), timeout=60).read()
            if data:
                open(os.path.join(OUT, name + ".png"), "wb").write(data)
                print("saved", name, flush=True)
                pending.pop(name)
        if pending:
            time.sleep(20)
    print("remaining", list(pending))


if __name__ == "__main__":
    main()
