using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LA VIE D'UN MONSTRE : besoins, humeur, caractère et liens.
///   - Faim : elle monte lentement ; le monstre dévore les cadavres d'intrus (Corpse) ou se sert au garde-manger.
///     Nourri de chair, il coûte moitié moins d'entretien ce jour-là. Affamé, il devient hargneux... puis déprime.
///   - Sommeil : au repos et fatigué, il s'endort (il voit moins loin) ; épuisé, il frappe moins fort.
///   - Ennui : sans combat, l'envie de se battre monte et l'humeur baisse ; une victoire le ravit.
///   - Humeur : heureux, il frappe plus fort ; malheureux trop longtemps, il déserte (sauf s'il est loyal).
///   - Traits (0 à 2) : lâche, sanguinaire, gourmand, paresseux, loyal, querelleur, sociable, vaillant.
///   - Liens (MonsterSociety) : amis et couples se renforcent côte à côte ; les rivaux se querellent.
/// </summary>
[RequireComponent(typeof(GobelinAI))]
public class MonsterLife : MonoBehaviour
{
    public enum Trait { Coward, Bloodthirsty, Glutton, Lazy, Loyal, Quarrelsome, Sociable, Valiant }

    public static string TraitName(Trait t) => t switch
    {
        Trait.Coward => "Lâche", Trait.Bloodthirsty => "Sanguinaire", Trait.Glutton => "Gourmand", Trait.Lazy => "Paresseux",
        Trait.Loyal => "Loyal", Trait.Quarrelsome => "Querelleur", Trait.Sociable => "Sociable", _ => "Vaillant",
    };

    public readonly List<Trait> traits = new List<Trait>();
    [Range(0f, 100f)] public float hunger = 20f, energy = 90f, boredom, mood = 60f;
    public bool Has(Trait t) => traits.Contains(t);
    public bool Sleeping { get; private set; }
    public bool FedToday => GameManager.Instance != null && lastFedDay == GameManager.Instance.CurrentDay;
    public MonsterLife Partner { get; set; }

    private GobelinAI ai;
    private MonsterUnit unit;
    private int lastFedDay = -1;
    private float unhappySince = -1f, starvingSince = -1f, joy, nextBark, nextThink;

    private enum Task { None, Eat, Sleep, Quarrel, Desert }
    private Task task;
    private float taskTimer;
    private Corpse corpse;
    private Vector2 target;
    private MonsterLife foe;

    public static readonly List<MonsterLife> All = new List<MonsterLife>();
    private void OnEnable() => All.Add(this);
    private void OnDisable() { All.Remove(this); if (Partner != null && Partner.Partner == this) Partner.Partner = null; }

    private void Awake()
    {
        ai = GetComponent<GobelinAI>();
        unit = GetComponent<MonsterUnit>();
    }

    private void Start()
    {
        if (traits.Count == 0) RollTraits();
        if (Has(Trait.Valiant)) { ai.maxHealth *= 1.1f; ai.currentHealth = Mathf.Min(ai.maxHealth, ai.currentHealth * 1.1f); }
        nextThink = Time.time + Random.Range(1f, 3f);
    }

    private void RollTraits()
    {
        var pool = new List<Trait>((Trait[])System.Enum.GetValues(typeof(Trait)));
        int n = Random.value < 0.3f ? 0 : Random.value < 0.75f ? 1 : 2;
        for (int i = 0; i < n; i++)
        {
            var t = pool[Random.Range(0, pool.Count)];
            pool.Remove(t);
            traits.Add(t);
            if (t == Trait.Coward) pool.Remove(Trait.Valiant);
            if (t == Trait.Valiant) pool.Remove(Trait.Coward);
            if (t == Trait.Sociable) pool.Remove(Trait.Quarrelsome);
            if (t == Trait.Quarrelsome) pool.Remove(Trait.Sociable);
        }
    }

    public int TraitMask { get { int m = 0; foreach (var t in traits) m |= 1 << (int)t; return m; } }
    public void SetTraitMask(int m)
    {
        traits.Clear();
        foreach (Trait t in System.Enum.GetValues(typeof(Trait))) if ((m & (1 << (int)t)) != 0) traits.Add(t);
    }

    // ─────────────────────────────────────────────
    // EFFETS SUR LE COMBAT
    // ─────────────────────────────────────────────

