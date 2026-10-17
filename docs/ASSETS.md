<div align="center">

# 📦 Assets et licences

*Ce qui est dans ce dépôt, ce qui n'y est pas, et où le retrouver.*

[← Retour au README](../README.md)

</div>

---

## ✅ Inclus dans le dépôt

| Contenu | Emplacement | Origine |
|---|---|---|
| Code du jeu et des outils éditeur | `Assets/scripts`, `Assets/Editor` | Créé pour le projet |
| Scripts Python | `Tools~/` | Créé pour le projet |
| Scène, prefabs, animations, catalogue | `Assets/Scenes`, `Assets/Prefabs`, `Assets/animations`, `Assets/Resources/*.asset` | Générés par nos menus éditeur |
| Créations PixelLab (bateaux, portes, pièges, personnages) | `Assets/Art/PixelLab` | Générées pour le projet avec [PixelLab](https://www.pixellab.ai) |
| Relief du volcan | `Assets/Art/Generated/Relief` | Calculé par `make_relief.py` |
| Bruitages et ambiances | `Assets/Resources/Audio/Sfx`, `…/Ambience` | Synthétisés par `make_sfx.py` et `make_ambience.py` |
| Mouette de l'intro, bannière | `Assets/Resources/FX/gull_32x32.png`, `docs/banner.png` | Dessinées par `make_gull.py` et `make_banner.py` |
| Polices Cinzel et Cinzel Decorative | `Assets/Fonts`, `Assets/Resources/Fonts` | © The Cinzel Project Authors — [SIL Open Font License 1.1](../Assets/Fonts/OFL.txt) |

## ❌ Non inclus (à récupérer)

Ces éléments sont utilisés par le jeu mais **leur licence interdit de les redistribuer tels quels**. Ils restent dans le projet local et sont exclus par le `.gitignore`.

### A* Pathfinding Project

- **Version** : gratuite, 4.2.17 — par Aron Granberg
- **Où** : [arongranberg.com/astar](https://arongranberg.com/astar/download)
- **Destination** : `Assets/AstarPathfindingProject/`

### TextMesh Pro Essentials

Fournis par Unity : *Window → TextMeshPro → Import TMP Essential Resources* (Unity le propose aussi à l'ouverture du projet).

### Packs graphiques craftpix.net

Tous viennent de [**craftpix.net**](https://craftpix.net) (gratuits ou payants). Leur [licence](https://craftpix.net/file-licenses/) autorise leur usage dans un jeu, mais pas leur redistribution en dehors du jeu. Les scripts de découpe attendent chaque pack **dans son dossier d'origine**, nommé `craftpix-net-<id>-<nom>` comme au téléchargement, dans le dossier indiqué.

<details open>
<summary><b>Dans <code>Assets/imports/</code></b> — tuiles, personnages, nature</summary>

| ID | Pack |
|---|---|
| 891121 | Free 2D RPG Desert Tileset |
| 141354 | Free Top Down Bushes Pixel Art |
| 160005 | Free Ruined Temple Top-Down Location Pixel Art |
| 169442 | Free 2D Top-Down Pixel Dungeon Asset Pack |
| 180537 | Free Swordsman 1-3 Level Pixel Top-Down Sprite Character |
| 218281 | Free Pixel Art Dungeon Objects Asset Pack |
| 363992 | Free Top-Down Orc Game Character Pixel Art |
| 385863 | Free Top-Down Trees Pixel Art |
| 504452 | Free Village Pixel Tileset for Top-Down Defense |
| 695666 | Free Undead Tileset Top-Down Pixel Art |
| 788364 | Free Slime Mobs Pixel Art Top-Down Sprite Pack |
| 789196 | Free Top-Down Hunt Animals Pixel Sprite Pack |

</details>

<details open>
<summary><b>Dans <code>Assets/imports/new/</code></b> — monstres, donjon, colonie</summary>

| ID | Pack |
|---|---|
| 125640 | Dungeon Tileset Pixel Top-Down for Indie Game |
| 200380 | Free Pixel Art Plants for Farm |
| 208004 | Free Vampire 4-Direction Pixel Character Sprite Pack |
| 298079 | Top-Down Dungeon Pixel Tileset for RPG and Roguelike Game |
| 299679 | Swordsman 7-9 Level Pixel Top-Down Sprite Character Pack |
| 393827 | Top-Down Pixel Gnolls Character Pack |
| 415491 | Giant Rat 4-Direction Pixel Character Sprite Pack |
| 447058 | Fantasy RPG Camp Top-Down Pixel Art Pack |
| 463118 | Demons 4-Direction Pixel Character Sprite Pack |
| 500988 | Imp Mobs Pixel Art Character Sprite Pack |
| 522456 | Treasure 32x32 Objects and Icons Pixel Pack |
| 543463 | Top-Down Pixel Lich Character Sprites |
| 550920 | Zombie 4-Direction Pixel Character Sprite Pack |
| 587497 | Pixel Art Market Square RPG Shop and NPC Assets Pack |
| 626036 | Pixel Art Training Arena Tileset for RPG Games |
| 665895 | Free Pixel Dungeon Props and Objects Asset Pack |
| 668008 | Free Bridges Top-Down Pixel Art Asset Pack |
| 710530 | Goblin Pixel Art Character Sprite Pack |
| 846584 | Top-Down Pixel Mushroom Sprite Pack |
| 870078 | Top-Down Pixel Skeletons Character Sprite Pack |
| 885927 | 2D Pixel Fishing Village Pack (dock, interior, boats, NPCs) |
| 894297 | Top-Down Pixel Ghost Character Sprite Pack |
| 900504 | Lizardmen Pixel Art Character Sprite Pack |
| 934911 | Swordsman Level 4-6 Pixel Character Top-Down Sprite Pack |

</details>

<details>
<summary><b>Dans <code>Assets/imports/new/to_add/</code></b> — bâtiments, intérieurs, golem</summary>

| ID | Pack |
|---|---|
| 189780 | Free Top-Down Pixel Art Guild Hall Asset Pack |
| 235765 | Rocky Top-Down Tileset Pixel Art for RPG |
| 289481 | Mage Tower Top-Down Pixel Art Asset Pack |
| 448424 | Winter Top-Down Pixel Art Tileset for RPG |
| 471853 | Top-Down Farm with Animals Pixel Art Asset Pack |
| 550931 | Path and Road Top-Down Tileset Pixel Art |
| 625807 | Golem Pixel Art Top-Down Sprite Pack |
| 666104 | Tavern Top-Down Pixel RPG Asset Pack |
| 688848 | Cave Tileset Top-Down Pixel Art |
| 692491 | Free Glassblowers Workshop Top-Down Pixel Art Asset |
| 741016 | Pixel Blacksmith House Interior and Exterior Assets |

</details>

<details>
<summary><b>Dans <code>Assets/imports/effects/</code></b> — effets visuels</summary>

| ID | Pack |
|---|---|
| 671189 | 10 Magic Sprite Sheet Effects Pixel Art |
| 671189 | Pixel Art Magic Sprite Effects and Icons Pack |
| 781160 | 10 Magic Effects Pixel Art Pack |
| 207889 | Magic Buff Effects Pack for Top-Down Games |
| 526500 | Smoke Effects Pixel Art |
| 819691 | Fire Magic Effects Pixel Art |

</details>

### Autres fichiers non publiés

- Quelques planches isolées dans `Assets/imports/` (`snow-expansion.png`, `dungeon_orb3.png`, `dungeon_tiles.png`, `full_traps.png`, `cave.png`, interface `gui_alpha…`), dont l'origine reste à documenter.
- Les **musiques** (`Assets/Resources/Audio/Music/*.mp3`) ont été **composées par l'auteur avec Gemini Music** (Google) et sont publiées avec le projet. Le fichier [`LISEZMOI.txt`](../Assets/Resources/Audio/Music/LISEZMOI.txt) explique comment nommer les pistes pour que le jeu les classe (jour, nuit, combat, héros, cinématique…).
- Tout ce qui est **dérivé directement des packs** : découpes `Assets/Art/Dungeon`, `Assets/Art/Settlement`, icônes, murs, effets, butin, intérieurs, tuiles et palettes. Les scripts de `Tools~/` et les menus éditeur les régénèrent.
