using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// SORTS DU NOYAU (barre de sorts du HUD), payés en mana, avec un temps de recharge :
///   - Terreur (niv. 2) : une vague d'effroi traverse le donjon ; les intrus figent, les plus faibles détalent.
///   - Soin des créatures (niv. 3) : tous vos monstres récupèrent 40 % de leurs PV.
///   - Éboulement (niv. 5) : la voûte s'effondre sur le plus grand groupe d'intrus (dégâts, étourdissement).
///   - Ombres aveuglantes (niv. 7) : les torches meurent, les intrus voient deux fois moins loin pendant 20 s.
/// Et le CRISTAL GRANDIT avec le niveau du Noyau : plus grand, plus lumineux, de couleur changeante (bleu, violet, or),
/// entouré d'éclats qui gravitent.
/// </summary>
public class CoreSpells : MonoBehaviour
{
    public static CoreSpells Instance { get; private set; }

    public class Spell
    {
        public string id, name, icon, desc;
        public int cost, level;
        public float cooldown, readyAt;
    }

    public readonly List<Spell> spells = Defs();

    /// <summary>Les sorts (définitions neuves, sans recharge).</summary>
    public static List<Spell> Defs() => new List<Spell>
    {
        new Spell { id = "fear", name = "Terreur", icon = "skull", cost = 60, level = 2, cooldown = 45f, desc = "Les intrus du donjon figent ; les plus faibles détalent." },
        new Spell { id = "heal", name = "Soin", icon = "heart", cost = 50, level = 3, cooldown = 40f, desc = "Vos monstres récupèrent 40 % de leurs PV." },
        new Spell { id = "cavein", name = "Éboulement", icon = "rock", cost = 90, level = 5, cooldown = 60f, desc = "La voûte s'effondre sur le plus grand groupe d'intrus." },
        new Spell { id = "shadow", name = "Ombres", icon = "moon", cost = 70, level = 7, cooldown = 60f, desc = "Torches éteintes, vue des intrus réduite de moitié (20 s)." },
    };

    private void Awake() { Instance = this; }

