using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// INTRO — d'après le premier chapitre de « The Dungeon Without a System », adapté à l'île :
/// réveil à la dérive, échouage sur la plage du futur ponton, « Statut ? », apprendre à respirer le mana,
/// la mouette, le survol de l'île, la fissure du volcan, la grotte qui devient un corps.
/// Pas de système qui parle : le HUD, ce sont les sens du Noyau ; chaque élément apparaît quand il le découvre.
/// </summary>
public partial class CinematicDirector
{
    public void PlayIntro(Action onEnd) => StartCoroutine(IntroRoutine(onEnd));

    /// <summary>Pas de temps des effets : une cinématique en pause continue de les animer.</summary>
    public static float Dt => Time.timeScale == 0f && Instance != null && Instance.IsPlaying ? Time.unscaledDeltaTime : Time.deltaTime;

    // ── Lumière (le jour, le coucher de soleil, les nuits qui défilent) ──
    private static readonly Color DayC = Color.white, SunsetC = new Color(1f, 0.6f, 0.42f),
        NightC = new Color(0.36f, 0.44f, 0.82f), DawnC = new Color(1f, 0.78f, 0.66f), CaveC = new Color(0.5f, 0.55f, 0.75f);
    private Light2D globalLight;

    // ── Acteurs ──
    private readonly List<GameObject> introTemp = new List<GameObject>();
    private bool introRunning;
    private Material unlitMat;
    private Transform orb;
    private SpriteRenderer orbSr;
    private Light2D orbLight;
    private Vector3 orbBase, orbScale = Vector3.one;
    private float orbBob, orbGlow = 1f, orbPuff = 1f, orbLightBase = 0.7f;
    private bool orbHeld;

    private Transform gull;
    private SpriteRenderer gullSr, gullShadow;
    private Light2D gullLight;
    private Sprite[] gullFrames;
    private Vector3 gullPos;
    private float gullAlt, gullHeading;
    private int gullPose = -1, gullWp;          // pose : -1 en vol, 3 se débat, 4 écrasée, 5 posée
    private bool gullGlide, gullStop, gullTinted;

    private Transform camFollow;
    private float camFollowSize;

    private class Mote { public Transform t; public SpriteRenderer sr; public int state; public float ang, rad, speed, seed; public Vector3 vel; }
    private readonly List<Mote> motes = new List<Mote>();
    private float motePull;                     // > 0 : l'inspiration attire les poussières

    private struct Geo
    {
        public Vector3 sea, beach, sails, entrance, outside, core, crater;
        public Vector2 seaDir;
        public List<Vector3> tunnel;            // de l'entrée jusqu'au cristal
        public List<(Vector3 pos, int dist, bool horizontal)> walls;
    }

