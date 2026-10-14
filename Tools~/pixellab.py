"""
Mini client MCP (HTTP) pour PixelLab.
Usage :
  python "Tools~/pixellab.py" list                      -> outils disponibles
  python "Tools~/pixellab.py" call <outil> '<json args>' -> appelle un outil
Le jeton est lu dans la variable d'environnement PIXELLAB_TOKEN.
"""
import json, os, sys, urllib.request

URL = "https://api.pixellab.ai/mcp"
TOKEN = os.environ.get("PIXELLAB_TOKEN", "")


def post(payload, session=None):
    headers = {
        "Authorization": f"Bearer {TOKEN}",
        "Content-Type": "application/json",
        "Accept": "application/json, text/event-stream",
    }
    if session:
        headers["Mcp-Session-Id"] = session
    req = urllib.request.Request(URL, data=json.dumps(payload).encode(), headers=headers, method="POST")
    with urllib.request.urlopen(req, timeout=180) as r:
        sid = r.headers.get("Mcp-Session-Id") or session
        body = r.read().decode("utf-8", "replace")
    # réponse JSON directe ou flux SSE ("data: {...}")
    msgs = []
    if body.lstrip().startswith("{"):
        msgs.append(json.loads(body))
    else:
        for line in body.splitlines():
            if line.startswith("data:"):
                try:
                    msgs.append(json.loads(line[5:].strip()))
                except json.JSONDecodeError:
                    pass
    return sid, msgs


def session():
    sid, _ = post({"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {
        "protocolVersion": "2025-03-26", "capabilities": {}, "clientInfo": {"name": "dungeoncore", "version": "1"}}})
    post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)
    return sid


def main():
    sid = session()
    if sys.argv[1] == "list":
        _, msgs = post({"jsonrpc": "2.0", "id": 2, "method": "tools/list"}, sid)
        for m in msgs:
            for t in m.get("result", {}).get("tools", []):
                print(t["name"])
        return
    tool, args = sys.argv[2], json.loads(sys.argv[3]) if len(sys.argv) > 3 else {}
    _, msgs = post({"jsonrpc": "2.0", "id": 3, "method": "tools/call", "params": {"name": tool, "arguments": args}}, sid)
    for m in msgs:
        if "error" in m:
            print("ERROR", json.dumps(m["error"]))
        for c in m.get("result", {}).get("content", []):
            if c.get("type") == "text":
                print(c["text"])
            elif c.get("type") == "image":
                print(f"[image {c.get('mimeType')} {len(c.get('data', ''))} b64 chars]")


if __name__ == "__main__":
    main()
