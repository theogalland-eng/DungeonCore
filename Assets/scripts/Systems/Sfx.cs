using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bruitages : Sfx.Play("hit_sword", position). Les sons viennent de Assets/Resources/Audio/Sfx
/// (générés par Tools~/make_sfx.py, remplaçables par de vrais fichiers du même nom, .wav/.mp3/.ogg).
/// Variantes « nom_1, nom_2... » tirées au hasard. Le volume baisse avec la distance à la caméra ;
/// un son hors de l'écran (et au-delà) n'est pas joué. Anti-cacophonie : un même son ne se répète pas trop vite.
/// </summary>
public class Sfx : MonoBehaviour
{
    private static Sfx instance;
    private readonly Dictionary<string, List<AudioClip>> clips = new Dictionary<string, List<AudioClip>>();
    private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    private readonly List<AudioSource> pool = new List<AudioSource>();
    private int next;

    private static Sfx Get()
    {
        if (instance != null) return instance;
        var go = new GameObject("Sfx");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<Sfx>();
        instance.Load();
        return instance;
    }

    private void Load()
    {
        foreach (var c in Resources.LoadAll<AudioClip>("Audio/Sfx"))
        {
            string n = c.name;
            int us = n.LastIndexOf('_');
            if (us > 0 && int.TryParse(n.Substring(us + 1), out _)) n = n.Substring(0, us);
            if (!clips.TryGetValue(n, out var l)) clips[n] = l = new List<AudioClip>();
            l.Add(c);
        }
        for (int i = 0; i < 16; i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.ignoreListenerPause = true;
            pool.Add(s);
        }
    }

    /// <summary>Joue un bruitage. pos = null : son d'interface (toujours audible).</summary>
    public static void Play(string name, Vector3? pos = null, float volume = 1f, float pitchVariation = 0.08f, float minInterval = 0.05f, float pitch = 1f)
    {
        if (string.IsNullOrEmpty(name) || GameSettings.SfxVolume <= 0f) return;
        var s = Get();
        if (!s.clips.TryGetValue(name, out var list) || list.Count == 0) return;
        float now = Time.unscaledTime;
        if (s.lastPlayed.TryGetValue(name, out float t) && now - t < minInterval) return;

        float att = 1f;
        if (pos.HasValue)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                // atténuation selon la distance au centre de l'écran (relative au zoom)
                float d = Vector2.Distance(cam.transform.position, pos.Value);
                float r = cam.orthographicSize * Mathf.Max(1f, cam.aspect);
                att = Mathf.Clamp01(1.25f - d / (r * 1.3f));
                if (att <= 0.02f) return;
                // plus on est dézoomé, plus les petits bruits s'effacent
                att *= Mathf.Lerp(1f, 0.45f, Mathf.InverseLerp(8f, 30f, cam.orthographicSize));
            }
        }
        s.lastPlayed[name] = now;
        var src = s.pool[s.next];
        s.next = (s.next + 1) % s.pool.Count;
        src.clip = list[Random.Range(0, list.Count)];
        src.volume = volume * att * GameSettings.SfxVolume;
        src.pitch = pitch * (1f + Random.Range(-pitchVariation, pitchVariation));
        src.Play();
    }
}
