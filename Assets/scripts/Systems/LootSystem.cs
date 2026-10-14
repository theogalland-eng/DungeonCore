using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Objet que peuvent porter les intrus.</summary>
public class LootDef
{
    public string id, name, desc;
    public int rarity;          // 0 commun … 4 légendaire
    public Sprite icon;
    public float Prestige => LootSystem.PrestigeByRarity[rarity];
    public int Mana => LootSystem.ManaByRarity[rarity];
}

/// <summary>
/// Butin des intrus tués : l'équipement tombe au sol, le Noyau l'absorbe dans son trésor.
/// Chaque objet peut être EXPOSÉ en trophée (prestige : renommée et mana ambiant chaque jour)
/// ou ABSORBÉ pour un gain de mana immédiat.
/// </summary>
public class LootSystem : MonoBehaviour
{
    public static LootSystem Instance { get; private set; }

    public const string TrophyId = "trophy";
    private const string TrophyPrefix = "trophy:";

    public static readonly string[] RarityNames = { "Commun", "Peu commun", "Rare", "Épique", "Légendaire" };
    public static readonly Color[] RarityColors =
    {
        new Color(0.8f, 0.78f, 0.72f), new Color(0.5f, 0.9f, 0.5f), new Color(0.45f, 0.7f, 1f),
        new Color(0.78f, 0.5f, 1f), new Color(1f, 0.75f, 0.3f),
    };
    public static readonly float[] PrestigeByRarity = { 1f, 2f, 4f, 7f, 12f };
    public static readonly int[] ManaByRarity = { 20, 40, 80, 150, 300 };
    [Tooltip("Chance de butin par palier d'intrus (pêcheur … héros)")]
    public float[] dropChance = { 0.45f, 0.55f, 0.7f, 0.85f, 1f };

    private static readonly (string id, string name, int rarity, string desc)[] Defs =
    {
        ("copper_coins", "Pièces de cuivre", 0, "La paie d'un pêcheur."),
        ("pouch", "Bourse de cuir", 0, "Quelques économies, cousues avec soin."),
        ("sack", "Sac de provisions", 0, "Pain rassis et poisson séché."),
        ("iron_key", "Clé rouillée", 0, "Elle n'ouvre plus rien depuis longtemps."),
        ("silver_coins", "Pièces d'argent", 1, "Frappées au sceau du Royaume."),
        ("copper_bar", "Lingot de cuivre", 1, "Un lingot brut du continent."),
        ("backpack", "Sac d'aventurier", 1, "Cordes, torches et cartes griffonnées."),
        ("silver_ring", "Anneau d'argent", 1, "Gravé d'un prénom oublié."),
        ("gold_coins", "Pièces d'or", 2, "La prime d'une guilde."),
        ("merc_sword", "Épée de mercenaire", 2, "Ébréchée par cent combats."),
        ("mage_staff", "Bâton de mage", 2, "Il crépite encore d'un sort inachevé."),
        ("mana_potion", "Potion de mana", 2, "Un concentré de ce dont vous êtes fait."),
        ("silver_bar", "Lingot d'argent", 2, "Du métal pur, froid au toucher."),
        ("bronze_crown", "Couronne de bronze", 2, "Celle d'un petit seigneur ambitieux."),
        ("emerald_blade", "Lame d'émeraude", 3, "Forgée par les elfes du continent."),
        ("silver_crown", "Diadème d'argent", 3, "Un bijou de cour, serti de cristaux."),
        ("ruby_ring", "Chevalière de rubis", 3, "Le sceau d'une maison noble."),
        ("bravery_medal", "Médaille de bravoure", 3, "Décernée aux chevaliers les plus hardis."),
        ("silver_chalice", "Calice d'argent", 3, "Béni par les prêtres du Royaume."),
        ("emerald_amulet", "Amulette d'émeraude", 3, "Un talisman contre les morts-vivants. Raté."),
        ("gold_bar", "Lingot d'or", 3, "De quoi acheter un village entier."),
        ("blessed_sword", "Épée bénie", 4, "L'arme d'un héros, bénie par la Déesse."),
        ("hero_crown", "Couronne du héros", 4, "Promise au vainqueur du Noyau."),
        ("sun_shield", "Bouclier solaire", 4, "Il brille même dans les ténèbres."),
        ("cursed_scepter", "Sceptre maudit", 4, "Il murmure votre nom."),
        ("priestess_wand", "Baguette de prêtresse", 4, "Elle guérissait les héros. Plus maintenant."),
        ("tourney_cup", "Coupe du tournoi", 4, "Le trophée d'un champion... désormais le vôtre."),
    };

