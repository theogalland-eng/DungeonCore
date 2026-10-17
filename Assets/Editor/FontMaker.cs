using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Polices TextMeshPro du jeu (dynamiques) à partir des TTF d'Assets/Fonts (Cinzel, licence OFL) :
///   - Resources/Fonts/Title SDF : Cinzel Decorative Bold (titres)
///   - Resources/Fonts/Head SDF  : Cinzel (en-têtes, boutons du menu)
///   - Resources/Fonts/Body SDF  : Alegreya Sans Medium (texte courant ; gras et italique liés)
/// </summary>
public static class FontMaker
{
    [MenuItem("DungeonCore/12. Créer les polices (Cinzel, Alegreya Sans)")]
    public static string Make()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Fonts")) AssetDatabase.CreateFolder("Assets/Resources", "Fonts");
        string a = Create("Assets/Fonts/CinzelDecorative-Bold.ttf", "Assets/Resources/Fonts/Title SDF.asset");
        string b = Create("Assets/Fonts/Cinzel-Variable.ttf", "Assets/Resources/Fonts/Head SDF.asset");
        // halo doré sous le titre (variante « underlay » du shader, enregistrée pour être gardée au build)
        var title = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Title SDF.asset");
        if (title != null)
        {
            AssetDatabase.DeleteAsset("Assets/Resources/Fonts/Title Glow.mat");
            var m = new Material(title.material) { name = "Title Glow" };
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(1f, 0.55f, 0.15f, 0.55f));
            m.SetFloat("_UnderlaySoftness", 0.75f);
            m.SetFloat("_UnderlayDilate", 0.35f);
            m.EnableKeyword("OUTLINE_ON");
            m.SetColor("_OutlineColor", new Color(0.18f, 0.08f, 0.02f, 1f));
            m.SetFloat("_OutlineWidth", 0.12f);
            m.SetFloat("_FaceDilate", 0.08f);
            AssetDatabase.CreateAsset(m, "Assets/Resources/Fonts/Title Glow.mat");
        }
        string c = MakeBody();
        AssetDatabase.SaveAssets();
        return a + " | " + b + " | " + c;
    }

    /// <summary>Texte courant : Alegreya Sans (humaniste, très lisible petit), avec ses graisses grasse et italique.</summary>
    public static string MakeBody()
    {
        string r = Create("Assets/Fonts/AlegreyaSans/AlegreyaSans-Medium.ttf", "Assets/Resources/Fonts/Body SDF.asset");
        Create("Assets/Fonts/AlegreyaSans/AlegreyaSans-Bold.ttf", "Assets/Resources/Fonts/Body Bold SDF.asset");
        Create("Assets/Fonts/AlegreyaSans/AlegreyaSans-Italic.ttf", "Assets/Resources/Fonts/Body Italic SDF.asset");
        var body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Body SDF.asset");
        var bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Body Bold SDF.asset");
        var italic = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Body Italic SDF.asset");
        if (body == null) return r;
        var table = body.fontWeightTable;
        table[7].regularTypeface = bold;     // <b> → Alegreya Sans Bold
        table[4].italicTypeface = italic;    // <i> → Alegreya Sans Italic
        EditorUtility.SetDirty(body);
        return r;
    }

    static string Create(string ttf, string path)
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
        if (font == null) return "introuvable : " + ttf;
        AssetDatabase.DeleteAsset(path);
        var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        fa.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(fa, path);
        foreach (var t in fa.atlasTextures) { if (t == null) continue; t.name = fa.name + " Atlas"; AssetDatabase.AddObjectToAsset(t, fa); }
        fa.material.name = fa.name + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        // pré-remplit les caractères courants (français compris)
        fa.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,;:!?'’«»-–—()…àâäçéèêëîïôöùûüœÀÂÄÇÉÈÊËÎÏÔÖÙÛÜŒ·◆", out _);
        EditorUtility.SetDirty(fa);
        return "ok " + path;
    }
}
