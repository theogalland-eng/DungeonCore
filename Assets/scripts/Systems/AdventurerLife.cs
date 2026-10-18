using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LA VIE D'UN INTRUS : un caractère, un estomac, des humeurs.
///   - Traits (0 à 2) : ivrogne, dépressif, peureux, chanceux, bagarreur, glouton, courageux, cupide.
///     Ils se révèlent au joueur en observant l'intrus (quand le trait se manifeste, ou après un moment dans le donjon).
///   - Faim : chacun débarque avec quelques rations ; la faim monte avec le temps passé sur l'île. À sec, il cherche
///     un garde-manger (appât du Noyau), chasse le gibier et le fait cuire, ou finit par rentrer. Affamé, il frappe moins fort.
///   - Humeurs : l'ivrogne boit (téméraire mais maladroit, il titube), le dépressif traîne, abandonne ou se jette sur
///     un monstre, le peureux détale au premier coup, le chanceux esquive.
///   - Bagarres : alcool, partage du butin, nerfs à vif... une dispute peut tourner au drame (et le Noyau récupère le corps).
/// </summary>
[RequireComponent(typeof(AdventurerAI))]
public class AdventurerLife : MonoBehaviour
{
    public enum Trait { Drunkard, Depressive, Coward, Lucky, Brawler, Glutton, Brave, Greedy }

    public static string TraitName(Trait t) => t switch
    {
        Trait.Drunkard => "Ivrogne", Trait.Depressive => "Dépressif", Trait.Coward => "Peureux", Trait.Lucky => "Chanceux",
        Trait.Brawler => "Bagarreur", Trait.Glutton => "Glouton", Trait.Brave => "Courageux", _ => "Cupide",
    };

    public static Color TraitColor(Trait t) => t switch
    {
        Trait.Drunkard => new Color(0.95f, 0.7f, 0.35f), Trait.Depressive => new Color(0.6f, 0.65f, 0.85f),
        Trait.Coward => new Color(0.85f, 0.85f, 0.6f), Trait.Lucky => new Color(0.55f, 0.95f, 0.6f),
        Trait.Brawler => new Color(1f, 0.5f, 0.4f), Trait.Glutton => new Color(0.95f, 0.6f, 0.75f),
        Trait.Brave => new Color(1f, 0.82f, 0.45f), _ => new Color(1f, 0.85f, 0.3f),
    };

    public static string TraitHint(Trait t) => t switch
    {
        Trait.Drunkard => "boit : téméraire mais maladroit",
        Trait.Depressive => "traîne, peut abandonner ou se jeter sur un monstre",
        Trait.Coward => "fuit au premier coup",
        Trait.Lucky => "esquive souvent",
        Trait.Brawler => "cherche la bagarre",
        Trait.Glutton => "a toujours faim",
        Trait.Brave => "ne recule presque jamais",
        _ => "ne partage pas le butin",
    };

    /// <summary>Statistiques (tests) : chasses lancées, bagarres.</summary>
    public static int Hunts, Brawls;
    public readonly List<Trait> traits = new List<Trait>();
    public readonly HashSet<Trait> revealed = new HashSet<Trait>();
    [Range(0f, 100f)] public float hunger;
    public int rations;
    [Range(0f, 1f)] public float drunk;

    public bool Has(Trait t) => traits.Contains(t);

    /// <summary>Ce qu'il fait en ce moment (fiche de l'intrus), null s'il n'est pas occupé.</summary>
    public string Activity => task switch
    {
        Task.Eat => "mange une ration", Task.Drink => "boit un coup", Task.Hunt => "chasse",
        Task.Cook => "fait griller du gibier", Task.Brawl => brawlToDeath ? "se bat à mort !" : "se bagarre",
        Task.Bait => "se goinfre au garde-manger", Task.Sulk => "broie du noir",
        Task.Puzzle => "cherche le mécanisme de la porte", Task.Rest => "dort au camp", _ => null,
    };
    public bool Hungry => hunger >= 70f;
    public bool Starving => hunger >= 92f;

