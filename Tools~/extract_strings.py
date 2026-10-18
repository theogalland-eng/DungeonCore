"""
Extrait les textes affichés du code C# (littéraux et chaînes interpolées) pour la traduction anglaise.
Chaque chaîne est découpée comme le fait Loc à l'exécution : par balises de texte riche (<color>, <size>...)
et par lignes ; les trous d'interpolation {expr} deviennent {0}, {1}...

Sortie : Tools~/strings_fr.txt (une entrée par ligne, sans doublon). Les entrées déjà traduites dans
Assets/Resources/Localization/en.txt sont ignorées (option --missing).

    python "Tools~/extract_strings.py" [--missing]
"""
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(__file__), "..", "Assets", "scripts")
EN = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Localization", "en.txt")
OUT = os.path.join(os.path.dirname(__file__), "strings_fr.txt")

TAG = re.compile(r"<[^<>]*>")
SKIP_CALLS = ("[Tooltip(", "[Header(", "Tooltip(\"", "Header(\"", "[ContextMenu(", "[MenuItem(", "Debug.Log", "DLog.Log", "Debug.LogWarning", "Debug.LogError", "Resources.Load", "GetMethod", "Find(", "Shader.Find",
              "PlayerPrefs", "Sfx.Play", "Fx.Play", "SetFlag", "HasFlag", "PlayOnce(\"", "LoadAll", "GameObject(")
FRENCH = re.compile(r"[a-zàâçéèêëîïôûùüÿœæÀÂÇÉÈÊËÎÏÔÛÙÜŸŒÆ]", re.I)


def literals(src):
    """Renvoie (texte, est_interpolée, position) pour chaque littéral de chaîne."""
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if src.startswith("//", i):
            j = src.find("\n", i)
            i = n if j < 0 else j
            continue
        if src.startswith("/*", i):
            j = src.find("*/", i)
            i = n if j < 0 else j + 2
            continue
        if c == "'":
            j = i + 1
            while j < n and src[j] != "'":
                j += 2 if src[j] == "\\" else 1
            i = j + 1
            continue
        interp = False
        verbatim = False
        k = i
        if src[k] in "$@":
            while k < n and src[k] in "$@":
                interp |= src[k] == "$"
                verbatim |= src[k] == "@"
                k += 1
        if k < n and src[k] == '"' and (k == i or src[i] in "$@"):
            j = k + 1
            buf = []
            depth = 0
            while j < n:
                ch = src[j]
                if interp and ch == "{":
                    if src.startswith("{{", j) and depth == 0:
                        buf.append("{"); j += 2; continue
                    depth += 1
                elif interp and ch == "}":
                    if src.startswith("}}", j) and depth == 0:
                        buf.append("}"); j += 2; continue
                    depth -= 1
                elif depth == 0 and ch == "\\" and not verbatim:
                    buf.append(src[j:j + 2]); j += 2; continue
                elif depth == 0 and ch == '"':
                    if verbatim and src.startswith('""', j):
                        buf.append('"'); j += 2; continue
                    break
                elif depth > 0 and ch == '"':
                    # chaîne imbriquée dans un trou : on la saute
                    j += 1
                    while j < n and src[j] != '"':
                        j += 2 if src[j] == "\\" else 1
                buf.append(ch)
                j += 1
            yield "".join(buf), interp, i
            i = j + 1
            continue
        i += 1


def holes_to_placeholders(s):
    out, idx, depth, cur = [], 0, 0, []
    for ch in s:
        if ch == "{":
            depth += 1
            if depth == 1:
                continue
        elif ch == "}":
            depth -= 1
            if depth == 0:
                out.append("{%d}" % idx); idx += 1
                continue
        if depth == 0:
            out.append(ch)
    return "".join(out)


def segments(text):
    text = text.replace("\\n", "\n").replace('\\"', '"').replace("\\\\", "\\")
    for part in TAG.split(text):
        for line in part.split("\n"):
            seg = line.strip()
            if not seg or not FRENCH.search(seg):
                continue
            # un segment fait seulement de trous et de ponctuation n'a rien à traduire
            if not re.sub(r"\{\d+\}", "", seg).strip(" .,:;!?()+-·%/×«»—–'\"0123456789"):
                continue
            yield seg


def main():
    missing = "--missing" in sys.argv
    known = set()
    if missing and os.path.exists(EN):
        for line in open(EN, encoding="utf-8"):
            if "\t" in line and not line.startswith("#"):
                known.add(line.split("\t", 1)[0])
    seen, order = set(), []
    sources = []
    for dirpath, _, files in os.walk(ROOT):
        sources += [os.path.join(dirpath, f) for f in files if f.endswith(".cs")]
    # noms et descriptions du catalogue (écrits dans l'asset par l'éditeur)
    sources.append(os.path.join(ROOT, "..", "Editor", "CatalogBuilder.cs"))
    for path in sources:
            f = os.path.basename(path)
            if f in ("PlayerControls.cs",):
                continue
            src = open(path, encoding="utf-8-sig").read()
            for text, interp, pos in literals(src):
                line_start = src.rfind("\n", 0, pos) + 1
                ctx = src[line_start:pos]
                if any(k in ctx for k in SKIP_CALLS):
                    continue
                if " " not in text and not re.search(r"[àâçéèêëîïôûùüœ]", text):
                    continue   # identifiants, chemins, noms d'objets
                if re.fullmatch(r"[\w/._\-]+", text) or text.startswith("DungeonCore/"):
                    continue
                t = holes_to_placeholders(text) if interp else text
                for seg in segments(t):
                    if seg in seen or seg in known:
                        continue
                    seen.add(seg)
                    order.append((f, seg))
    with open(OUT, "w", encoding="utf-8") as fo:
        cur = None
        for f, seg in order:
            if f != cur:
                fo.write(f"# {f}\n")
                cur = f
            fo.write(seg + "\n")
    print(len(order), "segments ->", OUT)


main()
