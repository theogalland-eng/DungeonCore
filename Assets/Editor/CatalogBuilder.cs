#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// Crée le tileset provisoire de l'île, les prefabs générés (monstres, pièges, décors)
/// et le catalogue Resources/DungeonCatalog. Menu : DungeonCore/…
/// </summary>
public static class CatalogBuilder
{
    public const string ArtPath = "Assets/Art/Generated/island_tiles.png";
    public const string TileDir = "Assets/Tiles/Generated";
    public const string PrefabDir = "Assets/Prefabs/Generated";
    public const string CatalogPath = "Assets/Resources/DungeonCatalog.asset";

    public static readonly string[] IslandTileNames =
    {
        "gravel_a", "gravel_b", "volcanic_a", "volcanic_b", "scree", "lava_a", "lava_b", "reef",
        "sea_deep", "sea_shallow", "vent", "abyss", "moss_a", "moss_b", "ice", "fungal",
        "crystal", "aqua_floor", "lava_floor", "cave_floor",
    };

    // ─────────────────────────────────────────────
    // TILESET
    // ─────────────────────────────────────────────

    [MenuItem("DungeonCore/1. Importer le tileset de l'île")]
    public static string ImportIslandTileset()
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(ArtPath);
        if (imp == null) return "texture introuvable";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath);
        int h = tex.height;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = 16;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        var metas = new List<SpriteMetaData>();
        for (int i = 0; i < IslandTileNames.Length; i++)
        {
            int col = i % 8, row = i / 8;
            metas.Add(new SpriteMetaData
            {
                name = "island_" + IslandTileNames[i],
                rect = new Rect(col * 16, h - (row + 1) * 16, 16, 16),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
            });
        }
#pragma warning disable 0618
        imp.spritesheet = metas.ToArray();
