using UnityEngine;

/// <summary>Vibrations du téléphone (option), jamais plus d'une par seconde.</summary>
public static class Haptics
{
    private static float last = -999f;

    public static void Pulse()
    {
        if (!GameSettings.Vibration || Time.unscaledTime - last < 1f) return;
        last = Time.unscaledTime;
#if UNITY_ANDROID || UNITY_IOS
        if (Application.isMobilePlatform) Handheld.Vibrate();
#endif
    }
}