    private AdventurerAI ai;
    private AdventurerCombat combat;

    private enum Task { None, Eat, Drink, Hunt, Cook, Brawl, Bait, Sulk, Puzzle, Rest }
    private Task task;
    private float taskTimer, taskEnd, nextThink, nextStatTick, insideTime;
    private WildAnimal prey;
    private PuzzleSwitch puzzle;
    private float nextPuzzle, puzzleArrive = -1f;
    private FoodBait bait;
    private AdventurerLife opponent;
    private bool brawlToDeath;
    private float nextPunch, recklessUntil;
    private GameObject campfire;

    private void Awake()
    {
        ai = GetComponent<AdventurerAI>();
        combat = GetComponent<AdventurerCombat>();
    }

    private void Start()
    {
        RollTraits();
        int tier = ai.TierIndex;
        rations = Random.Range(1, 3) + (tier >= 3 ? 1 : 0) + (Has(Trait.Glutton) ? 1 : 0);
        hunger = Random.Range(5f, 40f);
        if (Has(Trait.Drunkard)) drunk = Random.Range(0.4f, 0.8f);   // il a bu avant de débarquer
        nextThink = Time.time + Random.Range(2f, 5f);
    }

    private void RollTraits()
    {
        if (traits.Count > 0) return;
        // la personnalité de base (arrogance) oriente le tirage
        var pool = new List<Trait> { Trait.Drunkard, Trait.Depressive, Trait.Lucky, Trait.Brawler, Trait.Glutton, Trait.Greedy };
        if (ai.arrogance > 0.6f) pool.Add(Trait.Brave); else if (ai.arrogance < 0.4f) pool.Add(Trait.Coward);
        else { pool.Add(Trait.Brave); pool.Add(Trait.Coward); }
        int n = Random.value < 0.25f ? 0 : Random.value < 0.7f ? 1 : 2;
        for (int i = 0; i < n && pool.Count > 0; i++)
        {
            var t = pool[Random.Range(0, pool.Count)];
            pool.Remove(t);
            traits.Add(t);
            if (t == Trait.Brave) pool.Remove(Trait.Coward);
            if (t == Trait.Coward) pool.Remove(Trait.Brave);
        }
    }

    /// <summary>Le trait se manifeste : le joueur le découvre (petite bulle colorée).</summary>
    public void Reveal(Trait t)
    {
        if (!Has(t) || !revealed.Add(t)) return;
        FloatingText.Spawn(transform.position + Vector3.up * 1.05f, TraitName(t), TraitColor(t), 0.85f);
    }

    // ─────────────────────────────────────────────
    // ÉTAT : FAIM, IVRESSE, MODIFICATEURS
    // ─────────────────────────────────────────────

    private void Update()
    {
        if (ai == null || ai.IsDead) return;
        // l'IA a quitté l'état « occupé » (attaquée, fuite...) : la tâche en cours est abandonnée
        if (task != Task.None && !ai.IsBusy) Interrupt();
        float dt = Time.deltaTime;
        var gm = GameManager.Instance;
        float day = gm != null ? gm.dayLengthSeconds : 180f;
        // la faim monte d'environ 100 par jour sur l'île (plus vite dans le donjon : on s'y dépense)
        hunger = Mathf.Min(100f, hunger + 100f / day * dt * (Has(Trait.Glutton) ? 1.6f : 1f) * (ai.IsInsideDungeon ? 1.25f : 1f) * Seasons.HungerMult);
        drunk = Mathf.Max(0f, drunk - dt * 0.012f);
        if (ai.IsInsideDungeon)
        {
            insideTime += dt;
            // à force de l'observer, on finit par le connaître
            if (insideTime > 25f && traits.Count > revealed.Count)
            {
                insideTime = 0f;
                foreach (var t in traits) if (!revealed.Contains(t)) { Reveal(t); break; }
            }
        }

        if (Time.time >= nextStatTick)
        {
            nextStatTick = Time.time + 0.5f;
            ApplyModifiers();
            // en titubant, l'ivrogne zigzague un peu
            if (drunk > 0.45f && !ai.IsBusy && Random.value < 0.25f) ai.Slow(0.6f, 0.4f);
        }

        if (Time.time >= nextThink && !ai.IsBusy)
        {
            nextThink = Time.time + Random.Range(1.2f, 2.2f);
            Think();
        }
    }

