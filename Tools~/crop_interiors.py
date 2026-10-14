"""
Intérieurs des bâtiments de la colonie : découpe le mobilier, les murs, les sols et les personnages animés
des packs craftpix (taverne, hall de la guilde, forge, tour des mages) vers Assets/Resources/Interiors.
Les animations sont enregistrées en bandes nommées nom_LxH.png (découpées en images par ArtImporter).

    python "Tools~/crop_interiors.py"
"""
import os
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.join(os.path.dirname(__file__), "..", "Assets", "imports", "new", "to_add")
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Interiors")
os.makedirs(OUT, exist_ok=True)

TAV = "craftpix-net-666104-tavern-top-down-pixel-rpg-asset-pack/Tiled_files/"
GUILD = "craftpix-net-189780-free-top-down-pixel-art-guild-hall-asset-pack/PNG/"
SMITH = "craftpix-net-741016-pixel-blacksmith-house-interior-and-exterior-assets/PNG/"
MAGE = "craftpix-net-289481-mage-tower-top-down-pixel-art-asset-pack/PNG/"


def load(p):
    return Image.open(os.path.join(ROOT, p)).convert("RGBA")


def boxes(im):
    """Même découpage que l'aperçu numéroté : blocs d'alpha (dilatés d'1 px), triés par ligne puis colonne."""
    a = np.array(im)[:, :, 3] > 0
    lab, n = ndimage.label(ndimage.binary_dilation(a, iterations=1))
    out = []
    for sl in ndimage.find_objects(lab):
        y0, y1, x0, x1 = sl[0].start, sl[0].stop, sl[1].start, sl[1].stop
        if (x1 - x0) * (y1 - y0) >= 60:
            out.append((x0, y0, x1 - x0, y1 - y0))
    out.sort(key=lambda b: (b[1] // 24, b[0]))
    return out


def save(im, name):
    im.save(os.path.join(OUT, name + ".png"))


def pieces(sheet, mapping):
    im = load(sheet)
    bx = boxes(im)
    for idx, name in mapping.items():
        x, y, w, h = bx[idx]
        save(im.crop((x, y, x + w, y + h)), name)


# ── Mobilier ──
pieces(TAV + "Interior_1st_floor.png", {
    0: "tav_long_table", 2: "tav_shelf_bottles", 3: "tav_shelf", 4: "tav_stairs", 5: "tav_bar", 9: "tav_table_cloth",
    11: "tav_barrel_big", 12: "tav_barrel", 13: "tav_booth", 15: "tav_round_table", 16: "tav_bench", 19: "tav_table_cloth2",
    21: "tav_round_table2", 27: "tav_rug", 30: "tav_plant", 57: "tav_jugs",
})
pieces(GUILD + "Interior_objects.png", {
    5: "gld_dragon_skull", 19: "gld_cabinet", 22: "gld_bookshelf", 26: "gld_board", 27: "gld_board2", 31: "gld_armor",
    33: "gld_rug", 36: "gld_desk", 41: "gld_long_desk", 42: "gld_weapons", 45: "gld_banner", 46: "gld_vase", 60: "gld_chest",
})
pieces(SMITH + "House_interior_objects.png", {
    0: "smt_furnace", 9: "smt_quench", 10: "smt_rack", 12: "smt_hide", 24: "smt_anvil", 29: "smt_barrels", 39: "smt_wood",
    40: "smt_coal", 5: "smt_armor_crate", 48: "smt_workbench", 49: "smt_grindstone",
})
pieces(MAGE + "Interior_objects.png", {
    0: "mag_bookshelves", 1: "mag_bookshelf", 4: "mag_desk", 15: "mag_circle", 22: "mag_crystal", 23: "mag_hourglass",
    25: "mag_skull", 26: "mag_orb", 28: "mag_shelf", 35: "mag_rug",
})

# ── Murs (bandeaux avec fenêtres) et sols (motifs à répéter) ──
def crop(sheet, box, name):
    x, y, w, h = box
    save(load(sheet).crop((x, y, x + w, y + h)), name)


crop(TAV + "Walls_interior.png", (0, 190, 160, 46), "wall_tavern")
crop(GUILD + "Walls_interior.png", (12, 80, 263, 44), "wall_guild")
crop(SMITH + "Walls_interior.png", (172, 1, 216, 44), "wall_smith")
crop(MAGE + "Interior_walls.png", (0, 304, 192, 45), "wall_mage")
crop(GUILD + "Walls_interior.png", (288, 80, 48, 48), "floor_wood")
crop(MAGE + "Interior_walls.png", (0, 368, 32, 32), "floor_stone")
crop(SMITH + "Walls_interior.png", (176, 48, 112, 16), "floor_cobble")

# ── Personnages et feux animés (bandes horizontales) ──
def strip(sheet, fw, fh, name, frames=None):
    im = load(sheet)
    n = im.width // fw
    if frames:
        n = min(n, frames)
    im = im.crop((0, 0, n * fw, fh))
    save(im, f"{name}_{fw}x{fh}")


strip(TAV + "Animation_host.png", 32, 32, "npc_host")
strip(TAV + "Animation_Lute_player_full.png", 32, 48, "npc_bard")
strip(TAV + "Animation_Drinker1.png", 32, 40, "npc_drinker1")
strip(TAV + "Animation_Drinker2.png", 32, 40, "npc_drinker2")
strip(TAV + "Animation_dancer.png", 48, 48, "npc_dancer")
strip(TAV + "Animation_sleep_guy2.png", 40, 32, "npc_sleeper")
strip(GUILD + "Guildmaster.png", 48, 32, "npc_guildmaster")
strip(GUILD + "Talking_people.png", 48, 48, "npc_talkers")
strip(GUILD + "Reader1.png", 32, 48, "npc_reader")
strip(GUILD + "Fire.png", 48, 48, "fx_hearth")
strip(SMITH + "Forge_animation.png", 48, 64, "fx_forge")
strip(SMITH + "Girl_animation.png", 32, 48, "npc_apprentice")

print("ok", len([f for f in os.listdir(OUT) if f.endswith(".png")]), "images")