    public float DamageMult
    {
        get
        {
            float m = mood > 70f ? 1.1f : mood < 25f ? 0.9f : 1f;
            if (energy < 15f) m *= 0.9f;
            if (hunger >= 100f) m *= 1.15f;             // la faim rend hargneux
            if (Has(Trait.Bloodthirsty)) m *= 1.1f;
            int friends = MonsterSociety.FriendsNear(this, 3f, out bool partner);
            if (partner) m *= 1.15f; else if (friends > 0) m *= 1.1f;
            return m;
        }
    }

    /// <summary>Seuil de PV sous lequel il fuit chercher de l'aide.</summary>
    public float FleeThreshold => Has(Trait.Valiant) ? 0f : Has(Trait.Coward) ? 0.5f : 0.25f;

    public void OnFight() => boredom = Mathf.Max(0f, boredom - 2f);
    public void OnKill()
    {
        boredom = 0f;
        joy = Has(Trait.Bloodthirsty) ? 40f : 20f;
        if (Has(Trait.Bloodthirsty)) Bark("Encore !", new Color(1f, 0.45f, 0.4f));
    }

    // ─────────────────────────────────────────────
    // BESOINS ET HUMEUR
    // ─────────────────────────────────────────────

    private void Update()
    {
        if (CrawlerMode.Active || !ai.IsAlive()) return;
        float dt = Time.deltaTime;
        float day = GameManager.Instance != null ? GameManager.Instance.dayLengthSeconds : 180f;
        hunger = Mathf.Min(100f, hunger + 100f / (day * 1.5f) * dt * (Has(Trait.Glutton) ? 1.5f : 1f) * Seasons.HungerMult * Talents.MonsterHungerMult);
        if (Sleeping) energy = Mathf.Min(100f, energy + 8f * dt);
        else energy = Mathf.Max(0f, energy - 100f / day * dt * (Has(Trait.Lazy) ? 1.5f : 1f));
        if (ai.InCombat) OnFight();
        else boredom = Mathf.Min(100f, boredom + 100f / (day * 2f) * dt * (Has(Trait.Bloodthirsty) ? 2f : 1f));
        joy = Mathf.Max(0f, joy - dt * 0.3f);

        // humeur : elle glisse lentement vers ce que le monstre vit
        float targetMood = 60f + joy;
        if (hunger < 40f) targetMood += 8f; else if (hunger > 85f) targetMood -= 25f;
        if (energy < 20f) targetMood -= 10f;
        if (boredom > 70f) targetMood -= Has(Trait.Bloodthirsty) ? 25f : 12f;
        if (ThemedRooms.InAnyRoom(transform.position)) targetMood += 10f;    // une salle aménagée : un chez-soi
        int friends = MonsterSociety.FriendsNear(this, 4f, out bool partner);
        targetMood += Mathf.Min(10f, friends * 5f) + (partner ? 10f : 0f);
        targetMood -= MonsterSociety.RivalsNear(this, 4f) * 10f;
        mood = Mathf.MoveTowards(mood, Mathf.Clamp(targetMood, 0f, 100f), dt * 2f);

        if (hunger >= 100f) { if (starvingSince < 0f) starvingSince = Time.time; } else starvingSince = -1f;
        if (mood < 12f && !Has(Trait.Loyal) && !IsGuardian) { if (unhappySince < 0f) unhappySince = Time.time; }
        else unhappySince = -1f;
    }

    private bool IsGuardian => GetComponent<MonsterMind>() != null && GetComponent<MonsterMind>().IsGuardian;

    private void Bark(string text, Color c)
    {
        if (Time.time < nextBark) return;
        nextBark = Time.time + 6f;
        FloatingText.Spawn(transform.position + Vector3.up * 0.75f, text, c, 0.8f);
    }

    // ─────────────────────────────────────────────
    // CONTRÔLE (appelé par GobelinAI quand il n'a pas d'ordre ni de combat)
    // ─────────────────────────────────────────────

