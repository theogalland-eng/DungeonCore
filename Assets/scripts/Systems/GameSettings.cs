using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Options du joueur (PlayerPrefs), communes à tous les profils de l'appareil.</summary>
public static class GameSettings
{
    public static float Volume
    {
        get => PlayerPrefs.GetFloat("opt_volume", 0.8f);
        set { PlayerPrefs.SetFloat("opt_volume", Mathf.Clamp01(value)); Apply(); }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat("opt_music", 0.7f);
        set => PlayerPrefs.SetFloat("opt_music", Mathf.Clamp01(value));
    }

    public static float AmbienceVolume
    {
        get => PlayerPrefs.GetFloat("opt_ambience", 0.8f);
        set => PlayerPrefs.SetFloat("opt_ambience", Mathf.Clamp01(value));
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat("opt_sfx", 0.8f);
        set => PlayerPrefs.SetFloat("opt_sfx", Mathf.Clamp01(value));
    }

    public static bool Cinematics
    {
        get => PlayerPrefs.GetInt("opt_cine", 1) == 1;
        set => PlayerPrefs.SetInt("opt_cine", value ? 1 : 0);
    }

    public static bool PostProcessing
    {
        get => PlayerPrefs.GetInt("opt_post", 1) == 1;
        set { PlayerPrefs.SetInt("opt_post", value ? 1 : 0); Apply(); }
    }

    public static bool Ambient
    {
        get => PlayerPrefs.GetInt("opt_ambient", 1) == 1;
        set { PlayerPrefs.SetInt("opt_ambient", value ? 1 : 0); Apply(); }
    }

    public static bool AutoSave
    {
        get => PlayerPrefs.GetInt("opt_autosave", 1) == 1;
        set => PlayerPrefs.SetInt("opt_autosave", value ? 1 : 0);
    }

    /// <summary>Échelle de l'interface (0,8 à 1,4) : plus grand = boutons et textes plus gros.</summary>
    public static float UiScale
    {
        get => PlayerPrefs.GetFloat("opt_uiscale", 1f);
        set => PlayerPrefs.SetFloat("opt_uiscale", Mathf.Clamp(Mathf.Round(value * 10f) / 10f, 0.8f, 1.4f));
    }

    /// <summary>Vibrations du téléphone (cristal frappé, héros blessé).</summary>
    public static bool Vibration
    {
        get => PlayerPrefs.GetInt("opt_vibrate", 1) == 1;
        set => PlayerPrefs.SetInt("opt_vibrate", value ? 1 : 0);
    }

    public static bool ScreenShake
    {
        get => PlayerPrefs.GetInt("opt_shake", 1) == 1;
        set => PlayerPrefs.SetInt("opt_shake", value ? 1 : 0);
    }

    /// <summary>Applique les options qui ont un effet immédiat.</summary>
    public static void Apply()
    {
        AudioListener.volume = Volume;
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None)) v.enabled = PostProcessing;
        var fx = Object.FindFirstObjectByType<AmbientFx>();
        if (fx != null) fx.SetParticlesVisible(Ambient);
    }
}