    private IEnumerator IntroRoutine(Action onEnd)
    {
        Begin();
        introRunning = true;
        GameManager.Instance?.PushPause();
        var core = FindFirstObjectByType<DungeonCore>();
        var coreSr = core != null ? core.GetComponent<SpriteRenderer>() : null;
        Vector3 corePos = core != null ? core.transform.position : Vector3.zero;
        Vector3 coreScale = core != null ? core.transform.localScale : Vector3.one;
        Light2D coreLight = core != null ? core.GetComponentInChildren<Light2D>() : null;
        float coreLightI = coreLight != null ? coreLight.intensity : 0f;
        if (core != null) { core.cinematicControl = true; core.transform.localScale = Vector3.zero; }
        if (coreLight != null) coreLight.intensity = 0f;
        var mana = ManaSystem.Instance;
        float mana0 = mana != null ? mana.Raw : 150f;
        float manaMax = mana != null ? mana.Max : 150f;

        globalLight = null;
        foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            if (l.lightType == Light2D.LightType.Global) { globalLight = l; break; }
        GameManager.LightingOverride = true;
        if (globalLight != null) { globalLight.color = DayC; globalLight.intensity = 1f; }

        var geo = ComputeGeo(corePos);
        var unlit = Shader.Find("Sprites/Default");
        unlitMat = unlit != null ? new Material(unlit) : null;
        gullFrames = Fx.Frames("gull");
        CreateOrb(coreSr, geo.sea);
        StartCoroutine(OrbLoop());
        StartCoroutine(CamLoop());

        // ══ 1. RÉVEIL EN MER ══
        MountainRelief.Instance?.SetUserView(false);
        ForceDungeonView = false;
        fade.color = Color.black;
        topBar.sizeDelta = bottomBar.sizeDelta = new Vector2(0f, 120f);
        orbBob = 1f;
        SetCam(geo.sea + new Vector3(0f, 0.25f, 0f), 3f);
        SpawnFish(geo.sea);
        yield return Fade(1f, 0f, 3f);
        yield return Thought("...Je flotte.", 1.4f);
        yield return Thought("C'est agréable. Un balancement lent, comme dans une barque... Je n'ai pas envie de me réveiller.", 1.6f);
        yield return Thought("Une barque ? Non. Je me suis couché dans mon lit hier soir. Je m'en souviens très bien.", 1.6f);
        yield return Thought("Je prends une grande inspiration pour me réveiller pour de bon...", 0.8f);
        yield return Wait(1.4f);
        StartCoroutine(OrbPanic(3f));
        camCtrl?.Shake(0.05f, 0.4f);
        yield return Thought("...Ma poitrine ne bouge pas. Je ne respire pas.", 1f);
        camCtrl?.Shake(0.1f, 0.5f);
        yield return Thought("Je ne respire pas !", 1f);
        StartCoroutine(MoveCamera(Vector3.Lerp(geo.sea, geo.beach, 0.6f) - (Vector3)geo.seaDir * 4f, 13f, 5f));
        yield return Thought("De l'eau partout. Une île, juste là. Des poissons sous moi... Je les vois. Je vois dans toutes les directions en même temps.", 2.2f);
        yield return Thought("C'est un rêve. Je cligne des yeux et je me réveille.", 1.2f);
        yield return Wait(0.8f);
        yield return Thought("...Je n'arrive pas à cligner. Je ne sens pas mes bras. Je ne sens rien du tout.", 1.6f);
        StartCoroutine(MoveCamera(geo.sea + new Vector3(0f, 0.12f, 0f), 1.8f, 3f));
        yield return Thought("Il n'y a que ça. Une boule de cristal bleu, à moitié dans l'eau.", 1.8f);
        yield return Thought("C'est moi. Je suis cette chose.", 2.2f);

        // ══ 2. ÉCHOUÉ — « STATUT ? » ══
        StartCoroutine(MoveCamera(geo.beach + new Vector3(0f, 0.8f, 0f), 6f, 7f));
        StartCoroutine(DriftOrb(geo.sea, geo.beach, 7f));
        yield return Caption("La marée pousse la sphère vers une plage de sable noir, au pied du volcan.", 4.2f);
        StartCoroutine(LightTo(SunsetC, 0.85f, 5f));
        yield return Thought("Je me suis endormi chez moi, et je me réveille en caillou. Je suis mort dans mon sommeil ? C'est tout ?", 2.2f);
        yield return Thought("Un cristal, ça n'a pas de cerveau. Pas de nerfs. Alors avec quoi je pense ? Avec quoi je vois ?", 2f);
        StartCoroutine(LightTo(NightC, 0.42f, 5f));
        StartCoroutine(OrbLightTo(1.3f, 4f));
        yield return Caption("Le soleil se couche. Personne ne vient.", 2.4f);
        yield return Thought("Attends. Un cristal conscient, sur une île perdue... Je connais ça. Je l'ai lu des dizaines de fois.", 1.8f);
        yield return Thought("Un cœur de donjon. Une pierre vivante qui se nourrit de mana, creuse des galeries et les remplit de monstres pour se protéger.", 2.4f);
        yield return Thought("Si c'est ça, il doit y avoir un système. Une interface. Quelque chose.", 1.6f);
        MusicManager.Silence = true;
        yield return Thought("Statut ?", 2.6f);
        yield return Thought("...Statut. Menu. Aide.", 1.6f);
        yield return Wait(0.8f);
        yield return Thought("...Merde.", 1.8f);
        MusicManager.Silence = false;
        yield return Thought("Personne pour m'expliquer. D'accord. On se calme.", 1.4f);
        yield return Thought("Il n'y a rien à lire. Mais si je me concentre sur moi-même... je sens des choses.", 1.2f);
        mana?.SetRaw(0f);
        GameHUD.Instance?.IntroHud(true);
        StartCoroutine(TopBarTo(0f, 1f));
        StartCoroutine(HudIn("coeur", 1.6f));
        StartCoroutine(OrbBreath(1, 2.4f, 0.12f));
        yield return Thought("Ma solidité, d'abord. Pas une fêlure. Tant que je reste entier, je vis. Si je me brise... je préfère ne pas y penser.", 2.4f);

        // ══ 3. RESPIRER ══
        StartCoroutine(MoveCamera(geo.beach + new Vector3(0f, 0.4f, 0f), 3.4f, 3f));
        yield return Thought("Je vois sans yeux. Donc j'utilise déjà quelque chose. Du mana ?", 1.4f);
        yield return Thought("Je n'ai plus de poumons, mais l'idée de respirer est toujours là. J'essaie. Inspirer... expirer.", 0.6f);
        yield return OrbBreath(2, 2.8f, 0.1f);                    // rien ne vient
        SpawnFreeMotes(geo.beach, 36);
        StartCoroutine(MoteLoop());
        yield return Breathe(2, mana, manaMax * 0.15f);
        yield return Thought("...Là. Des points de lumière, sans couleur. Ils se rapprochent quand j'inspire.", 0.4f);
        StartCoroutine(HudIn("mana", 1.4f));
        yield return Breathe(2, mana, manaMax * 0.3f);
        yield return Thought("Et ceux que j'ai avalés, je peux les renvoyer. Les diriger. Mal, mais je peux.", 1.6f);
        // le temps passe : les jours et les nuits défilent, les orbites deviennent des anneaux
        var cycles = StartCoroutine(DayNightCycles(4, 2.2f));
        StartCoroutine(GrowRings(geo.beach, 9f, mana, manaMax));
        yield return Caption("Des jours passent. Peut-être deux semaines. La sphère ne fait rien d'autre que respirer.", 2.6f);
        yield return Thought("La nuit, le mana vient tout seul. Le jour, il faut forcer.", 2f);
        yield return Thought("Et je ne peux pas tout garder. Au-delà d'un certain point, ça déborde et ça m'échappe.", 0.4f);
        StartCoroutine(Overflow(8));
        yield return Wait(1.6f);
        if (cycles != null) StopCoroutine(cycles);   // nulle si elle s'est déjà terminée (cinématique passée)
        StartCoroutine(LightTo(DawnC, 0.8f, 2.5f));
        yield return Thought("Des anneaux tout autour de moi. C'est beau... et ça ne sert à rien. Je les fais tourner, c'est tout.", 1.8f);
        StartCoroutine(MoveCamera(geo.beach + new Vector3(0f, 1.6f, 0f), 6.5f, 2f));
        yield return Thought("Je ne vais pas passer l'éternité à tourner en rond sur une plage !", 0.6f);

        // ══ 4. LA MOUETTE ══
        // elle traverse le ciel au moment où tout part : le mana l'atteint en plein vol
        Vector3 gullFrom = geo.beach + new Vector3(-10f, 3.4f, 0f), gullTo = geo.beach + new Vector3(10f, 3.6f, 0f);
        CreateGull(gullFrom, 1f, 90f - 180f);
        gullStop = false;
        StartCoroutine(GullFly(new List<Vector3> { gullTo }, 6f, 1f, false));
        yield return Thought("...ASSEZ !", 0.2f);
        yield return Blast(geo.beach, mana);
        // la poussière qui la vise
        Vector3 hit = GullVisual() + (Vector3)(Forward(gullHeading) * 6f * 0.35f);
        yield return ShootMote(geo.beach, hit, 0.35f);
        gullStop = true;
        Fx.Play("shine", hit, 1f, 18f, 82, new Color(0.7f, 0.9f, 1f));
        Sfx.Play("hit_claw_1", hit, 0.5f);
        yield return GullFall(geo.beach + new Vector3(1.4f, 0.5f, 0f));
        StartCoroutine(LightTo(DayC, 1f, 4f));
        StartCoroutine(OrbLightTo(0.7f, 3f));
        StartCoroutine(MoveCamera(Vector3.Lerp(geo.beach, gullPos, 0.5f) + new Vector3(0f, 0.3f, 0f), 2.6f, 2f));
        yield return Caption("Une mouette passait par là. Mauvais moment.", 1.6f);
        yield return Thought("Non, non... je ne voulais pas...", 1.2f);
        StartCoroutine(PossessGull(geo.beach, 4f));
        yield return Thought("Elle est vivante. Je la sens. Il y a du mana en elle aussi, chaud, qui circule partout... et le mien est en train de le remplacer.", 2.4f);
        yield return Thought("Je pourrais tout prendre. Je sens que ça me ferait grandir.", 1.8f);
        yield return Thought("...Non. Pas comme ça.", 1.4f);
        gullPose = 5;
        gullHeading = 0f;
        Fx.Play("smoke_circle", gullPos, 0.8f, 16f, 30, new Color(0.3f, 0.3f, 0.32f, 0.7f));
        yield return Thought("Mais si mon mana est en elle... est-ce qu'elle m'obéit ? Vole.", 1f);

        // ══ 5. SURVOL DE L'ÎLE ══
        Vector3 crater = geo.crater;
        Vector3 toCrater = (crater - geo.beach).normalized;
        Vector3 side = new Vector3(-toCrater.y, toCrater.x, 0f);
        var flight = new List<Vector3>
        {
            geo.beach + toCrater * 3f + side * 1.5f,
            Vector3.Lerp(geo.beach, crater, 0.45f) + side * 5f,
            crater + new Vector3(-3.5f, -2.5f, 0f),
            crater + new Vector3(2.5f, -1f, 0f),
            crater + new Vector3(2f, 3f, 0f),
            crater + new Vector3(-3f, 2f, 0f),
            Vector3.Lerp(crater, geo.outside, 0.5f) - side * 2f,
            geo.outside + side * 1.5f,
            geo.beach - (Vector3)geo.seaDir * 6f + side * 4f,
            geo.beach + (Vector3)geo.seaDir * 3f + side * 2f,
            geo.beach + new Vector3(0f, -0.55f, 0f),
        };
        var sails = SpawnSails(geo.sails);
        StartCoroutine(ManaTo(mana, manaMax, 14f));
        camFollow = gull;
        camFollowSize = 11f;
        gullStop = false;
        StartCoroutine(GullFly(flight, 9f, 1f, true));
        yield return Thought("Je vois à travers ses yeux. Je sens le vent sous ses ailes... Je vole.", 1.6f);
        camFollowSize = 15f;
        while (!skip && gullWp < 2) yield return null;
        yield return Thought("Une seule plage. De la jungle partout. Et le volcan au milieu, qui fume. Il est vivant, lui aussi.", 2f);
        while (!skip && gullWp < 7) yield return null;
        camFollowSize = 12f;
        while (!skip && gullWp < 9) yield return null;
        // la mouette regarde vers le large : des voiles
        camFollow = null;
        StartCoroutine(MoveCamera(Vector3.Lerp(geo.beach, geo.sails, 0.6f), 9f, 2f));
        yield return Thought("Des voiles, là-bas. Il y a des humains, quelque part de l'autre côté.", 1.6f);
        camFollow = gull;
        camFollowSize = 6f;
        yield return Thought("S'ils accostent, la première chose qu'ils trouveront, c'est une pierre précieuse posée sur le sable. Je ne peux pas rester là.", 1.4f);
        while (!skip && gullPose != 5) yield return null;
        camFollowSize = 5f;
        gullHeading = 0f;
        orbHeld = true;
        orbBob = 0f;
        yield return Wait(0.5f);
        // vers la fissure : par la jungle, puis dans le tunnel
        var toCave = new List<Vector3>
        {
            geo.beach - (Vector3)geo.seaDir * 4f,
            Vector3.Lerp(geo.beach, geo.outside, 0.5f) + side * 2f,
            geo.outside,
        };
        camFollowSize = 10f;
        StartCoroutine(GullFly(toCave, 9f, 0.8f, false));
        yield return Thought("Il me faut un abri... Cette fissure, dans le flanc de la montagne.", 1.2f);
        while (!skip && gullWp < toCave.Count) yield return null;
        ForceDungeonView = true;
        camFollowSize = 4.5f;
        var inside = new List<Vector3>(geo.tunnel);
        Vector3 drop = corePos + new Vector3(0.04f, -0.36f, 0f) * coreScale.y;
        inside[inside.Count - 1] = drop + new Vector3(0f, -0.5f, 0f);
        yield return GullDirect(inside, 5f, 0.35f);
        gullHeading = 0f;
        yield return DropOrb(drop);
        yield return Thought("Doucement... Voilà. Tu peux partir. Merci.", 1f);
        var back = new List<Vector3>(geo.tunnel);
        back.Reverse();
        back.Add(geo.outside + (geo.outside - geo.entrance).normalized * 3f);
        camFollow = null;
        StartCoroutine(GullLeave(back));

        // ══ 6. DEVENIR LA GROTTE ══
        StartCoroutine(LightTo(CaveC, 0.18f, 2.5f));
        StartCoroutine(OrbLightTo(1.6f, 2.5f));
        StartCoroutine(MoveCamera(orb.position, 3.2f, 2.5f));
        yield return Thought("Sur la plage, mon mana s'était accroché à la roche. Ici, il n'y a que de la roche.", 1.6f);
        yield return Thought("J'expire. Lentement. Dans le sol, dans les parois...", 0.6f);
        StartCoroutine(OrbBreath(1, 3f, -0.08f));
        StartCoroutine(ManaTo(mana, manaMax * 0.55f, 3f));
        StartCoroutine(MoveCamera(Vector3.Lerp(corePos, geo.entrance, 0.4f), 5.5f, 4f));
        yield return Ripple(geo);
        yield return Thought("...Oh.", 1.2f);
        yield return Thought("Je sens la pierre comme je sentais la mouette. Le sol, les murs, le tunnel jusqu'à l'entrée. Tout ça, c'est moi.", 2.4f);
        StartCoroutine(MoveCamera(corePos + new Vector3(0f, 0.2f, 0f), 2.8f, 2f));
        yield return Thought("Et si c'est mon corps, je peux le bouger.", 1f);
        yield return RaisePedestal(coreSr, corePos, coreScale, mana, manaMax);
        if (core != null) core.transform.localScale = coreScale;
        if (coreLight != null) coreLight.intensity = coreLightI * 1.4f;
        if (orb != null) orb.gameObject.SetActive(false);
        yield return Thought("Un socle. Pour ne plus traîner par terre.", 1.6f);
        yield return Thought("...C'est épuisant. Chaque chose que je façonne me vide.", 2f);

        // ══ 7. LE PREMIER SOUFFLE ══
        yield return Thought("Alors je respire. Pas seulement avec moi. Avec toute la grotte.", 0.6f);
        StartCoroutine(MoveCamera(Vector3.Lerp(corePos, geo.entrance, 0.45f), 5.5f, 3f));
        StartCoroutine(LightTo(DayC, 1f, 5f));
        StartCoroutine(ManaTo(mana, Mathf.Max(mana0, manaMax * 0.9f), 4.5f));
        yield return CaveBreath(geo, corePos, 4.5f);
        if (coreLight != null) StartCoroutine(Pulse(coreLight, coreLightI, 2.5f, 1.6f));
        yield return Thought("C'est tellement plus fort...", 1.4f);
        StartCoroutine(MoveCamera(corePos, 6.5f, 3f));
        StartCoroutine(HudIn("xp", 1.4f));
        yield return Thought("Pour l'instant, je ne suis qu'un trou dans une montagne. Mais je peux grandir.", 2f);
        StartCoroutine(HudIn("jour", 1.4f));
        yield return Caption("Jour 1. Sous la montagne, quelque chose respire.", 2.4f);
        EvolutionFx.Play(corePos, new Color(0.55f, 0.85f, 1f), 1.6f);
        yield return Thought("Si je dois être un donjon... autant être un bon donjon.", 2.6f);
        caption.text = "";
        yield return Bars(false);

        // ── état final garanti (même si on a passé) ──
        introRunning = false;
        foreach (var go in introTemp) if (go != null) Destroy(go);
        introTemp.Clear();
        motes.Clear();
        if (sails != null) Destroy(sails);
        orb = null; gull = null;
        MusicManager.Silence = false;
        GameManager.LightingOverride = false;
        GameHUD.Instance?.IntroHud(false);
        mana?.SetRaw(mana0);
        if (core != null) { core.transform.localScale = coreScale; core.cinematicControl = false; }
        if (coreLight != null) coreLight.intensity = coreLightI;
        ForceDungeonView = true;
        if (cam != null) { cam.transform.position = new Vector3(corePos.x, corePos.y, cam.transform.position.z); cam.orthographicSize = 8f; }
        End();
        GameManager.Instance?.PopPause();
        onEnd?.Invoke();
    }