    /// <summary>true : la vie du monstre pilote ses déplacements ce pas-ci (manger, dormir, querelle, désertion).</summary>
    public bool Tick()
    {
        if (CrawlerMode.Active) return false;
        if (task != Task.None) return Step();
        if (Time.time < nextThink) return false;
        nextThink = Time.time + Random.Range(1.5f, 3f);
        Vector2 pos = transform.position;

        // désertion : malheureux depuis trop longtemps
        if (unhappySince > 0f && Time.time - unhappySince > 45f && Random.value < 0.4f)
        {
            var gate = FindFirstObjectByType<DungeonBoundary>();
            if (gate != null)
            {
                task = Task.Desert; target = gate.transform.position; taskTimer = 0f;
                ai.LifeGoTo(target);
                FloatingText.Spawn(transform.position + Vector3.up * 0.8f, "J'en ai assez !", new Color(0.8f, 0.85f, 1f), 1f);
                GameHUD.Toast($"{ai.gobelinName} déserte le donjon (malheureux)", GameHUD.WarningColor);
                return true;
            }
        }
        if (mood < 22f) Bark(hunger > 85f ? "J'ai faim..." : boredom > 70f ? "Je m'ennuie..." : "Grmbl...", new Color(0.75f, 0.75f, 0.85f));

        // manger : un cadavre, sinon le garde-manger
        if (hunger > 55f)
        {
            Corpse best = null;
            float bd = 22f;
            int layer = LayerOf(pos);
            foreach (var c in Corpse.All)
            {
                if (c == null || c.Portions <= 0 || LayerOf(c.transform.position) != layer) continue;
                float d = Vector2.Distance(c.transform.position, pos);
                if (d < bd) { bd = d; best = c; }
            }
            if (best != null) { Begin(Task.Eat); corpse = best; target = best.transform.position; ai.LifeGoTo(target); return true; }
            if (ThemedRooms.NearestRoom(pos, ThemedRooms.Kind.Pantry, 25f, out var pantry))
            {
                Begin(Task.Eat); corpse = null; target = pantry; ai.LifeGoTo(target); return true;
            }
            if (hunger >= 100f) Bark("Grrr... de la viande !", new Color(1f, 0.5f, 0.4f));
        }

        // querelle avec un rival tout proche
        var rival = MonsterSociety.RivalNear(this, 2.5f);
        if (rival != null && rival.task == Task.None && rival.ai.IdleForLife && Random.value < (Has(Trait.Quarrelsome) ? 0.35f : 0.12f))
        {
            Begin(Task.Quarrel); foe = rival;
            rival.Begin(Task.Quarrel); rival.foe = this;
            FloatingText.Spawn(transform.position + Vector3.up * 0.8f, "Grrr !", new Color(1f, 0.5f, 0.4f), 0.9f);
            return true;
        }

        // dormir quand il est fatigué (le paresseux plus tôt)
        if (energy < (Has(Trait.Lazy) ? 45f : 30f))
        {
            Begin(Task.Sleep);
            Sleeping = true;
            ai.LifeStop();
            return true;
        }
        return false;
    }

    private void Begin(Task t) { task = t; taskTimer = 0f; }

    public void Interrupt()
    {
        if (task == Task.Quarrel && foe != null && foe.foe == this) { foe.task = Task.None; foe.foe = null; }
        task = Task.None;
        Sleeping = false;
        corpse = null;
        foe = null;
    }

    private bool Step()
    {
        taskTimer += Time.fixedDeltaTime;
        Vector2 pos = transform.position;
        switch (task)
        {
            case Task.Sleep:
                ai.LifeStop();
                if (taskTimer > 2f && (energy >= 95f || ai.LifeDetect(0.5f) != null)) { if (energy < 95f) FloatingText.Spawn(pos + Vector2.up * 0.7f, "!", new Color(1f, 0.6f, 0.4f), 1f); Interrupt(); return false; }
                if (Mathf.Repeat(taskTimer, 4f) < Time.fixedDeltaTime) FloatingText.Spawn(pos + Vector2.up * 0.7f, "Zzz", new Color(0.7f, 0.75f, 1f), 0.7f);
                return true;

            case Task.Eat:
                if (corpse != null && corpse.Portions <= 0) { Interrupt(); return false; }
                if (Vector2.Distance(pos, target) > 0.8f)
                {
                    if (taskTimer > 25f) { Interrupt(); return false; }
                    ai.LifeMove(0.9f);
                    return true;
                }
                ai.LifeStop();
                ai.LifeFace(target);
                ai.LifeGesture(Mathf.Repeat(taskTimer, 0.6f) < 0.3f ? 0.1f : 0f);
                if (taskTimer > 3f && (corpse == null || corpse.Eat()))
                {
                    hunger = Mathf.Max(0f, hunger - (corpse != null ? 65f : 45f));
                    if (corpse != null && GameManager.Instance != null) lastFedDay = GameManager.Instance.CurrentDay;
                    FloatingText.Spawn(pos + Vector2.up * 0.7f, corpse != null ? "Miam !" : "Crr crr", new Color(1f, 0.75f, 0.5f), 0.8f);
                    Interrupt();
                    ai.ReturnToPost();
                    return false;
                }
                return true;

            case Task.Quarrel:
                if (foe == null || !foe.ai.IsAlive() || taskTimer > 4f)
                {
                    mood = Mathf.Max(0f, mood - 8f);
                    Interrupt();
                    ai.ReturnToPost();
                    return false;
                }
                if (Vector2.Distance(pos, foe.transform.position) > 0.9f) { ai.LifeGoTo(foe.transform.position); ai.LifeMove(1f); return true; }
                ai.LifeStop();
                ai.LifeFace(foe.transform.position);
                if (Mathf.Repeat(taskTimer, 1.1f) < Time.fixedDeltaTime)
                {
                    ai.LifeGesture(0.4f);
                    foe.ai.TakeDamage(foe.ai.maxHealth * 0.04f);   // une prise de bec, pas un massacre
                }
                return true;

            case Task.Desert:
                if (Vector2.Distance(pos, target) < 1f || taskTimer > 40f)
                {
                    Fx.Play("smoke_ring", pos, 1f);
                    Destroy(gameObject);
                    return true;
                }
                ai.LifeMove(1.1f);
                return true;
        }
        Interrupt();
        return false;
    }