    private void ApplyModifiers()
    {
        float hungerMult = hunger >= 92f ? 0.7f : hunger >= 75f ? 0.85f : 1f;
        float drunkMult = 1f + drunk * 0.15f;              // il frappe fort... quand il touche
        combat.damageMult = hungerMult * drunkMult * (Has(Trait.Brave) ? 1.08f : 1f) * (Time.time < recklessUntil ? 1.25f : 1f);
        combat.missChance = drunk * 0.32f;
        combat.dodgeChance = (Has(Trait.Lucky) ? 0.18f : 0f) - drunk * 0.05f;
        ai.moodSpeedMult = (Has(Trait.Depressive) ? 0.85f : 1f) * (hunger >= 92f ? 0.85f : 1f) * (drunk > 0.4f ? 0.92f : 1f);
        float bias = 0f;
        if (Has(Trait.Coward)) bias += 0.3f;
        if (Has(Trait.Brave)) bias -= 0.35f;
        bias -= drunk * 0.3f;                              // courage liquide
        if (hunger >= 80f) bias += 0.12f;
        if (Time.time < recklessUntil) bias -= 1f;         // le dépressif qui se jette dans la mêlée
        ai.fleeBias = bias;
    }

    // ─────────────────────────────────────────────
    // DÉCISIONS
    // ─────────────────────────────────────────────

