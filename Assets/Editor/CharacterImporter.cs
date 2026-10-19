#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Importe les personnages des packs craftpix (Assets/imports) :
///   découpe des planches, clips d'animation (4 directions), Animator Controller, prefab.
/// Menu : DungeonCore/4. Importer les personnages.
///
/// Pour ajouter un personnage : ajouter une entrée dans Specs() (fichiers des états + ordre des directions).
/// </summary>
public static class CharacterImporter
{
    public const string AnimDir = "Assets/Animations/Generated";
    public const string PrefabDir = "Assets/Prefabs/Characters";

    public enum Kind { Monster, Adventurer, Wildlife, Settler }

    public class Spec
    {
        public string id, displayName;
        public Kind kind;
        public int frame = 64;
        /// <summary>Hauteur d'image si différente de la largeur (0 = carrée).</summary>
        public int frameH;
        public float ppu = 32f;
        /// <summary>Ordre des lignes de la planche : D=bas, U=haut, L=gauche, R=droite. Vide = pas de directions.</summary>
        public string dirOrder = "DURL";
        /// <summary>État → nom de fichier (recherché dans Assets/imports, sans tenir compte de la casse).</summary>
        public Dictionary<string, string> files = new Dictionary<string, string>();
        /// <summary>Pour les planches sans directions : état → ligne.</summary>
        public Dictionary<string, int> rows;
        public string rowFile;
        public System.Action<GameObject> configure;
        /// <summary>Planches 8 directions (Tools~/make_8dir.py) : lignes S, SE, E, NE, N, NW, W, SW.</summary>
        public bool dirs8;
    }

    static readonly string[] States = { "Idle", "Walk", "Run", "Attack", "WalkAttack", "RunAttack", "Hurt", "Death", "Roll", "Crouch", "Use" };
    static readonly Dictionary<string, float> Fps = new Dictionary<string, float>
        { { "Idle", 8 }, { "Walk", 10 }, { "Run", 12 }, { "Attack", 14 }, { "WalkAttack", 12 }, { "RunAttack", 14 },
          { "Hurt", 12 }, { "Death", 10 }, { "Roll", 14 }, { "Crouch", 6 }, { "Use", 8 } };
    static readonly string[] Dir8Names = { "S", "SE", "E", "NE", "N", "NW", "W", "SW" };
    static readonly Vector2[] Dir8Vectors =
    {
        new Vector2(0, -1), new Vector2(0.7071f, -0.7071f), new Vector2(1, 0), new Vector2(0.7071f, 0.7071f),
        new Vector2(0, 1), new Vector2(-0.7071f, 0.7071f), new Vector2(-1, 0), new Vector2(-0.7071f, -0.7071f),
    };
    public const string Dir8Root = "Assets/Art/Characters8";

    /// <summary>Planches 8 directions d'un personnage, si Tools~/make_8dir.py les a produites.</summary>
    static Dictionary<string, string> Files8(string id)
    {
        var d = new Dictionary<string, string>();
        foreach (var st in States)
        {
            string p = $"{Dir8Root}/{id}/{id}_{st}.png";
            if (File.Exists(p)) d[st] = p;
        }
        return d.ContainsKey("Idle") ? d : null;
    }

    static Dictionary<string, string> Files(params string[] kv)
    {
        var d = new Dictionary<string, string>();
        for (int i = 0; i < kv.Length; i += 2) d[kv[i]] = kv[i + 1];
        return d;
    }

    static void Gob(GameObject go, string name, float hp, float dmg, float range, float patrol, float chase, float det = 5f, float cd = 1f)
    {
        var ai = go.GetComponent<GobelinAI>();
        ai.gobelinName = name; ai.maxHealth = hp; ai.currentHealth = hp; ai.attackDamage = dmg; ai.attackRange = range;
        ai.patrolSpeed = patrol; ai.chaseSpeed = chase; ai.detectionRange = det; ai.attackCooldown = cd; ai.fieldOfViewAngle = 360f;
    }

    /// <summary>
    /// Famille de monstres : 3 stades d'évolution (les planches 1-2-3 d'un même pack).
    /// Stats du stade 1 ; les stades suivants sont multipliés (PV ×1,7 / ×2,7, dégâts ×1,5 / ×2,2).
    /// </summary>
    public class Family
    {
        public string id, pack, folder;            // folder : préfixe du dossier/fichier ("Vampires", "Gnoll"...)
        public string[] names;                     // noms des 3 stades
        public float hp, dmg, range, chase, cd = 1f;
        public int cost, level, upkeep;
        public string desc;
        public string dirOrder = "DULR";
        public int frame = 64;
        public System.Func<int, string, string> file;   // (stade, état) → fichier ou fragment de chemin (null : planches 8 directions seulement)
        public System.Action<GameObject, int> traits;   // pouvoirs par stade
    }

    public static readonly string[] StdStates = { "Idle", "Walk", "Run", "Attack", "Hurt", "Death" };