    private static int LayerOf(Vector2 p) => DungeonLayers.Instance != null ? DungeonLayers.Instance.IndexAt(p) : 0;

    // ─────────────────────────────────────────────
    // FICHE
    // ─────────────────────────────────────────────

    public string Describe()
    {
        string m = mood > 70f ? "<color=#9CF0A8>heureux</color>" : mood > 40f ? "content" : mood > 22f ? "<color=#FFC266>maussade</color>" : "<color=#FF8A7A>malheureux</color>";
        string h = hunger > 85f ? "<color=#FF8A7A>affamé</color>" : hunger > 55f ? "<color=#FFC266>a faim</color>" : "repu";
        string e = Sleeping ? "dort" : energy < 20f ? "<color=#FFC266>épuisé</color>" : "en forme";
        var sb = new System.Text.StringBuilder($"Humeur : {m} · {h} · {e}");
        if (boredom > 70f) sb.Append(" · s'ennuie");
        if (traits.Count > 0)
        {
            sb.Append("\n<color=#E8C77F>");
            for (int i = 0; i < traits.Count; i++) sb.Append(i > 0 ? ", " : "").Append(TraitName(traits[i]));
            sb.Append("</color>");
        }
        string links = MonsterSociety.Links(this);
        if (!string.IsNullOrEmpty(links)) sb.Append("\n").Append(links);
        return sb.ToString();
    }
}

/// <summary>
/// Liens entre monstres : l'affinité monte entre voisins de la même famille (ou sociables), baisse entre familles
/// rivales (gobelins et gnolls, orcs et hommes-lézards, morts-vivants et vivants...). Amis : ≥ 60 ; couple : ≥ 90
/// (même famille) ; rivaux : ≤ -50.
/// </summary>
public static class MonsterSociety
{
    private static readonly Dictionary<long, float> affinity = new Dictionary<long, float>();
    private static float nextTick;

    private static long Key(MonsterLife a, MonsterLife b)
    {
        int x = a.GetInstanceID(), y = b.GetInstanceID();
        return x < y ? ((long)x << 32) ^ (uint)y : ((long)y << 32) ^ (uint)x;
    }

    public static float Affinity(MonsterLife a, MonsterLife b) => affinity.TryGetValue(Key(a, b), out float v) ? v : 0f;

    private static readonly HashSet<string> Undead = new HashSet<string> { "skeleton", "zombie", "ghost", "lich", "vampire" };

    private static float Drift(MonsterLife a, MonsterLife b)
    {
        string fa = a.GetComponent<MonsterUnit>()?.familyId ?? "", fb = b.GetComponent<MonsterUnit>()?.familyId ?? "";
        float d;
        if (fa == fb) d = 2f;
        else if ((fa == "goblin" && fb == "gnoll") || (fa == "gnoll" && fb == "goblin") || (fa == "orc" && fb == "lizard") || (fa == "lizard" && fb == "orc")
                 || (fa == "rat" && fb == "imp") || (fa == "imp" && fb == "rat")) d = -2.5f;
        else if (Undead.Contains(fa) != Undead.Contains(fb)) d = -0.8f;
        else d = 0.6f;
        if (d > 0f && (a.Has(MonsterLife.Trait.Sociable) || b.Has(MonsterLife.Trait.Sociable))) d *= 1.5f;
        if (a.Has(MonsterLife.Trait.Quarrelsome) || b.Has(MonsterLife.Trait.Quarrelsome)) d = d > 0f ? d * 0.4f : d * 2f - 0.5f;
        return d;
    }

