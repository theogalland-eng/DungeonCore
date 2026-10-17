using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum CatalogCategory { Build, Monster, Trap, Decor, Layer, Treasure }
public enum ItemKind { Tool, Monster, Trap, Decor }

/// <summary>Un élément achetable du panneau de construction.</summary>
[Serializable]
public class CatalogItem
{
    public string id;
    public string displayName;
    [TextArea] public string description;
    public CatalogCategory category;
    public ItemKind kind;
    [Tooltip("Pour les outils : mode de construction")]
    public DungeonBuilder.BuildMode tool;
    public Sprite icon;
    public Color iconTint = Color.white;
    public GameObject prefab;
    public int cost;
    public int unlockLevel = 1;
    [Tooltip("Prestige apporté (décor) : augmente la renommée chaque jour")]
    public float prestige;
    [Tooltip("Mana prélevé chaque matin (monstres : voir MonsterUnit du prefab)")]
    public int upkeep;
}

[Serializable]
public class TierPrefabs
{
    public List<GameObject> prefabs = new List<GameObject>();
}

/// <summary>Thème d'un étage : apparence et effets sur les intrus.</summary>
[Serializable]
public class LayerTheme
{
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Color stoneTint = Color.white;
    public Color floorTint = Color.white;
    [Tooltip("Tile de sol spécifique (vide = sol par défaut teinté)")]
    public TileBase floorTile;
    [Tooltip("Mur auto-connecté de l'étage (AutoWallTile)")]
    public TileBase wallTile;
    [Header("Effets sur les intrus")]
    public float intruderDamagePerSecond;
    public float intruderSpeedMult = 1f;
    public float intruderAttackCooldownMult = 1f;
    public float manaDrainMult = 1f;
    [Tooltip("Bonus aux chances de fuite des intrus (0,25 = +25 %)")]
    public float intruderFleeBonus;
    [Header("Effets sur les monstres")]
    public float monsterDamageMult = 1f;
    [Tooltip("Bonus de dégâts des morts-vivants (squelettes, zombies, liches, fantômes, vampires)")]
    public float undeadDamageMult = 1f;
    [Header("Ambiance")]
    public Color ambientLight = Color.clear;
}

/// <summary>
/// Catalogue du jeu (Resources/DungeonCatalog) : monstres, pièges, décors, thèmes d'étage.
/// Généré par le menu DungeonCore/Générer le catalogue, modifiable dans l'Inspector.
/// </summary>
[CreateAssetMenu(menuName = "DungeonCore/Catalogue", fileName = "DungeonCatalog")]
public class DungeonCatalog : ScriptableObject
{
    public List<CatalogItem> items = new List<CatalogItem>();
    public List<LayerTheme> themes = new List<LayerTheme>();
    [Tooltip("Prefab d'intrus par palier (0 = pêcheur, 1 = villageois, 2 = aventurier, 3 = chevalier, 4 = héros)")]
    public List<GameObject> adventurerPrefabs = new List<GameObject>();
    [Tooltip("Rôles des groupes d'aventuriers : archer, mage, soigneuse")]
    public GameObject archerPrefab, magePrefab, healerPrefab;
    /// <summary>Apparence du héros du mode Aventurier par palier d'armure (1 à 9) : contrôleurs d'animation.</summary>
    /// <summary>Portrait de départ (palier 1, de face) de chaque classe : aperçu de la personnalisation.</summary>
    public Sprite[] lookPreview = new Sprite[4];
    public List<RuntimeAnimatorController> lookWarrior = new List<RuntimeAnimatorController>(), lookArcher = new List<RuntimeAnimatorController>(),
        lookMage = new List<RuntimeAnimatorController>(), lookHealer = new List<RuntimeAnimatorController>();
    [Tooltip("Animaux sauvages de l'île")]
    public List<GameObject> wildlifePrefabs = new List<GameObject>();
    [Tooltip("Colons de la colonie humaine")]
    public List<GameObject> settlerPrefabs = new List<GameObject>();
    [Tooltip("Variantes d'apparence par palier d'intrus (épéistes niv. 1 à 9)")]
    public List<TierPrefabs> tierVariants = new List<TierPrefabs>();
    [Tooltip("Bateaux des expéditions : 0 petit (pêcheurs), 1 moyen (aventuriers), 2 grand (héros)")]
    public List<Sprite> boatSprites = new List<Sprite>();
    public Sprite stairsDownSprite;
    public Sprite stairsUpSprite;

    private static DungeonCatalog cached;

    public static DungeonCatalog Get()
    {
        if (cached == null) cached = Resources.Load<DungeonCatalog>("DungeonCatalog");
        if (cached == null) cached = CreateFallback();
        return cached;
    }

    public CatalogItem Find(string id) => items.Find(i => i.id == id);

    public IEnumerable<CatalogItem> InCategory(CatalogCategory c)
    {
        foreach (var i in items) if (i.category == c) yield return i;
    }

    /// <summary>Catalogue minimal si l'asset n'a pas été généré.</summary>
    private static DungeonCatalog CreateFallback()
    {
        var c = CreateInstance<DungeonCatalog>();
        c.items.Add(new CatalogItem { id = "dig", displayName = "Creuser", category = CatalogCategory.Build, kind = ItemKind.Tool, tool = DungeonBuilder.BuildMode.Digging });
        c.items.Add(new CatalogItem { id = "fill", displayName = "Remblayer", category = CatalogCategory.Build, kind = ItemKind.Tool, tool = DungeonBuilder.BuildMode.Filling });
        c.items.Add(new CatalogItem { id = "erase", displayName = "Gomme", category = CatalogCategory.Build, kind = ItemKind.Tool, tool = DungeonBuilder.BuildMode.Erasing });
        var spawner = FindFirstObjectByType<MonsterSpawner>();
        if (spawner != null && spawner.gobelinPrefab != null)
            c.items.Add(new CatalogItem { id = "goblin", displayName = "Gobelin", category = CatalogCategory.Monster, kind = ItemKind.Monster, prefab = spawner.gobelinPrefab, cost = 50 });
        var builder = FindFirstObjectByType<DungeonBuilder>();
        if (builder != null && builder.trapPrefab != null)
            c.items.Add(new CatalogItem { id = "blade", displayName = "Roue tranchante", category = CatalogCategory.Trap, kind = ItemKind.Trap, prefab = builder.trapPrefab, cost = 20 });
        c.themes.Add(new LayerTheme { id = "volcanic", displayName = "Roche volcanique" });
        return c;
    }
}