    public static List<Family> Families()
    {
        System.Func<int, string, string> Std(string prefix) => (n, st) => $"{prefix}{n}_{st}_with_shadow.png";
        var f = new List<Family>
        {
            new Family { id = "rat", folder = "Rat", names = new[] { "Rat géant", "Rat pestiféré", "Rat-roi" },
                hp = 45, dmg = 5, range = 1.0f, chase = 2.0f, cd = 0.8f, cost = 20, level = 1, upkeep = 3, frame = 128, file = Std("Rat"),
                desc = "Rapide et bon marché : idéal en meute." },
            new Family { id = "slime", folder = "Slime", names = new[] { "Slime", "Slime toxique", "Slime de magma" },
                hp = 60, dmg = 6, range = 1.1f, chase = 0.8f, cost = 30, level = 1, upkeep = 4, file = Std("Slime"),
                desc = "Lent et mou, il ralentit les intrus qu'il touche.",
                traits = (go, s) => { var t = T(go); t.slowMult = 0.7f; t.slowSeconds = 1.2f; if (s == 3) { t.burnDps = 4; t.burnSeconds = 3; } } },
            // le gobelin (petit, vif, fragile) passe avant l'orc (grand guerrier robuste)
            new Family { id = "goblin", folder = "Goblin", names = new[] { "Gobelin", "Gobelin pillard", "Chef gobelin" },
                hp = 55, dmg = 7, range = 1.1f, chase = 1.8f, cd = 0.75f, cost = 30, level = 2, upkeep = 4,
                file = (n, st) => n == 1 ? $"Goblin1/With_shadow/{st}0_with_shadow.png" : $"Goblin{n}/With_shadow/{st}_with_shadow.png",
                desc = "Vif et sournois, mais fragile : frappe vite." },
            new Family { id = "orc", folder = "orc", names = new[] { "Orc", "Orc guerrier", "Seigneur orc" },
                hp = 120, dmg = 13, range = 1.2f, chase = 1.4f, cost = 60, level = 5, upkeep = 7,
                file = (n, st) => $"orc{n}_{st.ToLower()}_with_shadow.png", desc = "Guerrier robuste qui donne l'alerte." },
            new Family { id = "skeleton", folder = "Skeleton", names = new[] { "Squelette", "Squelette guerrier", "Chevalier squelette" },
                hp = 80, dmg = 11, range = 1.2f, chase = 1.3f, cost = 45, level = 3, upkeep = 6, file = Std("Skeleton"),
                desc = "Soldat mort-vivant infatigable." },
            new Family { id = "mushroom", folder = "Mushroom", names = new[] { "Champignon", "Myconide", "Myconide ancien" },
                hp = 90, dmg = 7, range = 1.2f, chase = 0.9f, cost = 50, level = 4, upkeep = 6, file = Std("Mushroom"),
                desc = "Ses spores engourdissent : les intrus touchés ralentissent fortement.",
                traits = (go, s) => { var t = T(go); t.slowMult = 0.5f; t.slowSeconds = 2f + s; } },
            new Family { id = "zombie", folder = "Zombie", names = new[] { "Zombie", "Goule", "Abomination" },
                hp = 150, dmg = 12, range = 1.1f, chase = 0.8f, cd = 1.3f, cost = 60, level = 6, upkeep = 8, file = Std("Zombie"),
                desc = "Très résistant mais lent. Un mur de chair.",
                traits = (go, s) => { if (s >= 2) T(go).lifesteal = 0.15f; } },
            new Family { id = "gnoll", folder = "Gnoll", names = new[] { "Gnoll", "Gnoll traqueur", "Gnoll alpha" },
                hp = 110, dmg = 14, range = 1.3f, chase = 1.9f, cost = 75, level = 9, upkeep = 9,
                file = (n, st) => n == 1 && st == "Death" ? "Gnoll_Death_with_shadow.png" : $"Gnoll{n}_{st}_with_shadow.png",
                desc = "Chasseur rapide qui ne lâche pas sa proie." },
            new Family { id = "imp", folder = "Imp", names = new[] { "Diablotin", "Diablotin de feu", "Diablotin infernal" },
                hp = 60, dmg = 11, range = 3.0f, chase = 1.6f, cost = 70, level = 8, upkeep = 9,
                file = (n, st) => n == 2 && st == "Hurt" ? "Imp2_Hurt__with_shadow.png" : $"Imp{n}_{st}_with_shadow.png",
                desc = "Lance des boules de feu à distance (brûlure).",
                traits = (go, s) => { var t = T(go); t.projectileColor = new Color(1f, 0.55f, 0.2f); t.burnDps = 2 + s * 2; t.burnSeconds = 3; } },
            new Family { id = "lizard", folder = "Lizardman", names = new[] { "Homme-lézard", "Guerrier lézard", "Champion lézard" },
                hp = 140, dmg = 16, range = 1.4f, chase = 1.5f, cost = 90, level = 10, upkeep = 11, file = Std("Lizardman"),
                desc = "Combattant aguerri, à l'aise dans les grottes inondées." },
            new Family { id = "ghost", folder = "Ghost", names = new[] { "Spectre", "Fantôme", "Banshee" },
                hp = 90, dmg = 14, range = 1.3f, chase = 1.4f, cost = 90, level = 11, upkeep = 11, file = Std("Ghost"),
                desc = "Son toucher glacé ralentit et terrifie.",
                traits = (go, s) => { var t = T(go); t.slowMult = 0.65f; t.slowSeconds = 1.5f; go.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 0.85f); } },
            new Family { id = "lich", folder = "Lich", names = new[] { "Liche apprentie", "Liche", "Archiliche" },
                hp = 100, dmg = 8, range = 3.2f, chase = 1.0f, cd = 1.4f, cost = 140, level = 14, upkeep = 17, file = Std("Lich"),
                desc = "Petit sort à distance ; invoque 1, 2 puis 3 squelettes au combat.",
                traits = (go, s) => { var t = T(go); t.projectileColor = new Color(0.5f, 1f, 0.6f); t.summonCount = s; t.summonPrefab = Prefab("skeleton_1"); } },
            new Family { id = "vampire", folder = "Vampires", names = new[] { "Vampire", "Seigneur vampire", "Comte vampire" },
                hp = 160, dmg = 20, range = 1.2f, chase = 1.6f, cost = 150, level = 16, upkeep = 18, file = Std("Vampires"),
                desc = "Se soigne en buvant le sang des intrus (vol de vie).",
                traits = (go, s) => T(go).lifesteal = 0.3f + 0.1f * s },
            new Family { id = "demon", folder = "Demon", names = new[] { "Démon mineur", "Démon", "Archidémon" },
                hp = 320, dmg = 32, range = 1.6f, chase = 1.2f, cd = 1.3f, cost = 260, level = 18, upkeep = 30, frame = 128, file = Std("Demon"),
                desc = "Colosse infernal : ses coups embrasent les intrus.",
                traits = (go, s) => { var t = T(go); t.burnDps = 4 * s; t.burnSeconds = 3; } },
            new Family { id = "golem", folder = "Golem", names = new[] { "Golem d'argile", "Golem de pierre", "Golem de cristal" },
                hp = 210, dmg = 13, range = 1.3f, chase = 0.85f, cd = 1.5f, cost = 110, level = 12, upkeep = 12, frame = 128,
                file = (n, st) => $"Golem{n}/With_shadow/Golem{n}_{st}_with_shadow.png",
                desc = "Colosse de roche, lent mais presque indestructible : ses coups sonnent les intrus.",
                traits = (go, s) => { var t = T(go); t.slowMult = 0.25f; t.slowSeconds = 0.6f + 0.3f * s; } },
            // ── familles dessinées pour le projet (Tools~/make_creatures.py)
            new Family { id = "spider", folder = "Spider", names = new[] { "Araignée des cavernes", "Tisseuse", "Reine-araignée" },
                hp = 70, dmg = 9, range = 1.1f, chase = 1.8f, cd = 0.9f, cost = 55, level = 4, upkeep = 6,
                desc = "Son venin empoisonne ; la Tisseuse tend des toiles qui engluent les intrus, la Reine pond des araignées au combat.",
                traits = (go, s) =>
                {
                    var t = T(go); t.poisonDps = 2 + 2 * s; t.poisonSeconds = 4f;
                    if (s >= 2) { t.webCount = s == 2 ? 2 : 4; t.webSprite = TrapArt.Frames("web").FirstOrDefault(); }
                    if (s == 3) { t.summonPrefab = Prefab("spider_1"); t.summonCount = 2; t.summonCooldown = 15f; t.summonLifetime = 45f; t.summonName = "Araignée"; }
                } },
            new Family { id = "bat", folder = "Bat", names = new[] { "Chauve-souris", "Vampire volant", "Seigneur de la nuée" },
                hp = 40, dmg = 7, range = 1.0f, chase = 2.4f, cd = 0.7f, cost = 35, level = 3, upkeep = 3,
                desc = "Rapide et insaisissable, elle fond sur les archers et les mages. Le vampire volant boit le sang, le seigneur de la nuée appelle ses sœurs.",
                traits = (go, s) =>
                {
                    var t = T(go); t.preferRanged = true;
                    if (s >= 2) t.lifesteal = s == 2 ? 0.2f : 0.3f;
                    if (s == 3) { t.summonPrefab = Prefab("bat_1"); t.summonCount = 2; t.summonCooldown = 14f; t.summonLifetime = 40f; t.summonName = "Chauve-souris"; }
                } },
            new Family { id = "wolf", folder = "Wolf", names = new[] { "Louveteau des glaces", "Loup de givre", "Alpha blanc" },
                hp = 65, dmg = 10, range = 1.1f, chase = 2.2f, cd = 0.85f, cost = 50, level = 5, upkeep = 5,
                desc = "Chasse en meute : plus fort entouré d'autres loups, sa morsure glace. Le loup de givre hurle pour ralentir les intrus, l'alpha galvanise la meute.",
                traits = (go, s) =>
                {
                    var t = T(go); t.packBonus = 0.1f; t.slowMult = 0.8f; t.slowSeconds = 1f;
                    if (s >= 2) t.howlSlow = s == 2 ? 0.4f : 0.55f;
                    if (s == 3) t.howlBuff = 1.25f;
                } },
            new Family { id = "salamander", folder = "Salamander", names = new[] { "Salamandre", "Salamandre ardente", "Dragonnet de lave" },
                hp = 70, dmg = 9, range = 1.8f, chase = 1.5f, cd = 1.2f, cost = 60, level = 7, upkeep = 6,
                desc = "Souffle du feu sur tout ce qui se tient devant elle (brûlure) et traverse la lave sans dommage. Le dragonnet de lave a des ailes et un souffle redoutable.",
                traits = (go, s) =>
                {
                    var t = T(go); t.burnDps = 2 + 2 * s; t.burnSeconds = 3f;
                    t.splashMult = s == 3 ? 0.8f : 0.5f; t.splashCone = true;
                } },
            new Family { id = "mimic", folder = "Mimic", names = new[] { "Coffre mordeur", "Mimique", "Mimique royale" },
                hp = 120, dmg = 18, range = 1.2f, chase = 1.5f, cd = 1.2f, cost = 80, level = 6, upkeep = 6,
                desc = "Ne se cache que dans une salle du trésor : immobile, il passe pour un coffre et mord par surprise. La Mimique attire les curieux, la Mimique royale les engloutit.",
                traits = (go, s) =>
                {
                    go.GetComponent<GobelinAI>().patrolSpeed = 0f;    // un coffre ne se promène pas
                    var t = T(go);
                    if (s >= 2) t.lureAsChest = true;
                    if (s == 3) t.swallowSeconds = 1.8f;
                } },
        };
        return f;
    }

    /// <summary>
    /// Voie de rôle d'une famille : au passage au stade 2, le joueur choisit entre la voie classique et ces voies
    /// (planches Tools~/make_monster_roles.py : Characters8/&lt;famille&gt;_&lt;rôle&gt;_&lt;stade&gt;). Stats = famille × multiplicateurs.
    /// </summary>
    public class Branch
    {
        public string family, role;
        public string[] names;                      // stades 2 et 3
        public string desc;
        public float hp = 1f, dmg = 1f, range = -1f, chase = 1f, cd = -1f;
        public System.Action<GameObject, int> traits;
    }

    public static List<Branch> Branches() => new List<Branch>
    {
        new Branch { family = "goblin", role = "archer", names = new[] { "Gobelin archer", "Tireur d'élite gobelin" },
            hp = 0.85f, dmg = 0.9f, range = 4.5f, cd = 1.1f,
            desc = "Tire des flèches de loin et recule quand on l'approche. Le tireur d'élite vise plus loin et porte des coups critiques.",
            traits = (go, s) => { var t = T(go); t.projectileFx = "arrow"; if (s == 3) { t.critChance = 0.25f; go.GetComponent<GobelinAI>().attackRange = 5.5f; } } },
        new Branch { family = "goblin", role = "shaman", names = new[] { "Gobelin chaman", "Grand chaman" },
            hp = 0.9f, dmg = 0.8f, range = 3.5f, cd = 1.4f,
            desc = "Lance des boules de feu et soigne les gobelins autour de lui. Le grand chaman galvanise aussi leurs coups (+25 % de dégâts).",
            traits = (go, s) =>
            {
                var t = T(go); t.projectileColor = new Color(1f, 0.55f, 0.2f); t.burnDps = 2f; t.burnSeconds = 2f;
                t.healAuraPct = s == 3 ? 0.1f : 0.07f;
                if (s == 3) t.buffAuraMult = 1.25f;
            } },
        new Branch { family = "orc", role = "berserker", names = new[] { "Orc berserker", "Seigneur de guerre fou" },
            hp = 1.05f, dmg = 1.05f, chase = 1.1f,
            desc = "Plus il est blessé, plus il frappe vite et fort. Le seigneur de guerre fou entre dans une rage terrible.",
            traits = (go, s) => T(go).berserk = s == 3 ? 1.4f : 1f },
        new Branch { family = "orc", role = "shaman", names = new[] { "Orc chaman", "Chaman de sang" },
            hp = 0.85f, dmg = 0.75f, range = 3.5f, cd = 1.4f,
            desc = "Traits de sang qui le soignent ; au combat, plante un totem de guerre : les orcs autour frappent plus fort. Le chaman de sang les régénère aussi.",
            traits = (go, s) =>
            {
                var t = T(go); t.projectileColor = new Color(0.85f, 0.1f, 0.15f); t.lifesteal = 0.25f;
                t.totemFrames = TrapArt.Frames("war_totem"); t.totemMult = s == 3 ? 1.3f : 1.15f; t.totemHealPct = s == 3 ? 0.03f : 0f; t.totemSeconds = s == 3 ? 20f : 15f;
            } },
        new Branch { family = "gnoll", role = "hunter", names = new[] { "Gnoll chasseur", "Traqueur à la lance" },
            hp = 0.9f, dmg = 0.95f, range = 3.6f, cd = 1.3f,
            desc = "Lance des javelots et achève les blessés (dégâts doublés sous 40 % de PV). Le traqueur lance plus loin et plus fort.",
            traits = (go, s) => { var t = T(go); t.projectileFx = "javelin"; t.executeMult = s == 3 ? 1.5f : 1f; if (s == 3) go.GetComponent<GobelinAI>().attackRange = 4.6f; } },
        new Branch { family = "lizard", role = "blowgun", names = new[] { "Homme-lézard sarbacane", "Maître-venin" },
            hp = 0.85f, dmg = 0.6f, range = 4.2f, cd = 1.2f,
            desc = "Souffle des dards empoisonnés de loin. Le maître-venin a un poison bien plus violent, qui ralentit.",
            traits = (go, s) =>
            {
                var t = T(go); t.projectileFx = "dart"; t.poisonDps = s == 3 ? 8f : 4f; t.poisonSeconds = 5f;
                if (s == 3) { t.slowMult = 0.75f; t.slowSeconds = 2f; }
            } },
        new Branch { family = "zombie", role = "spitter", names = new[] { "Zombie cracheur", "Goule acide" },
            hp = 0.85f, dmg = 0.8f, range = 3.2f, cd = 1.5f,
            desc = "Crache de l'acide qui ronge l'armure : l'intrus touché subit 25 % de dégâts en plus pendant 5 s (40 % pour la goule acide).",
            traits = (go, s) =>
            {
                var t = T(go); t.projectileColor = new Color(0.55f, 1f, 0.2f); t.poisonDps = 2f; t.poisonSeconds = 3f;
                t.corrodeMult = s == 3 ? 1.4f : 1.25f; t.corrodeSeconds = 5f;
            } },
        new Branch { family = "rat", role = "swarm", names = new[] { "Nuée de rats", "Grande nuée" },
            hp = 1.4f, dmg = 0.8f, chase = 1.05f,
            desc = "Toute une nuée : ses morsures touchent aussi les intrus autour de sa cible. La grande nuée lâche en plus trois rats au combat.",
            traits = (go, s) =>
            {
                var t = T(go); t.splashMult = 0.6f; t.splashRadius = 1.2f;
                if (s == 3) { t.summonPrefab = Prefab("rat_1"); t.summonCount = 3; t.summonCooldown = 16f; t.summonLifetime = 35f; t.summonName = "Rat de la nuée"; }
            } },
        new Branch { family = "skeleton", role = "archer", names = new[] { "Squelette archer", "Archer d'ossements" },
            hp = 0.85f, dmg = 0.95f, range = 4.5f, cd = 1.2f,
            desc = "Tire des flèches d'os qui transpercent : l'intrus suivant sur la trajectoire est touché aussi. L'archer d'ossements tire plus loin et plus fort.",
            traits = (go, s) =>
            {
                var t = T(go); t.projectileFx = "arrow"; t.pierceMult = s == 3 ? 0.8f : 0.5f;
                if (s == 3) go.GetComponent<GobelinAI>().attackRange = 5.2f;
            } },
        new Branch { family = "skeleton", role = "mage", names = new[] { "Squelette mage", "Nécromancien" },
            hp = 0.8f, dmg = 0.95f, range = 4f, cd = 1.35f,
            desc = "Lance des traits de magie glacée qui ralentissent. Le nécromancien relève en squelettes les intrus tombés près de lui.",
            traits = (go, s) =>
            {
                var t = T(go); t.projectileColor = new Color(0.45f, 1f, 0.65f); t.slowMult = 0.75f; t.slowSeconds = 1.2f;
                if (s == 3) { t.raiseDead = 3; t.raisePrefab = Prefab("skeleton_1"); }
            } },
    };

    /// <summary>Prefabs des voies de rôle d'une famille (stade 3 d'abord : le stade 2 le référence).</summary>
    static IEnumerable<Spec> BranchSpecs(Family fa)
    {
        foreach (var b in Branches())
        {
            if (b.family != fa.id) continue;
            var br = b;
            for (int stage = 3; stage >= 2; stage--)
            {
                int s = stage;
                string id = $"{fa.id}_{br.role}_{s}";
                var f8 = Files8(id);
                if (f8 == null) { Debug.LogWarning($"[CharacterImporter] planches absentes : {id} (Tools~/make_monster_roles.py)"); continue; }
                yield return new Spec
                {
                    id = id, displayName = br.names[s - 2], kind = Kind.Monster, frame = fa.frame, files = f8, dirs8 = true,
                    configure = go =>
                    {
                        float range = br.range > 0f ? br.range : fa.range;
                        Gob(go, br.names[s - 2], Mathf.Round(fa.hp * HpMult[s - 1] * br.hp), Mathf.Round(fa.dmg * DmgMult[s - 1] * br.dmg), range,
                            0.5f, fa.chase * SpeedMult[s - 1] * br.chase, range > 2f ? 6.5f : 5f, br.cd > 0f ? br.cd : fa.cd);
                        var u = go.GetComponent<MonsterUnit>() ?? go.AddComponent<MonsterUnit>();
                        u.familyId = fa.id;
                        u.familyName = br.names[s - 2];
                        u.stage = s;
                        u.role = br.role;
                        u.pathDesc = br.desc;
                        u.branches = null;
                        u.upkeepPerDay = UpkeepFor(fa, s);
                        u.nextStage = s < 3 ? Prefab($"{fa.id}_{br.role}_3") : null;
                        u.xpToEvolve = s < 3 ? EvolveXp[1] : 0;
                        u.evolveCost = s < 3 ? Mathf.RoundToInt(fa.cost * 2.5f) : 0;
                        u.evolveLevelRequired = EvolveLevel(fa, s);
                        fa.traits?.Invoke(go, s);
                        br.traits?.Invoke(go, s);
                    },
                };
            }
        }
    }

    /// <summary>Niveau du Noyau requis pour faire évoluer un stade (stade 1 → 2 : niveau+2, 2 → 3 : niveau+4).</summary>
    public static int EvolveLevel(Family f, int stage) => f.level + (stage == 1 ? 2 : 4);

    /// <summary>Entretien journalier d'un stade (les formes évoluées coûtent plus cher à nourrir).</summary>
    public static int UpkeepFor(Family f, int stage) => Mathf.RoundToInt(f.upkeep * (stage == 1 ? 1f : stage == 2 ? 1.7f : 2.6f));

    static MonsterTraits T(GameObject go) => go.GetComponent<MonsterTraits>() ?? go.AddComponent<MonsterTraits>();

    static readonly float[] HpMult = { 1f, 1.7f, 2.7f }, DmgMult = { 1f, 1.5f, 2.2f }, SpeedMult = { 1f, 1.05f, 1.1f };
    static readonly int[] EvolveXp = { 60, 180 };

    public static List<Spec> Specs()
    {
        var list = new List<Spec>();

        // ── Monstres : familles à 3 stades (le stade 3 est construit d'abord : chaque stade référence le suivant)
        foreach (var fam in Families())
        {
            var fa = fam;
            for (int stage = 3; stage >= 1; stage--)
            {
                int s = stage;
                // voies de rôle (stades 3 puis 2) avant le stade 1, qui les référence
                if (s == 1) list.AddRange(BranchSpecs(fa));
                var files = new Dictionary<string, string>();
                if (fa.file != null) foreach (var st in StdStates) files[st] = fa.file(s, st);
                // planches 8 directions (Tools~/make_8dir.py) quand elles existent
                var f8 = Files8($"{fa.id}_{s}");
                list.Add(new Spec
                {
                    id = $"{fa.id}_{s}", displayName = fa.names[s - 1], kind = Kind.Monster, dirOrder = fa.dirOrder, frame = fa.frame,
                    files = f8 ?? files, dirs8 = f8 != null,
                    configure = go =>
                    {
                        Gob(go, fa.names[s - 1], Mathf.Round(fa.hp * HpMult[s - 1]), Mathf.Round(fa.dmg * DmgMult[s - 1]), fa.range,
                            0.5f, fa.chase * SpeedMult[s - 1], fa.range > 2f ? 6f : 5f, fa.cd);
                        var u = go.GetComponent<MonsterUnit>() ?? go.AddComponent<MonsterUnit>();
                        u.familyId = fa.id;
                        u.familyName = fa.names[s - 1];
                        u.stage = s;
                        u.role = null;
                        u.pathDesc = s == 2 ? $"Plus robuste et plus fort, il garde sa façon de combattre. {fa.desc}" : null;
                        u.branches = s == 1 ? Branches().Where(b => b.family == fa.id).Select(b => Prefab($"{fa.id}_{b.role}_2")).Where(p => p != null).ToArray() : null;
                        u.upkeepPerDay = UpkeepFor(fa, s);
                        u.nextStage = s < 3 ? Prefab($"{fa.id}_{s + 1}") : null;
                        u.xpToEvolve = s < 3 ? EvolveXp[s - 1] : 0;
                        u.evolveCost = s < 3 ? Mathf.RoundToInt(fa.cost * (s == 1 ? 1.2f : 2.5f)) : 0;
                        u.evolveLevelRequired = EvolveLevel(fa, s);
                        fa.traits?.Invoke(go, s);
                    },
                });
            }
        }

        // ── Monstre : sanglier des cavernes (forme unique)
        list.Add(new Spec
        {
            id = "cave_boar", displayName = "Sanglier des cavernes", kind = Kind.Monster, frame = 32, dirOrder = "DULR",
            files = Files("Idle", "Boar_Idle_with_shadow.png", "Walk", "Boar_Walk_with_shadow.png", "Run", "Boar_Run_with_shadow.png",
                          "Attack", "Boar_Attack_with_shadow.png", "Hurt", "Boar_Hurt_with_shadow.png", "Death", "Boar_Death_with_shadow.png"),
            configure = go =>
            {
                Gob(go, "Sanglier", 120, 16, 1.1f, 0.6f, 2.2f, 5f, 0.9f);
                go.GetComponent<SpriteRenderer>().color = new Color(0.75f, 0.7f, 0.85f);
                var u = go.GetComponent<MonsterUnit>() ?? go.AddComponent<MonsterUnit>();
                u.familyId = "boar"; u.familyName = "Sanglier des cavernes"; u.stage = 3; u.upkeepPerDay = 5; u.nextStage = null;
            },
        });

        // ── Monstre : esprit du temple (ancien fantôme, planche sans directions)
        list.Add(new Spec
        {
            id = "temple_spirit", displayName = "Esprit du temple", kind = Kind.Monster, frame = 96, frameH = 128, dirOrder = "", ppu = 48f,
            rowFile = "Ghost.png", rows = new Dictionary<string, int> { { "Idle", 1 }, { "Walk", 1 }, { "Attack", 2 }, { "Death", 3 } },
            configure = go =>
            {
                Gob(go, "Esprit du temple", 380, 30, 1.6f, 0.5f, 1.2f, 7f, 1.3f);
                var u = go.GetComponent<MonsterUnit>() ?? go.AddComponent<MonsterUnit>();
                u.familyId = "temple_spirit"; u.familyName = "Esprit du temple"; u.stage = 3; u.upkeepPerDay = 14; u.nextStage = null;
                var t = T(go); t.slowMult = 0.5f; t.slowSeconds = 2f;
            },
        });

        // ── Intrus : cultistes encapuchonnés (pêcheurs, villageois) — lignes : bas, gauche, droite, haut
        list.Add(new Spec
        {
            id = "fisherman", displayName = "Pêcheur", kind = Kind.Adventurer, frame = 32, dirOrder = "DLRU",
            files = Files("Idle", "Cultist3_Idle.png", "Walk", "Cultist3_Walk.png", "Attack", "Cultist3_Pray.png"),
        });
        list.Add(new Spec
        {
            id = "villager", displayName = "Villageois", kind = Kind.Adventurer, frame = 32, dirOrder = "DLRU",
            files = Files("Idle", "Cultist1_Idle.png", "Walk", "Cultist1_Walk.png", "Attack", "Cultist1_Pray.png"),
        });

        // ── Intrus : épéistes niveaux 1 à 9 (lignes : bas, gauche, droite, haut)
        for (int i = 1; i <= 9; i++)
        {
            int n = i;
            string p = n <= 3 ? $"Swordsman_lvl{n}_" : $"lvl{n}_";
            string atk = n <= 3 ? $"{p}attack_with_shadow.png" : $"{p}attack_with_shadow.png";
            var spec = new Spec
            {
                id = $"swordsman_{n}", displayName = $"Épéiste niv. {n}", kind = Kind.Adventurer, dirOrder = "DLRU",
                files = Files("Idle", $"{p}Idle_with_shadow.png", "Walk", $"{p}Walk_with_shadow.png", "Run", $"{p}Run_with_shadow.png",
                              "Attack", atk, "Hurt", $"{p}Hurt_with_shadow.png", "Death", $"{p}Death_with_shadow.png"),
            };
            var f8 = Files8(spec.id);
            if (f8 != null) { spec.files = f8; spec.dirs8 = true; }
            list.Add(spec);
        }

        // ── Colons : citoyens du marché (lignes : bas, gauche, droite, haut)
        for (int i = 1; i <= 5; i++)
        {
            int n = i;
            list.Add(new Spec
            {
                id = $"citizen_{n}", displayName = $"Colon {n}", kind = Kind.Settler, frame = 32, dirOrder = "DLRU",
                files = Files("Idle", $"Citizen{n}_Idle_with_shadow.png", "Walk", $"Citizen{n}_Walk_with_shadow.png"),
            });
        }
        list.Add(new Spec
        {
            id = "settler_fisher", displayName = "Pêcheur (colon)", kind = Kind.Settler, frame = 32, dirOrder = "DLRU",
            files = Files("Idle", "Cultist3_Idle.png", "Walk", "Cultist3_Walk.png"),
        });

        // ── Rôles des groupes : redessinés à partir de l'épéiste niveau 2 (Tools~/make_roles.py), mêmes animations
        foreach (var r in new[] { ("archer", "Archer"), ("mage", "Mage"), ("healer", "Soigneuse") })
        {
            string n = r.Item1;
            var spec = new Spec
            {
                id = "adv_" + n, displayName = r.Item2, kind = Kind.Adventurer, dirOrder = "DLRU",
                files = Files("Idle", $"Roles/{n}_Idle_with_shadow.png", "Walk", $"Roles/{n}_Walk_with_shadow.png",
                              "Run", $"Roles/{n}_Run_with_shadow.png", "Attack", $"Roles/{n}_Attack_with_shadow.png",
                              "Hurt", $"Roles/{n}_Hurt_with_shadow.png", "Death", $"Roles/{n}_Death_with_shadow.png"),
            };
            var f8 = Files8(spec.id);
            if (f8 != null) { spec.files = f8; spec.dirs8 = true; }
            list.Add(spec);
            // paliers d'armure du héros (mode Aventurier)
            for (int t = 1; t <= 9; t++)
            {
                var f = Files8($"adv_{n}_{t}");
                if (f != null) list.Add(new Spec { id = $"adv_{n}_{t}", displayName = $"{r.Item2} (palier {t})", kind = Kind.Adventurer, dirs8 = true, files = f });
            }
        }

        // ── Faune de l'île
        foreach (var a in new[] { "Deer", "Hare", "Fox", "Boar", "Black_grouse" })
        {
            string walk = a == "Fox" ? "Fox_walk_with_shadow.png" : $"{a}_Walk_with_shadow.png";
            string run = a == "Black_grouse" ? "Black_grouse_Flight_with_shadow.png" : $"{a}_Run_with_shadow.png";
            list.Add(new Spec
            {
                id = "wild_" + a.ToLower(), displayName = a, kind = Kind.Wildlife, frame = 32, dirOrder = "DULR",
                files = Files("Idle", $"{a}_Idle_with_shadow.png", "Walk", walk, "Run", run, "Hurt", $"{a}_Hurt_with_shadow.png", "Death", $"{a}_Death_with_shadow.png"),
            });
        }
        return list;
    }

    // ─────────────────────────────────────────────
    // POINT D'ENTRÉE
    // ─────────────────────────────────────────────

    [MenuItem("DungeonCore/4. Importer les personnages")]
    /// <summary>Importe seulement certains personnages (ex : les rôles PixelLab), puis régénère le catalogue.</summary>
    public static string ImportOnly(params string[] ids)
    {
        var index = IndexPngs();
        var log = new List<string>();
        foreach (var spec in Specs())
            if (System.Array.IndexOf(ids, spec.id) >= 0) log.Add(Import(spec, index));
        AssetDatabase.SaveAssets();
        // relecture forcée : sans elle, un prefab réimporté pouvait garder un contrôleur d'animation vide en mémoire
        foreach (var id in ids)
        {
            AssetDatabase.ImportAsset($"{AnimDir}/{id}/{id}.controller", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset($"{PrefabDir}/{id}.prefab", ImportAssetOptions.ForceUpdate);
            FixPrefabSprite(id);
        }
        CatalogBuilder.BuildCatalog();
        return string.Join(" | ", log);
    }

    public static string ImportRoles() => ImportOnly("adv_archer", "adv_mage", "adv_healer");
    public static string ImportRoles8()
    {
        var ids = new List<string>();
        foreach (var n in new[] { "archer", "mage", "healer" })
        {
            ids.Add("adv_" + n);
            for (int t = 1; t <= 9; t++) ids.Add($"adv_{n}_{t}");
        }
        return ImportOnly(ids.ToArray());
    }
    /// <summary>Tous les personnages 8 directions (épéistes, rôles et leurs paliers, monstres) en une passe.</summary>
    public static string ImportEverything8()
    {
        var ids = new List<string>();
        for (int n = 1; n <= 9; n++) ids.Add($"swordsman_{n}");
        foreach (var r in new[] { "archer", "mage", "healer" })
        {
            ids.Add("adv_" + r);
            for (int t = 1; t <= 9; t++) ids.Add($"adv_{r}_{t}");
        }
        foreach (var f in Families()) for (int s = 3; s >= 1; s--) ids.Add($"{f.id}_{s}");
        var res = ImportOnly(ids.ToArray());
        int errors = res.Split('|').Count(x => !x.Contains("états"));
        return $"{ids.Count} personnages, {errors} anomalies : " + string.Join(" | ", res.Split('|').Where(x => !x.Contains("états")));
    }
    public static string ImportMonsters8()
    {
        var ids = new List<string>();
        foreach (var f in Families()) for (int s = 1; s <= 3; s++) ids.Add($"{f.id}_{s}");
        return ImportOnly(ids.ToArray());
    }
    public static string ImportSwordsmen() => ImportOnly("swordsman_1", "swordsman_2", "swordsman_3", "swordsman_4", "swordsman_5",
        "swordsman_6", "swordsman_7", "swordsman_8", "swordsman_9");
    public static string ImportGolems() => ImportOnly("golem_3", "golem_2", "golem_1");
    public static string ImportGoblinOrc() => ImportOnly("goblin_3", "goblin_2", "goblin_1", "orc_3", "orc_2", "orc_1");

    public static string ImportAll()
    {
        var index = IndexPngs();
        var log = new List<string>();
        foreach (var spec in Specs())
        {
            try { log.Add(Import(spec, index)); }
            catch (System.Exception e) { log.Add($"{spec.id}: ERREUR {e.Message}"); Debug.LogException(e); }
        }
        AssetDatabase.SaveAssets();
        CatalogBuilder.BuildCatalog();
        return string.Join(" | ", log);
    }

    static readonly List<string> allPngs = new List<string>();

    /// <summary>Fichier d'une planche : nom seul, ou fragment de chemin ("Goblin2/With_shadow/Idle_with_shadow.png").</summary>
    static string FindPng(Dictionary<string, string> index, string file)
    {
        if (file.StartsWith("Assets/")) return File.Exists(file) ? file : null;
        if (!file.Contains("/")) return index.GetValueOrDefault(file.ToLowerInvariant());
        string frag = "/" + file.ToLowerInvariant();
        foreach (var p in allPngs) if (p.ToLowerInvariant().EndsWith(frag)) return p;
        return null;
    }

    /// <summary>Tous les PNG des packs, indexés par nom de fichier en minuscules (dossiers PNG prioritaires).</summary>
    static Dictionary<string, string> IndexPngs()
    {
        var d = new Dictionary<string, string>();
        allPngs.Clear();
        foreach (var f in Directory.GetFiles("Assets/imports", "*.png", SearchOption.AllDirectories))
        {
            string p = f.Replace('\\', '/');
            if (!p.Contains("/Tiled") && !p.Contains("__MACOSX") && !p.Contains("/PSD/")) allPngs.Add(p);
            if (p.Contains("__MACOSX") || p.Contains("/Tiled")) continue;
            string key = Path.GetFileName(p).ToLowerInvariant();
            if (!p.Contains("/craftpix")) continue;   // les anciens dossiers (Orc1, Slime1...) gardent leurs réglages
            if (!d.ContainsKey(key) || p.Contains("/With_shadow/") || p.Contains("/With_Shadow/")) d[key] = p;
        }
        return d;
    }

    static string Import(Spec spec, Dictionary<string, string> index)
    {
        string dir = $"{AnimDir}/{spec.id}";
        EnsureFolder(dir);
        EnsureFolder(PrefabDir);

        // clips[state][dirIndex] ; dirIndex : 0 bas, 1 haut, 2 gauche, 3 droite (ou -1 sans directions)
        var clips = new Dictionary<string, AnimationClip[]>();
        Sprite firstSprite = null;

        if (string.IsNullOrEmpty(spec.dirOrder))
        {
            string path = FindPng(index, spec.rowFile);
            if (path == null) return $"{spec.id}: planche introuvable";
            var grid = Slice(path, spec.frame, spec.frameH > 0 ? spec.frameH : spec.frame, spec.ppu);
            foreach (var kv in spec.rows)
            {
                if (kv.Value >= grid.Count) continue;
                var clip = MakeClip($"{dir}/{spec.id}_{kv.Key}.anim", grid[kv.Value], Fps[kv.Key], kv.Key != "Death" && kv.Key != "Hurt");
                clips[kv.Key] = new[] { clip };
                if (firstSprite == null && grid[kv.Value].Length > 0) firstSprite = grid[kv.Value][0];
            }
        }
        else
        {
            foreach (var st in States)
            {
                if (!spec.files.TryGetValue(st, out string file)) continue;
                string path = FindPng(index, file);
                if (path == null) { Debug.LogWarning($"[CharacterImporter] {spec.id}: {file} introuvable"); continue; }
                var grid = Slice(path, spec.frame, spec.frameH > 0 ? spec.frameH : spec.frame, spec.ppu);
                if (spec.dirs8)
                {
                    var arr8 = new AnimationClip[8];
                    bool loop = st != "Hurt" && st != "Death" && st != "Roll" && st != "WalkAttack" && st != "RunAttack" && st != "Use";
                    for (int r = 0; r < grid.Count && r < 8; r++)
                    {
                        arr8[r] = MakeClip($"{dir}/{spec.id}_{st}_{Dir8Names[r]}.anim", grid[r], Fps[st], loop);
                        if (st == "Idle" && r == 0 && grid[r].Length > 0) firstSprite = grid[r][0];
                    }
                    clips[st] = arr8;
                    continue;
                }
                var arr = new AnimationClip[4];
                for (int r = 0; r < grid.Count && r < spec.dirOrder.Length; r++)
                {
                    int d = "DULR".IndexOf(spec.dirOrder[r]);
                    string dn = new[] { "Down", "Up", "Left", "Right" }[d];
                    arr[d] = MakeClip($"{dir}/{spec.id}_{st}_{dn}.anim", grid[r], Fps[st], st != "Death" && st != "Hurt");
                    if (st == "Idle" && d == 0 && grid[r].Length > 0) firstSprite = grid[r][0];
                }
                clips[st] = arr;
            }
        }

        if (!clips.ContainsKey("Idle")) return $"{spec.id}: pas d'animation Idle";
        if (!clips.ContainsKey("Walk")) clips["Walk"] = clips.ContainsKey("Run") ? clips["Run"] : clips["Idle"];
        if (!clips.ContainsKey("Attack")) clips["Attack"] = clips["Idle"];

        var ctrl = BuildController($"{dir}/{spec.id}.controller", clips, !string.IsNullOrEmpty(spec.dirOrder));
        var prefab = BuildPrefab(spec, firstSprite, ctrl);
        return $"{spec.id}: {clips.Count} états" + (prefab != null ? "" : " (pas de prefab)");
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    // ─────────────────────────────────────────────
    // DÉCOUPE
    // ─────────────────────────────────────────────

    /// <summary>Découpe une planche en grille ; renvoie les sprites non vides par ligne (du haut vers le bas).</summary>
    public static List<Sprite[]> Slice(string path, int frame, float ppu) => Slice(path, frame, frame, ppu);

    public static List<Sprite[]> Slice(string path, int frameW, int frameH, float ppu)
    {
        var bytes = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);
        int w = tex.width, h = tex.height;
        int rows = h / frameH, cols = w / frameW;
        string baseName = Path.GetFileNameWithoutExtension(path);

        var metas = new List<SpriteMetaData>();
        var names = new List<List<string>>();
        var px = tex.GetPixels32();
        for (int r = 0; r < rows; r++)
        {
            var rowNames = new List<string>();
            for (int c = 0; c < cols; c++)
            {
                int x0 = c * frameW, y0 = h - (r + 1) * frameH;
                bool any = false;
                for (int y = y0; y < y0 + frameH && !any; y++)
                    for (int x = x0; x < x0 + frameW; x++)
                        if (px[y * w + x].a > 10) { any = true; break; }
                if (!any) continue;
                string n = $"{baseName}_{r}_{c}";
                metas.Add(new SpriteMetaData { name = n, rect = new Rect(x0, y0, frameW, frameH), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
                rowNames.Add(n);
            }
            names.Add(rowNames);
        }
        Object.DestroyImmediate(tex);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = ppu;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.isReadable = path.StartsWith(Dir8Root);   // le héros est recoloré à l'exécution (personnalisation)
#pragma warning disable 0618
        imp.spritesheet = metas.ToArray();
#pragma warning restore 0618
        imp.SaveAndReimport();

        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
        return names.Select(row => row.Where(sprites.ContainsKey).Select(n => sprites[n]).ToArray()).ToList();
    }

    // ─────────────────────────────────────────────
    // CLIPS & CONTRÔLEUR
    // ─────────────────────────────────────────────

    static AnimationClip MakeClip(string path, Sprite[] frames, float fps, bool loop)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool isNew = clip == null;
        if (isNew) clip = new AnimationClip();
        clip.frameRate = fps;
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[frames.Length + (loop ? 0 : 0)];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.stopTime = frames.Length / fps;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        if (isNew) AssetDatabase.CreateAsset(clip, path);
        else EditorUtility.SetDirty(clip);
        return clip;
    }

    static readonly Vector2[] DirVectors = { new Vector2(0, -1), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(1, 0) };

    static AnimatorController BuildController(string path, Dictionary<string, AnimationClip[]> clips, bool directional)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Running", AnimatorControllerParameterType.Bool);
        var sm = ctrl.layers[0].stateMachine;

        AnimatorState Make(string name)
        {
            if (!clips.TryGetValue(name, out var arr)) return null;
            if (!directional)
            {
                var s = sm.AddState(name);
                s.motion = arr[0];
                return s;
            }
            var st = ctrl.CreateBlendTreeInController(name, out BlendTree tree, 0);
            tree.blendType = BlendTreeType.SimpleDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveY";
            var vectors = arr.Length == 8 ? Dir8Vectors : DirVectors;
            for (int d = 0; d < arr.Length; d++)
            {
                var c = arr[d] ?? arr.FirstOrDefault(x => x != null);
                if (c != null) tree.AddChild(c, vectors[d]);
            }
            return st;
        }

        var idle = Make("Idle");
        var walk = Make("Walk");
        // accroupi (fouiller, prier) : boucle tant que le geste dure, le héros revient au repos par Animator.Play
        Make("Crouch");
        // états joués directement par le héros du mode Aventurier (Animator.Play) ; sans transition automatique
        AnimatorState run = clips.ContainsKey("Run") && clips["Run"] != clips["Walk"] ? Make("Run") : null;
        foreach (var extra in new[] { "WalkAttack", "RunAttack", "Roll", "Use" })
        {
            var s = Make(extra);
            if (s == null) continue;
            var back = s.AddTransition(idle);
            back.hasExitTime = true; back.exitTime = 1f; back.duration = 0f;
        }
        var attack = Make("Attack");
        var hurt = Make("Hurt");
        var death = Make("Death");
        sm.defaultState = idle;

        AnimatorStateTransition T(AnimatorState from, AnimatorState to)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0f;
            return t;
        }
        T(idle, walk).AddCondition(AnimatorConditionMode.Greater, 0.01f, "Speed");
        T(walk, idle).AddCondition(AnimatorConditionMode.Less, 0.01f, "Speed");
        if (run != null)
        {
            T(walk, run).AddCondition(AnimatorConditionMode.If, 0, "Running");
            T(run, walk).AddCondition(AnimatorConditionMode.IfNot, 0, "Running");
            T(run, idle).AddCondition(AnimatorConditionMode.Less, 0.01f, "Speed");
        }

        var toAtk = sm.AddAnyStateTransition(attack);
        toAtk.hasExitTime = false; toAtk.duration = 0f; toAtk.canTransitionToSelf = false;
        toAtk.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");
        toAtk.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
        T(attack, idle).AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");

        if (hurt != null)
        {
            var toHurt = sm.AddAnyStateTransition(hurt);
            toHurt.hasExitTime = false; toHurt.duration = 0f; toHurt.canTransitionToSelf = false;
            toHurt.AddCondition(AnimatorConditionMode.If, 0, "Hurt");
            toHurt.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            var back = hurt.AddTransition(idle);
            back.hasExitTime = true; back.exitTime = 1f; back.duration = 0f;
        }
        if (death != null)
        {
            var toDeath = sm.AddAnyStateTransition(death);
            toDeath.hasExitTime = false; toDeath.duration = 0f; toDeath.canTransitionToSelf = false;
            toDeath.AddCondition(AnimatorConditionMode.If, 0, "Dead");
        }
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    // ─────────────────────────────────────────────
    // PREFABS
    // ─────────────────────────────────────────────

    static GameObject BuildPrefab(Spec spec, Sprite sprite, AnimatorController ctrl)
    {
        GameObject go;
        if (spec.kind == Kind.Settler)
        {
            go = new GameObject(spec.displayName);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<Animator>();
            go.AddComponent<Pathfinding.Seeker>();
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.2f;
            go.AddComponent<SettlerAI>();
        }
        else if (spec.kind == Kind.Wildlife)
        {
            go = new GameObject(spec.displayName);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<Animator>();
            go.AddComponent<Pathfinding.Seeker>();
            go.AddComponent<WildAnimal>();
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.2f;
        }
        else
        {
            string template = spec.kind == Kind.Monster ? "Assets/Prefabs/gobelin.prefab" : "Assets/Prefabs/Aventurier_lv1.prefab";
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(template);
            if (src == null) return null;
            go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = spec.displayName;
            go.transform.localScale = Vector3.one;
            var col = go.GetComponent<CircleCollider2D>();
            if (col != null) { col.radius = 0.15f; col.offset = Vector2.zero; }
            if (spec.kind == Kind.Adventurer)
            {
                var combat = go.GetComponent<AdventurerCombat>();
                combat.adventurerName = spec.displayName;
            }
        }
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 3;
        sr.color = Color.white;
        var anim = go.GetComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        spec.configure?.Invoke(go);

        string path = $"{PrefabDir}/{spec.id}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    public static GameObject Prefab(string id) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{id}.prefab");

    /// <summary>Première image de l'animation d'attente (face à la caméra) : l'apparence réelle en jeu.</summary>
    public static Sprite IdleSprite(string id)
    {
        foreach (var name in new[] { $"{id}_Idle_Down", $"{id}_Idle_S", $"{id}_Idle", $"{id}_Walk_Down", $"{id}_Walk_S", $"{id}_Walk" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Generated/{id}/{name}.anim");
            if (clip == null) continue;
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
                if (keys != null && keys.Length > 0 && keys[0].value is Sprite s) return s;
            }
        }
        return null;
    }

    const string IconDir = "Assets/Art/Generated/Icons";

    /// <summary>
    /// Icône du panneau : première image d'attente recadrée sur le personnage
    /// (les images d'animation contiennent beaucoup de vide autour).
    /// </summary>
    public static Sprite IconSprite(string id)
    {
        var src = IdleSprite(id);
        if (src == null) return null;
        string texPath = AssetDatabase.GetAssetPath(src.texture);
        var tex = new Texture2D(2, 2);
        tex.LoadImage(System.IO.File.ReadAllBytes(texPath));
        var r = src.rect;
        int x0 = (int)r.x, y0 = (int)r.y, w = (int)r.width, h = (int)r.height;
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (tex.GetPixel(x0 + x, y0 + y).a > 0.1f)
                {
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
        if (maxX < 0) { Object.DestroyImmediate(tex); return src; }
        int cw = maxX - minX + 1, ch = maxY - minY + 1, side = Mathf.Max(cw, ch) + 2;
        var icon = new Texture2D(side, side, TextureFormat.RGBA32, false);
        var clear = new Color32[side * side];
        icon.SetPixels32(clear);
        int ox = (side - cw) / 2, oy = (side - ch) / 2;
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
                icon.SetPixel(ox + x, oy + y, tex.GetPixel(x0 + minX + x, y0 + minY + y));
        icon.Apply();
        EnsureFolder(IconDir);
        string path = $"{IconDir}/{id}.png";
        System.IO.File.WriteAllBytes(path, icon.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(icon);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>Le sprite du prefab (vu dans l'éditeur et le panneau) = son apparence en jeu.</summary>
    public static Sprite FixPrefabSprite(string id)
    {
        var pf = Prefab(id);
        var s = IdleSprite(id);
        if (pf == null || s == null) return s;
        var sr = pf.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != s)
        {
            sr.sprite = s;
            EditorUtility.SetDirty(pf);
        }
        // portrait recadré des formes évoluées (fenêtre de choix de voie)
        var mu = pf.GetComponent<MonsterUnit>();
        if (mu != null && mu.stage >= 2 && mu.portrait == null)
        {
            mu.portrait = IconSprite(id);
            EditorUtility.SetDirty(pf);
        }
        return s;
    }
}
#endif
