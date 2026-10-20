using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// L'ESPRIT DU NOYAU (mode Aventurier) : le donjon n'est pas un décor figé, une intelligence le gère et le fait grandir.
/// Elle amasse de l'essence (chaque jour, à chaque défaite du héros, à chaque expédition) et la dépense en choisissant,
/// selon la situation, l'action la plus utile :
///   - creuser une nouvelle salle (peuplée) sur un étage que le héros a bien exploré ;
///   - repeupler les salles vidées ;
///   - faire évoluer les monstres qui ont survécu (stade suivant) ;
///   - piéger les salles où le héros passe le plus ;
///   - envoyer les familles qui lui résistent le mieux (et, contre un héros à distance, des chasseurs rapides).
/// En dépensant, il s'éveille : les étages recréés ont plus de salles et des monstres plus évolués.
/// Il agit hors de la vue du héros ; ce qu'il a fait est raconté au retour dans le donjon et à la Guilde.
/// </summary>
public class CrawlerOverlord : MonoBehaviour
{
    public static CrawlerOverlord Instance { get; private set; }

    public float essence = 60f;
    private float spent;
    public int Level { get; private set; } = 1;
    public int RoomsDug { get; private set; }
    public int Evolved { get; private set; }
    public int Reinforced { get; private set; }
    public int TrapsSet { get; private set; }

    private const int MaxExtraRoomsPerFloor = 6;
    private const float DigCost = 90f, MonsterCost = 12f, EvolveCost = 30f, TrapCost = 20f;

    private System.Random rng;
    private readonly Dictionary<int, int> dugOnFloor = new Dictionary<int, int>();
    private readonly Dictionary<string, int> losses = new Dictionary<string, int>();
    private readonly Dictionary<int, Dictionary<int, float>> visits = new Dictionary<int, Dictionary<int, float>>();
    private readonly Dictionary<string, int> trapsInRoom = new Dictionary<string, int>();
    private readonly List<string> journal = new List<string>();
    private float visitTimer;

    private CrawlerMode Mode => CrawlerMode.Instance;

    private void Awake()
    {
        Instance = this;
        rng = new System.Random(System.Environment.TickCount);
        CrawlerDungeon.ExtraRooms = 0;
        CrawlerDungeon.StageBonus = 0f;
    }

