<div align="center">

# 📜 Journal de développement

*Comment un prototype de « tower defense dans une grotte » est devenu un donjon vivant, une île, une Guilde et une ville.*

[← Retour au README](../README.md)

</div>

---

Le développement avance par **itérations courtes** : on joue, on note ce qui ne va pas (« les pêcheurs font des allers-retours », « les mages tirent à travers les portes », « c'est trop facile en Moyen »…), puis on corrige et on enrichit. Chaque section ci-dessous correspond à une de ces vagues.

> **Légende** — 🧱 fondations · 🎮 gameplay · 🧠 IA · 🎨 art & ambiance · 🎬 mise en scène · 🔧 technique · 🐛 corrections

---

## 🌱 Avant le 24 septembre — le prototype

Un premier donjon de test dans `SampleScene` : un gobelin, un aventurier, un piège à lame tournante, un orbe à défendre. Les prefabs `gobelin`, `Aventurier_lv1` et `TrapSpinningBlade` datent de cette époque et servent encore de modèles.

---

## 🧱 24 septembre — Fondations

**Décision clé :** la partie commence **sans donjon**. Le joueur part d'une petite grotte naturelle et construit tout.

- 🧱 **Île procédurale** (`Editor/IslandGenerator.cs`, graine 1337) : récifs, plage de sable noir, forêt, volcan, grotte de départ, sentier, ponton — générée par le menu `DungeonCore/3`.
- 🧱 **Étages thématiques** (lave, eaux, glace, champignons, cristal, puis crypte et forteresse) reliés par des escaliers.
- 🧱 **Interface entièrement en code** : `GameHUD` + panneau latéral repliable `SidePanel` ; catalogue des objets dans un `ScriptableObject` généré.
- 🔧 **Pipeline d'import** : découpe automatique des planches de personnages (4 directions, animations, contrôleurs, prefabs) et des tilesets.
- 🎨 **Murs auto-raccordés** (`AutoWallTile`, masque 128 voisins) générés par `make_walls.py` ; arbres triés par profondeur.
- 🎮 Fuite des intrus = jet de chance selon leur rang ; déplacement du cœur (coûteux, interdit pendant une intrusion).

## 🐉 25 septembre — Un écosystème

- 🎨 **PixelLab** : premières créations générées (bateaux, portes, pièges, escaliers) via un client MCP maison (`Tools~/pixellab.py`, jeton lu dans l'environnement).
- 🎮 **Familles de monstres à 3 stades d'évolution** (`MonsterUnit`, `MonsterTraits`) : XP, évolution, **entretien quotidien** payé en mana à l'aube.
- 🎮 **Colonie humaine** (`scripts/Settlement`) : parcelles prévues par le générateur d'île, bâtiments, colons.
- 🎬 Premières **cinématiques** : naissance de l'orbe, premier bateau.
- 🎨 `AmbientFx` : lucioles, nuages, poussière, reflets, bloom et vignette.
- 🔧 **Menu d'accueil, profils et sauvegardes** (auto + emplacements) dans la même scène ; chargement = rechargement de scène + réapplication de l'état.
- 🎮 **Butin et trophées** (27 objets), **relations** avec les humains (corruption, trêve, expédition punitive), **raids nocturnes**.
- 🐛 Les fuyards se glissent entre les monstres (couloirs d'une case qui se bloquaient).

## 🌋 26 septembre — 2.5D, Guilde et rangs

- 🎨 **Relief du volcan en 2.5D** (`make_relief.py` + `MountainRelief`) : vue « île » avec cratère fumant et geysers, vue « donjon » qui révèle l'intérieur.
- 🧠 **Groupes à rôles** : guerrier, archer, mage, soigneur.
- 🎨 Bibliothèque d'**effets visuels** (`Fx.Play`, projectiles).
- 🎮 **Guilde des aventuriers** (`GuildSystem`) : rangs **F → SSS** pour les aventuriers *et* les donjons ; signalement → équipe d'évaluation → verdict officiel → réévaluation à chaque expédition.
- 🎮 **4 difficultés** (Facile, Moyen, Difficile, Impitoyable sans sauvegarde manuelle).
- 🎬 Cinématiques en file d'attente : arrivée de la Guilde, verdict, étapes de la colonie.

## 😨 27 septembre — La peur, et beaucoup de systèmes

**Retour clé :** *« le joueur doit avoir peur en permanence »*.

- 🎮 **Plus aucune annonce** des bateaux : heure d'arrivée aléatoire chaque jour.
- 🐛 Les aventuriers ne restent plus coincés dans les angles des murs (rayon de passage des points de chemin).
- 🎨 **Musique adaptative** (`MusicManager`) et **ambiances procédurales** (ressac, souffle de caverne, battement de cœur quand des intrus rôdent).
- 🎨 **Bruitages procéduraux** (`make_sfx.py`).
- 🎮 **Événements d'île** : tempête, brouillard, éruption, lune de sang, naufrages.
- 🧠 **Némésis** : les survivants reviennent plus forts et désarment les pièges qu'ils connaissent.
- 🎮 **Pensées du Noyau** (objectifs discrets), **succès**, **prison** pour les intrus assommés, **gardiens d'étage** et monstres qui parlent (liche, vampire, démon, fantôme), **salles thématiques** (crypte, armurerie, trésorerie, sanctuaire).
- 🎨 Décor de caverne, façades de la colonie, **routes pavées**, **intérieurs** de bâtiments.
- 🗡️ **Mode Aventurier v1**, puis **v2** : 10 étages procéduraux, ville où l'on entre physiquement dans les bâtiments, groupe recruté à la Guilde, points de passage aux étages 4 et 7, pierres de rappel.
- 🎨 Portes et escaliers tirés des packs graphiques ; 🐛 joystick virtuel décalé (pivot mal mesuré).
- 🎮 **Économie au mana** : plus de limite de monstres ou de pièges, seule la réserve compte (150 au niveau 1, +60 par niveau) ; les coffres coûtent de l'entretien ; déblocages des monstres du niveau 1 (rat, slime) au niveau 20 ; étages aux niveaux 4 / 9 / 14.
- 🧠 Le **décor encombrant bloque** le passage (et ne peut pas couper le donjon en deux) ; plus aucune unité coincée dans un mur.

## 🌫️ 1er octobre — Le brouillard de guerre

**Retour clé :** *« ce qui est amusant, c'est de construire un labyrinthe ; il faut que les aventuriers soient obligés de cartographier »*.

- 🧠 **`IntruderMap`** : les intrus ne voient que ce qui est dans leur champ de vision et explorent par frontières au lieu de foncer vers l'orbe.
- 🧠 L'**équipe d'évaluation** de la Guilde explore puis repart, sans jamais attaquer le cœur.
- 🎨 La musique calme termine toujours son morceau ; seul le combat coupe.
- 🎨 **Nouveau menu d'accueil** (polices Cinzel, panneau en dégradé, boutons qui s'adaptent à l'écran du téléphone), interface adaptative et zones sûres.
- 🎬 La cinématique de la Guilde devient une **vraie scène d'intérieur** : un pêcheur affolé raconte tout au maître de guilde, avec bulles de dialogue.
- 🎨 Archer, mage et soigneur **redessinés à partir du sprite de l'aventurier** (`make_roles.py`) pour rester cohérents.
- 🔧 **APK Android en un clic** (`DungeonCore/10`).

## 🚪 2 octobre — Portes, exploration v2 et nouvelle intro

**Retours clés :** *« les pêcheurs font des allers-retours »*, *« les mages tirent à travers les portes »*, *« c'est trop facile »*.

- 🧠 **Exploration v2** : un intrus choisit une galerie et la suit jusqu'au bout avant de décider de continuer ou de faire demi-tour ; une zone vide déjà vue ne l'attire plus ; il contourne les objets ; il ne renonce plus jamais.
- 🧠 **Fin de la connaissance partagée** : chaque expédition a sa propre carte ; seuls les vétérans (Némésis) reviennent avec la leur.
- 🎮 **Portes opaques** : fermées, elles bloquent la vue, les flèches et les sorts ; les monstres ne les franchissent que **sur ordre** ; forcées une fois, elles restent ouvertes toute l'expédition. Nouveaux dessins **vus de profil** pour les couloirs gauche-droite.
- 🎮 Les intrus **pillent les coffres** (fouille animée, jauge dorée, butin qui jaillit).
- 🎮 **Difficulté** : pendant une attaque, impossible de creuser, poser, invoquer ou faire évoluer — *il faut toujours être prêt*.
- 🎬 **Nouvelle cinématique d'introduction** (7 scènes), adaptée du premier chapitre de *The Dungeon Without a System* et **cohérente avec les mécaniques** : il n'y a pas de « système » qui parle, mais le HUD représente les sens du Noyau et apparaît au fil des découvertes (Cœur → Mana → Niveau → Jour). Réveil en mer, « Statut ? »… silence, apprendre à respirer le mana, la mouette, le survol de l'île, la fissure du volcan, le socle qui sort de la roche.

## 📦 14 octobre — Le dépôt

Mise en ligne du projet sur GitHub (ce dépôt), avec cette documentation.

## 🧭 17 octobre — Huit directions, armures visibles et ambiance

**Retours clés :** *« ce serait plus naturel en diagonale »*, *« rends le jeu beaucoup plus joli »*.

- 🎨 **Tous les personnages en 8 directions** (`Tools~/make_8dir.py`) : les packs ne dessinent que 4 directions, mais chaque personnage y est découpé en calques. Les diagonales sont recomposées en associant un corps et une tête de deux directions voisines (corps de face + tête de profil, corps de profil + tête de dos). Le style dessiné à la main est intact. 9 épéistes, 30 archers/mages/soigneuses, 45 monstres. Les quadrupèdes et les golems gardent le profil en diagonale.
- 🐛 Au passage : les planches d'attaque des épéistes 7 à 9 sont vides dans le pack (ils devenaient invisibles en attaquant), la tête manquait sur certaines images de dos en diagonale.
- 🎮 **Héros du mode Aventurier** : course avec **jauge d'endurance**, **roulade** (bouton Esquive, courte invulnérabilité, images rémanentes), attaques en marchant ou en courant, animation de mort réparée, **gestes** (s'accroupir pour fouiller un coffre ou prier, tendre la main pour ouvrir ou boire une potion).
- 🎨 **Armure visible** : chaque renfort acheté à la forge change l'apparence du héros, sur 9 paliers (toile, cuir, mailles, plates, chevalier doré). Archer, mage et soigneuse ont leurs 9 paliers (`make_roles.py` : étoffes à la couleur du rôle, arc ou bâton redessiné, chapeau de mage).
- 🎨 **Personnalisation** : nom, couleur des cheveux, de la peau et de la tenue, avec aperçu ; recoloration à la volée (`HeroLook`, palettes de `make_palettes.py`).
- 🎨 **Atmosphère** (`Atmosphere`) : pénombre sous la montagne, **torches portées par les intrus** (on voit leur lueur approcher), lanterne du héros, lanternes et fumée de cheminée dans la colonie, **détails de sol** procéduraux (fissures, cailloux, ossements, mousse, flaques — `make_decals.py`) et **ombre de contact** au pied des murs.
- 🎮 **Sensations de combat** : flash blanc à l'impact (shader maison), micro-pause sur les coups du héros, étincelles, secousse quand il achève, chiffres de dégâts qui jaillissent.
- 🐛 Ponton : les planches ne couvraient pas toute la zone praticable.

### Refonte visuelle de l'île et de l'interface

**Retours clés :** *« je vois des carrés noirs, remplace-les par de petits lacs »*, *« le chemin vers le donjon ne devrait pas exister au départ »*, *« une UI beaucoup plus soignée et pro »*.

- 🎨 **Direction artistique** posée sur une maquette (carte conceptuelle, matériaux, HUD, panneaux) : sable noir volcanique, herbe des clairières, jungle, roche du volcan, lave, mer et récif.
- 🎨 **Terrain en couches auto-raccordées** (`make_terrain.py`, `BlobTile`) : eau, sable, roche, herbe, jungle, lave. Bords organiques aux coins arrondis, écume sur les rives, texture sur 3 × 3 cases (9 « phases ») : plus aucune marche d'escalier ni répétition visible.
- 🎨 **Mer en dégradé continu** du lagon au grand large (texture lissée sous les vaguelettes animées), récifs de roche claire.
- 🎨 **Petits lacs** bordés de roseaux, avec nénuphars, à la place des ravins noirs.
- 🎨 **Volcan vivant** : coulées de lave qui palpitent, roche accordée au relief.
- 🎨 **Végétation par masses** : clairières fleuries, lisières de buissons, jungle dense.
- 🎮 **Aucun chemin au départ** : la grotte n'est qu'une **faille** au pied de la falaise, à demi cachée. Le passage des aventuriers et des colons **use le sol** (herbe foulée, puis terre battue) et le sentier se dessine de lui-même ; quand la colonie devient un hameau, ses ouvriers le **pavent** jour après jour (`TrailWear`, sauvegardé).
- 🎨 **Interface** : cadres pixel à liseré or et coins taillés, titres en Cinzel, icônes pixel (`make_ui_icons.py`), carte du Noyau (cristal, cœur, mana, niveau), vitesse en contrôle segmenté, cartes du panneau lisibles (verrou discret, niveau requis).
- 🎮 **Tactiques des monstres** : le gobelin **charge** (armement visible, ruée, coup renforcé), le gnoll **bondit** sur sa cible, les rats **contournent** pour prendre l'intrus de flanc.
- 🎨 **Armure selon le rang** : les aventuriers des expéditions portent la tenue de leur rang de Guilde, de la toile (F) à l'armure dorée (SSS).
- 🎵 **Musiques** publiées : composées par l'auteur avec Gemini Music.

### Des aventuriers et une faune qui vivent

**Retours clés :** *« les animaux boivent autour des points d'eau, suivent le jour et la nuit, les aventuriers veulent en manger »*, *« parfois ils se battent entre eux, certains boivent, sont dépressifs, peureux, chanceux »*, *« tank en première ligne, prêtre et mage en arrière »*.

- 🎨 **Interface** : police **Alegreya Sans** pour le texte courant, **fenêtre Colonie** refaite (tuiles de chiffres, jauge de relation en pixels, sections à icônes), **fiche d'intrus** au toucher.
- 🦌 **Faune vivante** (`WildAnimal`, `FreshWater`) : troupeaux de cerfs et de sangliers, **boire aux lacs** à l'aube et au crépuscule, **sommeil** serré la nuit, **renard** qui chasse lièvres et tétras la nuit, **sanglier** qui charge son agresseur, **naissances** chaque matin et retour d'une espèce disparue après quelques jours.
- 🍖 **Faim des intrus** (`AdventurerLife`) : rations à l'arrivée, faim qui monte sur l'île ; à sec, ils **chassent** le gibier et le **font griller** sur un feu de camp, ou rentrent. Affamés, ils frappent moins fort. Nouvel appât : le **garde-manger** — l'affamé se goinfre, immobile, le temps d'une embuscade.
- 🍞 **Faim du héros** (mode Aventurier) : jauge douce, jamais mortelle (endurance et force en baisse) ; rations à la taverne, dans les coffres, ou en **chassant et dépeçant** le gibier (touche G : manger).
- 🎭 **Caractères** : ivrogne, dépressif, peureux, chanceux, bagarreur, glouton, courageux, cupide. Ils se **révèlent** en observant l'intrus. **Bagarres** (alcool, partage du butin), parfois **à mort**.
- ⚔️ **Combat de groupe** : le guerrier ouvre la marche, tireurs et soigneuse suivent derrière, **focalisation** sur la cible du guerrier, le guerrier **s'interpose** et provoque, les blessés **se replient** derrière lui, l'éclaireur **repère les pièges** pour toute l'expédition. Mêmes règles pour les **compagnons** du héros.
- 👹 **Monstres** : tireurs qui gardent leurs distances, **meutes** qui encerclent, **mur de boucliers** (orcs, golems), **fuite** vers les alliés à l'agonie, **embuscades** des monstres postés.