    private void Think()
    {
        if (!ai.CanBeBusy) return;
        Vector2 pos = transform.position;

        // la nuit, ceux qui sont dehors dorment au camp de l'expédition
        var gmn = GameManager.Instance;
        if (gmn != null && gmn.IsNight && !ai.IsInsideDungeon && hunger < 85f)
        {
            var camp = IntruderCamp.Of(ai);
            if (camp != null && Begin(Task.Rest, 9999f))
            {
                restCamp = camp;
                ai.LifeGoTo(camp.RestSpot(ai));
                ai.Speak("On campe ici pour la nuit.", new Color(0.85f, 0.85f, 1f));
                return;
            }
        }

        // manger une ration
        if (hunger >= 60f && rations > 0)
        {
            if (Begin(Task.Eat, 2.4f))
            {
                ai.Speak(Has(Trait.Glutton) ? "Encore une petite ration..." : "Une pause pour manger.", new Color(0.9f, 0.85f, 0.7f));
                if (Has(Trait.Glutton)) Reveal(Trait.Glutton);
            }
            return;
        }

        if (Hungry)
        {
            // un garde-manger en vue (piège du Noyau)
            var b = FoodBait.Nearest(pos, Has(Trait.Glutton) ? 14f : 9f, this);
            if (b != null && ai.LifeSees(b.transform) && Begin(Task.Bait, 25f))
            {
                bait = b; bait.Reserve(this);
                ai.LifeGoTo(b.EatSpot(pos));
                ai.Speak("De la nourriture !", new Color(1f, 0.85f, 0.55f), true);
                return;
            }
            if (!ai.IsInsideDungeon)
            {
                // une carcasse à cuire
                foreach (var c in WildAnimal.Carcasses)
                    if (c != null && c.Meat > 0 && Vector2.Distance(c.transform.position, pos) < 16f)   // on partage la carcasse
                    {
                        if (!Begin(Task.Cook, 30f)) return;
                        prey = c; c.reservedBy = this;
                        ai.LifeGoTo(c.transform.position);
                        return;
                    }
                // du gibier
                WildAnimal best = null;
                float bd = 22f;
                foreach (var a in WildAnimal.All)
                {
                    if (a == null || a.IsDead) continue;   // on chasse à plusieurs
                    float d = Vector2.Distance(a.transform.position, pos);
                    if (d < bd) { bd = d; best = a; }
                }
                if (best != null && Begin(Task.Hunt, 30f))
                {
                    Hunts++;
                    prey = best; best.reservedBy = this;
                    ai.LifeFollow(best.transform);
                    ai.Speak($"Un {WildAnimal.Name(best.species).ToLowerInvariant()} ! Le dîner.", new Color(0.9f, 0.85f, 0.7f), true);
                    return;
                }
            }
            if (hunger >= 98f && Random.value < 0.15f)
            {
                ai.Speak("Je meurs de faim... je rentre.", new Color(0.8f, 0.85f, 1f), true);
                ai.LifeLeave();
                return;
            }
            if (Random.value < 0.2f) ai.Speak("J'ai faim...", new Color(0.85f, 0.8f, 0.7f));
        }

        // l'ivrogne reprend une gorgée
        if (Has(Trait.Drunkard) && drunk < 0.25f && Random.value < 0.3f && Begin(Task.Drink, 1.8f))
        {
            ai.Speak("*glou glou*", new Color(0.95f, 0.75f, 0.4f), true);
            Reveal(Trait.Drunkard);
            return;
        }

        // le dépressif
        if (Has(Trait.Depressive) && Random.value < 0.05f)
        {
            float r = Random.value;
            if (r < 0.3f) { ai.Speak("À quoi bon...", new Color(0.65f, 0.7f, 0.9f), true); Reveal(Trait.Depressive); ai.LifeLeave(); return; }
            if (r < 0.65f && ai.LifeNearestMonster() != null)
            {
                recklessUntil = Time.time + 25f;
                ai.Embolden(25f);
                ai.Speak("Qu'on en finisse !", new Color(0.65f, 0.7f, 0.9f), true);
                Reveal(Trait.Depressive);
                return;
            }
            if (Begin(Task.Sulk, Random.Range(4f, 7f))) { ai.Speak("...", new Color(0.65f, 0.7f, 0.9f), true); Reveal(Trait.Depressive); }
            return;
        }

        // bagarre
        bool hotHead = Has(Trait.Brawler) || drunk > 0.55f;
        if (hotHead && Random.value < (Has(Trait.Brawler) ? 0.05f : 0.025f) * (1f + drunk))
        {
            var other = PickQuarrel(pos);
            if (other != null) StartBrawl(other, "Tu me cherches ?!");
        }
    }

    private AdventurerLife PickQuarrel(Vector2 pos)
    {
        var party = new List<AdventurerAI>();
        ai.Party(party);
        AdventurerLife best = null;
        float bd = 3.5f;
        foreach (var a in party)
        {
            if (a.Life == null || !a.CanBeBusy) continue;
            float d = Vector2.Distance(a.transform.position, pos);
            if (d < bd) { bd = d; best = a.Life; }
        }
        return best;
    }

    // ─────────────────────────────────────────────
    // TÂCHES (état « occupé » de l'IA)
    // ─────────────────────────────────────────────

    private bool Begin(Task t, float maxDuration)
    {
        if (!ai.BeginBusy()) return false;
        task = t;
        taskTimer = 0f;
        taskEnd = maxDuration;
        return true;
    }

    private void Finish()
    {
        if (prey != null && prey.reservedBy == this) prey.reservedBy = null;
        if (puzzle != null && puzzle.reservedBy == this) puzzle.reservedBy = null;
        puzzle = null;
        if (bait != null) bait.Release(this);
        if (campfire != null) Destroy(campfire);
        prey = null; bait = null; opponent = null; campfire = null;
        task = Task.None;
    }

