"""
Icônes pixel de l'interface (16 x 16), palette de la maquette Design (UI) : cœur, mana, niveau, menu, colonie,
volcan, pause, lecture, menace, bateau, épée. Dessinées en « ASCII art » pour rester nettes au pixel près.
Sortie : Assets/Resources/UI/Icons/icon_<nom>.png  (import : DungeonCore/UI · UIArt.ImportIcons)
Usage  : python "Tools~/make_ui_icons.py"
"""
import os

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'UI', 'Icons')

PAL = {
    'O': '#120F1A', 'R': '#D9534A', 'r': '#8F2F35', 'W': '#FFD8CF',
    'M': '#8E7CFF', 'm': '#5A48C0', 'w': '#E2DCFF',
    'G': '#C9A45C', 'Y': '#F2D27A', 'g': '#8A6A34',
    'T': '#EFE9DC', 't': '#A59FB6',
    'S': '#6B6470', 's': '#3D3739', 'L': '#FF7A2A',
    'A': '#5CC4C0', 'a': '#1F7A8C', 'B': '#A8361C', 'b': '#5A4228', 'D': '#D8C8B0', 'K': '#8A6A44',
}

ICONS = {
    'heart': [
        '.OOO...OOO.',
        'OWWRO.ORRRO',
        'OWRRRORRRRO',
        'ORRRRRRRRrO',
        'ORRRRRRRRrO',
        '.ORRRRRRrO.',
        '..ORRRRrO..',
        '...ORRrO...',
        '....ORO....',
        '.....O.....',
    ],
    'mana': [
        '....O....',
        '...OwO...',
        '..OwMmO..',
        '.OwMMmmO.',
        'OwMMMmmmO',
        'OMMMMmmmO',
        'OMMMMmmmO',
        '.OMMMmmO.',
        '..OMMmO..',
        '...OMO...',
        '....O....',
    ],
    'star': [
        '.....O.....',
        '....OYO....',
        '....OYO....',
        'OOOOGYGOOOO',
        'OYYYYYYYGGO',
        '.OGYYYYYGO.',
        '..OGYYYGO..',
        '..OGYGYGO..',
        '.OGGO.OGGO.',
        '.OGO...OGO.',
        '.OO.....OO.',
    ],
    'menu': [
        'OOOOOOOOOOO',
        'OTTTTTTTTtO',
        'OOOOOOOOOOO',
        '...........',
        'OOOOOOOOOOO',
        'OTTTTTTTTtO',
        'OOOOOOOOOOO',
        '...........',
        'OOOOOOOOOOO',
        'OTTTTTTTTtO',
        'OOOOOOOOOOO',
    ],
    'house': [
        '.....O.....',
        '....OBO....',
        '...OBBBO...',
        '..OBBBBBO..',
        '.OBBBBBBBO.',
        'OOOOOOOOOOO',
        '.ODDDDDDDO.',
        '.ODDOOODDO.',
        '.ODDObODDO.',
        '.ODDObODDO.',
        '.OOOOOOOOO.',
    ],
    'volcano': [
        '....OLO....',
        '...OLLLO...',
        '...OSLSO...',
        '..OSSLSSO..',
        '..OSSsSSO..',
        '.OSSSssSSO.',
        '.OSSssssSO.',
        'OSSSsssssSO',
        'OOOOOOOOOOO',
    ],
    'pause': [
        'OOOO.OOOO',
        'OTtO.OTtO',
        'OTtO.OTtO',
        'OTtO.OTtO',
        'OTtO.OTtO',
        'OTtO.OTtO',
        'OTtO.OTtO',
        'OTtO.OTtO',
        'OOOO.OOOO',
    ],
    'play': [
        'OO......',
        'OTOO....',
        'OTTTOO..',
        'OTTTTTOO',
        'OTTTTTtO',
        'OTTTtOO.',
        'OTtOO...',
        'OOO.....',
    ],
    'skull': [
        '..OOOOOOO..',
        '.OTTTTTTTO.',
        'OTTTTTTTTtO',
        'OTOOTTTOOtO',
        'OTOOTTTOOtO',
        'OTTTTOTTTtO',
        '.OTTTTTTtO.',
        '..OTOTOtO..',
        '..OOOOOOO..',
    ],
    'boat': [
        '....O......',
        '....OTO....',
        '....OTTO...',
        '....OTTtO..',
        '....OTTttO.',
        '....OOOOOO.',
        'OOOOOOOOOOO',
        'OKKKKKKKKbO',
        '.OKKKKKKbO.',
        '..OOOOOOO..',
    ],
    'lock': [
        '...OOOOO...',
        '..OtOOOtO..',
        '..OtO.OtO..',
        '..OtO.OtO..',
        '.OOOOOOOOO.',
        '.OGYYYYYGO.',
        '.OGYYOYYgO.',
        '.OGYYOYYgO.',
        '.OGGGGGggO.',
        '.OOOOOOOOO.',
    ],
    'people': [
        '...OOO.....',
        '..OTTTO....',
        '..OTTTO.OO.',
        '...OOO.OTTO',
        '..OMMMOOTTO',
        '.OMMMMMOOO.',
        '.OMMMMMOGGO',
        '.OMmmmMOGGO',
        '.OOOOOOOOOO',
    ],
    'coin': [
        '...OOOOO...',
        '..OYYYYYO..',
        '.OYYGGGYGO.',
        '.OYGYYYGgO.',
        '.OYGYYYGgO.',
        '.OYGYYYGgO.',
        '.OYYGGGGgO.',
        '..OGgggggO.',
        '...OOOOO...',
    ],
    'log': [
        '..OOOOOOOO.',
        '.OKKKKKKKDO',
        'OKKbKKKKODO',
        'OKKKKbKKODO',
        'OKbKKKKKODO',
        '.OKKKKKKKOO',
        '..OOOOOOOO.',
    ],
    'rock': [
        '....OOO....',
        '..OOSSSOO..',
        '.OSSSSSsSO.',
        'OSSSSSssssO',
        'OSSSsssssSO',
        '.OsssssssO.',
        '..OOOOOOO..',
    ],
    'fish': [
        '...OOOO..OO',
        '.OOAAAAOOAO',
        'OTAATAAAAaO',
        'OAAAAAAAaO.',
        '.OOaaaaOOaO',
        '...OOOO..OO',
    ],
    'mood': [
        '..OOOOOOO..',
        '.OYYYYYYYO.',
        'OYYOYYYOYGO',
        'OYYOYYYOYGO',
        'OYYYYYYYYGO',
        'OYOYYYYOYGO',
        'OYYOOOOYGGO',
        '.OYYYYYGGO.',
        '..OOOOOOO..',
    ],
    'close': [
        'OO.....OO',
        'OTO...OTO',
        '.OTO.OTO.',
        '..OTOTO..',
        '...OTO...',
        '..OTOTO..',
        '.OTO.OTO.',
        'OTO...OTO',
        'OO.....OO',
    ],
    'torch': [
        '...OLO...',
        '..OLYLO..',
        '..OLYLO..',
        '...OLO...',
        '...OKO...',
        '...OKO...',
        '...OKO...',
        '...ObO...',
        '...OOO...',
    ],
    'moon': [
        '...OOOO...',
        '..OttTTO..',
        '.OtTOOOO..',
        'OtTO......',
        'OtTO......',
        'OtTO......',
        'OtTTO.OO..',
        '.OtTTOOTO.',
        '..OttTTO..',
        '...OOOO...',
    ],
    'sword': [
        '.........OO',
        '........OTO',
        '.......OTtO',
        '......OTtO.',
        '.....OTtO..',
        '.O..OTtO...',
        '.OOOTtO....',
        '..OGOO.....',
        '.OGOOO.....',
        'OGO..O.....',
        'OO.........',
    ],
}


def main():
    os.makedirs(OUT, exist_ok=True)
    sheet = Image.new('RGBA', (len(ICONS) * 18, 18), (27, 23, 38, 255))
    for i, (name, rows) in enumerate(ICONS.items()):
        img = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
        h, w = len(rows), max(len(r) for r in rows)
        ox, oy = (16 - w) // 2, (16 - h) // 2
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if ch in PAL:
                    c = PAL[ch].lstrip('#')
                    img.putpixel((ox + x, oy + y), tuple(int(c[k:k + 2], 16) for k in (0, 2, 4)) + (255,))
        img.save(os.path.join(OUT, f'icon_{name}.png'))
        sheet.alpha_composite(img, (i * 18 + 1, 1))
    sheet.resize((sheet.width * 6, sheet.height * 6), Image.NEAREST).save(
        os.path.join(ROOT, 'Temp', 'ui_icons_preview.png'))
    print('icônes :', ', '.join(ICONS))


if __name__ == '__main__':
    main()
