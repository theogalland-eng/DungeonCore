"""
Assemble le dictionaire anglais : Tools~/strings_fr.txt (extrait par extract_strings.py) ligne à ligne avec
Tools~/loc/en_*.txt (mêmes lignes, en anglais, en-têtes « # fichier » compris), plus Tools~/loc/en_extra.txt
(paires « français TAB anglais » écrites à la main). Les entrées déjà présentes dans en.txt et absentes
de la nouvelle extraction sont conservées.

Sortie : Assets/Resources/Localization/en.txt

    python "Tools~/build_loc.py"                       (tout)
    python "Tools~/build_loc.py" "Tools~/loc/add_N.txt"  (ajout après extract_strings.py --missing)
"""
import glob
import os
import sys

HERE = os.path.dirname(__file__)
OUT = os.path.join(HERE, "..", "Assets", "Resources", "Localization", "en.txt")


def lines(path):
    return [l.rstrip("\n").rstrip("\r") for l in open(path, encoding="utf-8")]


fr = lines(os.path.join(HERE, "strings_fr.txt"))
en = []
if len(sys.argv) > 1:
    # ajout : strings_fr.txt vient de « extract_strings.py --missing », l'anglais est dans le fichier donné
    en = lines(sys.argv[1])
else:
    for p in sorted(glob.glob(os.path.join(HERE, "loc", "en_[0-9]*.txt")), key=lambda s: int(s.rsplit("_", 1)[1].split(".")[0])):
        en += lines(p)
fr = [l for l in fr if l.strip()]
en = [l for l in en if l.strip()]
if len(fr) != len(en):
    print(f"ERREUR : {len(fr)} lignes françaises, {len(en)} anglaises")
    for i, (a, b) in enumerate(zip(fr, en)):
        if a.startswith("#") != b.startswith("#") or (a.startswith("#") and a != b):
            print("premier décalage ligne", i + 1, ":", a, "|", b)
            break
    sys.exit(1)

entries = {}
order = []
for a, b in zip(fr, en):
    if a.startswith("#"):
        if a != b:
            print("ERREUR en-tête :", a, "|", b)
            sys.exit(1)
        continue
    if a not in entries:
        order.append(a)
    entries[a] = b
for l in lines(os.path.join(HERE, "loc", "en_extra.txt")):
    if "\t" in l and not l.startswith("#"):
        a, b = l.split("\t", 1)
        if a not in entries:
            order.append(a)
        entries[a] = b
# entrées anciennes conservées
if os.path.exists(OUT):
    for l in lines(OUT):
        if "\t" in l and not l.startswith("#"):
            a, b = l.split("\t", 1)
            if a not in entries:
                order.append(a)
                entries[a] = b

os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, "w", encoding="utf-8", newline="\n") as fo:
    fo.write("# Traduction anglaise (Loc) : français TAB anglais. {0}, {1}... : trous (noms, nombres).\n")
    for a in order:
        fo.write(a.replace("\n", "\\n") + "\t" + entries[a].replace("\n", "\\n") + "\n")
print(len(order), "entrées ->", os.path.abspath(OUT).encode("ascii", "replace").decode())