    /// <summary>Appelé par l'IA à chaque pas tant qu'elle est « occupée ». false : la tâche est finie.</summary>
    public bool BusyTick()
    {
        taskTimer += Time.fixedDeltaTime;
        // un monstre surgit : on lâche tout (sauf au milieu d'une bagarre à mort... même là, on se retourne)
        var monster = ai.LifeNearestMonster();
        if (monster != null && task != Task.Sulk)
        {
            var o = opponent;
            Finish();
            if (o != null) o.StopBrawl();
            ai.LifeEngage(monster);
            return true;
        }
        if (taskTimer > taskEnd) { Finish(); return false; }

        switch (task)
        {
            case Task.Eat:
                ai.LifeStop();
                ai.LifeGesture(Mathf.Repeat(taskTimer, 0.8f) < 0.4f ? 0.1f : 0f);
                if (taskTimer >= 2.2f)
                {
                    rations--;
                    hunger = Mathf.Max(0f, hunger - 55f);
                    Finish();
                    return false;
                }
                return true;

            case Task.Drink:
                ai.LifeStop();
                ai.LifeGesture(0.1f);
                if (taskTimer >= 1.6f) { drunk = Mathf.Min(1f, drunk + 0.55f); Finish(); return false; }
                return true;

            case Task.Sulk:
                ai.LifeStop();
                return true;

            case Task.Bait: return TickBait();
            case Task.Hunt: return TickHunt();
            case Task.Cook: return TickCook();
            case Task.Brawl: return TickBrawl();
            case Task.Puzzle: return TickPuzzle();
            case Task.Rest: return TickRest();
        }
        Finish();
        return false;
    }

    private bool TickBait()
    {
        if (bait == null || bait.Portions <= 0) { Finish(); return false; }
        float d = Vector2.Distance(transform.position, bait.transform.position);
        if (d > 1.1f) { ai.LifeMove(1.1f); return true; }
        ai.LifeStop();
        ai.LifeFace(bait.transform.position);
        ai.LifeGesture(Mathf.Repeat(taskTimer, 0.7f) < 0.35f ? 0.1f : 0f);
        // il se goinfre, immobile et sans méfiance : le moment idéal pour une embuscade
        if (bait.EatTick(this, Time.fixedDeltaTime))
        {
            hunger = Mathf.Max(0f, hunger - 70f);
            ai.Speak("Délicieux...", new Color(1f, 0.85f, 0.55f), true);
            CoreVoice.Say("bait", "Ils mordent à l'appât. Les ventres pleins ne regardent jamais derrière eux.");
            Finish();
            return false;
        }
        return true;
    }

    private bool TickHunt()
    {
        if (prey == null) { Finish(); return false; }
        if (prey.IsDead)
        {
            // gibier abattu : on passe à la cuisson
            var p = prey;
            task = Task.Cook; taskTimer = 0f; taskEnd = 30f;
            prey = p; p.reservedBy = this;
            ai.LifeGoTo(p.transform.position);
            ai.Speak("Touché !", new Color(0.9f, 0.85f, 0.7f));
            return true;
        }
        float d = Vector2.Distance(transform.position, prey.transform.position);
        if (d > 24f) { Finish(); return false; }   // il s'est enfui
        if (d <= combat.attackRange && ai.LifeSees(prey.transform))
        {
            ai.LifeStop();
            ai.LifeFace(prey.transform.position);
            if (combat.CanAttack())
            {
                combat.lastAttackTime = Time.time;
                ai.LifeGesture(0.4f);
                if (Random.value >= combat.missChance) prey.TakeDamage(combat.attackDamage * combat.damageMult, transform);
                Sfx.Play(combat.role == AdventurerAI.Role.Archer ? "arrow_shoot" : "swing", transform.position, 0.6f);
            }
            return true;
        }
        ai.LifeMove(1.15f);
        return true;
    }