    private void Start()
    {
        if (CrawlerMode.Active) return;
        var core = FindFirstObjectByType<DungeonCore>();
        if (core != null && core.GetComponent<CoreForm>() == null) core.gameObject.AddComponent<CoreForm>();
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public static int Level => PlayerLevel.Instance != null ? PlayerLevel.Instance.currentLevel : 1;

    public bool CanCast(Spell s, out string why)
    {
        why = null;
        if (Level < s.level) { why = $"Niv. {s.level}"; return false; }
        if (Time.time < s.readyAt) { why = $"{Mathf.CeilToInt(s.readyAt - Time.time)} s"; return false; }
        if (ManaSystem.Instance != null && !ManaSystem.Instance.CanAfford(CostOf(s))) { why = $"{CostOf(s)} mana"; return false; }
        return true;
    }

    public float Cooldown01(Spell s) => Time.time >= s.readyAt ? 0f : Mathf.Clamp01((s.readyAt - Time.time) / s.cooldown);

    public void Cast(Spell s)
    {
        if (!CanCast(s, out string why)) { GameHUD.Toast($"{s.name} : {why}", GameHUD.WarningColor); return; }
        bool ok = s.id switch { "fear" => Fear(), "heal" => Heal(), "cavein" => CaveIn(), "shadow" => Shadows(), _ => false };
        if (!ok) return;
        ManaSystem.Instance?.TrySpend(CostOf(s));
        s.readyAt = Time.time + s.cooldown;
    }

    /// <summary>Prix réel d'un sort (talent « Cauchemars »).</summary>
    public static int CostOf(Spell s) => Mathf.RoundToInt(s.cost * Talents.SpellCostMult);

    private static List<AdventurerAI> Inside()
    {
        var l = new List<AdventurerAI>();
        foreach (var a in AdventurerAI.All) if (a != null && !a.IsDead && a.IsInsideDungeon && !a.Spared) l.Add(a);
        return l;
    }

    private bool Fear()
    {
        var list = Inside();
        if (list.Count == 0) { GameHUD.Toast("Aucun intrus dans le donjon", GameHUD.InfoColor); return false; }
        CameraController.Instance?.Shake(0.12f, 0.6f);
        Sfx.Play("growl", null, 1f);
        int fled = 0;
        foreach (var a in list)
        {
            bool coward = a.Life != null && a.Life.Has(AdventurerLife.Trait.Coward);
            if (Random.value < (coward ? 0.8f : 0.3f)) { a.Speak("Fuyez !", new Color(0.85f, 0.85f, 1f), true); a.LifePanic(); fled++; }
            else { a.Hold(1.5f); a.Speak("Qu'est-ce que... ?", new Color(0.85f, 0.85f, 1f), true); }
        }
        CoreVoice.Say("fear", fled > 0 ? "Sentez-vous ce froid dans vos os ?" : "Ils tremblent... mais restent. Courageux. Pour l'instant.");
        return true;
    }

    private bool Heal()
    {
        int n = 0;
        foreach (var g in GobelinAI.All)
        {
            if (g == null || !g.IsAlive() || g.currentHealth >= g.maxHealth) continue;
            g.Heal(g.maxHealth * 0.4f);
            Fx.Play("heal", g.transform.position, 1.1f, 14f, 16, null, false, g.transform, new Color(0.45f, 0.75f, 1f));
            n++;
        }
        if (n == 0) { GameHUD.Toast("Vos monstres sont en pleine forme", GameHUD.InfoColor); return false; }
        Sfx.Play("heal", null, 0.9f);
        CoreVoice.Say("heal", "Relevez-vous, mes enfants.");
        return true;
    }

    private bool CaveIn()
    {
        var list = Inside();
        AdventurerAI best = null;
        int bestN = 0;
        foreach (var a in list)
        {
            int n = 0;
            foreach (var b in list) if (Vector2.Distance(a.transform.position, b.transform.position) < 2.2f) n++;
            if (n > bestN) { bestN = n; best = a; }
        }
        if (best == null) { GameHUD.Toast("Aucun intrus dans le donjon", GameHUD.InfoColor); return false; }
        Vector2 at = best.transform.position;
        CameraController.Instance?.Shake(0.3f, 0.8f);
        Sfx.Play("explosion", at, 1f);
        Fx.Play("smoke_ring", at, 2.6f, 14f, 30, new Color(0.6f, 0.55f, 0.5f));
        for (int i = 0; i < 6; i++) Fx.Play("smoke_rise", at + Random.insideUnitCircle * 1.5f, 1.2f);
        int hit = 0;
        foreach (var a in list)
        {
            if (Vector2.Distance(a.transform.position, at) > 2.2f || a.combat == null) continue;
            a.combat.TakeDamage(35f + a.combat.maxHealth * 0.1f);
            a.Hold(2f);
            hit++;
        }
        CoreVoice.Say("cavein", hit > 1 ? "La montagne vous écrase. Elle m'obéit." : "Un seul ? La montagne est gourmande.");
        return true;
    }

    private bool Shadows()
    {
        var list = Inside();
        if (list.Count == 0) { GameHUD.Toast("Aucun intrus dans le donjon", GameHUD.InfoColor); return false; }
        foreach (var a in list)
        {
            a.TorchWet = true;
            a.BlindUntil = Time.time + 20f;
            a.Speak("Je n'y vois plus rien !", new Color(0.8f, 0.8f, 0.95f));
        }
        Sfx.Play("growl", null, 0.6f);
        CoreVoice.Say("shadow", "Les ténèbres sont ma maison. Elles vous dévorent.");
        return true;
    }
}

/// <summary>
/// La voix du Noyau : il commente ce qu'il voit, à mi-voix, dans le fil des messages (jamais trop souvent).
/// </summary>
public static class CoreVoice
{
    private static readonly Dictionary<string, float> lastByKey = new Dictionary<string, float>();
    private static float last = -999f;

    public static void Say(string key, string text, float keyCooldown = 90f)
    {
        if (CrawlerMode.Active) return;
        float t = Time.time;
        if (t - last < 10f) return;
        if (lastByKey.TryGetValue(key, out float k) && t - k < keyCooldown) return;
        last = t;
        lastByKey[key] = t;
        GameHUD.Toast($"<i>« {text} »</i>", new Color(0.72f, 0.66f, 1f));
    }
}