    private void OnEnable() { if (GameManager.Instance != null) GameManager.Instance.OnNewDay += OnNewDay; }
    private void OnDisable() { if (GameManager.Instance != null) GameManager.Instance.OnNewDay -= OnNewDay; }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        CrawlerDungeon.ExtraRooms = 0;
        CrawlerDungeon.StageBonus = 0f;
    }

    // ─────────────────────────────────────────────
    // CE QUE LE NOYAU OBSERVE
    // ─────────────────────────────────────────────

    private void Update()
    {
        var m = Mode;
        if (m == null || m.Player == null || m.floor <= 0) return;
        visitTimer += Time.deltaTime;
        if (visitTimer < 0.5f) return;
        int room = RoomAt(m.FloorData(m.floor), m.Player.transform.position);
        if (room > 0)
        {
            if (!visits.TryGetValue(m.floor, out var v)) visits[m.floor] = v = new Dictionary<int, float>();
            v.TryGetValue(room, out float t);
            v[room] = t + visitTimer;
        }
        visitTimer = 0f;
    }

    private void OnNewDay(int day)
    {
        if (Mode == null || Mode.Player == null) return;
        essence += 50f + 12f * Mode.deepest;
        Think(3);
    }

    public void OnKill(GobelinAI g)
    {
        var u = g != null ? g.GetComponent<MonsterUnit>() : null;
        if (u != null && !string.IsNullOrEmpty(u.familyId)) { losses.TryGetValue(u.familyId, out int n); losses[u.familyId] = n + 1; }
        essence += 5f;   // il récupère un peu de l'âme de ses morts
    }

    public void OnPlayerDeath()
    {
        essence += 100f;
        journal.Add("Le Noyau s'est nourri de votre défaite.");
    }

    /// <summary>Le héros ressort du donjon : le Noyau a le champ libre.</summary>
    public void OnTripEnd()
    {
        essence += 30f;
        Think(5);
    }

    /// <summary>Le héros redescend : on lui fait sentir que le donjon a changé.</summary>
    public void OnEnterDungeon()
    {
        if (journal.Count == 0) return;
        GameHUD.Toast("Le donjon a changé pendant votre absence...", GameHUD.DangerColor);
        int shown = 0;
        foreach (var line in journal) if (shown++ < 3) GameHUD.Toast(line, new Color(0.85f, 0.6f, 1f));
        journal.Clear();
    }

    /// <summary>Résumé pour la Guilde.</summary>
    public string Report() =>
        $"Éveil du Noyau : niveau {Level} · salles creusées : {RoomsDug} · monstres évolués : {Evolved} · renforts : {Reinforced} · pièges posés : {TrapsSet}";

    // ─────────────────────────────────────────────
    // CE QUE LE NOYAU DÉCIDE
    // ─────────────────────────────────────────────

    private enum Act { Repopulate, Dig, Evolve, Trap }

    /// <summary>Choisit et exécute jusqu'à « maxActs » actions, tant que l'essence le permet.</summary>
    public void Think(int maxActs)
    {
        var m = Mode;
        if (m == null) return;
        int failures = 0;
        for (int n = 0; n < maxActs; n++)
        {
            var options = new List<(Act act, CrawlerDungeon.Floor f, float score)>();
            foreach (var f in m.LiveFloors())
            {
                if (f.number >= CrawlerDungeon.FloorCount) continue;   // l'antre du cristal reste telle quelle
                int empty = EmptyRooms(f).Count;
                if (empty > 0 && essence >= MonsterCost * 2) options.Add((Act.Repopulate, f, 10f * empty + Interest(f) * 5f));
                dugOnFloor.TryGetValue(f.number, out int dug);
                if (dug < MaxExtraRoomsPerFloor && essence >= DigCost) options.Add((Act.Dig, f, 18f + Explored(f) * 30f));
                int evo = Evolvable(f).Count;
                if (evo > 0 && essence >= EvolveCost) options.Add((Act.Evolve, f, Mathf.Min(30f, evo * 4f)));
                if (TrapTarget(f) > 0 && essence >= TrapCost) options.Add((Act.Trap, f, 12f + Interest(f) * 4f));
            }
            if (options.Count == 0) break;
            float total = 0f;
            foreach (var o in options) total += o.score;
            float pick = (float)rng.NextDouble() * total;
            var choice = options[0];
            foreach (var o in options) { pick -= o.score; if (pick <= 0f) { choice = o; break; } }
            bool done = false;
            switch (choice.act)
            {
                case Act.Repopulate: done = Repopulate(choice.f); break;
                case Act.Dig: done = Dig(choice.f); break;
                case Act.Evolve: done = Evolve(choice.f); break;
                case Act.Trap: done = Trap(choice.f); break;
            }
            if (!done && ++failures > 3) break;   // action impossible (plus de place, rien à faire) : on essaie autre chose
            if (!done) n--;
        }
    }

    public float Spent => spent;

    /// <summary>Reprise d'une aventure sauvegardée.</summary>
    public void Restore(CrawlerSave s)
    {
        essence = s.overlordEssence;
        spent = 0f;
        Level = 1;
        RoomsDug = s.overlordRooms; Evolved = s.overlordEvolved; Reinforced = s.overlordReinforced; TrapsSet = s.overlordTraps;
        spent = s.overlordSpent;
        Level = Mathf.Min(10, 1 + Mathf.FloorToInt(spent / 250f));
        CrawlerDungeon.ExtraRooms = Mathf.Min(6, Level - 1);
        CrawlerDungeon.StageBonus = Mathf.Min(0.5f, 0.06f * (Level - 1));
    }

    private void Spend(float cost)
    {
        essence -= cost;
        spent += cost;
        int lvl = Mathf.Min(10, 1 + Mathf.FloorToInt(spent / 250f));
        if (lvl <= Level) return;
        Level = lvl;
        CrawlerDungeon.ExtraRooms = Mathf.Min(6, Level - 1);
        CrawlerDungeon.StageBonus = Mathf.Min(0.5f, 0.06f * (Level - 1));
        GameHUD.Toast($"L'Esprit du Noyau s'éveille (niveau {Level}) : le donjon grandit, ses créatures naissent plus fortes", new Color(0.85f, 0.6f, 1f));
    }

    private bool Repopulate(CrawlerDungeon.Floor f)
    {
        var rooms = EmptyRooms(f);
        if (rooms.Count == 0) return false;
        var room = rooms[rng.Next(rooms.Count)];
        int count = Mathf.Clamp(1 + f.number / 3, 1, 4);
        int made = 0;
        for (int i = 0; i < count && essence >= MonsterCost; i++)
        {
            var pos = RandomPos(room.rect);
            if (CrawlerDungeon.SpawnMonster(ChooseFamily(f), CrawlerDungeon.RollStage(f.number, rng), pos, f) == null) continue;
            Spend(MonsterCost);
            made++;
        }
        if (made == 0) return false;
        Reinforced += made;
        Tell(f, $"Étage {f.number} : de nouvelles créatures rôdent dans une salle que vous aviez nettoyée.");
        return true;
    }

    private bool Dig(CrawlerDungeon.Floor f)
    {
        var fog = CrawlerFog.Instance;
        RectInt? avoid = Mode.floor == f.number && fog != null ? fog.CurrentRoom : null;
        var room = CrawlerDungeon.ExpandFloor(f, rng, avoid);
        if (room == null) { dugOnFloor[f.number] = MaxExtraRoomsPerFloor; return false; }   // plus de place : on n'y pense plus
        Spend(DigCost);
        dugOnFloor.TryGetValue(f.number, out int d);
        dugOnFloor[f.number] = d + 1;
        RoomsDug++;
        CrawlerDungeon.PopulateRoom(f, room, rng, key => Mode.ChestLooted(key));
        fog?.Cover(f, new RectInt(room.rect.x - 8, room.rect.y - 8, room.rect.width + 16, room.rect.height + 16));
        if (Mode.floor == f.number) GameHUD.Toast("La roche gronde au loin : le Noyau creuse une nouvelle salle...", new Color(0.85f, 0.6f, 1f));
        Tell(f, $"Étage {f.number} : une nouvelle salle a été creusée.");
        return true;
    }

    private bool Evolve(CrawlerDungeon.Floor f)
    {
        var list = Evolvable(f);
        if (list.Count == 0) return false;
        var g = list[rng.Next(list.Count)];
        var u = g.GetComponent<MonsterUnit>();
        var next = u.HasChoice && rng.Next(3) == 0 && u.branches.Length > 0 ? u.branches[rng.Next(u.branches.Length)] ?? u.nextStage : u.nextStage;
        if (next == null) return false;
        Vector2 pos = g.transform.position;
        var sp = MonsterSpawner.Instance;
        if (sp == null) return false;
        var ng = sp.SpawnFree(next, pos, pos, next.GetComponent<GobelinAI>()?.gobelinName ?? next.name);
        if (ng == null) return false;
        CrawlerDungeon.Tame(ng);
        f.spawned.Add(ng.gameObject);
        f.spawned.Remove(g.gameObject);
        Destroy(g.gameObject);
        Spend(EvolveCost);
        Evolved++;
        Tell(f, $"Étage {f.number} : un monstre qui vous avait échappé a évolué ({ng.gobelinName}).");
        return true;
    }

    private bool Trap(CrawlerDungeon.Floor f)
    {
        int roomIndex = TrapTarget(f);
        if (roomIndex <= 0) return false;
        var traps = TrapsOn(f.number);
        if (traps.Length == 0) return false;
        var item = DungeonCatalog.Get().Find(traps[rng.Next(traps.Length)]);
        if (item == null) return false;
        var r = f.rooms[roomIndex].rect;
        var cell = new Vector3Int(rng.Next(r.xMin + 1, r.xMax - 1), rng.Next(r.yMin + 1, r.yMax - 1), 0);
        if (DungeonBuilder.Instance.PlaceItemFree(item, cell) == null) return false;
        string key = $"{f.number}:{roomIndex}";
        trapsInRoom.TryGetValue(key, out int n);
        trapsInRoom[key] = n + 1;
        Spend(TrapCost);
        TrapsSet++;
        Tell(f, $"Étage {f.number} : le Noyau a remarqué vos passages et piégé une salle.");
        return true;
    }

    // ─────────────────────────────────────────────
    // OUTILS
    // ─────────────────────────────────────────────

    private void Tell(CrawlerDungeon.Floor f, string line)
    {
        if (journal.Count < 12) journal.Add(line);
    }

    /// <summary>Famille envoyée : celles qui ont le moins péri sous les coups du héros ; contre un héros à distance, des chasseurs rapides.</summary>
    private string ChooseFamily(CrawlerDungeon.Floor f)
    {
        var pool = new List<string>(CrawlerDungeon.FamiliesOn(f.number));
        var role = Mode.Player != null ? Mode.Player.role : AdventurerAI.Role.Warrior;
        bool ranged = role == AdventurerAI.Role.Archer || role == AdventurerAI.Role.Mage;
        if (ranged && f.number >= 3) { pool.Add("wolf"); pool.Add("bat"); }
        if (rng.Next(3) == 0) return pool[rng.Next(pool.Count)];   // un peu de variété
        string best = pool[0];
        int bestLoss = int.MaxValue;
        foreach (var fam in pool)
        {
            losses.TryGetValue(fam, out int l);
            if (l < bestLoss || (l == bestLoss && rng.Next(2) == 0)) { best = fam; bestLoss = l; }
        }
        return CrawlerDungeon.MonsterPrefab(best, 1) != null ? best : CrawlerDungeon.RandomFamily(f.number, rng);
    }

    private static string[] TrapsOn(int floor) => floor <= 1 ? new[] { "spikes" } : floor <= 3 ? new[] { "spikes", "bear", "web" } : floor <= 6 ? new[] { "saw", "flame", "spikes", "cage" } : new[] { "rune", "pendulum", "ghost_trap", "flame" };

    /// <summary>Salle la plus fréquentée par le héros qui n'a pas encore trois pièges du Noyau (0 : aucune).</summary>
    private int TrapTarget(CrawlerDungeon.Floor f)
    {
        if (!visits.TryGetValue(f.number, out var v)) return 0;
        int best = 0; float bestT = 4f;   // au moins quelques secondes passées dans la salle
        int here = Mode.floor == f.number ? RoomAt(f, Mode.Player.transform.position) : -1;
        foreach (var kv in v)
        {
            if (kv.Key <= 0 || kv.Key >= f.rooms.Count || kv.Key == here) continue;
            trapsInRoom.TryGetValue($"{f.number}:{kv.Key}", out int n);
            if (n < 3 && kv.Value > bestT) { best = kv.Key; bestT = kv.Value; }
        }
        return best;
    }

    /// <summary>Salles sans monstre vivant (hors salle d'arrivée et salle où se tient le héros).</summary>
    private List<CrawlerDungeon.Room> EmptyRooms(CrawlerDungeon.Floor f)
    {
        var occupied = new HashSet<int>();
        foreach (var go in f.spawned)
        {
            if (go == null) continue;
            var g = go.GetComponent<GobelinAI>();
            if (g == null || g.currentHealth <= 0f) continue;
            occupied.Add(RoomAt(f, go.transform.position));
        }
        int here = Mode.floor == f.number && Mode.Player != null ? RoomAt(f, Mode.Player.transform.position) : -1;
        var list = new List<CrawlerDungeon.Room>();
        for (int i = 1; i < f.rooms.Count; i++)
            if (!occupied.Contains(i) && i != here && FarFromHero(f, f.rooms[i].rect)) list.Add(f.rooms[i]);
        return list;
    }

    /// <summary>Monstres vivants pouvant évoluer, loin des yeux du héros (pas les gardiens).</summary>
    private List<GobelinAI> Evolvable(CrawlerDungeon.Floor f)
    {
        var list = new List<GobelinAI>();
        foreach (var go in f.spawned)
        {
            if (go == null) continue;
            var g = go.GetComponent<GobelinAI>();
            var u = go.GetComponent<MonsterUnit>();
            if (g == null || u == null || g.currentHealth <= 0f || u.nextStage == null) continue;
            if (go.GetComponent<MonsterMind>()?.IsGuardian == true) continue;
            if (Mode.Player != null && Mode.floor == f.number && Vector2.Distance(go.transform.position, Mode.Player.transform.position) < 10f) continue;
            list.Add(g);
        }
        return list;
    }

    private bool FarFromHero(CrawlerDungeon.Floor f, RectInt r)
    {
        if (Mode.Player == null || Mode.floor != f.number) return true;
        Vector2 c = new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f);
        return Vector2.Distance(c, Mode.Player.transform.position) > 10f;
    }

    /// <summary>Part des salles de l'étage où le héros a passé du temps (0..1).</summary>
    private float Explored(CrawlerDungeon.Floor f)
    {
        if (!visits.TryGetValue(f.number, out var v) || f.rooms.Count <= 1) return 0f;
        return Mathf.Clamp01(v.Count / (float)(f.rooms.Count - 1));
    }

    /// <summary>Étage où se joue la partie : celui du héros et le plus profond atteint comptent davantage.</summary>
    private float Interest(CrawlerDungeon.Floor f) => (Mode.floor == f.number ? 1f : 0f) + (Mode.deepest == f.number ? 1f : 0f);

    private static int RoomAt(CrawlerDungeon.Floor f, Vector2 p)
    {
        if (f == null || DungeonBuilder.Instance == null) return -1;
        var c = DungeonBuilder.Instance.floorTilemap.WorldToCell(p);
        for (int i = 0; i < f.rooms.Count; i++)
        {
            var r = f.rooms[i].rect;
            if (c.x >= r.xMin && c.x < r.xMax && c.y >= r.yMin && c.y < r.yMax) return i;
        }
        return -1;
    }

    private Vector2 RandomPos(RectInt r) =>
        DungeonBuilder.Instance.floorTilemap.GetCellCenterWorld(new Vector3Int(rng.Next(r.xMin + 1, r.xMax - 1), rng.Next(r.yMin + 1, r.yMax - 1), 0));
}
