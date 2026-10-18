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
