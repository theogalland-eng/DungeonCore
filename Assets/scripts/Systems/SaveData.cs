using System;
using System.Collections.Generic;

/// <summary>
/// Instantané complet d'une partie (JSON). Les cases sont stockées à plat : x0, y0, x1, y1...
/// Les intrus présents au moment de la sauvegarde ne sont pas conservés (ils « repartent »).
/// </summary>
[Serializable]
public class SaveData
{
    public int version = 1;
    public string savedAt;          // date lisible
    public long savedTicks;
    public string mode = "campaign";          // "campaign" (Noyau) ou "crawler" (Aventurier)
    public CrawlerSave crawler;
    public float playSeconds;

    // Temps
    public int day = 1;
    public float dayTimer;
    public int statKills, statFled, statExpeditions;
    public int statHeroKills, statEvolutions, objectiveIndex;

    // Noyau
    public float mana;
    public int level = 1, xp, xpForNext = 100;
    public int coreX, coreY;
    public float coreHealth;

    // Réputation & expéditions
    public float threat, renown;
    public bool discovered;
    public int lostExpeditions;
    public int nextExpeditionDay = 3;

    // Guilde des Aventuriers & difficulté
    public int difficulty = -1;          // -1 : ancienne sauvegarde (Facile)
    public int guildStage;
    public int guildRank = -1, guildReportedRank = 3;
    public int guildAssessDay = -1, guildAssessedDay = -1;
    public float guildGreed, guildHostility;
    public int guildDeaths;
    public string guildReport;
    /// <summary>Plan du donjon connu de la Guilde (x, y à plat) et emplacement supposé du cristal.</summary>
    public List<int> guildMap = new List<int>();
    public bool guildCoreKnown;
    public int guildCoreX, guildCoreY;
    public int islandEvent, islandEventEnds = -1;
    public List<NemesisRecord> nemeses = new List<NemesisRecord>();
    public bool captureEnabled;
    public List<int> trails = new List<int>();
    public List<string> talents = new List<string>();   // talents du Noyau acquis
    public List<string> chronicle = new List<string>();  // « jour|texte »   // sentiers nés du passage : x, y, usure, pavé
    public List<PrisonerSave> prisoners = new List<PrisonerSave>();

    // Donjon
    public List<LayerSave> layers = new List<LayerSave>();
    public List<int> territory = new List<int>();
    public List<int> placedWalls = new List<int>();
    public List<ObjectSave> objects = new List<ObjectSave>();
    public List<MonsterSave> monsters = new List<MonsterSave>();

    // Colonie
    public SettlementSave colony = new SettlementSave();

    // Histoire, butin, relations, raids
    public List<string> storyFlags = new List<string>();
    public List<LootStack> loot = new List<LootStack>();
    public int lootCollected;
    public float relation;
    public bool truce;
    public int truceSinceDay = -1;
    public int punitiveDay = -1;
    public int punitiveBonus;
    public int raidsDone;

    // Vue
    public float camX, camY, camSize = 6f;
    public int view;
}

[Serializable]
public class LayerSave
{
    public bool unlocked, connected;
    public int themeIndex;
    public int stairsX, stairsY;
}

[Serializable]
public class ObjectSave
{
    public string id;
    public int x, y;
    public string data;   // trophée : objet exposé
}

[Serializable]
public class MonsterSave
{
    public string family;
    public int stage = 1;
    public string name;
    public float x, y, ax, ay;
    public float hp;
    public int xp;
    public int behavior;
    public bool guardian;
    public string guardianName;
    // vie du monstre (traits = -1 : ancienne sauvegarde, tirés au hasard)
    public int traits = -1;
    public float hunger = 20f, energy = 90f, mood = 60f;
    public string variant;   // thème de la variante d'étage (vide : aucune)
    public string role;      // voie de rôle (archer, mage...) ; vide : voie classique
}

[Serializable]
public class SettlementSave
{
    public int stage;
    public float prosperity, morale = 60f;
    public int wood, stone, food = 20;
    public int expeditionsSeen;
    public int population;
    public List<BuildingSave> buildings = new List<BuildingSave>();
    public List<float> felledTrees = new List<float>();
    public List<int> minedRocks = new List<int>();
    public List<SettlerSave> settlers = new List<SettlerSave>();
    public bool outpost;
}

[Serializable]
public class SettlerSave
{
    public string name, profession, prefab;
    public int arrivedDay;
}

[Serializable]
public class BuildingSave
{
    public string id;
    public float progress;
    public bool paid;
}

[Serializable]
public class LootStack
{
    public string id;
    public int count;
}

/// <summary>Partie du mode Aventurier : le héros, son équipement, son groupe, sa progression, l'Esprit du Noyau.</summary>
[Serializable]
public class CrawlerSave
{
    public int role, seed;
    public string heroName, hair, skin, outfit;
    public int gold, potions, rations, recallStones, kills, weaponLvl, armorLvl, rank;
    public int deepest, reportedDeepest, trip;
    public float satiety = 100f;
    public List<GearSave> bag = new List<GearSave>();
    public List<GearSave> equipped = new List<GearSave>();
    public List<string> skills = new List<string>();
    public List<ContractSave> offered = new List<ContractSave>();
    public List<ContractSave> active = new List<ContractSave>();
    public List<CompanionSave> companions = new List<CompanionSave>();
    public List<string> lootedChests = new List<string>();
    public float overlordEssence = 60f, overlordSpent;
    public int overlordRooms, overlordEvolved, overlordReinforced, overlordTraps;
}

[Serializable]
public class GearSave
{
    public string name;
    public int slot, rarity, level;
    public List<int> statKeys = new List<int>();
    public List<float> statValues = new List<float>();
}

[Serializable]
public class ContractSave
{
    public int kind, goal, progress, minFloor, gold, reward;
    public string family;
}

[Serializable]
public class CompanionSave
{
    public string name;
    public int role, rank;
    public bool female;
    public float health = 1f;
}

/// <summary>Résumé affiché dans la liste des emplacements (lu sans charger toute la partie).</summary>
[Serializable]
public class SaveSummary
{
    public string slot;
    public bool exists;
    public int day, level;
    public string savedAt;
    public long savedTicks;
    public string colony;
    public bool adventure;      // partie du mode Aventurier
    public string hero;         // « Guerrier · étage 4 · 230 or »
}
