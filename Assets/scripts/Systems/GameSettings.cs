using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Options du joueur (PlayerPrefs), communes à tous les profils de l'appareil.</summary>
public static class GameSettings
{
    // Les options sont lues à chaque image (volumes, échelle de l'interface, langue de chaque texte affiché) :
    // PlayerPrefs coûte cher sur Android (appel Java), on garde donc une copie en mémoire.
    private static readonly System.Collections.Generic.Dictionary<string, float> floats = new System.Collections.Generic.Dictionary<string, float>();
    private static readonly System.Collections.Generic.Dictionary<string, int> ints = new System.Collections.Generic.Dictionary<string, int>();
    private static readonly System.Collections.Generic.Dictionary<string, string> strings = new System.Collections.Generic.Dictionary<string, string>();

    private static float GetF(string k, float d) { if (!floats.TryGetValue(k, out var v)) floats[k] = v = PlayerPrefs.GetFloat(k, d); return v; }
    private static int GetI(string k, int d) { if (!ints.TryGetValue(k, out var v)) ints[k] = v = PlayerPrefs.GetInt(k, d); return v; }
    private static string GetS(string k, string d) { if (!strings.TryGetValue(k, out var v)) strings[k] = v = PlayerPrefs.GetString(k, d); return v; }
    private static void SetF(string k, float v) { floats[k] = v; PlayerPrefs.SetFloat(k, v); }
    private static void SetI(string k, int v) { ints[k] = v; PlayerPrefs.SetInt(k, v); }
    private static void SetS(string k, string v) { strings[k] = v; PlayerPrefs.SetString(k, v); }

    public static float Volume
    {
        get => GetF("opt_volume", 0.8f);
        set { SetF("opt_volume", Mathf.Clamp01(value)); Apply(); }
    }

    public static float MusicVolume
    {
        get => GetF("opt_music", 0.7f);
        set => SetF("opt_music", Mathf.Clamp01(value));
    }

    public static float AmbienceVolume
    {
        get => GetF("opt_ambience", 0.8f);
        set => SetF("opt_ambience", Mathf.Clamp01(value));
    }

    public static float SfxVolume
    {
        get => GetF("opt_sfx", 0.8f);
        set => SetF("opt_sfx", Mathf.Clamp01(value));
    }

    public static bool Cinematics
    {
        get => GetI("opt_cine", 1) == 1;
        set => SetI("opt_cine", value ? 1 : 0);
    }

    public static bool PostProcessing
    {
        get => GetI("opt_post", 1) == 1;
        set { SetI("opt_post", value ? 1 : 0); Apply(); }
    }

    public static bool Ambient
    {
        get => GetI("opt_ambient", 1) == 1;
        set { SetI("opt_ambient", value ? 1 : 0); Apply(); }
    }

    public static bool AutoSave
    {
        get => GetI("opt_autosave", 1) == 1;
        set => SetI("opt_autosave", value ? 1 : 0);
    }

    /// <summary>Échelle de l'interface (0,8 à 1,4) : plus grand = boutons et textes plus gros.</summary>
    public static float UiScale
    {
        get => GetF("opt_uiscale", 1f);
        set => SetF("opt_uiscale", Mathf.Clamp(Mathf.Round(value * 10f) / 10f, 0.8f, 1.4f));
    }

    /// <summary>Vibrations du téléphone (cristal frappé, héros blessé).</summary>
    public static bool Vibration
    {
        get => GetI("opt_vibrate", 1) == 1;
        set => SetI("opt_vibrate", value ? 1 : 0);
    }

    /// <summary>Langue de l'interface : "fr" (d'origine) ou "en" (traduction par Loc).</summary>
    public static string Language
    {
        get => GetS("opt_lang", Application.systemLanguage == SystemLanguage.French ? "fr" : "en");
        set => SetS("opt_lang", value);
    }

    /// <summary>Tutoriel guidé : conseil sous l'objectif et flèche vers le bouton à toucher.</summary>
    public static bool TutorialHints
    {
        get => GetI("opt_tuto", 1) == 1;
        set => SetI("opt_tuto", value ? 1 : 0);
    }

    /// <summary>Qualité graphique (GraphicsQuality) : 0 Économie, 1 Équilibrée (téléphone), 2 Haute (ordinateur).</summary>
    public static int QualityLevel
    {
        get => GetI("opt_quality", Application.isMobilePlatform ? 1 : 2);
        set => SetI("opt_quality", Mathf.Clamp(value, 0, 2));
    }

    /// <summary>Le joueur a choisi sa qualité : plus de descente automatique.</summary>
    public static bool QualityChosen
    {
        get => GetI("opt_quality_set", 0) == 1;
        set => SetI("opt_quality_set", value ? 1 : 0);
    }

    public static bool ScreenShake
    {
        get => GetI("opt_shake", 1) == 1;
        set => SetI("opt_shake", value ? 1 : 0);
    }

    /// <summary>Applique les options qui ont un effet immédiat.</summary>
    public static void Apply()
    {
        AudioListener.volume = Volume;
        GraphicsQuality.Apply();   // post-traitement, halo, particules, cadence selon la qualité graphique
    }
}