    private bool TickCook()
    {
        if (prey == null || prey.Meat <= 0) { Finish(); return false; }
        float d = Vector2.Distance(transform.position, prey.transform.position);
        if (d > 0.9f) { ai.LifeMove(); return true; }
        ai.LifeStop();
        ai.LifeFace(prey.transform.position);
        if (campfire == null) campfire = MakeCampfire((Vector2)prey.transform.position + Vector2.right * 0.4f);
        if (taskTimer >= 6f)
        {
            if (prey.TakeMeat())
            {
                hunger = Mathf.Max(0f, hunger - 80f);
                rations += prey.Meat > 0 && prey.TakeMeat() ? 1 : 0;   // il emporte une part pour plus tard
                ai.Speak("Rien ne vaut la viande grillée.", new Color(1f, 0.8f, 0.55f), true);
            }
            Finish();
            return false;
        }
        return true;
    }

    private static GameObject MakeCampfire(Vector2 at)
    {
        var go = new GameObject("Feu de camp");
        go.transform.position = at;
        var l = go.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        l.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        l.color = new Color(1f, 0.6f, 0.25f);
        l.intensity = 0.9f;
        l.pointLightOuterRadius = 2.5f;
        go.AddComponent<LavaGlow>();
        Fx.Play("smoke_rise", at, 1f);
        return go;
    }

    private IntruderCamp restCamp;

    private bool TickRest()
    {
        var gm = GameManager.Instance;
        if (restCamp == null || gm == null || !gm.IsNight) { Finish(); return false; }   // l'aube : on repart
        Vector2 spot = restCamp.RestSpot(ai);
        if (Vector2.Distance(transform.position, spot) > 0.5f) { ai.LifeMove(); return true; }
        ai.LifeStop();
        ai.LifeFace(restCamp.Center);
        // au coin du feu : on reprend des forces, la faim monte moins vite
        hunger = Mathf.Max(0f, hunger - Time.fixedDeltaTime * 0.25f);
        if (combat.currentHealth < combat.maxHealth) combat.currentHealth = Mathf.Min(combat.maxHealth, combat.currentHealth + combat.maxHealth * 0.01f * Time.fixedDeltaTime);
        if (Mathf.Repeat(taskTimer, 6f) < Time.fixedDeltaTime) FloatingText.Spawn(transform.position + Vector3.up * 0.7f, "Zzz", new Color(0.7f, 0.75f, 1f), 0.7f);
        return true;
    }

    // ─────────────────────────────────────────────
    // ÉNIGMES : porte scellée
    // ─────────────────────────────────────────────

    /// <summary>Bloqué devant une porte scellée : il part actionner un levier ou une rune (chacun le sien).</summary>
    public void OnSealedDoor(DungeonDoor door)
    {
        if (Time.time < nextPuzzle || task != Task.None || !ai.CanBeBusy) return;
        nextPuzzle = Time.time + 2f;
        var s = PuzzleSwitch.NearestIdle(door, transform.position, this);
        if (s == null) return;   // tout est déjà actionné : on attend que la porte s'ouvre
        if (!Begin(Task.Puzzle, 45f)) return;
        puzzle = s;
        puzzleArrive = -1f;
        s.reservedBy = this;
        ai.LifeGoTo(s.transform.position + Vector3.down * 0.6f);
        ai.Speak(door.sealSwitches >= 3 ? "Ces runes... il faut les éveiller." : "Un mécanisme ! Je vais chercher le levier.", new Color(0.8f, 0.75f, 1f), true);
    }

