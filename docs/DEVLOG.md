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

### Le donjon refait

**Retour clé :** *« donjon refait + salles à fonction, décor qui donne des bonus, pièces à énigme »*.

- 🎨 **Murs et sols dessinés pour le projet** (`make_dungeon.py`) pour les **9 ambiances** d'étage : roche volcanique (orgues basaltiques), fournaise (fissures de braise), sanctuaire englouti (briques ruisselantes), caverne gelée, jardin fongique, temple de cristal, crypte, forteresse, catacombes (niches à crânes). La masse rocheuse reste sombre et calme ; les sols ont 9 « phases » raccordables. Plus aucune tuile de pack dans le donjon.
- 🏛️ **Salles à fonction** (trois objets rapprochés) : **couvoir** (nids : un monstre naît chaque matin, de la famille la plus présente), **garde-manger** (vivres : entretien des monstres -10 % par salle, jusqu'à -30 %), **forge des monstres** (enclumes, forges : -15 % de dégâts subis, évolutions -25 %). La **salle du trésor** attire maintenant les cupides de bien plus loin.
- 🧩 **Énigmes** : **porte à levier** (un intrus doit trouver et baisser le levier), **porte runique** (trois pierres runiques doivent briller ensemble : le groupe se disperse), **plaque de pression** (fléchettes, portes qui claquent et coupent le groupe en deux, monstres alertés).

### Des monstres qui vivent

- 🍖 **Faim** : les monstres **dévorent les cadavres d'intrus** tombés dans le donjon (ou se servent au garde-manger). Nourri de chair, un monstre coûte **moitié moins d'entretien** ce jour-là ; affamé, il devient hargneux.
- 💤 **Sommeil** : au repos et fatigués, ils s'endorment (« Zzz », ils voient moins loin) ; épuisés, ils frappent moins fort.
- ⚔️ **Envie de se battre** : sans combat, l'ennui monte ; une victoire les ravit (surtout les sanguinaires).
- 🙂 **Humeur** : heureux, ils frappent plus fort ; malheureux trop longtemps, ils **désertent** (sauf les loyaux et les gardiens). Une salle aménagée les rend plus heureux.
- 🎭 **Traits** : lâche, sanguinaire, gourmand, paresseux, loyal, querelleur, sociable, vaillant.
- 💞 **Liens** : amitiés entre voisins de la même famille, **couples inséparables** (bonus côte à côte), **rivalités** entre familles (gobelins et gnolls, orcs et hommes-lézards, morts-vivants et vivants) qui finissent en **querelles**. La mort d'un ami ou d'un compagnon les affecte.
- La fiche du monstre sélectionné montre son humeur, ses besoins, ses traits et ses liens ; tout est sauvegardé (sauf les liens, qui se retissent).

### Saisons, météo et camps

- 🍂 **Saisons** (6 jours chacune) : printemps, été, **automne** roux, **hiver** enneigé (neige par plaques). La faune ne se reproduit qu'au printemps et en été ; l'hiver, tout le monde a plus faim (intrus, héros, monstres) et la colonie produit moitié moins de vivres ; l'automne, des récoltes abondantes et des pluies fréquentes.
- 🌧️ **Pluie** : les torches des intrus restés dehors s'éteignent ; dans le donjon ils voient moins loin. **Brume matinale** certains jours. **Orages** : chaque coup de tonnerre fait sursauter les intrus du donjon (les peureux détalent).
- 🌋 **Réveil du volcan** : les **coulées de lave avancent** sur les hautes terres, il **pleut des cendres**, diablotins et démons s'enhardissent ; la lave nouvelle refroidit ensuite.
- ⛺ **Camps d'expédition** : un groupe d'au moins trois intrus plante ses tentes et allume un feu près de la plage ; les blessés s'y soignent, et **la nuit ceux qui sont dehors y dorment**. **Raid sur le camp** (fenêtre Colonie, la nuit) : vos monstres fondent sur les dormeurs (dégâts doublés), puis rentrent avant l'aube.

### L'arbre de talents du Noyau

- 🌟 **Un point de talent par niveau**, à dépenser dans trois branches de cinq talents (bouton **Talents** du HUD) :
  - **Architecte** : creuser moins cher, +3 pièges, entretien des pièges et décors -25 %, portes plus dures à forcer et plaques plus cruelles, prestige ×1,5 ;
  - **Maître des bêtes** : invocations moins chères, monstres plus robustes, moins affamés, +3 monstres, évolutions moins chères et expérience accrue ;
  - **Ombre** : intrus plus prompts à fuir, qui voient moins loin, drain de mana accru, embuscades plus rapides et plus cruelles, torches qui s'éteignent dans le donjon.
- 👑 **Dominer l'île** (objectif de fin de partie, suivi dans la fenêtre des talents) : **soumettre** la colonie (trêve et relation au plus haut) ou la **conquérir** (plus un habitant), abattre le **Champion de la Guilde** (Aldéric l'Invaincu, rang SSS, envoyé quand le donjon atteint le rang S, il revient s'il fuit), et installer le cristal au **Cœur du volcan**. Les trois réunis : écran de victoire, et l'on peut continuer à régner.
- 💎 **Le cristal grandit** avec le niveau du Noyau : plus grand, plus lumineux, du bleu au violet puis à l'or, entouré d'éclats qui gravitent.
- ✨ **Sorts du Noyau** (barre au bas de l'écran, mana et recharge) : **Terreur** (niv. 2), **Soin des créatures** (niv. 3), **Éboulement** sur le plus grand groupe d'intrus (niv. 5), **Ombres aveuglantes** (niv. 7).
- 🗣️ **La voix du Noyau** commente, à mi-voix, ce qu'il voit : bagarres, couples de monstres, appâts, cadavres dévorés, énigmes résolues...
- 👁️ **Possession** : bouton **Compétence** selon la famille du monstre — **Charge** (gobelins, orcs, rats), **Bond** (gnolls, hommes-lézards, vampires), **Salve** à distance (diablotins, liches), **Rugissement** qui fige ou fait fuir (les autres) ; touche Q, clic droit ou bouton. Le monde s'assombrit au-delà de ce que voit la créature.
- ♿ **Confort** : **taille de l'interface** réglable (80 à 140 %), **vibrations** du téléphone quand le cristal est frappé ou que le héros est blessé ; fenêtre d'options en deux colonnes.
- 📖 **Chroniques de l'île** (menu pause) : le journal des moments marquants, jour après jour — chaque intrus mort avec son caractère et ce qui l'a tué (*« Ivan (ivrogne) est tué par son compagnon Yann (dépressif) au cours d'une bagarre »*), couples, rivalités et désertions de monstres, évolutions, gardiens tombés, Champion, colonie, saisons, météo, camps, talents. Sauvegardées.
- 🔊 **Ambiances par zone** (synthétisées, `make_ambience.py`) : oiseaux et feuillages de la jungle (grillons et chouette la nuit, silence d'hiver), clapotis et grenouilles près des lacs, grondements et bulles de lave près du volcan, murmures, feu et marteau près du village. `ZoneAmbience` les mêle selon ce que montre la caméra.
- 🧭 **Tutoriel guidé** : les « Pensées du Noyau » expliquent *comment* faire (ligne dorée sous l'objectif) et un **cadre doré pulsant + une flèche** montrent le bouton à toucher, une chose à la fois (déplier le panneau → onglet → élément). Nouvel objectif « Choisir un talent du Noyau ». Désactivable dans les options (« Tutoriel guidé »).

### La colonie prend vie

- 🧑‍🌾 **Colons nommés** : chacun a un prénom, un **métier** qu'il exerce de préférence (bûcheron·ne, carrier·ère, pêcheur·euse, bâtisseur·euse), une **maison attitrée** (la moins pleine), et une **routine** : travail le jour, **veillée autour du feu** le soir (on y bavarde : *« Il paraît qu'un cristal y pense. »*), sommeil chez soi la nuit. Arrivées, départs et morts sont nommés dans les Chroniques. Sauvegardés.
- 🗂️ **Fenêtre Colonie en trois onglets** : **Relations** (jauge, pots-de-vin, trêve, intrus, raids), **Habitants** (chaque colon, son métier, sa maison, depuis quand il est là et ce qu'il fait), **Commerce**.
- 🤝 **Commerce secret** (relation ≥ 25 ou trêve) : le plus ancien colon sert de contact et dépose des marchandises à l'entrée de la caverne contre du mana — **vivres de contrebande** (les monstres mangent), **rumeurs du port** (date et rang de la prochaine expédition, expédition punitive en vue), **pics et lanternes** (creuser à moitié prix jusqu'à l'aube). Une fois par jour chacune ; +relation, +prospérité.
- 🏴 **Avant-poste du Noyau** : une colonie vidée de ses habitants peut être **annexée** (200 mana) — maisons teintées de violet, éclats de cristal flottants, mana chaque matin selon les bâtiments ; plus de milice ni de renforts pour les expéditions.
- 🐛 Les messages émis pendant une cinématique (HUD masqué) attendent son retour au lieu de faire planter TextMesh Pro.

### Mode Aventurier : équipement et contrats

- 🎒 **Équipement** : arme, armure et bijou, de **commun** à **légendaire** (couleur de rareté). Une qualité principale (dégâts pour l'arme, PV pour l'armure, critique / vitesse / endurance / esquive pour le bijou) et 0 à 3 qualités secondaires selon la rareté ; plus l'étage est profond, plus l'objet est fort. Noms selon la classe et la qualité dominante (*Fauchon du Marcheur*, *Arc de la Vipère*, *Masse « Cœur de Givre »*...).
- 💰 **Butin** : coffres (souvent), monstres (rarement, davantage en profondeur), gardiens (toujours, rare au moins), primes des contrats. **Sac** de dix objets (touche **I** ou bouton « Sac ») : chaque objet est comparé à celui qu'il remplacerait (+ vert, − rouge), on l'équipe d'un geste et on le vend en ville. Coups **critiques** (×2) et **esquive** gagnés par l'équipement.
- 📜 **Contrats de la Guilde** (hall de la Guilde) : trois propositions renouvelées à chaque retour du donjon, deux contrats à la fois — **chasse** (N créatures d'une famille), **exploration** (atteindre un étage), **pillage** (ouvrir des coffres), **prime** (abattre le gardien de l'étage 5, 8 ou 10). Prime : de l'or et un objet de rareté garantie. Suivi dans le HUD.
- 🌳 **Arbres de compétences des classes** (touche **K** ou bouton « Talents ») : trois voies de trois talents par classe, de coût 1, 2 et 3 — un point par nouvel étage atteint et par rang validé. *Guerrier* : Rempart (dégâts subis réduits, PV), Furie (dégâts, **rage** sous la moitié des PV, vol de vie), Tourbillon (rayon, recharge, puissance). *Archer* : Œil de faucon (critique, dégâts), Pas léger (vitesse, esquive, endurance), Volée (**+2 flèches**, recharge, puissance et portée). *Mage* : Pyromancie (dégâts, explosion élargie), Arcanes (recharge, endurance, bouclier de mana), Nova (rayon, puissance, recharge). *Soigneuse* : Foi (potions +50 %, PV, bouclier), Prière (soins accrus, **soigne tout le groupe**, recharge), Châtiment (dégâts, vol de vie, critique).
- 👹 **Boss d'étage en trois phases** : un gardien nommé garde la dernière salle de chaque étage (Skrag la Fouine, Mère Moisissure, Ossegrince, Grukk, Hurle-Lune, Vrask, Sa'Veth, Comtesse Ysolde, et le seigneur de l'antre devant le cristal). Sous 66 % de PV il **s'enrage** et appelle deux créatures ; sous 33 % il **frappe le sol** (cercle rouge d'alerte puis onde de choc sur tout le groupe à portée). Jauge de vie avec repères de phases en haut de l'écran.

### Voix et musique dynamique

- 🗣️ **Voix non verbales** (synthèse par formants, `Tools~/make_voices.py`, remplaçables par de vrais enregistrements du même nom) : chaque famille de monstres a sa voix — **gobelins** qui jacassent, **orcs** qui grondent, **rats** qui couinent, **gnolls** qui ricanent, **morts-vivants** qui gémissent (cliquetis d'os), **diablotins** qui crissent, **démons** qui rugissent, **slimes** qui glougloutent, **hommes-lézards** qui sifflent, **golems** qui grincent — quand ils repèrent un intrus, sont blessés ou meurent. Les intrus, compagnons et le héros (voix d'homme ou de femme) lancent un cri de guerre, gémissent, paniquent et meurent. Chaque individu a sa hauteur de voix (plus grave quand il évolue) et ne crie pas sans cesse.
- 🥁 **Musique en couches** (`Tools~/make_music_layers.py`) : par-dessus les morceaux, sans tempo pour ne jamais les heurter, des **tambours de guerre** montent avec l'intensité des combats (monstres lancés à la poursuite, intrus qui se battent, raids, assaut du cristal) et une **nappe d'effroi** dissonante avec le danger (cristal entamé, Champion de la Guilde, boss d'étage selon sa phase, héros à bout de forces).

### English

- 🇬🇧 **Version anglaise** (Options → *English*, ou automatiquement si le téléphone est en anglais) : un pré-processeur TextMesh Pro (`Loc`) traduit chaque texte juste avant l'affichage, sans toucher au code. Le texte est découpé par balises et par lignes ; chaque morceau est cherché dans `Resources/Localization/en.txt` (≈ 1 850 entrées : interface, dialogues, cinématiques, catalogue, rumeurs, chroniques), sinon comparé aux modèles à trous (`Talent acquis : {0}`) dont le contenu est traduit à son tour, puis découpé par séparateurs (`·`, `→`, virgules, ponctuation finale). Outils : `Tools~/extract_strings.py` + `build_loc.py`.

### Retours de jeu (bagarres, bulles, intro)

- 🥊 **Bagarres plus rares**, presque jamais au fond du donjon ; **à mort** seulement exceptionnellement (≈ 3 à 10 %, l'alcool ou l'or s'en mêlant, trois fois moins dans le donjon).
- 💬 **Bulles de dialogue** au-dessus des têtes (intrus, colons, monstres), qui suivent le personnage et s'écartent pour ne pas se chevaucher. Une bagarre a désormais une **raison** que l'on comprend en lisant leurs répliques : dette, triche aux dés, insulte, bière renversée, butin, peur au fond du donjon.
- 🎬 **Cinématique d'introduction raccourcie à une minute** (même histoire : réveil en mer, la plage, le mana, la mouette, la fissure, la grotte) ; texte des sous-titres à vitesse constante.
- 👑 **Boss d'étage nommés (mode Donjon)** : sélectionnez un monstre → **Nommer boss d'étage** (un seul par étage). On lui choisit un nom (ou une suggestion) ; il **évolue aussitôt jusqu'à sa forme ultime** (le coût réunit la promotion et les évolutions restantes), prend son poste dans la **dernière salle, devant l'escalier qui descend** — ou devant le **cristal** s'il repose à cet étage — et un **sceau** de runes ferme le passage : l'escalier ne mène nulle part et le cristal ne peut être frappé **tant que le boss vit**. Il combat en **trois phases** comme les boss du mode Aventurier (rage et rejetons de sa famille, puis ondes de choc qui frappent les intrus), avec sa jauge en haut de l'écran ; remis de ses blessures, il retrouve ses forces d'origine. Sauvegardé.
- 🌳 **Arbre de talents du Noyau en forme d'arbre** : il pousse depuis le cristal, trois **branches maîtresses** (Architecte, Maître des bêtes, Ombre) qui se divisent chacune en deux **sous-branches** — *Pièges* / *Bâtisseur*, *Meute* / *Lignée*, *Terreur* / *Prédation* — soit 24 talents (9 nouveaux : Lames affûtées, Arsenal, Architecte de l'abîme, Couvée prolifique, Fureur bestiale, Rois des profondeurs, Cauchemars, Effroi, Festin). Rameaux acquis dorés, talents disponibles qui pulsent ; toucher un talent l'affiche en bas avec le bouton *Apprendre*. Les anciennes parties gardent leurs talents.
- 🏛️ **Onglet « Salles »** (panneau de droite) : vos salles formées (effet, étage, nombre d'objets, bouton *Voir*), les salles **en formation** (« Armurerie 2/3 — encore 1 objet tout près : … ») et un **guide** de toutes les salles avec leurs objets. Chaque objet de thème porte une **pastille colorée** dans les onglets Décor et Pièges, et pendant la pose un **cercle de regroupement** suit le curseur avec le compte « Crypte 2/3 ». Nouvel objectif du tutoriel : *Aménager une première salle*.

### Monstres par étage

- 🗺️ **Habitats** : quatre familles **communes** (rat, slime, gobelin, squelette) s'invoquent partout ; toutes les autres sont **natives** d'un ou plusieurs thèmes d'étage (Orc, Gnoll, Sanglier : roche volcanique ; Diablotin, Démon : Fournaise ; Homme-lézard : Sanctuaire englouti ; Spectre : Caverne gelée et Crypte ; Golem : Gelée, Cristal, Forteresse ; Champignon : Jardin fongique ; Esprit du temple : Cristal ; Liche : Crypte et Catacombes ; Vampire : Catacombes ; Zombie : Englouti, Crypte, Catacombes) et ne s'invoquent que là, avec **+15 % de PV et de dégâts chez elles**. L'onglet Monstres ne montre que ce que l'étage regardé permet.
- 🔥 **Variantes d'étage** : un monstre commun né sur un étage à thème en prend la variante — couleurs repeintes (rampe du thème, détails gardés), lueur, particules (braises, gouttes, neige, spores, éclats, feux follets), un nom et un pouvoir : *Slime de feu* (traînée brûlante), *Gobelin pyromane* (fioles de feu), *Rat des braises* (explose en mourant), *Slime de givre* (ralentit), *Gobelin des glaces* (javelots), *Squelette gelé* (éclats de glace), *Rat pestiféré* (poison), *Slime toxique* (nuage mortel), *Gobelin sporé* (aveugle), *Slime de cristal* (renvoie les dégâts), *Gobelin prismatique* (attire les intrus), *Gobelin goule* (vol de vie), *Squelette légionnaire* (bouclier)... 32 variantes en tout. Elles survivent à l'évolution et se sauvegardent.

### Pièges redessinés

- 🪤 **Pièges dessinés pour le projet** (`Tools~/make_traps.py`, pixel art 16 px, animés) : chacun **tient dans une case**, **prévient avant de frapper** (lueur, ombre, déclic) et **s'oriente seul** selon le couloir ou le mur.
  - Remplacés : roue tranchante → **Lame pendulaire** ; lames rotatives → **Scie au sol** (dans l'axe du couloir) ; guillotine → **Cage tombante** (enferme 5 s) ; bombe → **Rune explosive** (rougeoie 0,8 s puis souffle) ; colonne → **Lances murales** (fixées au mur, traversent le couloir). Les anciennes parties les convertissent.
  - Nouveaux : **Trappe** (chute à l'étage du dessous ou dans une fosse), **Dalle huilée** (on glisse dans le piège suivant ; une flamme l'embrase), **Lanceur de fléchettes** (mural), **Rocher roulant** (dévale tout le couloir).
  - Pièges de thème, posables seulement sur leur étage : **Geyser de magma** (Fournaise), **Maelström** (Englouti), **Stalactites** (Gelée), **Champignon à spores** (Fongique), **Prisme laser** (Cristal), **Mains squelettiques** (Crypte, Catacombes), **Arbalète cachée** (Forteresse).
  - Redessinés : pics (les trous rougissent avant de frapper), piège à loup, gaz, buse de flammes (crache dans l'axe du couloir), dalle spectrale, toile.

### Repas des monstres

- 🍖 **Les monstres vont se servir seuls au garde-manger** quand ils ont faim : un cadavre tout proche d'abord, sinon le **garde-manger** garni le plus proche **du même étage** (une portion chacun, regarni chaque matin), sinon une salle de réserves. Un monstre à qui l'on a ordonné de **tenir la position** ne quitte plus son poste, ni pour manger, ni pour se quereller (l'ordre interrompt un repas en cours).

### Nouvelles voies et familles — salve 1

- 🌿 **Évolution à embranchements** : au passage au stade 2, certaines familles proposent, en plus de la **voie classique**, une **voie de rôle**. Le bouton *Évoluer* ouvre une fenêtre de choix : une carte par voie (apparence, rôle, PV, dégâts, mêlée ou distance, forme ultime). La voie choisie se sauvegarde ; dans le mode Aventurier, un monstre sur trois de stade 2+ prend une voie de rôle.
- 🏹 **Gobelin archer → Tireur d'élite gobelin** : tire des flèches de loin et recule quand on l'approche ; le tireur d'élite vise plus loin (5,5 cases) et porte des **coups critiques** (25 %, dégâts doublés). Arc tenu contre le flanc, bandé puis relâché à l'attaque, carquois dans le dos pour le stade 3 (`Tools~/make_monster_roles.py`, à partir des calques du pack).
- 💀 **Squelette mage → Nécromancien** : traits de magie glacée qui ralentissent ; le nécromancien **relève en squelettes** (3 au plus, 60 s) les intrus tombés près de lui. Chapeau pointu et bâton à orbe, crâne au sommet du bâton pour le nécromancien.
- 🕷️ **Nouvelle famille : les araignées** (natives du Jardin fongique et des Catacombes), dessinées pour le projet (`Tools~/make_creatures.py` : petites formes 3D projetées en vue plongeante, 8 directions, pattes animées) : *Araignée des cavernes* (venin), *Tisseuse* (tend jusqu'à 2 **toiles** qui engluent les intrus), *Reine-araignée* (4 toiles, **pond des araignées** au combat).
- 🐛 Faire évoluer un monstre seul sur son ancre de patrouille détruisait l'ancre (erreurs en fin de combat) : la nouvelle forme s'inscrit avant que l'ancienne se retire.
- 🇬🇧 Noms et descriptions de toutes les familles de monstres enfin traduits.

### Nouvelles voies et familles — salve 2

- 🔥 **Gobelin chaman → Grand chaman** (voie de rôle du gobelin) : boules de feu, et toutes les 4 s il **soigne les gobelins** autour de lui (7 %, puis 10 % de leurs PV) ; le grand chaman les **galvanise** aussi (+25 % de dégâts). Bâton à orbe de braise, coiffe de plumes. Le gobelin a désormais trois voies au stade 2 : pillard, archer, chaman.
- 🦴 **Squelette archer → Archer d'ossements** : arc d'os, **flèches perçantes** (l'intrus suivant sur la trajectoire prend 50 %, puis 80 % des dégâts).
- 🧰 **Nouvelle famille : les mimiques** (`Tools~/make_creatures.py`, coffres à couvercle bombé qui s'ouvrent sur une gueule dentée) : *Coffre mordeur*, *Mimique*, *Mimique royale* (laquée de rouge, ferrures d'or, gemmes). Elles ne s'invoquent **que dans une salle du trésor**, ne se promènent pas, restent tapies en coffre et frappent par **surprise** (bonus d'embuscade, sans devenir transparentes) ; la Mimique **attire** les intrus curieux (« Un coffre ! »), la Mimique royale les **engloutit** un instant.

### Nouvelles voies et familles — salve 3

- 🪓 **Orc berserker → Seigneur de guerre fou** : garde sa lame, crinière et barbe couleur sang ; **plus il est blessé, plus il frappe vite et fort** (jusqu'à +45 % de vitesse d'attaque, +40 % de dégâts, davantage pour le seigneur de guerre) — « Rage ! » sous la barre des 50 %.
- 🩸 **Orc chaman → Chaman de sang** : vieil orc à barbe blanche et bâton à crâne ; ses traits de sang le soignent, et dès qu'il se bat il **plante un totem de guerre** (dessiné pour le projet) : les orcs autour frappent plus fort (+15 %, +30 %) et le chaman de sang les **régénère**.
- 🦇 **Nouvelle famille : les chauves-souris** (natives de la roche volcanique et des Catacombes), dessinées en vol stationnaire avec ailes membraneuses : *Chauve-souris*, *Vampire volant* (vol de vie), *Seigneur de la nuée* (deux petites chauves-souris tournent autour de lui ; il en appelle au combat). Rapides, elles **fondent sur les archers, mages et soigneurs** plutôt que sur les guerriers.

### Nouvelles voies et familles — salve 4

- 🗡️ **Gnoll chasseur → Traqueur à la lance** : javelot tenu debout, ramené au-dessus de l'épaule puis lancé (projectile dessiné pour le projet) ; il **achève les blessés** (dégâts doublés, puis ×2,5, sous 40 % de PV).
- 🎯 **Homme-lézard sarbacane → Maître-venin** : sarbacane de roseau portée à la bouche, **dards empoisonnés** de loin ; le maître-venin a un poison bien plus violent, qui ralentit.
- 🐺 **Nouvelle famille : les loups de givre** (natifs de la Caverne gelée), quadrupèdes dessinés pour le projet (trot en diagonale, tête basse à l'attaque, éclats de givre) : *Louveteau des glaces*, *Loup de givre*, *Alpha blanc*. **Chasse en meute** (+10 % de dégâts par loup proche, 3 au plus), morsure glacée ; le loup de givre **hurle** au combat (intrus ralentis de 40 %, 55 % pour l'alpha) et l'alpha **galvanise la meute**.

### Nouvelles voies et familles — salve 5

- 🧪 **Zombie cracheur → Goule acide** : bave verte, boule d'acide qui gonfle devant la bouche puis part en gouttes ; l'acide **ronge l'armure** : l'intrus touché subit +25 % de dégâts pendant 5 s (+40 % pour la goule).
- 🐀 **Nuée de rats → Grande nuée** : le sprite devient toute une nuée (3 puis 5 rats réduits, chacun à son rythme) ; ses morsures touchent aussi les intrus **autour de sa cible**, et la grande nuée lâche **trois rats** au combat.
- 🦎 **Nouvelle famille : les salamandres** (natives de la Fournaise), dessinées pour le projet (corps ondulant, pattes écartées, taches lumineuses, braises) : *Salamandre*, *Salamandre ardente*, *Dragonnet de lave* (ailes et cornes). Elles **soufflent le feu** sur tous les intrus devant elles (cône, brûlure).
- 🐛 Les voies de rôle prennent la taille d'image de leur famille (les rats sont en 128 px).

### Nouvelles voies et familles — salve 6 (dernière)

- 🔱 **Nouvelle famille : les nagas** (natives du Sanctuaire englouti) : buste dressé sur une queue de serpent qui ondule, trident : *Naga*, *Naga guerrière* (cuirasse), *Reine des abysses* (couronne, lueurs). Leur **chant envoûtant** attire les intrus qui passent vers leur trident (« Ce chant... d'où vient-il ? ») ; la reine les **fige** un instant. Plus vives sur un étage inondé.
- 🗿 **Nouvelle famille : les gargouilles** (natives de la Forteresse et du Temple de cristal) : immobiles sur leur **socle**, ailes repliées, on les prend pour des **statues** ; elles frappent par surprise et leur **peau de pierre** encaisse (20 à 35 % de dégâts en moins, +40 % figées). *Gargouille*, *Gargouille de pierre* (mousse), *Gargouille de cristal* (ses éclats ralentissent).
- ✅ Les 17 voies et familles prévues sont faites : 10 voies de rôle (gobelin archer et chaman, squelette mage et archer, orc berserker et chaman, gnoll chasseur, homme-lézard sarbacane, zombie cracheur, nuée de rats) et 7 familles (araignées, mimiques, chauves-souris, loups de givre, salamandres, nagas, gargouilles).

### Relief des murs et fluidité sur téléphone

- 🧱 **Les murs masquent ce qui se tient derrière eux** : la roche était dessinée d'un bloc sous tous les personnages, si bien qu'un monstre, un intrus ou une statue posté juste au nord d'un mur passait par-dessus. Les murs du bord des galeries sont désormais recopiés dans un tilemap « avant-plan » trié case par case avec les personnages (`WallOcclusion`) : ce qui est derrière un mur est caché par lui, ce qui est devant passe devant. Seules ~1 000 tuiles sont triées (le reste de la roche, invisible, reste en un bloc) et les copies suivent les creusements. Le totem de guerre est trié de même.
- ⚡ **Optimisations** (mesurées au profileur sur une scène chargée — 15 monstres, 12 intrus) :
  - **Tuiles animées** : Unity faisait avancer ~45 000 tuiles animées à chaque image (mer + 16 000 tuiles de collision jamais affichées). Au lancement, `TileDiet` fige les tuiles de collision et toute la mer hors d'une bande côtière de 5 cases (29 184 → 4 382) : **0,75 ms → 0,07 ms** par image.
  - **Ramasse-miettes** : les requêtes de zone (IA, pièges, pouvoirs) passent par des listes réutilisées (`Phys.Circle`) au lieu d'un tableau neuf à chaque appel, le HUD se rafraîchit 4 fois par seconde, le rectangle de sélection (OnGUI) ne tourne que pendant un glisser, l'intérieur de la montagne n'est plus reparcouru en vue du donjon : **2,2 Ko → 1,0 Ko alloués par image**.
  - **Options lues en mémoire** : volumes, échelle de l'interface et langue étaient relus dans PlayerPrefs à chaque image et pour chaque texte affiché (un appel Java coûteux sur Android).
  - Plus de lancer de rayon souris à chaque image (`Camera.eventMask`), échelle de l'interface recalculée seulement quand l'écran change.
  - Bilan sur PC : logique du jeu **7,8 ms → 5,0 ms** par image, pire image 39 → 29 ms.
- 🎚️ **Qualité graphique** (Options) : *Haute* (HDR, halo, 60 i/s — ordinateur), *Équilibrée* (sans HDR ni halo, les deux passes les plus lourdes — téléphone par défaut), *Économie* (sans post-traitement, rendu à 75 % de la résolution, particules d'ambiance coupées, 30 i/s). Sur téléphone, si le jeu tourne sous 45 i/s pendant 10 s, la qualité descend d'un cran toute seule (tant que le joueur n'a pas choisi).

### Fuites de mémoire, intrus qui tournaient en rond, météo allégée

- 🧭 **Les intrus ne tournent plus en rond dans les salles meublées** : leur carte mentale ne retenait une case que s'ils la voyaient, et les objets posés (tonneaux, coffres, statues...) leur cachaient la vue — les cases derrière restaient « à découvrir » pour toujours. Ils voient désormais par-dessus les objets (seuls les murs et les portes fermées arrêtent le regard) : depuis les mêmes points de vue, 149 cases vues au lieu de 103.
- 🍖 **Garde-manger inaccessible** : un intrus affamé qui n'arrivait pas à l'atteindre abandonnait au bout de 25 s... puis le revoyait et recommençait, sans fin. Il se sert désormais à portée de bras, et renonce 90 s à un garde-manger vraiment inaccessible.
- 🧹 **Fuites de mémoire** (le jeu s'alourdissait au fil de la partie) : chaque chiffre de dégâts, bulle ou étiquette à contour créait son propre matériau de police, jamais libéré (≈ 500 par minute de combat) — un seul matériau partagé désormais (`UIKit.Outline`), et les chiffres flottants sont recyclés. Chaque lumière éphémère (projectile, sort, butin, évolution) laissait aussi un maillage orphelin : `Light2DCleanup` les libère. Mesuré : matériaux stables à 260 (au lieu de +1 300 en 3 minutes).
- 🌧️ **Pluie et brume repensées** : la pluie est un seul système de particules (un seul dessin) au lieu de 110 sprites éclairés déplacés un à un ; la brume une seule nappe non éclairée au lieu de 9 calques plus grands que l'écran (très coûteux sur téléphone).
- 📦 **Textures compressées sur Android** (ASTC 4×4 : 8 bits par pixel au lieu de 32) : la mémoire des sprites est divisée par 4, sans perte visible sur le pixel art.
- ⛏️ **Creusements en attente maîtrisables** : dès que des cases sont marquées, un bandeau sous la date indique « Creusement en cours · N case(s) » avec **Suspendre / Reprendre** (les marques restent, rien n'avance ni ne coûte — pratique quand des intrus entrent) et **Tout annuler**.
- 🍖 **Garde-manger plus utile** : les monstres y vont dès 40 de faim (au lieu de 55) en l'annonçant (« Je vais manger ! »), et il contient 10 portions par jour (au lieu de 4) puisqu'il nourrit à la fois les monstres et les intrus appâtés.

### Retours du téléphone : étages, boss, météo, escaliers

- 🪜 **Escaliers redessinés et cliquables** : une vraie paire — une trémie de pierre dont les marches s'enfoncent dans le noir à l'étage du haut, une volée de marches qui grimpe vers une ouverture éclairée dans la paroi à l'étage du bas (adossée au mur, triée avec lui). Toucher un escalier (sans monstre sélectionné) emmène à l'étage où il mène, centré sur l'escalier d'arrivée ; avec des monstres sélectionnés, c'est toujours un ordre de déplacement.
- 🔱 **Pendant une attaque**, l'onglet Étages reste utilisable (la surcouche rouge ne couvre plus que les onglets de construction) ; les **creusements en attente** se suspendent ou s'annulent **au milieu de cette surcouche** (le bandeau en haut de l'écran disparaît).
- 👑 **Barre du boss d'étage** : affichée seulement quand il combat, qu'on le sélectionne ou qu'il vient d'être frappé (elle restait à l'écran dès qu'il avait perdu un PV).
- 🌧️ **Météo seulement dehors** : plus de pluie ni de brume en vue du donjon ni dans les étages du dessous ; le brouillard est nettement moins dense.
- 🧰 **Mimiques à la taille des coffres** (elles faisaient un tiers d'un coffre posé).

### Taille de l'interface refaite (mobile)

- 📐 **Taille de l'interface** (Options) : elle agrandissait tout le canevas, ce qui faisait déborder la colonne du menu et chevaucher les panneaux du haut (dessinés pour 1920×1080). Désormais chaque bloc grandit depuis son coin (`HudScaler`) en gardant la disposition de son groupe ; l'échelle de la rangée du haut est plafonnée pour que la carte du Noyau, le calendrier et la menace tiennent côte à côte ; le panneau de construction garde la hauteur de l'écran ; les fenêtres centrées suivent la taille choisie sans jamais dépasser l'écran (`FitToScreen`) ; les boutons du menu principal se réorganisent selon la place.
- 📱 **Taille par défaut selon l'écran réel** : sur téléphone, l'interface démarre plus grande (110 à 130 % selon la hauteur physique de l'écran) pour que les textes restent lisibles.

### Mode Aventurier : brouillard de guerre et vue rapprochée

- 🌑 **Brouillard de guerre** sous la montagne (`CrawlerFog`) : on ne voit que la salle où se tient le héros, ses murs et le début de ses sorties ; dans un couloir, quelques cases autour de soi. Les salles déjà visitées restent assombries, le reste de l'étage est noir (monstres et barres de vie compris). L'étage redevient inconnu à chaque nouvelle descente.
- 🎥 **Vue rapprochée** dans le donjon (caméra plus proche qu'avant) et **calée sur la salle** courante tant qu'on y est, à la manière d'un Zelda ; elle suit librement le héros dans les couloirs.
- 🗺️ **Donjon souterrain plus grand** : les trois étages souterrains passent de 84 à 180 cases de large (même hauteur et même position : sauvegardes compatibles, grille de déplacement inchangée), en mode Noyau comme en mode Aventurier. Outil d'éditeur `IslandGenerator.WidenUnderground` pour l'appliquer à la scène.
- 🕸️ **Étages d'aventure plus vastes et ramifiés** : 7 à 13 salles selon la profondeur (au lieu de 5 à 9), un peu plus grandes, qui partent dans plusieurs directions, avec quelques couloirs en boucle entre salles voisines.

### Mode Aventurier : l'Esprit du Noyau

- 🧠 **Une intelligence gère le donjon** (`CrawlerOverlord`) : elle amasse de l'essence (chaque jour, à chaque défaite du héros, à chaque expédition, un peu à chaque monstre tué) et la dépense en choisissant l'action la plus utile du moment : **creuser une nouvelle salle** peuplée sur un étage que vous avez bien exploré, **repeupler** les salles vidées, **faire évoluer** les monstres qui vous ont échappé, **piéger** les salles où vous passez le plus, **envoyer les familles qui vous résistent** (et des chasseurs rapides contre un archer ou un mage). Elle agit hors de votre vue.
- 🌱 **Éveil** : en dépensant, l'Esprit monte de niveau ; les étages recréés ont plus de salles et des monstres plus évolués.
- 📜 Ce qu'il a fait est raconté au retour dans le donjon (« Le donjon a changé pendant votre absence... ») et résumé au hall de la Guilde.

### Cinématiques : intro allégée, verdict de la Guilde en direct

- 🎬 **Intro sans saccades** : les plans larges (île, survol à dos de mouette) sont resserrés (≈ 195 appels de dessin au lieu de 346 au survol) et, pendant l'écran noir du début, la caméra passe une image sur chaque plan pour charger textures et shaders avant la cinématique (leur premier affichage en plein survol faisait saccader le téléphone).
- 🏛️ **Nouvelle cinématique : le verdict de la Guilde.** Au hall, sur le continent : les survivants de l'équipe d'évaluation rentrent un à un et font leur rapport (en nommant les disparus) — ou personne ne rentre, la greffière n'a aucune nouvelle, les semaines passent. Puis le maître de guilde dresse **le bilan en direct** sur le registre : créatures, pièges, puissance du cristal, étages, aventuriers tombés, issue de la mission ; **l'estimation du rang change à chaque ligne**, et le tampon tombe.

### Stabilité sur téléphone (retours du 20/10)

- 🌡️ **30 images/s sur téléphone en qualité Équilibrée** (60 seulement en Haute) : à 60 en continu, le téléphone chauffe puis ralentit au bout de 10-15 minutes ; la descente automatique de qualité tient compte de la nouvelle cadence.
- 🧱 **Ombres de contact et détails du sol dessinés en tilemaps** : un rendu par couche au lieu d'un à quatre objets par case creusée (des centaines, puis des milliers à mesure que le donjon grandit).
- 🧪 Outil d'endurance (`SoakTest`) : partie accélérée avec raids, relevés de mémoire, d'objets et de temps d'image toutes les 30 s.
- 🚀 **Lancement plus rapide** : le graphe de déplacement de l'île (196×354 cases) n'est plus scanné à chaque lancement (≈ 0,45 s sur ordinateur, plusieurs secondes sur téléphone) : il est précalculé dans la scène (*DungeonCore › Précalculer le graphe de déplacement*), identique case pour case. L'allègement des tuiles d'eau animées est lui aussi fait une fois pour toutes dans la scène (≈ 0,2 s de moins au lancement).
- 💾 **Chargement d'une partie** : plus de second scan complet de l'île ; le graphe n'est recalculé qu'autour des cases creusées ou remblayées et des escaliers (vérifié : même graphe qu'avant la sauvegarde).
- 💾 **Sauvegarde du mode Aventurier** : sauvegarder une aventure enregistrait en fait l'état du mode Noyau — au rechargement, on se retrouvait en mode donjon (et une aventure pouvait écraser une partie du Noyau). Désormais l'aventure a ses propres emplacements (automatique + 3) : héros (classe, nom, apparence), or, potions, rations, satiété, rang, sac et équipement, talents, contrats, compagnons, record de profondeur, coffres pillés, graine du donjon et éveil de l'Esprit du Noyau. Au chargement, l'aventure reprend à la taverne. Le menu de chargement a deux onglets (Noyau / Aventure) et « Continuer » reprend la dernière partie, quel que soit le mode. Sauvegarde automatique au retour en ville et quand l'application passe en arrière-plan.

### Mode Noyau : creusements, colonne de boutons, panneau de construction

- ⛏️ **Creusement bloqué** : suspendu pendant une attaque, il le restait une fois les intrus partis (le bouton n'existe que pendant l'attaque). Il reprend désormais tout seul à la fin de l'attaque.
- 🧱 **Encart des creusements pendant une attaque** refait : à la largeur du panneau, titre, nombre de cases, deux boutons proportionnés (Suspendre/Reprendre, Tout annuler).
- 🔘 **Colonne en haut à gauche** : Menu, Talents, Vue île, puis Colonie en dessous quand elle existe, sans trou ; les points de talent s'affichent dans une pastille au coin du bouton (ils débordaient sous la colonne).
- 👆 **Panneau de sélection** masqué quand aucun monstre n'est sélectionné ; un petit bouton « Tous les monstres » le remplace.
- 🗂️ **Pièges et décors rangés** en sections avec en-têtes — pièges au sol, muraux, de l'étage, portes et mécanismes ; décor utile, lumières, cristaux et nature, macabre, mobilier et trophées, trésors et prestige — triés par niveau requis puis par coût.

### Mode Aventurier : boutique, course, caméra, gardiens

- 🛒 **Fenêtre d'achat dupliquée** impossible à fermer : un achat recréait l'ancien encart plein écran des intérieurs par-dessus la boutique. Corrigé.
- 🏃 **Plus de sprint** (mécanique sans intérêt) ; la marche est un peu plus rapide et la jauge d'endurance sert à la roulade.
- 🎥 **Caméra rapprochée sans à-coups** : le passage salle ↔ couloir se fait en fondu (avant, la caméra changeait de cible d'un coup au seuil des portes). Vérifié en marchant d'une salle à l'autre : plus de saut d'une image à l'autre.
- 👑 **Un gardien à chaque étage, escalier scellé** : de l'étage 1 (nouveau : Krog le Chef de bande) à l'étage 9, un boss nommé garde la salle de l'escalier ; la descente est scellée par un cercle de runes tant qu'il vit (« Scellé — vaincre … »). Un gardien vaincu le reste, même quand l'étage est recréé (sauvegardé).
- 🪜 La salle de l'escalier de descente est fixée à la génération : les salles creusées plus tard par l'Esprit du Noyau ne déplacent plus l'escalier ni le point d'arrivée.
- 🌳 **Ordre d'affichage corrigé** (héros, monstres, aventuriers, colons, animaux, habitants et meubles des intérieurs) : les personnages sont des images de 64 px dont le pivot est au centre, alors que leurs pieds sont ≈ 0,4 unité plus bas ; triés par le centre, on passait derrière un buisson devant lequel on se tenait, devant un arbre ou une façade qu'on longeait par l'arrière, devant le comptoir de la taverne... L'axe de tri du Renderer 2D prend désormais en compte la profondeur, et chaque personnage (ou meuble d'intérieur) reçoit un décalage égal à la hauteur de ses pieds (`FeetSort`) : il est trié à ses pieds, sans que l'image, les collisions ni les positions changent. Vérifié devant/derrière un buisson, un arbre, la taverne et au comptoir.

### La cité sur l'île : première itération du plan (20/10)

Plan validé avec Claude Design (île complète, quartiers, stades d'évolution), puis posé en jeu.

- 🏝️ **L'île s'agrandit vers le sud** (52 rangées de plus) : des terres neuves pour la cité, le port et le manoir. Pas de terres rapportées : la forme de l'île elle-même se prolonge (un grand rectangle aux coins arrondis fondu dans l'ellipse d'origine), avec la même pente de côte, la même grève et le même bruit que le reste. Les poches de mer et la grève isolées au milieu des terres disparaissent (lac ou basse terre).
- 🗺️ **Emplacements fixes du plan** : camp des aventuriers à ≈ 40 cases de l'entrée du donjon, cité au sud, port de pêche et ponton sur la côte sud-ouest, terrain du héros près du port (visible en mode Aventurier seulement), fermes au nord-est, manoir des nobles au bord de son lac au sud-est. Routes de terre entre les quartiers.
- 🏘️ **La cité** (40 × 27 cases, trois portes) : rue du nord et rue du sud d'un rempart à l'autre, deux rues nord-sud, avenue de la porte nord, place du marché dallée et chemin de ronde le long de la future enceinte ; le reste des îlots reste en herbe, prêt pour les bâtiments. Rangée nord : tour des mages, bibliothèque, forge, taverne, Guilde ; centre : maisons, marché, entraînement ; sud : chapelle, maisons, prison.
- 🚩 **Repères du plan** : un écriteau nommé là où se dressera un lieu dont l'art n'est pas encore importé (bibliothèque, chapelle, prison, manoir, moulin, vergers, portes…).
- 💾 Les étages souterrains descendent de 52 cases ; les anciennes sauvegardes sont converties au chargement (version 2). Les deux précalculs (graphe de déplacement, tuiles fixes) sont refaits ; le précalcul reconstruit maintenant les collisions des tilemaps avant de scanner (juste après une génération, tout était vu comme praticable).

### La cité finale (21/10)

Les retours sur la première itération : cité trop petite et serrée, trop peu de maisons, pavés gris étranges, routes mal dessinées. La cité est refaite en grand, avec les quatre nouveaux packs (manoir, maison du héros, prison, bibliothèque) et tous ceux déjà importés.

- 🧩 **Tampons** (`Tools~/make_stamps.py`, `StampImporter`, `StampVisual`) : chaque scène Tiled d'un pack (un bâtiment avec sa cour, son jardin, ses habitants) est posée telle quelle. Le sol est fusionné en une image plate sous les personnages ; les objets sont découpés en morceaux, chacun trié à ses pieds (on passe devant ou derrière une statue, une table, un arbre, une rangée de clôture) ; les tuiles animées (drapeaux, fumée, fontaine, buveurs, marchands, gardes, combattants de l'arène) restent animées ; les remparts bleutés qui entourent chaque scène dans les packs sont retirés et les bords du sol sont fondus dans le terrain (tramage irrégulier).
- 🏰 **La cité** (72 × 40, quatre quartiers, trois portes) : rangée nord — tour des mages, bibliothèque, taverne, hall de la Guilde et armurerie ; milieu — six maisons, place du marché, terrain d'entraînement, forge ; sud — vieille chapelle (temple en ruine relevé), maisons, verrerie, prison. Faubourgs aux portes ouest et nord ; grande ferme au nord-est (moulin, grange, champs, vergers, enclos, mare) ; manoir des nobles et son lac à l'est ; terrain du héros près du port. Vingt maisons variées (colombages, chaumières).
- 🧱 **Selon le stade** : rues de terre, puis pavées de briques chaudes (nouvelle tuile) quand la cité se fortifie ; palissade de rondins au stade Village, remparts de pierre avec tours aux angles et aux portes au stade Bourg fortifié. Six stades (nouveau : Cité du Donjon, 2000 de prospérité), chacun aux emplacements du plan final.
- 🛣️ **Routes redessinées** : plus court chemin en 8 directions, tracé simplifié puis lissé en courbes, peint au pinceau rond ; les routes contournent les bâtiments et finissent devant leur porte.
- 🛏️ **Mode Aventurier — logement** : jauge de **fatigue** (temps passé debout, étages descendus ; 50 % endurance plus lente, 80 % coups plus faibles et roulade plus chère, 100 % on s'effondre et on se réveille à la taverne, chambre et soins payés). La tavernière **loue une chambre pour la nuit** (nuit sautée, santé pleine, fatigue effacée).
- 🏡 **La maison du héros** : terrain à vendre près du port (500 or), maison commandée au charpentier (1000 or), chantier visible pendant trois jours (les colons y travaillent). Ensuite on y dort gratuitement, et c'est là qu'on se réveille.
- 🚪 **Nouveaux intérieurs** tirés des cartes des packs : bibliothèque (le bibliothécaire parle de l'étage suivant), prison (le chef des gardes), manoir (banquet des nobles, le seigneur), maison du héros (son lit). Murs solides calculés d'après les cases de sol, entrée par la vraie porte.
- 💾 Île encore agrandie au sud et à l'est : les sauvegardes v1 et v2 sont converties (v3).

### La cité qui grandit (21/10, suite des retours)

- 🌳 **Plus de chemins au début** : les routes naissent avec la colonie — le chemin du camp à sa fondation, celui du port au stade 2, les routes vers la future cité et les champs au hameau, la grande ferme au village, le manoir à la cité. L'emplacement de la cité est un bois clair ; au hameau on y trace les rues et la forêt s'éclaircit (trois arbres sur dix restent dans les jardins), la palissade dégage sa ligne.
- 🏘️ **Cité plus dense** (63 × 36) : rues de deux cases, avenue de trois, plus de chemin de ronde ; les îlots se remplissent de maisons serrées le long des rues ; les emprises des bâtiments suivent la taille réelle de leur scène. La **vraie chapelle et son cimetière** (pack ajouté) remplacent le temple en ruine, avec un intérieur et un prêtre (bénédiction payante).
- 🧱 **Remparts refaits** : grès chaud (accordé aux rues de briques), grandes tours octogonales aux angles, de part et d'autre des portes et le long des murs.
- 🚪 **Portes, volets et herses** : plus d'animation en boucle. Les portes (et la herse de la prison) s'ouvrent quand quelqu'un passe et se referment derrière lui ; les volets s'ouvrent le matin et se ferment le soir.
- 🍺 **Taverne et Guilde sur deux étages**, tirées des cartes des packs (comme les images de référence) : la salle de la taverne et ses chambres à l'étage — on loue une chambre à la tavernière, on monte l'escalier et on dort dans sa chambre ; le hall de la Guilde et sa salle d'entraînement magique à l'étage.
- 🧑‍🌾 **La ville vit** : des passants vont et viennent (marché, taverne, portes), bavardent, rentrent chez eux le soir ; on peut parler aux passants et aux colons.
- 🏝️ **Île arrondie** : elle s'élargit du côté de la ferme et du manoir, jusqu'au bout de l'île.
- 🛠️ **Aperçu dans l'éditeur** (*DungeonCore › Aperçu de la cité finale*) : les bâtiments et les intérieurs, qui naissent en jeu, s'affichent dans la vue Scène sans être enregistrés ; un bâtiment déplacé dans l'aperçu garde sa place (*Enregistrer les déplacements de l'aperçu*, repris par le générateur).