    /// <summary>Appelé régulièrement : les voisins se lient (ou se détestent).</summary>
    public static void Tick()
    {
        if (Time.time < nextTick) return;
        nextTick = Time.time + 3f;
        var list = MonsterLife.All;
        for (int i = 0; i < list.Count; i++)
            for (int j = i + 1; j < list.Count; j++)
            {
                var a = list[i]; var b = list[j];
                if (a == null || b == null || Vector2.Distance(a.transform.position, b.transform.position) > 3.5f) continue;
                long k = Key(a, b);
                affinity.TryGetValue(k, out float v);
                float nv = Mathf.Clamp(v + Drift(a, b), -100f, 100f);
                affinity[k] = nv;
                // couple : deux inséparables de la même famille
                if (nv >= 90f && a.Partner == null && b.Partner == null &&
                    a.GetComponent<MonsterUnit>()?.familyId == b.GetComponent<MonsterUnit>()?.familyId)
                {
                    a.Partner = b; b.Partner = a;
                    var na = a.GetComponent<GobelinAI>().gobelinName; var nb = b.GetComponent<GobelinAI>().gobelinName;
                    GameHUD.Toast($"{na} et {nb} sont désormais inséparables", GameHUD.GoldColor);
                }
                if (v > -50f && nv <= -50f)
                    GameHUD.Toast($"{a.GetComponent<GobelinAI>().gobelinName} et {b.GetComponent<GobelinAI>().gobelinName} ne se supportent plus", GameHUD.WarningColor);
            }
    }

    public static int FriendsNear(MonsterLife me, float r, out bool partner)
    {
        partner = me.Partner != null && Vector2.Distance(me.Partner.transform.position, me.transform.position) <= r;
        int n = 0;
        foreach (var o in MonsterLife.All)
            if (o != null && o != me && Affinity(me, o) >= 60f && Vector2.Distance(o.transform.position, me.transform.position) <= r) n++;
        return n;
    }

    public static int RivalsNear(MonsterLife me, float r)
    {
        int n = 0;
        foreach (var o in MonsterLife.All)
            if (o != null && o != me && Affinity(me, o) <= -50f && Vector2.Distance(o.transform.position, me.transform.position) <= r) n++;
        return n;
    }

    public static MonsterLife RivalNear(MonsterLife me, float r)
    {
        foreach (var o in MonsterLife.All)
            if (o != null && o != me && Affinity(me, o) <= -50f && Vector2.Distance(o.transform.position, me.transform.position) <= r) return o;
        return null;
    }

    /// <summary>Un monstre meurt : ses amis le pleurent, son compagnon s'effondre (ou enrage).</summary>
    public static void OnDied(MonsterLife dead)
    {
        foreach (var o in MonsterLife.All)
        {
            if (o == null || o == dead) continue;
            float a = Affinity(dead, o);
            if (o.Partner == dead)
            {
                o.Partner = null;
                o.mood = Mathf.Max(0f, o.mood - 40f);
                FloatingText.Spawn(o.transform.position + Vector3.up * 0.8f, "Noooon !", new Color(0.8f, 0.85f, 1f), 1f);
            }
            else if (a >= 60f) o.mood = Mathf.Max(0f, o.mood - 15f);
            else if (a <= -50f) o.mood = Mathf.Min(100f, o.mood + 5f);
        }
    }

    public static string Links(MonsterLife me)
    {
        var sb = new System.Text.StringBuilder();
        if (me.Partner != null) sb.Append($"<color=#F2A0C0>Inséparable de {me.Partner.GetComponent<GobelinAI>().gobelinName}</color>  ");
        int friends = 0, rivals = 0;
        foreach (var o in MonsterLife.All)
        {
            if (o == null || o == me || o == me.Partner) continue;
            float a = Affinity(me, o);
            if (a >= 60f) friends++; else if (a <= -50f) rivals++;
        }
        if (friends > 0) sb.Append($"<color=#9CF0A8>{friends} ami{(friends > 1 ? "s" : "")}</color>  ");
        if (rivals > 0) sb.Append($"<color=#FF8A7A>{rivals} rival{(rivals > 1 ? "s" : "")}</color>");
        return sb.ToString();
    }
}