    private bool TickPuzzle()
    {
        if (puzzle == null) { Finish(); return false; }
        if (puzzle.IsActive) { Finish(); return false; }
        if (Vector2.Distance(transform.position, puzzle.transform.position) > 0.9f) { ai.LifeMove(1.05f); return true; }
        ai.LifeStop();
        ai.LifeFace(puzzle.transform.position);
        ai.LifeGesture(0.1f);
        if (puzzleArrive < 0f) puzzleArrive = taskTimer;
        if (taskTimer - puzzleArrive > 1.8f)
        {
            puzzle.Activate();
            Finish();
            return false;
        }
        return true;
    }

    // ─────────────────────────────────────────────
    // BAGARRES
    // ─────────────────────────────────────────────

    /// <summary>Déclenche une bagarre entre deux intrus (les deux arrêtent ce qu'ils faisaient).</summary>
    public bool StartBrawl(AdventurerLife other, string line)
    {
        if (other == null || other == this || !other.ai.CanBeBusy || !ai.CanBeBusy) return false;
        // à mort ? surtout quand l'alcool ou l'or s'en mêle
        bool lethal = Random.value < 0.3f + (drunk > 0.5f || other.drunk > 0.5f ? 0.3f : 0f) + (Has(Trait.Greedy) ? 0.1f : 0f);
        if (!Begin(Task.Brawl, 16f)) return false;
        if (!other.Begin(Task.Brawl, 16f)) { Finish(); ai.EndBusy(); return false; }
        opponent = other; other.opponent = this;
        Brawls++;
        CoreVoice.Say("brawl", lethal ? "Qu'ils s'entretuent. Moins de travail pour moi." : "Ils se battent entre eux... Délicieux.");
        brawlToDeath = other.brawlToDeath = lethal;
        ai.Speak(line, new Color(1f, 0.5f, 0.4f), true);
        other.ai.Speak(Random.value < 0.5f ? "Viens là !" : "Tu vas le regretter !", new Color(1f, 0.5f, 0.4f), true);
        if (Has(Trait.Brawler)) Reveal(Trait.Brawler);
        if (drunk > 0.5f) Reveal(Trait.Drunkard);
        return true;
    }

    /// <summary>Abandon de la tâche en cours (et de la bagarre : l'adversaire s'arrête aussi).</summary>
    public void Interrupt()
    {
        var o = opponent;
        Finish();
        if (o != null) o.StopBrawl();
    }

    private void StopBrawl()
    {
        if (task != Task.Brawl) return;
        opponent = null;
        Finish();
        ai.EndBusy();
    }

    private bool TickBrawl()
    {
        if (opponent == null || opponent.ai == null || opponent.ai.IsDead || opponent.task != Task.Brawl)
        {
            Finish();
            return false;
        }
        var oc = opponent.combat;
        float d = Vector2.Distance(transform.position, opponent.transform.position);
        if (d > 0.85f) { ai.LifeFollow(opponent.transform); ai.LifeMove(1.1f); return true; }
        ai.LifeStop();
        ai.LifeFace(opponent.transform.position);
        if (Time.time >= nextPunch)
        {
            nextPunch = Time.time + Random.Range(0.8f, 1.2f);
            ai.LifeGesture(0.4f);
            // à mains nues, sauf quand ça tourne mal : on sort les armes
            float dmg = combat.attackDamage * (brawlToDeath ? 0.8f : 0.4f) * combat.damageMult;
            if (Random.value < combat.missChance + 0.15f)
                FloatingText.Spawn(opponent.transform.position + Vector3.up * 0.5f, "Raté", new Color(0.75f, 0.72f, 0.8f), 0.8f);
            else
            {
                Sfx.Play("hit_flesh", opponent.transform.position, 0.6f);
                oc.TakeDamage(dmg);
            }
        }
        // fin de la bagarre : quelqu'un est à terre (ou mort)
        float pct = oc.maxHealth > 0f ? oc.currentHealth / oc.maxHealth : 0f;
        if (!brawlToDeath && pct < 0.25f)
        {
            var loser = opponent;
            loser.ai.Speak(Random.value < 0.5f ? "Ça va, ça va ! J'arrête !" : "Je m'en vais, bande de brutes !", new Color(0.8f, 0.85f, 1f), true);
            loser.StopBrawl();
            if (Random.value < 0.5f) loser.ai.LifeLeave();     // il quitte l'expédition
            Finish();
            return false;
        }
        return true;
    }

