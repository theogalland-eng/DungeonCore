using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// AMBIANCES PAR ZONE : par-dessus le fond sonore de l'île (MusicManager), des couches se mêlent selon ce que
/// montre la caméra — oiseaux et feuillages de la jungle (grillons et chouette la nuit), clapotis et grenouilles
/// près d'un lac, grondements et bulles de lave près du volcan, murmures, feu et marteau près du village.
/// Muettes sous la montagne (vue donjon) et dans les intérieurs.
/// </summary>
public class ZoneAmbience : MonoBehaviour
{
    private class Layer
    {
        public AudioSource src;
        public float target;
    }

    private readonly Dictionary<string, Layer> layers = new Dictionary<string, Layer>();
    private Tilemap jungle, grass, lava, rock;
    private float nextSample;

    private void Start()
    {
        foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (tm.name == "T_Jungle") jungle = tm;
            else if (tm.name == "T_Herbe") grass = tm;
            else if (tm.name == "T_Lave") lava = tm;
            else if (tm.name == "T_Roche") rock = tm;
        }
        foreach (var id in new[] { "amb_jungle", "amb_night", "amb_lake", "amb_volcano", "amb_village" })
        {
            var clip = Resources.Load<AudioClip>("Audio/Ambience/" + id);
            if (clip == null) continue;
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
            layers[id] = new Layer { src = src };
        }
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextSample)
        {
            nextSample = Time.unscaledTime + 0.5f;
            Sample();
        }
        float master = GameSettings.AmbienceVolume * 0.55f;
        foreach (var l in layers.Values)
        {
            float v = Mathf.MoveTowards(l.src.volume, l.target * master, Time.unscaledDeltaTime * 0.25f);
            l.src.volume = v;
            if (v > 0.001f && !l.src.isPlaying) { l.src.time = Random.Range(0f, l.src.clip.length); l.src.Play(); }
            else if (v <= 0.001f && l.src.isPlaying) l.src.Stop();
        }
    }

    private void Set(string id, float w) { if (layers.TryGetValue(id, out var l)) l.target = Mathf.Clamp01(w); }

    private void Sample()
    {
        var cam = Camera.main;
        bool home = MainMenu.Instance != null && MainMenu.Instance.InHome;
        var relief = MountainRelief.Instance;
        bool underground = !CrawlerMode.Active && relief != null && relief.DungeonView;
        bool indoors = InteriorSystem.Instance != null && InteriorSystem.Instance.Physical;
        if (cam == null || underground || indoors || (CrawlerMode.Active && CrawlerMode.Instance != null && CrawlerMode.Instance.Player != null && DungeonLayers.Instance != null &&
            DungeonTerritory.Instance != null && DungeonTerritory.Instance.IsInside(CrawlerMode.Instance.Player.transform.position)))
        {
            foreach (var k in new List<string>(layers.Keys)) Set(k, 0f);
            return;
        }
        Vector2 c = cam.transform.position;
        float radius = Mathf.Clamp(cam.orthographicSize * 1.3f, 6f, 14f);
        int total = 0, nature = 0, lavaN = 0, rockN = 0;
        for (float dx = -radius; dx <= radius; dx += 1.5f)
            for (float dy = -radius; dy <= radius; dy += 1.5f)
            {
                if (dx * dx + dy * dy > radius * radius) continue;
                total++;
                var p = new Vector3(c.x + dx, c.y + dy, 0f);
                if (jungle != null && jungle.HasTile(jungle.WorldToCell(p))) nature += 2;
                else if (grass != null && grass.HasTile(grass.WorldToCell(p))) nature++;
                if (lava != null && lava.HasTile(lava.WorldToCell(p))) lavaN++;
                if (rock != null && rock.HasTile(rock.WorldToCell(p))) rockN++;
            }
        float natureW = total > 0 ? Mathf.Clamp01(nature / (total * 1.2f)) : 0f;
        bool night = GameManager.Instance != null && GameManager.Instance.IsNight;
        bool winter = Seasons.Current == Seasons.Season.Winter;
        Set("amb_jungle", night ? 0f : natureW * (winter ? 0.25f : 1f));       // l'hiver, les oiseaux se taisent
        Set("amb_night", night ? Mathf.Max(0.25f, natureW) * (winter ? 0.4f : 1f) : 0f);

        float lakeW = 0f;
        foreach (var lake in FreshWater.Lakes)
        {
            float d = Vector2.Distance(lake.center, c);
            lakeW = Mathf.Max(lakeW, Mathf.Clamp01(1f - (d - 3f) / 10f));
        }
        Set("amb_lake", lakeW * (winter ? 0.3f : 1f));

        float volcanoW = total > 0 ? Mathf.Clamp01(lavaN * 6f / total + rockN * 0.6f / total) : 0f;
        if (IslandEvents.Instance != null && IslandEvents.Instance.Current == IslandEvent.Eruption) volcanoW = Mathf.Max(volcanoW, 0.6f);
        Set("amb_volcano", volcanoW);

        float villageW = 0f;
        var sm = SettlementManager.Instance;
        if (sm != null && sm.Site != null && sm.stage > 0)
        {
            float d = Vector2.Distance((Vector2)sm.Site.LandingWorld + new Vector2(0f, 6f), c);
            villageW = Mathf.Clamp01(1f - (d - 6f) / 14f) * Mathf.Clamp01(sm.Population / 6f + 0.3f) * (night ? 0.4f : 1f);
        }
        Set("amb_village", villageW);
        if (home) foreach (var k in new List<string>(layers.Keys)) layers[k].target *= 0.5f;
    }
}