    // ─────────────────────────────────────────────
    // GÉOGRAPHIE : la mer, la plage du ponton, la fissure du volcan
    // ─────────────────────────────────────────────

    private static Tilemap FindTilemap(string name)
    {
        foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None)) if (tm.name == name) return tm;
        return null;
    }

    private static bool IsSea(Tilemap blk, Vector3Int c)
    {
        var t = blk != null ? blk.GetTile(c) : null;
        return t != null && t.name.Contains("sea");
    }

    private Geo ComputeGeo(Vector3 corePos)
    {
        var g = new Geo { core = corePos, seaDir = Vector2.down };
        var spawner = FindFirstObjectByType<AdventurerSpawner>();
        Vector3 dock = spawner != null ? spawner.SpawnPosition : corePos + new Vector3(-20f, -30f, 0f);
        var sol = FindTilemap("Sol");
        var blk = FindTilemap("NoPathfinding");
        g.beach = dock + new Vector3(4f, 3f, 0f);
        if (sol != null && blk != null)
        {
            // une case de sable au bord de l'eau, près du ponton (mais pas dessus)
            var c0 = sol.WorldToCell(dock);
            float best = float.MaxValue;
            Vector3Int bestC = c0;
            Vector3Int bestDir = Vector3Int.down;
            for (int dx = -18; dx <= 18; dx++)
                for (int dy = -18; dy <= 18; dy++)
                {
                    if (Mathf.Abs(dx) < 12) continue;
                    var c = c0 + new Vector3Int(dx, dy, 0);
                    var tile = sol.GetTile(c);
                    if (tile == null || !tile.name.Contains("gravel") || blk.HasTile(c)) continue;
                    foreach (var d in DungeonTerritory.Dirs)
                    {
                        if (!IsSea(blk, c + d) || !IsSea(blk, c + d * 4)) continue;
                        float score = Mathf.Abs(new Vector2(dx, dy).magnitude - 16f);
                        if (score < best) { best = score; bestC = c; bestDir = d; }
                    }
                }
            if (best < float.MaxValue)
            {
                g.beach = sol.GetCellCenterWorld(bestC);
                g.seaDir = new Vector2(bestDir.x, bestDir.y);
                int k = 8;
                while (k > 3 && !IsSea(blk, bestC + bestDir * k)) k--;
                g.sea = sol.GetCellCenterWorld(bestC + bestDir * k);
                // les voiles : au large, sur le côté
                var lateral = new Vector3Int(-bestDir.y, bestDir.x, 0);
                g.sails = g.sea + (Vector3)g.seaDir * 4f + new Vector3(lateral.x, lateral.y, 0f) * 6f;
                foreach (var off in new[] { 6, -6, 9, -9, 3 })
                {
                    var sc = bestC + bestDir * (k + 4) + lateral * off;
                    if (IsSea(blk, sc)) { g.sails = sol.GetCellCenterWorld(sc); break; }
                }
            }
        }
        if (g.sea == Vector3.zero) g.sea = g.beach + (Vector3)g.seaDir * 6f;
        if (g.sails == Vector3.zero) g.sails = g.sea + new Vector3(9f, -5f, 0f);
        g.crater = MountainRelief.Instance != null && MountainRelief.Instance.craterWorld != Vector2.zero
            ? (Vector3)MountainRelief.Instance.craterWorld : corePos + new Vector3(10f, 10f, 0f);

        // la grotte : le tunnel de l'entrée jusqu'au cristal, et les parois (pour le mana qui s'y répand)
        g.tunnel = new List<Vector3>();
        g.walls = new List<(Vector3, int, bool)>();
        var t = DungeonTerritory.Instance;
        if (t != null)
        {
            var cells = new HashSet<Vector3Int>(t.Cells);
            var coreCell = t.CoreCell;
            var entrance = t.EntranceCell;
            var dist = new Dictionary<Vector3Int, int> { [coreCell] = 0 };
            var prev = new Dictionary<Vector3Int, Vector3Int>();
            var q = new Queue<Vector3Int>();
            q.Enqueue(coreCell);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in DungeonTerritory.Dirs)
                {
                    var n = c + d;
                    if (!cells.Contains(n) || dist.ContainsKey(n)) continue;
                    dist[n] = dist[c] + 1;
                    prev[n] = c;
                    q.Enqueue(n);
                }
            }
            for (var c = entrance; ; c = prev[c])
            {
                g.tunnel.Add(t.CellCenter(c));
                if (c == coreCell || !prev.ContainsKey(c)) break;
            }
            foreach (var kv in dist)
                foreach (var d in DungeonTerritory.Dirs)
                    if (t.IsStone(kv.Key + d)) g.walls.Add((t.CellCenter(kv.Key) + new Vector3(d.x, d.y, 0f) * 0.47f, kv.Value, d.y != 0));
            g.entrance = t.CellCenter(entrance);
            Vector3Int outDir = Vector3Int.down;
            foreach (var d in DungeonTerritory.Dirs) if (!cells.Contains(entrance + d) && !t.IsStone(entrance + d)) { outDir = d; break; }
            g.outside = g.entrance + new Vector3(outDir.x, outDir.y, 0f) * 1.6f;
        }
        else
        {
            g.entrance = corePos + Vector3.down * 5f;
            g.outside = g.entrance + Vector3.down * 1.6f;
            g.tunnel.Add(g.entrance);
            g.tunnel.Add(corePos);
        }
        return g;
    }

    // ─────────────────────────────────────────────
    // OUTILS DE L'INTRO
    // ─────────────────────────────────────────────

    private GameObject Temp(string name)
    {
        var go = new GameObject(name);
        introTemp.Add(go);
        return go;
    }

    private void SetCam(Vector3 p, float size)
    {
        if (cam == null) return;
        cam.transform.position = new Vector3(p.x, p.y, cam.transform.position.z);
        cam.orthographicSize = size;
    }

    private IEnumerator CamLoop()
    {
        while (introRunning)
        {
            if (camFollow != null && cam != null)
            {
                float k = 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime);
                Vector3 p = cam.transform.position;
                cam.transform.position = Vector3.Lerp(p, new Vector3(camFollow.position.x, camFollow.position.y, p.z), k);
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, camFollowSize, 1f - Mathf.Exp(-1.2f * Time.unscaledDeltaTime));
            }
            yield return null;
        }
    }

    private IEnumerator LightTo(Color c, float intensity, float duration)
    {
        if (globalLight == null) yield break;
        Color c0 = globalLight.color;
        float i0 = globalLight.intensity;
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / duration)
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            globalLight.color = Color.Lerp(c0, c, e);
            globalLight.intensity = Mathf.Lerp(i0, intensity, e);
            yield return null;
        }
        globalLight.color = c;
        globalLight.intensity = intensity;
    }

    private IEnumerator DayNightCycles(int n, float each)
    {
        for (int i = 0; i < n && !skip; i++)
        {
            yield return LightTo(DawnC, 0.8f, each * 0.15f);
            yield return LightTo(DayC, 1f, each * 0.2f);
            yield return Wait(each * 0.15f);
            yield return LightTo(SunsetC, 0.85f, each * 0.15f);
            yield return LightTo(NightC, 0.42f, each * 0.15f);
            yield return Wait(each * 0.2f);
        }
    }

    private IEnumerator TopBarTo(float h, float duration)
    {
        float h0 = topBar.sizeDelta.y;
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / duration)
        {
            topBar.sizeDelta = new Vector2(0f, Mathf.Lerp(h0, h, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        topBar.sizeDelta = new Vector2(0f, h);
    }

    private IEnumerator HudIn(string part, float duration)
    {
        var hud = GameHUD.Instance;
        if (hud == null) yield break;
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / duration)
        {
            hud.IntroReveal(part, t);
            yield return null;
        }
        hud.IntroReveal(part, 1f);
    }

    private IEnumerator ManaTo(ManaSystem m, float to, float duration)
    {
        if (m == null) yield break;
        float from = m.Raw;
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / duration)
        {
            m.SetRaw(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        m.SetRaw(to);
    }

    // ─────────────────────────────────────────────
    // L'ORBE
    // ─────────────────────────────────────────────

    private void CreateOrb(SpriteRenderer coreSr, Vector3 at)
    {
        var go = Temp("OrbeIntro");
        orb = go.transform;
        orbSr = go.AddComponent<SpriteRenderer>();
        var tex = coreSr != null && coreSr.sprite != null ? coreSr.sprite.texture : null;
        // l'orbe seule, découpée dans le sprite du cœur (sans son socle)
        orbSr.sprite = tex != null ? Sprite.Create(tex, new Rect(40, 68, 56, 46), new Vector2(0.5f, 0.5f), coreSr.sprite.pixelsPerUnit) : SpriteUtil.Circle;
        if (unlitMat != null) orbSr.material = unlitMat;
        orbSr.sortingOrder = 25;
        orbScale = Vector3.one * 0.72f;
        orb.localScale = orbScale;
        orbBase = at;
        orb.position = at;
        var lg = new GameObject("Lueur");
        lg.transform.SetParent(orb, false);
        orbLight = lg.AddComponent<Light2D>();
        orbLight.lightType = Light2D.LightType.Point;
        orbLight.color = new Color(0.45f, 0.8f, 1f);
        orbLight.pointLightOuterRadius = 1.8f;
        orbLight.pointLightInnerRadius = 0.1f;
        orbLightBase = 0.7f;
        orbLight.intensity = orbLightBase;
    }

    private Vector3 GullBeak() => gull.position + gull.rotation * new Vector3(0f, 0.46f, 0f) * gull.localScale.y;

    private IEnumerator OrbLoop()
    {
        while (introRunning && orb != null)
        {
            float t = Time.unscaledTime;
            if (orbHeld && gull != null) orb.position = GullBeak();
            else orb.position = orbBase + new Vector3(Mathf.Sin(t * 0.9f) * 0.03f, Mathf.Sin(t * 1.7f) * 0.06f, 0f) * orbBob;
            orb.localScale = orbScale * orbPuff;
            orbSr.sortingOrder = orbHeld ? 83 : 25;
            if (orbLight != null) orbLight.intensity = orbLightBase * orbGlow * (0.9f + 0.2f * Mathf.PerlinNoise(t * 2.3f, 0.5f));
            yield return null;
        }
    }

    private IEnumerator OrbLightTo(float to, float duration)
    {
        float from = orbLightBase;
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / duration)
        {
            orbLightBase = Mathf.Lerp(from, to, t);
            yield return null;
        }
        orbLightBase = to;
    }

    /// <summary>La lueur s'affole (panique).</summary>
    private IEnumerator OrbPanic(float duration)
    {
        for (float t = 0f; t < duration && !skip; t += Time.unscaledDeltaTime)
        {
            orbGlow = 0.5f + 1.5f * Mathf.PerlinNoise(t * 14f, 3f);
            yield return null;
        }
        orbGlow = 1f;
    }

    /// <summary>Respirations « à vide » : l'orbe gonfle (amp &gt; 0) ou se vide (amp &lt; 0).</summary>
    private IEnumerator OrbBreath(int cycles, float each, float amp)
    {
        for (int i = 0; i < cycles && !skip; i++)
            for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / each)
            {
                float b = Mathf.Sin(t * Mathf.PI);
                orbPuff = 1f + amp * b;
                orbGlow = 1f + Mathf.Abs(amp) * 6f * b;
                yield return null;
            }
        orbPuff = 1f;
        orbGlow = 1f;
    }

    private IEnumerator DriftOrb(Vector3 from, Vector3 to, float duration)
    {
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / duration)
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            // le ressac : on avance par vagues
            float surge = e + Mathf.Sin(t * Mathf.PI * 6f) * 0.03f * (1f - e);
            orbBase = Vector3.Lerp(from, to, surge);
            orbBob = 1f - e;
            yield return null;
        }
        orbBase = to;
        orbBob = 0f;
    }

    private IEnumerator DropOrb(Vector3 at)
    {
        orbHeld = false;
        orbBob = 0f;
        Vector3 from = orb.position;
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / 0.35f)
        {
            orbBase = Vector3.Lerp(from, at, t * t);
            yield return null;
        }
        orbBase = at;
        Sfx.Play("place", at, 0.5f);
        Fx.Play("smoke_circle", at + new Vector3(0f, -0.1f, 0f), 0.6f, 16f, 24, new Color(0.45f, 0.42f, 0.4f, 0.7f));
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / 0.25f)
        {
            orbBase = at + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.08f;
            yield return null;
        }
        orbBase = at;
    }

    // ─────────────────────────────────────────────
    // LA MER : quelques poissons sous la surface
    // ─────────────────────────────────────────────

    private void SpawnFish(Vector3 around)
    {
        var root = Temp("Poissons");
        for (int i = 0; i < 7; i++)
        {
            var f = new GameObject("Poisson");
            f.transform.SetParent(root.transform, false);
            var sr = f.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteUtil.Circle;
            sr.color = new Color(0.02f, 0.08f, 0.12f, 0.32f);
            sr.sortingOrder = 20;
            StartCoroutine(Swim(f.transform, sr, around, UnityEngine.Random.Range(1f, 3.2f), UnityEngine.Random.Range(0.25f, 0.6f) * (UnityEngine.Random.value < 0.5f ? 1f : -1f), UnityEngine.Random.value * 6.28f));
        }
    }

    private IEnumerator Swim(Transform f, SpriteRenderer sr, Vector3 center, float r, float w, float a0)
    {
        float s = UnityEngine.Random.Range(0.18f, 0.3f);
        while (introRunning && f != null)
        {
            float a = a0 + Time.unscaledTime * w;
            Vector3 p = center + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.6f, 0f);
            Vector3 v = new Vector3(-Mathf.Sin(a) * r, Mathf.Cos(a) * r * 0.6f, 0f) * Mathf.Sign(w);
            f.position = p;
            f.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg + Mathf.Sin(Time.unscaledTime * 9f + a0) * 8f);
            f.localScale = new Vector3(s * 1.6f, s * 0.55f, 1f);
            // les poissons restent sous l'eau : ils s'effacent quand la sphère s'éloigne
            float d = Vector2.Distance(orb != null ? orb.position : center, center);
            sr.color = new Color(0.02f, 0.08f, 0.12f, 0.32f * Mathf.Clamp01(1f - d / 4f));
            yield return null;
        }
    }

    private GameObject SpawnSails(Vector3 at)
    {
        var art = SettlementArt.Get();
        var frames = art != null ? art.Frames("boat_sail") : null;
        if (frames == null || frames.Length == 0) return null;
        var go = Temp("Voiles");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sortingOrder = 18;
        go.transform.localScale = Vector3.one * 0.7f;
        go.transform.position = at;
        StartCoroutine(Sail(go.transform, at));
        return go;
    }

    private IEnumerator Sail(Transform s, Vector3 at)
    {
        while (introRunning && s != null)
        {
            s.position = at + new Vector3(Time.unscaledTime * 0.05f % 4f, Mathf.Sin(Time.unscaledTime * 1.1f) * 0.05f, 0f);
            yield return null;
        }
    }

    // ─────────────────────────────────────────────
    // LE MANA : poussières, anneaux, explosion
    // ─────────────────────────────────────────────

    private Mote NewMote(Vector3 at, int state)
    {
        var go = Temp("Mana");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteUtil.Glow;
        if (unlitMat != null) sr.material = unlitMat;
        sr.sortingOrder = 26;
        go.transform.position = at;
        var m = new Mote { t = go.transform, sr = sr, state = state, seed = UnityEngine.Random.value * 100f };
        float s = UnityEngine.Random.Range(0.07f, 0.13f);
        go.transform.localScale = Vector3.one * s;
        sr.color = new Color(0.9f, 0.95f, 1f, 0f);
        motes.Add(m);
        return m;
    }

    private void SpawnFreeMotes(Vector3 around, int n)
    {
        for (int i = 0; i < n; i++)
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(1.2f, 4.2f);
            NewMote(around + new Vector3(r.x, r.y * 0.8f, 0f), 0);
        }
    }

    private void MakeOrbit(Mote m, float rmin, float rmax)
    {
        m.state = 1;
        m.ang = UnityEngine.Random.value * Mathf.PI * 2f;
        m.rad = UnityEngine.Random.Range(rmin, rmax);
        m.speed = UnityEngine.Random.Range(0.8f, 1.6f) / Mathf.Sqrt(m.rad);
    }

    /// <summary>
    /// États : 0 libre (dérive, attiré à l'inspiration), 1 en orbite, 2 absorbé (invisible), 3 projeté (vel), 4 s'éteint,
    /// 5 piloté par un script (trajet).
    /// </summary>
    private IEnumerator MoteLoop()
    {
        while (introRunning)
        {
            float dt = Time.unscaledDeltaTime;
            float time = Time.unscaledTime;
            Vector3 c = orb != null ? orb.position : Vector3.zero;
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                var m = motes[i];
                if (m.t == null) { motes.RemoveAt(i); continue; }
                float tw = 0.65f + 0.35f * Mathf.PerlinNoise(time * 3f, m.seed);   // scintillement
                var col = m.sr.color;
                switch (m.state)
                {
                    case 0:
                        Vector3 drift = new Vector3(Mathf.PerlinNoise(time * 0.3f, m.seed) - 0.5f, Mathf.PerlinNoise(m.seed, time * 0.3f) - 0.5f, 0f) * 0.6f;
                        Vector3 toC = c - m.t.position;
                        m.t.position += drift * dt + (motePull > 0f ? toC.normalized * motePull * dt * (0.6f + 2.4f / Mathf.Max(0.6f, toC.magnitude)) : motePull < 0f ? toC.normalized * motePull * dt * 0.4f : Vector3.zero);
                        col.a = Mathf.MoveTowards(col.a, tw * 0.8f, dt);
                        break;
                    case 1:
                        m.ang += m.speed * dt;
                        m.t.position = c + new Vector3(Mathf.Cos(m.ang) * m.rad, Mathf.Sin(m.ang) * m.rad * 0.38f + 0.04f, 0f);
                        m.sr.sortingOrder = Mathf.Sin(m.ang) > 0f ? 24 : 26;    // l'anneau passe derrière puis devant l'orbe
                        col.a = Mathf.MoveTowards(col.a, tw * 0.9f, dt * 2f);
                        break;
                    case 2:
                        col.a = 0f;
                        break;
                    case 3:
                        m.t.position += m.vel * dt;
                        m.vel *= Mathf.Exp(-2.2f * dt);
                        if (m.vel.sqrMagnitude < 0.04f) m.state = 4;
                        break;
                    case 5:
                        break;
                    case 4:
                        col.a = Mathf.MoveTowards(col.a, 0f, dt * 0.5f);
                        if (col.a <= 0f) { Destroy(m.t.gameObject); motes.RemoveAt(i); continue; }
                        break;
                }
                m.sr.color = col;
            }
            yield return null;
        }
    }

    /// <summary>Inspirer (les poussières approchent, les plus proches sont avalées), expirer (elles repartent en orbite).</summary>
    private IEnumerator Breathe(int cycles, ManaSystem mana, float manaTarget)
    {
        float m0 = mana != null ? mana.Raw : 0f;
        int swallowed = 0;
        for (int i = 0; i < cycles && !skip; i++)
        {
            for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / 1.5f)
            {
                float b = Mathf.Sin(t * Mathf.PI);
                motePull = 2.2f * b;
                orbPuff = 1f + 0.1f * b;
                orbGlow = 1f + 0.6f * b;
                Vector3 c = orb.position;
                foreach (var m in motes)
                    if (m.state == 0 && Vector2.Distance(m.t.position, c) < 0.32f) { m.state = 2; swallowed++; }
                if (mana != null) mana.SetRaw(Mathf.Lerp(m0, manaTarget, (i + t) / cycles));
                yield return null;
            }
            // expiration : ce qui a été avalé ressort, dirigé, en orbite
            foreach (var m in motes) if (m.state == 2) { MakeOrbit(m, 0.45f, 0.75f); m.t.position = orb.position; }
            for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / 1.7f)
            {
                motePull = -0.8f * Mathf.Sin(t * Mathf.PI);
                orbPuff = 1f - 0.04f * Mathf.Sin(t * Mathf.PI);
                yield return null;
            }
        }
        motePull = 0f;
        orbPuff = 1f;
        orbGlow = 1f;
        if (mana != null) mana.SetRaw(manaTarget);
    }

    /// <summary>Le temps passe : le mana afflue de partout et les orbites deviennent des anneaux.</summary>
    private IEnumerator GrowRings(Vector3 around, float duration, ManaSystem mana, float max)
    {
        float m0 = mana != null ? mana.Raw : 0f;
        float spawn = 0f;
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / duration)
        {
            spawn += Time.unscaledDeltaTime * Mathf.Lerp(4f, 12f, t);
            while (spawn >= 1f)
            {
                spawn -= 1f;
                Vector2 r = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(2f, 5f);
                var m = NewMote(around + (Vector3)r, 0);
                StartCoroutine(MoteToOrbit(m, Mathf.Lerp(0.45f, 0.6f, t), Mathf.Lerp(0.8f, 1.5f, t)));
            }
            if (mana != null) mana.SetRaw(Mathf.Lerp(m0, max, t));
            yield return null;
        }
        if (mana != null) mana.SetRaw(max);
    }

    private IEnumerator MoteToOrbit(Mote m, float rmin, float rmax)
    {
        m.state = 5;
        Vector3 from = m.t.position;
        for (float t = 0f; t < 1f && m.t != null && !skip; t += Time.unscaledDeltaTime / 1.2f)
        {
            m.t.position = Vector3.Lerp(from, orb.position, t * t);
            m.sr.color = new Color(0.9f, 0.95f, 1f, t);
            yield return null;
        }
        if (m.t != null) MakeOrbit(m, rmin, rmax);
    }

    /// <summary>Le trop-plein : quelques poussières s'échappent des anneaux.</summary>
    private IEnumerator Overflow(int n)
    {
        int done = 0;
        foreach (var m in motes)
        {
            if (done >= n || m.state != 1 || m.rad < 1.1f) continue;
            m.state = 3;
            m.vel = (m.t.position - orb.position).normalized * UnityEngine.Random.Range(1.5f, 2.5f);
            done++;
            yield return Wait(0.18f);
        }
    }

    /// <summary>« ASSEZ ! » : les anneaux sont aspirés, puis projetés dans toutes les directions.</summary>
    private IEnumerator Blast(Vector3 at, ManaSystem mana)
    {
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / 0.5f)
        {
            foreach (var m in motes) if (m.state == 1) m.rad = Mathf.Max(0.05f, m.rad * (1f - 6f * Time.unscaledDeltaTime));
            orbPuff = 1f - 0.15f * t;
            orbGlow = 1f + 3f * t;
            yield return null;
        }
        yield return Fade(0f, 0.9f, 0.08f, Color.white);
        Sfx.Play("explosion_1", at, 0.6f);
        camCtrl?.Shake(0.25f, 0.6f);
        foreach (var m in motes)
        {
            if (m.state != 1 && m.state != 0) continue;
            m.state = 3;
            m.t.position = at;
            Vector2 d = UnityEngine.Random.insideUnitCircle.normalized;
            m.vel = new Vector3(d.x, d.y, 0f) * UnityEngine.Random.Range(5f, 14f);
            m.sr.color = new Color(0.9f, 0.95f, 1f, 1f);
        }
        mana?.SetRaw(0f);
        orbPuff = 1f;
        orbGlow = 1f;
        yield return Fade(0.9f, 0f, 0.5f, Color.white);
    }

    private IEnumerator ShootMote(Vector3 from, Vector3 to, float duration)
    {
        var m = NewMote(from, 5);
        m.sr.color = Color.white;
        m.t.localScale = Vector3.one * 0.35f;
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / duration)
        {
            m.t.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
        m.state = 4;
    }

    /// <summary>Le mana se répand dans la roche : une onde de lueurs court le long des parois.</summary>
    private IEnumerator Ripple(Geo g)
    {
        var glints = new List<(SpriteRenderer sr, int dist)>();
        foreach (var w in g.walls)
        {
            var go = Temp("Veine");
            go.transform.position = w.pos;
            go.transform.localScale = w.horizontal ? new Vector3(1.02f, 0.07f, 1f) : new Vector3(0.07f, 1.02f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteUtil.Square;
            if (unlitMat != null) sr.material = unlitMat;
            sr.sortingOrder = 26;
            sr.color = new Color(0.45f, 0.8f, 1f, 0f);
            glints.Add((sr, w.dist));
        }
        int maxD = 1;
        foreach (var gl in glints) maxD = Mathf.Max(maxD, gl.dist);
        float dur = 1.2f + maxD * 0.35f;
        for (float t = 0f; t < dur + 1.2f && !skip; t += Time.unscaledDeltaTime)
        {
            float front = t / 0.35f;
            foreach (var gl in glints)
            {
                float k = front - gl.dist;
                float a = k < 0f ? 0f : k < 1f ? k : Mathf.Lerp(1f, 0.35f, Mathf.Clamp01((k - 1f) / 3f));
                gl.sr.color = new Color(0.5f, 0.85f, 1f, a * (0.7f + 0.3f * Mathf.PerlinNoise(Time.unscaledTime * 2f, gl.dist)));
            }
            yield return null;
        }
        // elles restent, faibles, jusqu'à la fin de la cinématique
    }

    /// <summary>Le premier souffle de la grotte : un fleuve de poussières entre par la fissure et remonte jusqu'au cœur.</summary>
    private IEnumerator CaveBreath(Geo g, Vector3 corePos, float duration)
    {
        var path = new List<Vector3> { g.outside + (g.outside - g.entrance).normalized * 2f, g.outside };
        path.AddRange(g.tunnel);
        path[path.Count - 1] = corePos + new Vector3(0.04f, 0.27f, 0f);
        float spawn = 0f;
        for (float t = 0f; t < duration && !skip; t += Time.unscaledDeltaTime)
        {
            spawn += Time.unscaledDeltaTime * 22f;
            while (spawn >= 1f)
            {
                spawn -= 1f;
                var m = NewMote(path[0], 5);
                StartCoroutine(FollowPath(m, path, UnityEngine.Random.Range(4f, 6.5f), UnityEngine.Random.Range(-0.3f, 0.3f)));
            }
            yield return null;
        }
    }

    private IEnumerator FollowPath(Mote m, List<Vector3> path, float speed, float lateral)
    {
        for (int i = 1; i < path.Count && m.t != null && !skip; i++)
        {
            Vector3 a = path[i - 1], b = path[i];
            Vector3 n = Vector3.Cross(b - a, Vector3.forward).normalized * lateral * (i == path.Count - 1 ? 0f : 1f);
            float len = Vector3.Distance(a, b);
            for (float t = 0f; t < 1f && m.t != null && !skip; t += Time.unscaledDeltaTime * speed / Mathf.Max(0.1f, len))
            {
                m.t.position = Vector3.Lerp(a, b, t) + n + new Vector3(0f, Mathf.Sin(Time.unscaledTime * 7f + m.seed) * 0.06f, 0f);
                m.sr.color = new Color(0.85f, 0.95f, 1f, 0.9f);
                yield return null;
            }
        }
        if (m.t != null) { m.state = 4; m.sr.color = new Color(1f, 1f, 1f, 0.6f); }
    }

    private IEnumerator RaisePedestal(SpriteRenderer coreSr, Vector3 corePos, Vector3 coreScale, ManaSystem mana, float manaMax)
    {
        if (coreSr == null || coreSr.sprite == null) yield break;
        var tex = coreSr.sprite.texture;
        float ppu = coreSr.sprite.pixelsPerUnit;
        var go = Temp("Socle");
        var sr = go.AddComponent<SpriteRenderer>();
        // le socle seul (le bas du sprite du cœur), posé par sa base
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 128, 66), new Vector2(0.5f, 0f), ppu);
        sr.sortingOrder = coreSr.sortingOrder;
        Vector3 basePos = corePos + new Vector3(0f, -64f / ppu * coreScale.y, 0f);
        go.transform.position = basePos;
        Vector3 orbFrom = orbBase, orbTo = corePos + new Vector3(4f, 27f, 0f) / ppu * coreScale.y;
        Vector3 s0 = orbScale;
        Sfx.Play("dig_1", corePos, 0.8f);
        camCtrl?.Shake(0.08f, 2.6f);
        StartCoroutine(ManaTo(mana, manaMax * 0.12f, 2.6f));
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / 2.6f)
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            go.transform.localScale = new Vector3(coreScale.x * Mathf.Lerp(0.6f, 1f, e), coreScale.y * e, 1f);
            orbBase = Vector3.Lerp(orbFrom, orbTo, e);
            orbScale = Vector3.Lerp(s0, coreScale, e);
            if (UnityEngine.Random.value < 0.12f)
                Fx.Play("smoke_circle", basePos + new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), UnityEngine.Random.Range(0f, 0.2f), 0f), 0.7f, 16f, 30, new Color(0.35f, 0.32f, 0.3f, 0.75f));
            if (UnityEngine.Random.value < 0.03f) Sfx.Play("dig_2", corePos, 0.5f);
            yield return null;
        }
        go.transform.localScale = coreScale;
        orbBase = orbTo;
        orbScale = coreScale;
        Sfx.Play("place", corePos, 0.8f);
        Fx.Play("smoke_circle", basePos, 1.6f, 14f, 30, new Color(0.35f, 0.32f, 0.3f, 0.8f));
    }

    // ─────────────────────────────────────────────
    // LA MOUETTE
    // ─────────────────────────────────────────────

    private void CreateGull(Vector3 at, float alt, float heading)
    {
        var go = Temp("Mouette");
        gull = go.transform;
        gullSr = go.AddComponent<SpriteRenderer>();
        gullSr.sortingOrder = 82;
        gullSr.sprite = gullFrames != null && gullFrames.Length > 2 ? gullFrames[2] : SpriteUtil.Diamond;
        var sh = Temp("OmbreMouette");
        gullShadow = sh.AddComponent<SpriteRenderer>();
        gullShadow.sprite = SpriteUtil.Circle;
        gullShadow.color = new Color(0f, 0f, 0f, 0.3f);
        gullShadow.sortingOrder = 81;
        gullPos = at;
        gullAlt = alt;
        gullHeading = heading;
        gullPose = -1;
        gullTinted = false;
        StartCoroutine(GullLoop());
    }

    private Vector3 GullVisual() => gullPos + Vector3.up * gullAlt * 0.9f;

    private IEnumerator GullLoop()
    {
        int[] flap = { 0, 1, 2, 3, 2, 1 };
        while (introRunning && gull != null)
        {
            float time = Time.unscaledTime;
            int f = gullPose >= 0 ? gullPose : gullGlide ? 2 : flap[(int)(time * 12f) % flap.Length];
            if (gullFrames != null && f < gullFrames.Length) gullSr.sprite = gullFrames[f];
            gull.position = GullVisual();
            gull.rotation = Quaternion.Euler(0f, 0f, gullHeading);
            gull.localScale = Vector3.one * (1f + gullAlt * 0.6f);
            gullShadow.transform.position = gullPos + new Vector3(0f, -0.12f, 0f);
            float sh = gullPose >= 0 ? 1f : 1f - gullAlt * 0.35f;
            gullShadow.transform.localScale = new Vector3(0.55f * sh, 0.22f * sh, 1f);
            gullShadow.color = new Color(0f, 0f, 0f, 0.3f * (1f - gullAlt * 0.5f));
            if (gullTinted)
            {
                float p = 0.5f + 0.5f * Mathf.Sin(time * 3f);
                gullSr.color = Color.Lerp(Color.white, new Color(0.62f, 0.85f, 1f), 0.35f + 0.4f * p);
            }
            yield return null;
        }
    }

    private static Vector2 Forward(float heading)
    {
        float r = heading * Mathf.Deg2Rad;
        return new Vector2(-Mathf.Sin(r), Mathf.Cos(r));
    }

    /// <summary>Vol libre à travers des points (virages progressifs). land : se pose sur le dernier point.</summary>
    private IEnumerator GullFly(List<Vector3> pts, float speed, float cruise, bool land)
    {
        gullPose = -1;
        Vector2 dir = Forward(gullHeading);
        float air = 0f;
        for (int i = 0; i < pts.Count && !skip && !gullStop; i++)
        {
            gullWp = i;
            bool last = i == pts.Count - 1;
            while (!skip && !gullStop)
            {
                float dt = Time.unscaledDeltaTime;
                air += dt;
                Vector2 to = pts[i] - gullPos;
                float d = to.magnitude;
                if (last ? d < 0.04f : d < 1.8f) break;
                Vector2 want = to / d;
                float turn = 300f * (last && d < 3f ? 3f : 1f);
                dir = last && d < 0.7f ? want : (Vector2)Vector3.RotateTowards(dir, want, turn * Mathf.Deg2Rad * dt, 0f);
                float v = speed * (last && land ? Mathf.Clamp(d / 3f, 0.3f, 1f) : 1f);
                gullPos += (Vector3)(dir * Mathf.Min(v * dt, d));
                float target = last && land ? cruise * Mathf.Clamp01((d - 0.2f) / 3f) : cruise;
                gullAlt = last && land && d < 3f ? Mathf.Min(gullAlt, target) : Mathf.MoveTowards(gullAlt, target, dt * 0.9f);
                gullGlide = (last && land && d < 3f) || Mathf.Sin(air * 0.8f) > 0.55f;
                gullHeading = Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;
                yield return null;
            }
        }
        gullWp = pts.Count;
        if (land && !gullStop) { gullAlt = 0f; gullPose = 5; }
    }

    /// <summary>Vol en ligne droite d'un point à l'autre (le tunnel, trop étroit pour virer).</summary>
    private IEnumerator GullDirect(List<Vector3> pts, float speed, float alt)
    {
        gullPose = -1;
        for (int i = 0; i < pts.Count && !skip; i++)
        {
            while (!skip)
            {
                float dt = Time.unscaledDeltaTime;
                Vector2 to = pts[i] - gullPos;
                float d = to.magnitude;
                if (d < 0.03f) break;
                Vector2 dir = to / d;
                gullPos += (Vector3)(dir * Mathf.Min(speed * dt, d));
                gullAlt = Mathf.MoveTowards(gullAlt, alt, dt * 0.8f);
                gullHeading = Mathf.MoveTowardsAngle(gullHeading, Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg, 720f * dt);
                gullGlide = false;
                yield return null;
            }
        }
        gullPose = -1;
    }

    private IEnumerator GullFall(Vector3 at)
    {
        Vector3 from = gullPos;
        float alt0 = gullAlt;
        gullPose = 3;
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime / 1f)
        {
            gullPos = Vector3.Lerp(from, at, t);
            gullAlt = alt0 * (1f - t * t);
            gullHeading += 620f * Time.unscaledDeltaTime;
            yield return null;
        }
        gullPos = at;
        gullAlt = 0f;
        gullPose = 4;
        gullHeading = 25f;
        Sfx.Play("dig_3", at, 0.4f);
        Fx.Play("smoke_circle", at, 1f, 16f, 30, new Color(0.3f, 0.3f, 0.32f, 0.8f));
        camCtrl?.Shake(0.05f, 0.25f);
    }

    /// <summary>Le mana de l'orbe envahit la mouette : un filet de lumière, puis des veines bleues qui pulsent.</summary>
    private IEnumerator PossessGull(Vector3 from, float duration)
    {
        float spawn = 0f;
        for (float t = 0f; t < duration && !skip; t += Time.unscaledDeltaTime)
        {
            spawn += Time.unscaledDeltaTime * 8f;
            while (spawn >= 1f)
            {
                spawn -= 1f;
                var m = NewMote(orb.position, 5);
                m.t.localScale = Vector3.one * 0.14f;
                StartCoroutine(FollowPath(m, new List<Vector3> { orb.position, Vector3.Lerp(orb.position, gullPos, 0.5f) + Vector3.up * 0.4f, gullPos }, 2.2f, 0f));
            }
            if (t > 0.8f && !gullTinted)
            {
                gullTinted = true;
                var lg = new GameObject("Lueur");
                lg.transform.SetParent(gull, false);
                gullLight = lg.AddComponent<Light2D>();
                gullLight.lightType = Light2D.LightType.Point;
                gullLight.color = new Color(0.45f, 0.8f, 1f);
                gullLight.intensity = 0.7f;
                gullLight.pointLightOuterRadius = 1.3f;
            }
            yield return null;
        }
    }

    private IEnumerator GullLeave(List<Vector3> path)
    {
        orbHeld = false;
        gullTinted = false;
        gullSr.color = Color.white;
        if (gullLight != null) gullLight.enabled = false;
        yield return GullDirect(path, 5.5f, 0.5f);
        if (gull == null) yield break;
        for (float t = 0f; t < 1f && introRunning && gull != null; t += Time.unscaledDeltaTime / 0.6f)
        {
            gullSr.color = new Color(1f, 1f, 1f, 1f - t);
            gullShadow.color = new Color(0f, 0f, 0f, 0.3f * (1f - t));
            yield return null;
        }
        if (gull != null) gull.gameObject.SetActive(false);
        if (gullShadow != null) gullShadow.gameObject.SetActive(false);
    }
}