    private readonly Dictionary<string, LootDef> defs = new Dictionary<string, LootDef>();
    private readonly Dictionary<string, int> inventory = new Dictionary<string, int>();
    private Sprite tableSprite;

    public int TotalCollected { get; private set; }
    public event Action OnChanged;

    public IEnumerable<LootDef> AllDefs => defs.Values;

    private void Awake()
    {
        Instance = this;
        foreach (var d in Defs)
        {
            var sp = Resources.Load<Sprite>("Loot/loot_" + d.id);
            defs[d.id] = new LootDef { id = d.id, name = d.name, rarity = d.rarity, desc = d.desc, icon = sp };
        }
        tableSprite = Resources.Load<Sprite>("Loot/trophy_table");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public LootDef Def(string id) => id != null && defs.TryGetValue(id, out var d) ? d : null;
    public int CountOf(string id) => id != null && inventory.TryGetValue(id, out int n) ? n : 0;

    /// <summary>Objets du trésor (au moins un exemplaire), du plus rare au plus commun.</summary>
    public List<LootDef> Owned()
    {
        var list = new List<LootDef>();
        foreach (var kv in inventory) if (kv.Value > 0 && defs.ContainsKey(kv.Key)) list.Add(defs[kv.Key]);
        list.Sort((a, b) => b.rarity != a.rarity ? b.rarity.CompareTo(a.rarity) : string.CompareOrdinal(a.name, b.name));
        return list;
    }

    public int OwnedCount { get { int n = 0; foreach (var v in inventory.Values) n += v; return n; } }

    public void Add(string id, int n = 1, bool isNew = true)
    {
        if (!defs.ContainsKey(id ?? "")) return;
        inventory[id] = CountOf(id) + n;
        if (isNew) TotalCollected += n;
        OnChanged?.Invoke();
    }

    private bool Take(string id)
    {
        if (CountOf(id) <= 0) return false;
        inventory[id]--;
        OnChanged?.Invoke();
        return true;
    }

    // ─────────────────────────────────────────────
    // BUTIN
    // ─────────────────────────────────────────────

    /// <summary>Un intrus meurt : il laisse peut-être tomber son équipement.</summary>
    public void OnIntruderKilled(AdventurerAI adv)
    {
        if (adv == null) return;
        int tier = Mathf.Clamp(adv.TierIndex, 0, 4);
        if (UnityEngine.Random.value > dropChance[tier]) return;
        // rareté : le plus souvent celle du palier, parfois en dessous
        float r = UnityEngine.Random.value;
        int rarity = r < 0.5f ? tier : r < 0.85f ? tier - 1 : tier - 2;
        if (tier == 4 && UnityEngine.Random.value < 0.6f) rarity = 4;   // un héros porte presque toujours une relique
        rarity = Mathf.Clamp(rarity, 0, 4);
        var pool = new List<LootDef>();
        foreach (var d in defs.Values) if (d.rarity == rarity) pool.Add(d);
        if (pool.Count == 0) return;
        var def = pool[UnityEngine.Random.Range(0, pool.Count)];
        LootPickup.Spawn(def, adv.transform.position);
    }

    /// <summary>Appelé par le butin au sol quand le Noyau l'absorbe.</summary>
    public void Collect(LootDef def, Vector3 at)
    {
        Add(def.id);
        Sfx.Play("coin", at);
        FloatingText.Spawn(at + Vector3.up * 0.5f, def.name, RarityColors[def.rarity], 1.1f);
        Fx.Play("shine", at + Vector3.up * 0.2f, 1f + def.rarity * 0.2f, 12f, 17, RarityColors[def.rarity]);
        GameHUD.Toast($"Butin : <color=#{ColorUtility.ToHtmlStringRGB(RarityColors[def.rarity])}>{def.name}</color> ({RarityNames[def.rarity].ToLowerInvariant()}) — panneau Trésor", GameHUD.GoldColor);
        StoryManager.Instance?.PlayOnce("first_loot",
            new StoryManager.Line(StoryManager.SelfName, "Il transportait quelque chose... Mes couloirs l'ont avalé."),
            new StoryManager.Line(StoryManager.SystemName, "Onglet TRÉSOR : exposez le butin en TROPHÉE (prestige : renommée et mana chaque jour) ou ABSORBEZ-le pour du mana."));
    }

    /// <summary>Dissout un objet du trésor en mana.</summary>
    /// <summary>Un coffre pillé offre un objet du trésor (le moins précieux d'abord) ; null si le trésor est vide.</summary>
    public LootDef TakeForChest()
    {
        LootDef best = null;
        foreach (var kv in inventory)
        {
            if (kv.Value <= 0 || !defs.TryGetValue(kv.Key, out var d)) continue;
            if (best == null || d.rarity < best.rarity) best = d;
        }
        if (best == null) return null;
        inventory[best.id]--;
        OnChanged?.Invoke();
        return best;
    }

    public bool Absorb(string id)
    {
        var def = Def(id);
        if (def == null || !Take(id)) return false;
        ManaSystem.Instance?.Add(def.Mana);
        GameHUD.Toast($"{def.name} absorbé : +{def.Mana} mana", GameHUD.ManaColor);
        return true;
    }

    // ─────────────────────────────────────────────
    // TROPHÉES
    // ─────────────────────────────────────────────

    public static bool IsTrophyItem(CatalogItem item) => item != null && item.id != null && item.id.StartsWith(TrophyPrefix);
    public static string TrophyLootId(CatalogItem item) => item.id.Substring(TrophyPrefix.Length);

    /// <summary>Élément « à placer » pour le mode de pose du DungeonBuilder.</summary>
    public CatalogItem TrophyItem(string lootId)
    {
        var def = Def(lootId);
        if (def == null) return null;
        return new CatalogItem
        {
            id = TrophyPrefix + lootId, displayName = "Trophée : " + def.name, kind = ItemKind.Decor, category = CatalogCategory.Treasure,
            icon = def.icon, cost = 0, prestige = def.Prestige,
            description = $"Exposé sur une table, il rapporte {def.Prestige:0} de prestige (renommée et mana ambiant).",
        };
    }

    public void PlaceTrophy(string lootId, Vector3Int cell)
    {
        if (!Take(lootId)) return;
        var po = CreateTrophy(lootId, cell);
        if (po != null) FloatingText.Spawn(po.transform.position + Vector3.up * 0.6f, $"+{po.prestige:0} prestige", GameHUD.GoldColor, 1f);
        StoryManager.Instance?.PlayOnce("first_trophy",
            new StoryManager.Line(StoryManager.SelfName, "Qu'ils voient ce qu'il reste de ceux qui sont venus avant eux."));
    }

    /// <summary>Crée un présentoir (table + objet) sur une case du donjon.</summary>
    public PlacedObject CreateTrophy(string lootId, Vector3Int cell)
    {
        var def = Def(lootId);
        var builder = DungeonBuilder.Instance;
        if (def == null || builder == null) return null;
        Vector3 pos = builder.floorTilemap.GetCellCenterWorld(cell);
        var go = new GameObject("Trophée : " + def.name);
        go.transform.SetParent(builder.ObjectsParent, false);
        go.transform.position = pos;
        var table = go.AddComponent<SpriteRenderer>();
        table.sprite = tableSprite != null ? tableSprite : SpriteUtil.Square;
        if (tableSprite == null) { table.color = new Color(0.45f, 0.3f, 0.2f); go.transform.localScale = new Vector3(0.8f, 0.4f, 1f); }
        table.sortingOrder = 3;
        table.spriteSortPoint = SpriteSortPoint.Pivot;

        var item = new GameObject("Objet");
        item.transform.SetParent(go.transform, false);
        item.transform.localPosition = new Vector3(0f, 0.42f, 0f);
        var isr = item.AddComponent<SpriteRenderer>();
        isr.sprite = def.icon;
        isr.sortingOrder = 4;
        var bob = item.AddComponent<TrophyShine>();
        bob.rarity = def.rarity;

        if (def.rarity >= 3)
        {
            var l = item.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = RarityColors[def.rarity];
            l.intensity = 0.6f;
            l.pointLightOuterRadius = 1.6f;
        }

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.9f, 0.9f);

        var po = go.AddComponent<PlacedObject>();
        po.kind = ItemKind.Decor;
        po.itemId = TrophyId;
        po.data = lootId;
        po.prestige = def.Prestige;
        return po;
    }

    // ─────────────────────────────────────────────
    // SAUVEGARDE
    // ─────────────────────────────────────────────

    public void Capture(SaveData d)
    {
        foreach (var kv in inventory) if (kv.Value > 0) d.loot.Add(new LootStack { id = kv.Key, count = kv.Value });
        d.lootCollected = TotalCollected;
    }

    public void Restore(SaveData d)
    {
        inventory.Clear();
        foreach (var s in d.loot) if (defs.ContainsKey(s.id)) inventory[s.id] = s.count;
        TotalCollected = d.lootCollected;
        OnChanged?.Invoke();
    }
}