    // ─────────────────────────────────────────────
    // ÉVÉNEMENTS
    // ─────────────────────────────────────────────

    public void OnHurt(float amount)
    {
        if (amount <= 0f) return;
        if (Has(Trait.Coward) && !ai.IsFleeing() && Random.value < 0.5f)
        {
            ai.Speak("Au secours !", new Color(0.9f, 0.9f, 0.65f), true);
            Reveal(Trait.Coward);
            CoreVoice.Say("coward", "Celui-là tremble comme une feuille.");
            ai.LifePanic();
        }
        if (Has(Trait.Lucky) && Random.value < 0.15f) Reveal(Trait.Lucky);
    }

    /// <summary>Un intrus vient de piller un coffre : les cupides voisins réclament leur part.</summary>
    public static void OnLooted(AdventurerAI looter)
    {
        if (looter == null || looter.Life == null) return;
        foreach (var a in AdventurerAI.All)
        {
            if (a == null || a == looter || a.Life == null || a.Map != looter.Map) continue;
            if (!a.Life.Has(Trait.Greedy) || Vector2.Distance(a.transform.position, looter.transform.position) > 6f) continue;
            a.Life.Reveal(Trait.Greedy);
            if (Random.value < 0.45f) a.Life.StartBrawl(looter.Life, "Ce trésor est à moi !");
            else a.Speak("Et ma part ?!", new Color(1f, 0.85f, 0.3f), true);
            break;
        }
    }

    /// <summary>Un compagnon tombe : le peureux détale, le courageux enrage, le dépressif s'effondre.</summary>
    public static void OnDied(AdventurerAI dead)
    {
        if (dead == null) return;
        foreach (var a in AdventurerAI.All)
        {
            if (a == null || a == dead || a.IsDead || a.Life == null || a.Map != dead.Map) continue;
            if (Vector2.Distance(a.transform.position, dead.transform.position) > 7f) continue;
            var l = a.Life;
            if (l.Has(Trait.Coward) && Random.value < 0.6f) { a.Speak("On va tous mourir !", new Color(0.9f, 0.9f, 0.65f), true); l.Reveal(Trait.Coward); a.LifePanic(); }
            else if (l.Has(Trait.Brave)) { a.Speak("Vengeance !", new Color(1f, 0.8f, 0.45f), true); l.Reveal(Trait.Brave); a.Embolden(15f); }
            else if (l.Has(Trait.Depressive) && Random.value < 0.4f) { a.Speak("C'est fini pour nous...", new Color(0.65f, 0.7f, 0.9f), true); l.Reveal(Trait.Depressive); a.LifeLeave(); }
        }
    }

    /// <summary>Texte de la fiche d'un intrus (traits découverts, faim, provisions).</summary>
    public string Describe()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var t in traits)
        {
            if (revealed.Contains(t)) sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(TraitColor(t))}><b>{TraitName(t)}</b></color> <size=80%><color=#A59FB6>{TraitHint(t)}</color></size>\n");
            else sb.Append("<color=#6F6A80>Trait inconnu (observez-le)</color>\n");
        }
        if (traits.Count == 0) sb.Append("<color=#A59FB6>Rien de remarquable</color>\n");
        string h = hunger >= 92f ? "<color=#FF8A7A>affamé</color>" : hunger >= 70f ? "<color=#FFC266>a faim</color>" : hunger >= 40f ? "rassasié" : "repu";
        sb.Append($"Faim : {h} · rations : {rations}");
        if (drunk > 0.3f) sb.Append(" · <color=#F2B860>ivre</color>");
        return sb.ToString();
    }
}
