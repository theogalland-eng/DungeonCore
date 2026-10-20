using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// QUALITÉ GRAPHIQUE (Options) — trois niveaux, Équilibrée par défaut sur téléphone, Haute sur ordinateur :
///   Haute      : HDR, halo lumineux (bloom), vignettage et couleurs, 60 images/s ;
///   Équilibrée : sans HDR ni halo (les deux passes les plus coûteuses), vignettage et couleurs, 30 images/s sur téléphone (60 sur ordinateur) ;
///   Économie   : aucun post-traitement, rendu à 75 % de la résolution (téléphone), particules d'ambiance coupées, 30 images/s.
/// Sur téléphone, si le jeu tourne nettement sous 45 images/s, on descend d'un niveau tout seul (une fois par partie),
/// tant que le joueur n'a pas choisi lui-même.
/// </summary>
public static class GraphicsQuality
{
    public enum Level { Economy = 0, Balanced = 1, High = 2 }

    public static Level Current => (Level)Mathf.Clamp(GameSettings.QualityLevel, 0, 2);

    public static string Name(Level l) => l == Level.Economy ? "Économie" : l == Level.Balanced ? "Équilibrée" : "Haute";

    public static void Set(Level l, bool byPlayer)
    {
        GameSettings.QualityLevel = (int)l;
        if (byPlayer) GameSettings.QualityChosen = true;
        Apply();
    }

    public static void Apply()
    {
        var level = Current;
        foreach (var cam in Camera.allCameras)
        {
            cam.eventMask = 0;                                  // aucun OnMouse* dans le jeu : pas de lancer de rayon à chaque image
            cam.allowHDR = level == Level.High;
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data != null) data.renderPostProcessing = level != Level.Economy && GameSettings.PostProcessing;
        }
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            v.enabled = level != Level.Economy && GameSettings.PostProcessing;
            if (v.profile != null && v.profile.TryGet<Bloom>(out var bloom)) bloom.active = level == Level.High;
        }
        // téléphone : 60 images/s seulement en Haute — à 60 en continu il chauffe et ralentit au bout de 10-15 min
        Application.targetFrameRate = level == Level.High || (level == Level.Balanced && !Application.isMobilePlatform) ? 60 : 30;
        // l'échelle de rendu modifie l'asset du pipeline : seulement dans le jeu installé, pas dans l'éditeur
        if (!Application.isEditor && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            urp.renderScale = level == Level.Economy ? 0.75f : 1f;
        var fx = Object.FindFirstObjectByType<AmbientFx>();
        if (fx != null) fx.SetParticlesVisible(GameSettings.Ambient && level != Level.Economy);
    }

    // ── Descente automatique sur téléphone
    private static float watchFrom = -1f, sum;
    private static int count;
    private static bool lowered;

    /// <summary>Appelé à chaque image (GameManager) : moyenne des images sur 10 s, après 20 s de jeu.</summary>
    public static void Watch()
    {
        if (!Application.isMobilePlatform || lowered || GameSettings.QualityChosen || Current == Level.Economy) return;
        if (watchFrom < 0f) { watchFrom = Time.realtimeSinceStartup + 20f; return; }
        if (Time.realtimeSinceStartup < watchFrom) return;
        sum += Time.unscaledDeltaTime;
        count++;
        if (sum < 10f) return;
        float fps = count / sum;
        sum = 0f; count = 0;
        if (fps >= (Application.targetFrameRate <= 30 ? 25f : 45f)) return;
        lowered = true;
        Set(Current - 1, false);
        GameHUD.Toast($"Qualité graphique : {Name(Current)} (pour plus de fluidité — modifiable dans les Options)", GameHUD.ManaColor);
    }
}