#pragma warning restore 0618
        imp.SaveAndReimport();
        return $"{metas.Count} sprites";
    }

    public static Dictionary<string, Sprite> Sprites(string texturePath) =>
        AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().GroupBy(s => s.name).ToDictionary(g => g.Key, g => g.First());

    /// <summary>Tile générée (sans verrou de couleur) pour un sprite.</summary>
    public static Tile TileFor(Sprite s, Tile.ColliderType collider = Tile.ColliderType.Grid)
    {
        if (!AssetDatabase.IsValidFolder(TileDir)) AssetDatabase.CreateFolder("Assets/Tiles", "Generated");
        string path = $"{TileDir}/{s.name}.asset";
        var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (t == null)
        {
            t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = s;
            t.colliderType = collider;
            t.flags = TileFlags.None;
            AssetDatabase.CreateAsset(t, path);
        }
        else if (t.colliderType != collider || t.flags != TileFlags.None)
        {
            t.colliderType = collider;
            t.flags = TileFlags.None;
            EditorUtility.SetDirty(t);
        }
        return t;
    }

    // ─────────────────────────────────────────────
    // PREFABS
    // ─────────────────────────────────────────────

    private static GameObject SpriteChild(Transform parent, string name, Sprite s, float targetHeight, int order, Vector2 offset)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = offset;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = order;
        if (s != null && s.bounds.size.y > 0f)
            go.transform.localScale = Vector3.one * (targetHeight / s.bounds.size.y);
        return go;
    }

    private static Light2D AddLight(Transform parent, Color c, float radius, float intensity, Vector2 offset)
    {
        var go = new GameObject("Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = offset;
        var l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.pointLightOuterRadius = radius;
        l.pointLightInnerRadius = radius * 0.15f;
        l.falloffIntensity = 0.6f;
        return l;
    }

    private static GameObject SavePrefab(GameObject go)
    {
        if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/Prefabs", "Generated");
        string path = $"{PrefabDir}/{go.name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    /// <summary>Sprite de la cellule (Resources/Prison, dessiné par Claude) : 32 px par unité, filtrage net.</summary>
    /// <summary>Objet dessiné pour le projet (Assets/Art/Generated/Props, PPU 16).</summary>
    private static Sprite PropSprite(string name)
    {
        string path = $"Assets/Art/Generated/Props/{name}.png";
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.spritePixelsPerUnit != 16f || imp.filterMode != FilterMode.Point))
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 16f;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite PrisonSprite(string name)
    {
        string path = $"Assets/Resources/Prison/{name}.png";
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.spritePixelsPerUnit != 32f || imp.filterMode != FilterMode.Point))
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 32f;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static GameObject Decor(string name, Sprite s, float height, ItemKind kind = ItemKind.Decor, int order = 3)
    {
        var root = new GameObject(name);
        var po = root.AddComponent<PlacedObject>();
        po.kind = kind;
        SpriteChild(root.transform, "Sprite", s, height, order, new Vector2(0, height * 0.5f - 0.45f));
        return root;
    }

    [MenuItem("DungeonCore/2. Générer le catalogue et les prefabs")]
    public static string BuildCatalog()
    {
        ImportIslandTileset();
        WallBuilder.BuildAll();
        var island = Sprites(ArtPath);
        var dungeon = Sprites("Assets/imports/dungeon_tiles.png");
        var traps = Sprites("Assets/imports/full_traps.png");
        var snow = Sprites("Assets/imports/snow-expansion.png");

        // ── Monstres ──
        var goblinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/gobelin.prefab");
        GameObject slimePrefab = BuildSlime(goblinPrefab);

        // ── Pièges ──
        TrapArt.ImportAll();
        Sprite[] TA(string n) => TrapArt.Frames(n);
        GameObject Kit<T>(string name, string art, int order, System.Action<T> cfg = null, Color? light = null, float lightRadius = 2f) where T : TrapKit
        {
            var root = new GameObject(name);
            root.AddComponent<PlacedObject>().kind = ItemKind.Trap;
            var child = new GameObject("Sprite");
            child.transform.SetParent(root.transform, false);
            var sr = child.AddComponent<SpriteRenderer>();
            var fr = TA(art);
            sr.sprite = fr.Length > 0 ? fr[0] : null;
            sr.sortingOrder = order;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            var k = root.AddComponent<T>();
            k.frames = fr;
            k.sr = sr;
            cfg?.Invoke(k);
            if (light.HasValue) AddLight(root.transform, light.Value, lightRadius, 0.7f, Vector2.zero);
            return SavePrefab(root);
        }
        var pendulumP = Kit<PendulumTrap>("LamePendulaire", "pendulum", 3);
        var sawP = Kit<SawTrap>("ScieAuSol", "saw", 1);
        var cageP = Kit<CageTrap>("CageTombante", "cage", 3);
        var runeP = Kit<RuneTrap>("RuneExplosive", "rune", 1);
        var spearsP = Kit<WallTrap>("LancesMurales", "wallspears", 3, t => { t.mode = WallTrap.Mode.Spears; t.damage = 26f; t.interval = 3f; });
        var dartsP = Kit<WallTrap>("Flechettes", "darts", 3, t => { t.mode = WallTrap.Mode.Darts; t.damage = 12f; t.interval = 4f; });
        var crossbowP = Kit<WallTrap>("ArbaleteCachee", "crossbow", 3, t => { t.mode = WallTrap.Mode.Crossbow; t.damage = 30f; t.interval = 2.5f; });
        var trapdoorP = Kit<TrapdoorTrap>("Trappe", "trapdoor", 1);
        var oilP = Kit<OilTrap>("DalleHuilee", "oil", 1);
        var boulderP = Kit<BoulderTrap>("RocherRoulant", "boulder", 3);
        var geyserP = Kit<GeyserTrap>("GeyserMagma", "geyser", 3, null, new Color(1f, 0.5f, 0.2f), 2f);
        var whirlP = Kit<WhirlpoolTrap>("Tourbillon", "whirlpool", 1, null, new Color(0.35f, 0.75f, 1f), 1.6f);
        var stalP = Kit<StalactiteTrap>("Stalactites", "stalactite", 3);
        var sporeP = Kit<SporeTrap>("ChampignonSpores", "spores", 3, null, new Color(0.6f, 1f, 0.4f), 1.4f);
        var prismP = Kit<PrismTrap>("PrismeLaser", "prism", 3, null, new Color(0.7f, 0.5f, 1f), 1.8f);
        var graspP = Kit<GraspTrap>("MainsSquelettiques", "grasp", 2);
        var bladePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TrapSpinningBlade.prefab");
        var flame = new GameObject("FlameTrap");
        flame.AddComponent<PlacedObject>().kind = ItemKind.Trap;
        SpriteChild(flame.transform, "Nozzle", TA("flamevent").FirstOrDefault() ?? traps.GetValueOrDefault("full_traps_1"), 1f, 2, Vector2.zero);
        var flameSprite = SpriteChild(flame.transform, "Flame", traps.GetValueOrDefault("full_traps_5"), 1.4f, 5, new Vector2(0, 0.55f));
        AddLight(flame.transform, new Color(1f, 0.55f, 0.2f), 3.5f, 1.3f, new Vector2(0, 0.5f));
        var ft = flame.AddComponent<FlameTrap>();
        ft.flameRenderer = flameSprite.GetComponent<SpriteRenderer>();
        ft.flameFrames = Enumerable.Range(2, 7).Select(i => traps.GetValueOrDefault($"full_traps_{i}")).Where(x => x != null).ToArray();
        var flamePrefab = SavePrefab(flame);

        // ── Décors ──
        var torch = new GameObject("Torche");
        torch.AddComponent<PlacedObject>().kind = ItemKind.Decor;
        SpriteChild(torch.transform, "Base", dungeon.GetValueOrDefault("dungeon_tiles_219"), 1f, 3, new Vector2(0, 0f));
        SpriteChild(torch.transform, "Flame", dungeon.GetValueOrDefault("dungeon_tiles_196"), 1f, 4, new Vector2(0, 0.75f));
        AddLight(torch.transform, new Color(1f, 0.62f, 0.3f), 5.5f, 1.15f, new Vector2(0, 0.8f));
        torch.AddComponent<TorchFlicker>();
        var torchPrefab = SavePrefab(torch);

        var chest = Decor("Coffre", dungeon.GetValueOrDefault("dungeon_tiles_170"), 0.95f);
        chest.AddComponent<TreasureChest>();
        var chestPrefab = SavePrefab(chest);

        var gold = Decor("TasDOr", traps.GetValueOrDefault("full_traps_297"), 0.9f);
        gold.AddComponent<TreasureChest>().refillTime = 90f;
        AddLight(gold.transform, new Color(1f, 0.85f, 0.4f), 2.2f, 0.6f, Vector2.zero);
        var goldPrefab = SavePrefab(gold);

        var barrel = SavePrefab(Decor("Tonneau", traps.GetValueOrDefault("full_traps_195"), 0.95f));
        var supplies = SavePrefab(Decor("Reserves", traps.GetValueOrDefault("full_traps_260"), 1.1f));
        // Garde-manger (Tools~/make_props.py) : appât des intrus affamés
        var foodSprite = PropSprite("food_cache");
        var foodGo = Decor("GardeManger", foodSprite, 1.15f);
        foodGo.AddComponent<FoodBait>();
        var foodPrefab = SavePrefab(foodGo);
        // Salles à fonction et énigmes (Tools~/make_props.py)
        var nestPrefab = SavePrefab(Decor("NidDeCouvee", PropSprite("nest"), 0.7f));
        var anvilPrefab = SavePrefab(Decor("Enclume", PropSprite("anvil"), 0.75f));
        var forgeGo = Decor("Forge", PropSprite("forge"), 0.85f);
        AddLight(forgeGo.transform, new Color(1f, 0.55f, 0.2f), 2.6f, 0.9f, new Vector2(0f, 0.2f));
        forgeGo.AddComponent<TorchFlicker>();
        var forgePrefab = SavePrefab(forgeGo);
        GameObject Switch(string name, string off, string on, PuzzleSwitch.Kind kind, float secs, float h, Color? light)
        {
            var g = Decor(name, PropSprite(off), h);
            var sw = g.AddComponent<PuzzleSwitch>();
            sw.kind = kind; sw.offSprite = PropSprite(off); sw.onSprite = PropSprite(on); sw.activeSeconds = secs;
            if (light.HasValue) AddLight(g.transform, light.Value, 1.8f, 0f, new Vector2(0f, 0.3f));
            return SavePrefab(g);
        }
        var leverPrefab = Switch("Levier", "lever_off", "lever_on", PuzzleSwitch.Kind.Lever, 40f, 0.7f, null);
        var runePrefab = Switch("PierreRunique", "rune_off", "rune_on", PuzzleSwitch.Kind.Rune, 25f, 0.85f, new Color(0.6f, 0.5f, 1f));
        var plateGo = Decor("PlaqueDePression", PropSprite("plate"), 0.45f, ItemKind.Trap, 1);
        var plate = plateGo.AddComponent<PressurePlate>();
        plate.upSprite = PropSprite("plate"); plate.downSprite = PropSprite("plate_down");
        var platePrefab = SavePrefab(plateGo);
        var banner = SavePrefab(Decor("Banniere", traps.GetValueOrDefault("full_traps_200"), 1.5f));
        var helm = SavePrefab(Decor("Heaume", traps.GetValueOrDefault("full_traps_199"), 0.9f));
        var rack = SavePrefab(Decor("Ratelier", traps.GetValueOrDefault("full_traps_218"), 1.2f));
        var skull = Decor("CraneDragon", traps.GetValueOrDefault("full_traps_190"), 1.7f);
        // Cellule (prisonniers) : fond + barreaux de face, le prisonnier s'assoit entre les deux
        var cellBack = PrisonSprite("cell_back");
        var cellFront = PrisonSprite("cell_front");
        var cellGo = new GameObject("Cellule");
        cellGo.AddComponent<PlacedObject>().kind = ItemKind.Decor;
        cellGo.AddComponent<PrisonCell>();
        SpriteChild(cellGo.transform, "Fond", cellBack, 1.375f, 2, new Vector2(0, 0.24f));
        var frontGo = SpriteChild(cellGo.transform, "Barreaux", cellFront, 1.375f, 4, new Vector2(0, 0.24f));
        var cellPrefab = SavePrefab(cellGo);
        AddLight(skull.transform, new Color(0.6f, 0.3f, 1f), 3f, 0.7f, new Vector2(0, 0.4f));
        var skullPrefab = SavePrefab(skull);

        // ── Tuiles de thème ──
        Tile T(string n) => TileFor(island["island_" + n], Tile.ColliderType.None);

        // ── Catalogue ──
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var cat = AssetDatabase.LoadAssetAtPath<DungeonCatalog>(CatalogPath);
        bool isNew = cat == null;
        if (isNew) cat = ScriptableObject.CreateInstance<DungeonCatalog>();
        cat.items.Clear();
        cat.themes.Clear();


        void U(int upkeep) => cat.items[cat.items.Count - 1].upkeep = upkeep;
        void Add(string id, string n, CatalogCategory c, ItemKind k, GameObject prefab, int cost, int lvl, Sprite icon, string desc, float prestige = 0, DungeonBuilder.BuildMode tool = DungeonBuilder.BuildMode.None)
            => cat.items.Add(new CatalogItem { id = id, displayName = n, category = c, kind = k, prefab = prefab, cost = cost, unlockLevel = lvl, icon = icon, description = desc, prestige = prestige, tool = tool });

        var builder = Object.FindFirstObjectByType<DungeonBuilder>();
        int digCost = builder != null ? builder.miningCost : 10;
        int fillCost = builder != null ? builder.fillCost : 5;
        Add("dig", "Creuser", CatalogCategory.Build, ItemKind.Tool, null, digCost, 1, snow.GetValueOrDefault("snow-expansion_195"), "Marquer la roche à creuser", 0, DungeonBuilder.BuildMode.Digging);
        Add("fill", "Remblayer", CatalogCategory.Build, ItemKind.Tool, null, fillCost, 1, island["island_volcanic_a"], "Reboucher une case", 0, DungeonBuilder.BuildMode.Filling);
        Add("erase", "Gomme", CatalogCategory.Build, ItemKind.Tool, null, 0, 1, null, "Retirer un objet (remboursé à moitié)", 0, DungeonBuilder.BuildMode.Erasing);
        var orb = AssetDatabase.LoadAllAssetsAtPath("Assets/imports/dungeon_orb3.png").OfType<Sprite>().FirstOrDefault();
        Add("move_core", "Déplacer le cœur", CatalogCategory.Build, ItemKind.Tool, null, 0, 1, orb, "Transférer le cœur ailleurs dans le donjon (90 % du mana max)", 0, DungeonBuilder.BuildMode.MovingCore);

        // Monstres : prefabs animés générés par CharacterImporter (sinon anciens prefabs)
        GameObject C(string id) => CharacterImporter.Prefab(id);
        Sprite Icon(GameObject g) => g != null ? g.GetComponent<SpriteRenderer>()?.sprite : null;
        void Monster(string id, string n, int cost, int lvl, string desc, GameObject fallback = null)
        {
            var pf = C(id) ?? fallback;
            if (pf == null) return;
            // Icône = première image de l'animation d'attente (le prefab gardait le sprite du modèle gobelin)
            CharacterImporter.FixPrefabSprite(id);
            var icon = CharacterImporter.IconSprite(id) ?? Icon(pf);
            Add(id, n, CatalogCategory.Monster, ItemKind.Monster, pf, cost, lvl, icon, desc);
            var sr = pf.GetComponent<SpriteRenderer>();
            if (sr != null) cat.items[cat.items.Count - 1].iconTint = sr.color;
        }
        // Familles de monstres : seul le stade 1 s'achète, les stades 2 et 3 s'obtiennent par évolution
        foreach (var fam in CharacterImporter.Families())
        {
            for (int stg = 1; stg <= 3; stg++)
            {
                CharacterImporter.FixPrefabSprite($"{fam.id}_{stg}");
                var mu = C($"{fam.id}_{stg}")?.GetComponent<MonsterUnit>();
                if (mu != null && (mu.upkeepPerDay != CharacterImporter.UpkeepFor(fam, stg) || (stg < 3 && mu.evolveLevelRequired != CharacterImporter.EvolveLevel(fam, stg))))
                {
                    mu.upkeepPerDay = CharacterImporter.UpkeepFor(fam, stg);
                    if (stg < 3) mu.evolveLevelRequired = CharacterImporter.EvolveLevel(fam, stg);
                    EditorUtility.SetDirty(mu.gameObject);
                }
            }
            var paths = CharacterImporter.Branches().Where(br => br.family == fam.id).Select(br => br.names[0]).ToArray();
            Monster($"{fam.id}_1", fam.names[0], fam.cost, fam.level,
                $"{fam.desc} Évolue en {fam.names[1]} puis {fam.names[2]}" + (paths.Length > 0 ? $" — ou prend une autre voie : {string.Join(", ", paths)}" : "") +
                $". Entretien : {fam.upkeep} mana/jour.");
        }
        Monster("cave_boar", "Sanglier des cavernes", 70, 7, "Très rapide, charge les intrus qui fuient. Entretien : 5 mana/jour.");
        Monster("temple_spirit", "Esprit du temple", 320, 20, "Gardien antique : glace les intrus d'effroi. Entretien : 14 mana/jour.");
        // Menu trié par niveau de déblocage puis par coût (plus de monstres de niveau 3-4 après ceux de niveau 6)
        {
            int first = cat.items.FindIndex(it => it.kind == ItemKind.Monster);
            if (first >= 0)
            {
                var mons = cat.items.GetRange(first, cat.items.Count - first);
                var sorted = mons.OrderBy(it => it.unlockLevel).ThenBy(it => it.cost).ToList();
                cat.items.RemoveRange(first, mons.Count);
                cat.items.AddRange(sorted);
            }
        }
        // Squelettes invoqués par les liches : prefab sans coût (non listé)

        // Intrus : apparence par palier (pêcheurs, villageois, épéistes de niveaux croissants)
        foreach (var id in new[] { "fisherman", "villager", "wild_deer", "wild_hare", "wild_fox", "wild_boar", "wild_black_grouse", "adv_archer", "adv_mage", "adv_healer" })
            CharacterImporter.FixPrefabSprite(id);
        for (int i = 1; i <= 9; i++) CharacterImporter.FixPrefabSprite($"swordsman_{i}");
        cat.adventurerPrefabs.Clear();
        foreach (var id in new[] { "fisherman", "villager", "swordsman_1", "swordsman_4", "swordsman_7" }) cat.adventurerPrefabs.Add(C(id));
        cat.archerPrefab = C("adv_archer");
        cat.magePrefab = C("adv_mage");
        cat.healerPrefab = C("adv_healer");
        RuntimeAnimatorController Ctrl(string id) =>
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{CharacterImporter.AnimDir}/{id}/{id}.controller");
        cat.lookWarrior.Clear(); cat.lookArcher.Clear(); cat.lookMage.Clear(); cat.lookHealer.Clear();
        var previews = new[] { "swordsman_1", "adv_archer_1", "adv_mage_1", "adv_healer_1" };
        cat.lookPreview = new Sprite[4];
        for (int i = 0; i < 4; i++)
        {
            // première image du repos, de face, sur la planche 8 directions (texture lisible : recoloration de l'aperçu)
            string sheet = $"{CharacterImporter.Dir8Root}/{previews[i]}/{previews[i]}_Idle.png";
            Sprite first = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(sheet))
                if (o is Sprite s0 && s0.name.EndsWith("_0_0")) first = s0;
            cat.lookPreview[i] = first;
        }
        for (int t = 1; t <= 9; t++)
        {
            cat.lookWarrior.Add(Ctrl($"swordsman_{t}"));
            cat.lookArcher.Add(Ctrl($"adv_archer_{t}"));
            cat.lookMage.Add(Ctrl($"adv_mage_{t}"));
            cat.lookHealer.Add(Ctrl($"adv_healer_{t}"));
        }
        cat.tierVariants.Clear();
        int[][] variants = { new[] { 0 }, new[] { 0 }, new[] { 1, 2, 3 }, new[] { 3, 4, 5, 6 }, new[] { 7, 8, 9 } };
        for (int t = 0; t < variants.Length; t++)
        {
            var tp = new TierPrefabs();
            foreach (int v in variants[t])
                if (v > 0 && C($"swordsman_{v}") != null) tp.prefabs.Add(C($"swordsman_{v}"));
            cat.tierVariants.Add(tp);
        }
        cat.wildlifePrefabs.Clear();
        foreach (var id in new[] { "wild_deer", "wild_deer", "wild_hare", "wild_hare", "wild_fox", "wild_boar", "wild_black_grouse" })
            if (C(id) != null) cat.wildlifePrefabs.Add(C(id));
        cat.settlerPrefabs.Clear();
        foreach (var id in new[] { "citizen_1", "citizen_2", "citizen_3", "citizen_4", "citizen_5", "settler_fisher" })
        {
            CharacterImporter.FixPrefabSprite(id);
            if (C(id) != null) cat.settlerPrefabs.Add(C(id));
        }

        // Le spawner de la scène utilise l'épéiste par défaut
        var sp = Object.FindFirstObjectByType<AdventurerSpawner>();
        if (sp != null && C("swordsman_1") != null)
        {
            sp.adventurerPrefab = C("swordsman_1");
            EditorUtility.SetDirty(sp);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sp.gameObject.scene);
        }
        // Le Noyau invoque le gobelin par défaut (outil historique)
        var ms = Object.FindFirstObjectByType<MonsterSpawner>();
        if (ms != null && C("goblin_1") != null) { ms.gobelinPrefab = C("goblin_1"); EditorUtility.SetDirty(ms); }

        // ── Objets générés avec PixelLab (Assets/Art/PixelLab) ──
        var pl = ImportPixelLab();
        Sprite PL(string n) => pl.GetValueOrDefault(n);

        GameObject FloorTrap(string name, Sprite s)
        {
            var root = new GameObject(name);
            root.AddComponent<PlacedObject>().kind = ItemKind.Trap;
            SpriteChild(root.transform, "Sprite", s, 1f, 1, Vector2.zero);
            return root;
        }
        var spikes = FloorTrap("Pics", PL("trap_spikes_off"));
        var st = spikes.AddComponent<SpikeTrap>();
        st.retractedSprite = PL("trap_spikes_off"); st.extendedSprite = PL("trap_spikes_on");
        st.spriteRenderer = spikes.GetComponentInChildren<SpriteRenderer>();
        var spikesPrefab = SavePrefab(spikes);

        var bear = FloorTrap("PiegeALoup", PL("trap_bear_open"));
        var bt = bear.AddComponent<BearTrap>();
        bt.openSprite = TA("bear").FirstOrDefault() ?? PL("trap_bear_open"); bt.closedSprite = TA("bear").LastOrDefault() ?? PL("trap_bear_closed");
        bear.GetComponentInChildren<SpriteRenderer>().sprite = bt.openSprite;
        bear.GetComponentInChildren<SpriteRenderer>().transform.localScale = Vector3.one;
        bt.spriteRenderer = bear.GetComponentInChildren<SpriteRenderer>();
        var bearPrefab = SavePrefab(bear);

        var gas = FloorTrap("GazToxique", PL("trap_poison"));
        gas.GetComponentInChildren<SpriteRenderer>().sortingOrder = 4;
        if (TA("gas").Length > 0)
        {
            var gsr = gas.GetComponentInChildren<SpriteRenderer>();
            gsr.sprite = TA("gas")[0];
            gsr.transform.localScale = Vector3.one;
            var gan = gsr.gameObject.AddComponent<SpriteAnim>(); gan.frames = TA("gas"); gan.fps = 6; gan.randomStart = true;
        }
        AddLight(gas.transform, new Color(0.45f, 1f, 0.35f), 2.6f, 0.6f, Vector2.zero);
        gas.AddComponent<PoisonTrap>();
        var gasPrefab = SavePrefab(gas);

        ArtImporter.ImportAll();
        Sprite[] AF(string n) => ArtImporter.Frames(ArtImporter.DungeonDir, n);

        // Portes des packs (voûte de pierre, battant qui s'ouvre / herse qui se lève), posées au bas de leur case
        GameObject Door(string name, Sprite[] frames, Sprite fallback, float hold, float bite = 0f, string text = null, Sprite[] side = null, System.Action<GameObject> cfg = null)
        {
            var root = new GameObject(name);
            root.AddComponent<PlacedObject>().kind = ItemKind.Decor;
            var d = root.AddComponent<DungeonDoor>();
            d.holdSeconds = hold;
            d.biteDamage = bite;
            if (text != null) d.forceText = text;
            if (frames != null && frames.Length > 0)
            {
                var child = new GameObject("Sprite");
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(0f, -0.5f, 0f);
                var sr = child.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0];
                sr.sortingOrder = 3;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
                d.frames = frames.Length > 1 ? frames : null;
                d.sideFrames = side != null && side.Length > 1 ? side : null;
                d.doorRenderer = sr;
            }
            else
            {
                SpriteChild(root.transform, "Sprite", fallback, 1f, 3, Vector2.zero);
                d.doorRenderer = root.GetComponentInChildren<SpriteRenderer>();
            }
            cfg?.Invoke(root);
            return SavePrefab(root);
        }
        var woodDoor = Door("PorteBois", AF("door_wood"), PL("door_wood"), 2.5f, 0f, null, AF("door_wood_side"));
        var ironDoor = Door("Herse", AF("door_gate"), PL("door_iron"), 5f, 0f, "Soulève la herse…", AF("door_gate_side"));
        var skullDoor = Door("GueuleOssements", AF("door_skull"), null, 6f, 12f, "La gueule mord !");
        // Énigmes : portes scellées (sceau runique peint par-dessus la herse)
        System.Action<GameObject> Seal(int switches, Color glow) => g =>
        {
            g.GetComponent<DungeonDoor>().sealSwitches = switches;
            var seal = SpriteChild(g.transform, "Sceau", PropSprite("seal"), 0.6f, 4, new Vector2(0f, 0.45f));
            seal.GetComponent<SpriteRenderer>().color = glow;
            AddLight(g.transform, new Color(0.6f, 0.5f, 1f), 1.6f, 0.5f, new Vector2(0f, 0.4f));
        };
        var leverDoor = Door("PorteLevier", AF("door_gate"), PL("door_iron"), 0f, 0f, "Scellée", AF("door_gate_side"), Seal(1, new Color(0.9f, 0.85f, 0.7f)));
        var runeDoor = Door("PorteRunique", AF("door_gate"), PL("door_iron"), 0f, 0f, "Scellée", AF("door_gate_side"), Seal(3, Color.white));
        GameObject Anim(string name, string frames, AnimatedTrap.Trigger trig, float dmg, float radius, System.Action<AnimatedTrap> cfg = null, Color? light = null)
        {
            var root = new GameObject(name);
            root.AddComponent<PlacedObject>().kind = ItemKind.Trap;
            var fr = AF(frames);
            var child = new GameObject("Sprite");
            child.transform.SetParent(root.transform, false);
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = fr.Length > 0 ? fr[0] : null;
            // Piège plat (pics) : sous les murs. Piège haut : posé au bas de la case, devant le mur du fond.
            bool flat = frames == "trap_spikes";
            sr.sortingOrder = flat ? 1 : 3;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            if (!flat) child.transform.localPosition = new Vector3(0f, -0.48f, 0f);
            var at = root.AddComponent<AnimatedTrap>();
            at.frames = fr;
            at.spriteRenderer = sr;
            at.trigger = trig;
            at.damage = dmg;
            at.radius = radius;
            at.hitFrame = Mathf.Max(1, fr.Length / 2);
            cfg?.Invoke(at);
            if (light.HasValue) AddLight(root.transform, light.Value, 2.5f, 0f, Vector2.zero);
            return SavePrefab(root);
        }
        var spikesAnim = Anim("PicsAnimes", "trap_spikes", AnimatedTrap.Trigger.Periodic, 22, 0.5f, a => { a.interval = 2.4f; a.fps = 8; a.hitFrame = 3; if (TA("spikes").Length > 0) { a.frames = TA("spikes"); a.spriteRenderer.sprite = a.frames[0]; a.spriteRenderer.transform.localPosition = Vector3.zero; } });
        var guillotine = Anim("Guillotine", "trap_guillotine", AnimatedTrap.Trigger.Periodic, 48, 0.5f, a => { a.interval = 3.6f; a.fps = 16; a.hitFrame = 3; a.shake = 0.05f; });
        var column = Anim("ColonneLances", "trap_column", AnimatedTrap.Trigger.Periodic, 26, 1.35f, a => { a.interval = 3f; a.fps = 10; a.hitFrame = 3; });
        var bomb = Anim("Bombe", "trap_bomb", AnimatedTrap.Trigger.Proximity, 75, 1.7f, a => { a.fps = 10; a.hitFrame = 6; a.triggerRadius = 0.7f; a.rearmSeconds = 25f; a.hideWhileRearming = true; a.shake = 0.18f; a.hitFx = "explosion"; a.hitFxSize = 2.2f; },
            new Color(0.4f, 1f, 0.85f));
        var ghostTrap = Anim("PiegeSpectral", "trap_ghost", AnimatedTrap.Trigger.Proximity, 30, 1f, a => { a.fps = 12; a.hitFrame = 5; a.triggerRadius = 0.8f; a.rearmSeconds = 12f; a.slowMult = 0.45f; a.slowSeconds = 3f; a.hitFx = "ice"; a.hitFxSize = 1.4f; if (TA("ghostplate").Length > 0) { a.frames = TA("ghostplate"); a.fps = 8; a.hitFrame = 2; a.spriteRenderer.sprite = a.frames[0]; a.spriteRenderer.transform.localPosition = Vector3.zero; a.spriteRenderer.sortingOrder = 1; } },
            new Color(0.6f, 0.9f, 1f));
        var blades = Anim("LamesRotatives", "trap_blades", AnimatedTrap.Trigger.Continuous, 0, 0.6f, a => { a.fps = 14; a.dps = 16f; });
        var webT = new GameObject("Toile");
        webT.AddComponent<PlacedObject>().kind = ItemKind.Trap;
        SpriteChild(webT.transform, "Sprite", TA("web").FirstOrDefault() ?? ArtImporter.Single(ArtImporter.DungeonDir, "web"), 1f, 1, Vector2.zero);
        var webAt = webT.AddComponent<AnimatedTrap>();
        webAt.frames = new[] { TA("web").FirstOrDefault() ?? ArtImporter.Single(ArtImporter.DungeonDir, "web") };
        webAt.spriteRenderer = webT.GetComponentInChildren<SpriteRenderer>();
        webAt.trigger = AnimatedTrap.Trigger.Continuous; webAt.dps = 0f; webAt.radius = 0.55f; webAt.slowMult = 0.35f; webAt.slowSeconds = 0.8f;
        var web = SavePrefab(webT);
        spikes = spikesAnim;

        // Décors animés et objets des cryptes
        GameObject AnimDecor(string name, string frames, float fps, float prestige, Color? light = null, float lightRadius = 3f, System.Action<GameObject> cfg = null)
        {
            var root = new GameObject(name);
            root.AddComponent<PlacedObject>().kind = ItemKind.Decor;
            var fr = AF(frames);
            var child = new GameObject("Sprite");
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = fr.Length > 0 ? fr[0] : null;
            sr.sortingOrder = 3;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            if (fr.Length > 1) { var an = child.AddComponent<SpriteAnim>(); an.frames = fr; an.fps = fps; an.randomStart = true; }
            if (light.HasValue) { AddLight(root.transform, light.Value, lightRadius, 1f, new Vector2(0f, 0.3f)); root.AddComponent<TorchFlicker>(); }
            cfg?.Invoke(root);
            return SavePrefab(root);
        }
        var brazier = AnimDecor("Brasero", "brazier", 9, 1.5f, new Color(0.45f, 1f, 0.85f), 4.5f);
        var wallTorch = AnimDecor("TorcheSpectrale", "torch_wall", 9, 1f, new Color(0.45f, 1f, 0.85f), 3.5f);
        var candelabra = AnimDecor("Candelabre", "candelabra", 8, 1f, new Color(0.5f, 1f, 0.8f), 2.5f);
        var statueFire = AnimDecor("StatueDemoniaque", "statue_fire", 8, 6f, new Color(0.4f, 1f, 0.7f), 4f);
        var fountain = AnimDecor("Fontaine", "fountain", 7, 5f, new Color(0.5f, 0.8f, 1f), 3f);
        GameObject Obj(string name, string sprite) => AnimDecor(name, sprite, 0, 0);
        var sarcophagus = Obj("Sarcophage", "sarcophagus");
        var sarcoOpen = Obj("SarcophageOuvert", "sarcophagus_open");
        var coffin = Obj("Cercueil", "coffin_wood_open");
        var gargoyle = Obj("Gargouille", "gargoyle");
        var statueKnight = Obj("StatueChevalier", "statue_knight");
        var hanging = Obj("SqueletteSuspendu", "hanging_skeleton");
        var skulls = Obj("PileCranes", "skull_pile");
        var bones = Obj("Ossements", "bones");
        var goldPile = AnimDecor("PileOr", "gold_pile", 0, 0, null, 0, g => { g.AddComponent<TreasureChest>(); AddLight(g.transform, new Color(1f, 0.85f, 0.4f), 2f, 0.5f, Vector2.zero); });
        var royal = AnimDecor("CoffreRoyal", "chest_royal", 0, 0, null, 0, g => g.AddComponent<TreasureChest>());
        // Caverne (pack « cave tileset ») : cristaux lumineux, champignons, stalagmites, faille de magma, autel démoniaque
        var crysBlue = AnimDecor("CristauxBleus", "cave_crystal_blue", 0, 0, new Color(0.45f, 0.7f, 1f), 2.5f);
        var crysRed = AnimDecor("CristauxRouges", "cave_crystal_red", 0, 0, new Color(1f, 0.35f, 0.35f), 2.5f);
        var crysYellow = AnimDecor("CristauxAmbre", "cave_crystal_yellow", 0, 0, new Color(1f, 0.8f, 0.35f), 2.5f);
        var crysGreen = AnimDecor("CristauxVerts", "cave_crystal_green", 0, 0, new Color(0.4f, 1f, 0.55f), 2.2f);
        var crysBlack = Obj("CristauxNoirs", "cave_crystal_black");
        var mushLong = AnimDecor("ChampignonsGeants", "cave_mushrooms_long", 0, 0, new Color(1f, 0.55f, 0.3f), 1.8f);
        var mushSlime = AnimDecor("ChampignonGluant", "cave_mushroom_slime", 0, 0, new Color(0.4f, 1f, 0.9f), 1.8f);
        var mushSmall = Obj("Champignons", "cave_mushrooms_small");
        var stalag = Obj("Stalagmites", "cave_stalagmites");
        var stalagGray = Obj("StalagmitesGrises", "cave_stalagmites_gray");
        var magma = AnimDecor("FailleMagma", "cave_magma", 7, 0, new Color(1f, 0.5f, 0.2f), 3f);
        var demonHead = AnimDecor("AutelDemoniaque", "cave_demon_head", 7, 0, new Color(1f, 0.35f, 0.25f), 5f);
        var demonHand = AnimDecor("MainDemoniaque", "cave_demon_hand", 7, 0, new Color(1f, 0.5f, 0.3f), 2.5f);

        U(2);
        Add("pendulum", "Lame pendulaire", CatalogCategory.Trap, ItemKind.Trap, pendulumP, 35, 1, TA("pendulum").Skip(3).FirstOrDefault(), "Une lame balaie la case sans fin : 28 dégâts à chaque passage."); U(3);
        Add("saw", "Scie au sol", CatalogCategory.Trap, ItemKind.Trap, sawP, 55, 2, TA("saw").FirstOrDefault(), "Court dans sa fente, dans l'axe du couloir : ronge et ralentit qui marche dessus (15 dégâts/s)."); U(5);
        Add("cage", "Cage tombante", CatalogCategory.Trap, ItemKind.Trap, cageP, 50, 2, TA("cage").Skip(4).FirstOrDefault(), "Tombe sur l'intrus et l'enferme 5 s : vos monstres n'ont plus qu'à frapper. Se réarme en 12 s."); U(4);
        Add("rune", "Rune explosive", CatalogCategory.Trap, ItemKind.Trap, runeP, 60, 3, TA("rune").Skip(2).FirstOrDefault(), "Rougeoie 0,8 s quand on marche dessus, puis souffle la zone (70 dégâts). Se réarme en 20 s."); U(5);
        Add("wallspears", "Lances murales", CatalogCategory.Trap, ItemKind.Trap, spearsP, 65, 3, TA("wallspears").Skip(3).FirstOrDefault(), "Se fixe au mur : des lances traversent le couloir toutes les 3 s (26 dégâts, deux cases)."); U(5);
        Add("darts", "Lanceur de fléchettes", CatalogCategory.Trap, ItemKind.Trap, dartsP, 45, 2, TA("darts").LastOrDefault(), "Se fixe au mur : trois fléchettes dans l'axe du couloir dès qu'un intrus s'y engage (6 cases)."); U(3);
        Add("trapdoor", "Trappe", CatalogCategory.Trap, ItemKind.Trap, trapdoorP, 70, 4, TA("trapdoor").FirstOrDefault(), "Le sol s'ouvre : l'intrus tombe à l'étage du dessous (ou au fond d'une fosse). Se réarme en 15 s."); U(5);
        Add("oil", "Dalle huilée", CatalogCategory.Trap, ItemKind.Trap, oilP, 25, 2, TA("oil").FirstOrDefault(), "On y glisse sans contrôle, droit dans le piège suivant. Une flamme voisine l'embrase."); U(2);
        Add("boulder", "Rocher roulant", CatalogCategory.Trap, ItemKind.Trap, boulderP, 80, 4, TA("boulder").FirstOrDefault(), "Au bout d'un couloir : dévale toute la ligne quand un intrus s'y engage (45 dégâts à chacun). Revient en 30 s."); U(6);
        Add("geyser", "Geyser de magma", CatalogCategory.Trap, ItemKind.Trap, geyserP, 60, 3, TA("geyser").Skip(3).FirstOrDefault(), "Fournaise : bouillonne, puis crache la lave toutes les 4 s (35 dégâts et brûlure)."); U(5);
        Add("whirlpool", "Maelström", CatalogCategory.Trap, ItemKind.Trap, whirlP, 55, 3, TA("whirlpool").FirstOrDefault(), "Sanctuaire englouti : aspire et ralentit les intrus alentour, noie ceux qui y tombent."); U(4);
        Add("stalactite", "Stalactites", CatalogCategory.Trap, ItemKind.Trap, stalP, 50, 3, TA("stalactite").Skip(2).FirstOrDefault(), "Caverne gelée : l'ombre grandit, le glaçon tombe (40 dégâts, ralentit). Se réarme en 10 s."); U(4);
        Add("spores", "Champignon à spores", CatalogCategory.Trap, ItemKind.Trap, sporeP, 50, 3, TA("spores").Skip(2).FirstOrDefault(), "Jardin fongique : un nuage de spores empoisonne et aveugle les intrus proches."); U(4);
        Add("prism", "Prisme laser", CatalogCategory.Trap, ItemKind.Trap, prismP, 70, 4, TA("prism").Skip(2).FirstOrDefault(), "Temple de cristal : se charge, puis tire un rayon dans l'axe du couloir (18 dégâts, 4 cases de chaque côté)."); U(5);
        Add("grasp", "Mains squelettiques", CatalogCategory.Trap, ItemKind.Trap, graspP, 55, 3, TA("grasp").Skip(3).FirstOrDefault(), "Crypte et catacombes : des mains d'os jaillissent et agrippent l'intrus 3 s."); U(4);
        Add("crossbow", "Arbalète cachée", CatalogCategory.Trap, ItemKind.Trap, crossbowP, 60, 3, TA("crossbow").FirstOrDefault(), "Forteresse : se fixe au mur et tire un carreau dans l'axe du couloir toutes les 2,5 s (30 dégâts)."); U(5);
        Add("spikes", "Pics rétractables", CatalogCategory.Trap, ItemKind.Trap, spikes, 35, 1, TA("spikes").Skip(3).FirstOrDefault() ?? PL("trap_spikes_on"), "Jaillissent du sol toutes les 2,4 s (22 dégâts)."); U(3);
        Add("web", "Toile d'araignée", CatalogCategory.Trap, ItemKind.Trap, web, 15, 1, ArtImporter.Single(ArtImporter.DungeonDir, "web"), "Englue les intrus : vitesse -65 % tant qu'ils la traversent."); U(2);
        Add("ghost_trap", "Piège spectral", CatalogCategory.Trap, ItemKind.Trap, ghostTrap, 80, 4, TA("ghostplate").Skip(2).FirstOrDefault() ?? AF("trap_ghost").Skip(4).FirstOrDefault(), "Un spectre surgit : 30 dégâts et les intrus, glacés, ralentissent 3 s."); U(8);
        Add("bear", "Piège à loup", CatalogCategory.Trap, ItemKind.Trap, bearPrefab, 40, 2, TA("bear").FirstOrDefault() ?? PL("trap_bear_open"), "Se referme sur un intrus : le blesse et l'immobilise 3,5 s. Se réarme seul."); U(3);
        Add("poison", "Gaz toxique", CatalogCategory.Trap, ItemKind.Trap, gasPrefab, 70, 3, TA("gas").FirstOrDefault() ?? PL("trap_poison"), "Nuage permanent : empoisonne et ralentit les intrus alentour."); U(6);
        Add("door_wood", "Porte en bois", CatalogCategory.Trap, ItemKind.Decor, woodDoor, 25, 1, AF("door_wood").FirstOrDefault() ?? PL("door_wood"), "Porte voûtée : les intrus perdent 2,5 s à la forcer, puis elle reste ouverte. Fermée, elle arrête la vue, les tirs... et vos monstres, sauf sur votre ordre.", 0.5f); U(2);
        Add("lever_door", "Porte à levier", CatalogCategory.Trap, ItemKind.Decor, leverDoor, 45, 2, PropSprite("seal"), "Énigme : elle ne se force pas. Un intrus doit trouver et baisser un levier (à 12 cases au plus) : le groupe se disperse, vos monstres en profitent.", 1f); U(2);
        Add("lever", "Levier", CatalogCategory.Trap, ItemKind.Decor, leverPrefab, 20, 2, PropSprite("lever_off"), "Ouvre les portes à levier voisines (12 cases) pendant 40 s. Cachez-le au fond d'une impasse gardée.", 0.3f);
        Add("rune_door", "Porte runique", CatalogCategory.Trap, ItemKind.Decor, runeDoor, 90, 5, PropSprite("seal"), "Énigme : trois pierres runiques (à 12 cases au plus) doivent briller en même temps pour l'ouvrir. Les intrus se séparent pour les éveiller.", 2f); U(4);
        Add("rune", "Pierre runique", CatalogCategory.Trap, ItemKind.Decor, runePrefab, 35, 5, PropSprite("rune_off"), "Brille 25 s quand on la touche. Trois en même temps ouvrent une porte runique.", 0.8f);
        Add("pressure_plate", "Plaque de pression", CatalogCategory.Trap, ItemKind.Trap, platePrefab, 30, 2, PropSprite("plate"), "Fléchettes (9 dégâts), les portes voisines claquent et restent bloquées 8 s, vos monstres proches accourent. Se réarme en 10 s.", 0.3f);
        Add("door_iron", "Herse de fer", CatalogCategory.Trap, ItemKind.Decor, ironDoor, 60, 3, AF("door_gate").FirstOrDefault() ?? PL("door_iron"), "Retient chaque intrus 5 s le temps de la soulever : parfait devant une embuscade.", 1f); U(3);
        if (AF("door_skull").Length > 0) { Add("door_skull", "Gueule d'ossements", CatalogCategory.Trap, ItemKind.Decor, skullDoor, 110, 5, AF("door_skull")[0], "Porte de crypte vivante : retient 6 s et mord (12 dégâts) chaque intrus qui la force.", 2.5f); U(6); }
        Add("flame", "Jet de flammes", CatalogCategory.Trap, ItemKind.Trap, flamePrefab, 45, 2, traps.GetValueOrDefault("full_traps_7"), "Crache du feu par intermittence. Gros dégâts."); U(5);

        Add("torch", "Torche", CatalogCategory.Decor, ItemKind.Decor, torchPrefab, 15, 1, dungeon.GetValueOrDefault("dungeon_tiles_196"), "Éclaire les couloirs (surtout la nuit).", 1f);
        Add("chest", "Coffre au trésor", CatalogCategory.Decor, ItemKind.Decor, chestPrefab, 60, 1, dungeon.GetValueOrDefault("dungeon_tiles_170"), "Appât : les intrus cupides font un détour pour le piller. Chaque jour, le Noyau le remplit (butin pris aux morts, ou richesses nées du mana).", 2f); U(4);
        Add("barrel", "Tonneau", CatalogCategory.Decor, ItemKind.Decor, barrel, 8, 1, traps.GetValueOrDefault("full_traps_195"), "Simple décor.", 0.5f);
        Add("nest", "Nid de couvée", CatalogCategory.Decor, ItemKind.Decor, nestPrefab, 60, 3, PropSprite("nest"), "Couvoir : trois nids rapprochés font naître chaque matin un monstre (de la famille la plus présente dans la salle).", 0.5f);
        Add("anvil", "Enclume", CatalogCategory.Decor, ItemKind.Decor, anvilPrefab, 45, 4, PropSprite("anvil"), "Forge des monstres (3 enclumes ou forges) : vos monstres y encaissent 15 % de dégâts en moins et y évoluent pour 25 % de mana en moins.", 1f);
        Add("forge", "Forge", CatalogCategory.Decor, ItemKind.Decor, forgePrefab, 70, 4, PropSprite("forge"), "Forge des monstres (3 enclumes ou forges) : protection et évolutions moins chères. Elle éclaire la salle.", 1.5f);
        Add("food_cache", "Garde-manger", CatalogCategory.Decor, ItemKind.Decor, foodPrefab, 40, 1, foodSprite, "Appât : un intrus affamé qui le voit accourt et se goinfre, immobile et sans méfiance. Regarni chaque matin.", 0.5f);
        Add("supplies", "Réserves", CatalogCategory.Decor, ItemKind.Decor, supplies, 12, 1, traps.GetValueOrDefault("full_traps_260"), "Caisses et tonneaux.", 0.7f);
        Add("banner", "Bannière", CatalogCategory.Decor, ItemKind.Decor, banner, 20, 2, traps.GetValueOrDefault("full_traps_200"), "Un donjon qui a du style attire les curieux.", 1.5f);
        Add("helm", "Heaume trophée", CatalogCategory.Decor, ItemKind.Decor, helm, 20, 2, traps.GetValueOrDefault("full_traps_199"), "Souvenir d'un chevalier malchanceux.", 1.5f);
        Add("rack", "Râtelier d'armes", CatalogCategory.Decor, ItemKind.Decor, rack, 35, 2, traps.GetValueOrDefault("full_traps_218"), "Armes des vaincus.", 2f);
        Add("crystal_blue", "Cristaux bleus", CatalogCategory.Decor, ItemKind.Decor, crysBlue, 35, 2, AF("cave_crystal_blue").FirstOrDefault(), "Lueur froide. Réunis, les cristaux forment un sanctuaire.", 2.5f);
        Add("crystal_red", "Cristaux rouges", CatalogCategory.Decor, ItemKind.Decor, crysRed, 35, 2, AF("cave_crystal_red").FirstOrDefault(), "Ils pulsent comme un cœur.", 2.5f);
        Add("crystal_yellow", "Cristaux d'ambre", CatalogCategory.Decor, ItemKind.Decor, crysYellow, 40, 3, AF("cave_crystal_yellow").FirstOrDefault(), "On jurerait de l'or. Les intrus aussi.", 3f);
        Add("crystal_green", "Cristaux verts", CatalogCategory.Decor, ItemKind.Decor, crysGreen, 30, 2, AF("cave_crystal_green").FirstOrDefault(), "Une lueur malade, apaisante pour vos créatures.", 2f);
        Add("crystal_black", "Cristaux noirs", CatalogCategory.Decor, ItemKind.Decor, crysBlack, 20, 1, AF("cave_crystal_black").FirstOrDefault(), "Obsidienne brute.", 1f);
        Add("mushrooms_long", "Champignons géants", CatalogCategory.Decor, ItemKind.Decor, mushLong, 25, 1, AF("cave_mushrooms_long").FirstOrDefault(), "Phosphorescents. Réunis, ils forment un jardin fongique.", 1.2f);
        Add("mushroom_slime", "Champignon gluant", CatalogCategory.Decor, ItemKind.Decor, mushSlime, 25, 2, AF("cave_mushroom_slime").FirstOrDefault(), "Il suinte. Les slimes adorent.", 1.2f);
        Add("mushrooms_small", "Champignons", CatalogCategory.Decor, ItemKind.Decor, mushSmall, 8, 1, AF("cave_mushrooms_small").FirstOrDefault(), "Quelques champignons dans les recoins.", 0.4f);
        Add("stalagmites", "Stalagmites", CatalogCategory.Decor, ItemKind.Decor, stalag, 10, 1, AF("cave_stalagmites").FirstOrDefault(), "La roche qui pousse.", 0.5f);
        Add("stalagmites_gray", "Stalagmites grises", CatalogCategory.Decor, ItemKind.Decor, stalagGray, 10, 1, AF("cave_stalagmites_gray").FirstOrDefault(), "Des crocs de pierre.", 0.5f);
        Add("magma_vent", "Faille de magma", CatalogCategory.Decor, ItemKind.Decor, magma, 45, 3, AF("cave_magma").FirstOrDefault(), "La roche saigne du feu. Éclaire et impressionne.", 2f);
        Add("demon_hand", "Main démoniaque", CatalogCategory.Decor, ItemKind.Decor, demonHand, 60, 4, AF("cave_demon_hand").FirstOrDefault(), "Elle sort du sol et s'agite. Glaçant.", 3f);
        Add("demon_altar", "Autel démoniaque", CatalogCategory.Decor, ItemKind.Decor, demonHead, 220, 6, AF("cave_demon_head").FirstOrDefault(), "Un crâne cornu couronné de flammes : le cœur d'un sanctuaire infernal.", 12f);
        Add("cell", "Cellule", CatalogCategory.Decor, ItemKind.Decor, cellPrefab, 90, 3, PrisonSprite("cell_icon"), "Activez les captures : un intrus vaincu y est enfermé au lieu de mourir. Il nourrit le cristal chaque matin.", 0f);
        Add("gold", "Tas d'or", CatalogCategory.Decor, ItemKind.Decor, goldPrefab, 120, 3, traps.GetValueOrDefault("full_traps_297"), "Appât irrésistible, raconté dans toutes les tavernes.", 4f);
        Add("brazier", "Brasero spectral", CatalogCategory.Decor, ItemKind.Decor, brazier, 30, 1, AF("brazier").FirstOrDefault(), "Flammes d'âmes : éclaire loin, ambiance garantie.", 1.5f);
        Add("wall_torch", "Torche spectrale", CatalogCategory.Decor, ItemKind.Decor, wallTorch, 18, 1, AF("torch_wall").FirstOrDefault(), "Petite flamme verte vacillante.", 1f);
        Add("candelabra", "Candélabre", CatalogCategory.Decor, ItemKind.Decor, candelabra, 22, 2, AF("candelabra").FirstOrDefault(), "Bougies éternelles.", 1f);
        Add("bones", "Ossements", CatalogCategory.Decor, ItemKind.Decor, bones, 8, 1, ArtImporter.Single(ArtImporter.DungeonDir, "bones"), "Rappelle aux visiteurs le sort de leurs prédécesseurs.", 0.5f);
        Add("skull_pile", "Pile de crânes", CatalogCategory.Decor, ItemKind.Decor, skulls, 15, 2, ArtImporter.Single(ArtImporter.DungeonDir, "skull_pile"), "Effrayant : prestige des cryptes.", 1f);
        Add("coffin", "Cercueil", CatalogCategory.Decor, ItemKind.Decor, coffin, 20, 2, ArtImporter.Single(ArtImporter.DungeonDir, "coffin_wood_open"), "Un occupant s'y repose... pour l'instant.", 1.2f);
        Add("hanging", "Squelette enchaîné", CatalogCategory.Decor, ItemKind.Decor, hanging, 25, 2, ArtImporter.Single(ArtImporter.DungeonDir, "hanging_skeleton"), "Avertissement explicite.", 1.5f);
        Add("sarcophagus", "Sarcophage", CatalogCategory.Decor, ItemKind.Decor, sarcophagus, 45, 3, ArtImporter.Single(ArtImporter.DungeonDir, "sarcophagus"), "Tombeau d'un roi oublié.", 2.5f);
        Add("sarcophagus_open", "Sarcophage ouvert", CatalogCategory.Decor, ItemKind.Decor, sarcoOpen, 50, 3, ArtImporter.Single(ArtImporter.DungeonDir, "sarcophagus_open"), "Son occupant s'est levé...", 2.5f);
        Add("gargoyle", "Gargouille", CatalogCategory.Decor, ItemKind.Decor, gargoyle, 40, 3, ArtImporter.Single(ArtImporter.DungeonDir, "gargoyle"), "Veille sur les couloirs.", 2f);
        Add("statue_knight", "Statue de chevalier", CatalogCategory.Decor, ItemKind.Decor, statueKnight, 55, 3, ArtImporter.Single(ArtImporter.DungeonDir, "statue_knight"), "Hommage ironique aux chevaliers tombés.", 3f);
        Add("gold_pile", "Pile d'or", CatalogCategory.Decor, ItemKind.Decor, goldPile, 90, 3, ArtImporter.Single(ArtImporter.DungeonDir, "gold_pile"), "Appât : les cupides s'y précipitent.", 3f);
        Add("chest_royal", "Coffre royal", CatalogCategory.Decor, ItemKind.Decor, royal, 110, 4, ArtImporter.Single(ArtImporter.DungeonDir, "chest_royal"), "Le trésor dont parlent les tavernes : attire les héros. Un entretien coûteux pour de vraies richesses.", 5f); U(9);
        Add("fountain", "Fontaine sacrée", CatalogCategory.Decor, ItemKind.Decor, fountain, 150, 4, AF("fountain").FirstOrDefault(), "Eau miroitante. Grand prestige.", 6f);
        Add("statue_fire", "Statue démoniaque", CatalogCategory.Decor, ItemKind.Decor, statueFire, 180, 5, AF("statue_fire").FirstOrDefault(), "Flammes vertes éternelles. Prestige immense.", 7f);
        Add("skull", "Crâne de dragon", CatalogCategory.Decor, ItemKind.Decor, skullPrefab, 200, 4, traps.GetValueOrDefault("full_traps_190"), "Pièce maîtresse. Énorme prestige.", 8f);

        cat.themes.Add(new LayerTheme { id = "volcanic", displayName = "Roche volcanique", description = "La roche noire de l'île.", stoneTint = new Color(0.36f, 0.33f, 0.33f), floorTile = T("cave_floor") });
        cat.themes.Add(new LayerTheme { id = "lava", displayName = "Fournaise", description = "Les intrus brûlent lentement (1,5 PV/s). Vos monstres frappent 25 % plus fort.",
            stoneTint = new Color(0.72f, 0.36f, 0.28f), floorTile = T("lava_floor"), intruderDamagePerSecond = 1.5f, monsterDamageMult = 1.25f });
        cat.themes.Add(new LayerTheme { id = "aqua", displayName = "Sanctuaire englouti", description = "Sol inondé : les intrus avancent 35 % moins vite.",
            stoneTint = new Color(0.38f, 0.52f, 0.82f), floorTile = T("aqua_floor"), intruderSpeedMult = 0.65f });
        cat.themes.Add(new LayerTheme { id = "ice", displayName = "Caverne gelée", description = "Le froid engourdit : les intrus attaquent 50 % moins souvent.",
            stoneTint = new Color(0.72f, 0.86f, 1f), floorTile = T("ice"), intruderAttackCooldownMult = 1.5f });
        cat.themes.Add(new LayerTheme { id = "fungal", displayName = "Jardin fongique", description = "Spores toxiques (0,8 PV/s) et drain de mana +50 %.",
            stoneTint = new Color(0.5f, 0.62f, 0.45f), floorTile = T("fungal"), intruderDamagePerSecond = 0.8f, manaDrainMult = 1.5f });
        cat.themes.Add(new LayerTheme { id = "crystal", displayName = "Temple de cristal", description = "Le cristal amplifie l'absorption : drain de mana des intrus ×2,2.",
            stoneTint = new Color(0.7f, 0.56f, 0.95f), floorTile = T("crystal"), manaDrainMult = 2.2f });
        cat.themes.Add(new LayerTheme { id = "crypt", displayName = "Crypte hantée", description = "Ossements et murmures : les intrus ont 25 % de chances en plus de fuir. Drain de mana +30 %.",
            stoneTint = new Color(0.6f, 0.7f, 0.62f), floorTile = WallBuilder.Floor("crypt"), intruderFleeBonus = 0.25f, manaDrainMult = 1.3f });
        cat.themes.Add(new LayerTheme { id = "fortress", displayName = "Forteresse", description = "Pierre taillée : vos monstres frappent 15 % plus fort, les intrus attaquent 20 % moins souvent.",
            stoneTint = new Color(0.55f, 0.56f, 0.68f), floorTile = WallBuilder.Floor("fortress"), monsterDamageMult = 1.15f, intruderAttackCooldownMult = 1.2f });
        cat.themes.Add(new LayerTheme { id = "catacomb", displayName = "Catacombes", description = "Les morts veillent : vos morts-vivants (squelettes, zombies, liches, fantômes, vampires) frappent 30 % plus fort. Drain de mana +20 %.",
            stoneTint = new Color(0.55f, 0.42f, 0.36f), floorTile = WallBuilder.Floor("fortress"), floorTint = new Color(0.8f, 0.7f, 0.62f), undeadDamageMult = 1.3f, manaDrainMult = 1.2f });
        foreach (var th in cat.themes) th.wallTile = WallBuilder.Wall(th.id);
        // sols dessinés pour le projet (Tools~/make_dungeon.py) : ils remplacent les sols des packs
        foreach (var th in cat.themes)
        {
            var f = TerrainArt.DungeonFloor(th.id);
            if (f == null) continue;
            th.floorTile = f;
            th.floorTint = Color.white;
            th.stoneTint = Color.white;
        }

        // paire dessinée pour le projet (Tools~/make_creatures.py stairs) : trémie qui descend / volée qui monte vers la paroi
        cat.stairsDownSprite = TrapArt.Frames("stairs_down").FirstOrDefault() ?? AF("stairs_down").FirstOrDefault() ?? PL("stairs_down") ?? dungeon.GetValueOrDefault("dungeon_tiles_311");
        cat.stairsUpSprite = TrapArt.Frames("stairs_up").FirstOrDefault() ?? AF("stairs_up").FirstOrDefault() ?? PL("stairs_up") ?? dungeon.GetValueOrDefault("dungeon_tiles_82");
        cat.boatSprites.Clear();
        foreach (var b in new[] { "boat_small", "boat_medium", "boat_large" }) cat.boatSprites.Add(PL(b));

        // objets encombrants : on le signale dans la description
        foreach (var it in cat.items)
            if (PlacedObject.IsBlocking(it.id) && it.description != null && !it.description.Contains("Encombrant"))
                it.description += " Encombrant : bloque le passage (contre un mur ou dans un recoin).";

        if (isNew) AssetDatabase.CreateAsset(cat, CatalogPath);
        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        return $"catalogue : {cat.items.Count} éléments, {cat.themes.Count} thèmes";
    }

    public const string PixelLabDir = "Assets/Art/PixelLab";

    /// <summary>Sprites générés par PixelLab : 32 px = 1 case, bateaux à l'échelle de l'île (16 px = 1 case).</summary>
    public static Dictionary<string, Sprite> ImportPixelLab()
    {
        var result = new Dictionary<string, Sprite>();
        if (!AssetDatabase.IsValidFolder(PixelLabDir)) return result;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PixelLabDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            float ppu = name.StartsWith("boat") ? 16f : 32f;
            if (imp.textureType != TextureImporterType.Sprite || imp.spritePixelsPerUnit != ppu || imp.filterMode != FilterMode.Point)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = ppu;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) result[name] = s;
        }
        return result;
    }

    private static GameObject BuildSlime(GameObject goblinPrefab)
    {
        if (goblinPrefab == null) return null;
        string path = $"{PrefabDir}/Slime.prefab";
        if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/Prefabs", "Generated");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(goblinPrefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Slime";

        var slimeSprites = Sprites("Assets/imports/Slime1/With_shadow/Slime1_Idle_with_shadow.png");
        var sr = go.GetComponent<SpriteRenderer>();
        var first = slimeSprites.Values.OrderBy(s => s.name.Length).ThenBy(s => s.name).FirstOrDefault();
        if (first != null) sr.sprite = first;
        var anim = go.GetComponent<Animator>();
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/animations/Slime1.controller");
        if (anim != null) anim.runtimeAnimatorController = ctrl;

        var ai = go.GetComponent<GobelinAI>();
        ai.gobelinName = "Slime";
        ai.maxHealth = 60f;
        ai.attackDamage = 6f;
        ai.attackRange = 1.2f;
        ai.patrolSpeed = 0.25f;
        ai.chaseSpeed = 0.7f;
        ai.detectionRange = 4f;
        ai.healingPerSecond = 0.5f;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }
}
#endif
