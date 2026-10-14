#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Réglages ponctuels du projet (menu DungeonCore/…). Sans effet sur le jeu.
/// </summary>
public static class ProjectSetup
{
    [MenuItem("DungeonCore/Réparer le prefab du piège")]
    public static string FixTrapPrefab()
    {
        const string path = "Assets/Prefabs/TrapSpinningBlade.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        string log = "";
        try
        {
            var rootTrap = root.GetComponent<SpinningBladeTrap>();
            foreach (Transform child in root.transform)
            {
                // La roue doit être centrée sur la case du piège
                if (child.localPosition != Vector3.zero)
                {
                    log += $"{child.name} localPosition {child.localPosition} -> 0; ";
                    child.localPosition = Vector3.zero;
                }
                // Composant de piège en double sur l'enfant
                var dup = child.GetComponent<SpinningBladeTrap>();
                if (dup != null && rootTrap != null)
                {
                    Object.DestroyImmediate(dup, true);
                    log += $"doublon supprimé sur {child.name}; ";
                }
                if (rootTrap != null && rootTrap.bladeTransform == null) rootTrap.bladeTransform = child;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return string.IsNullOrEmpty(log) ? "rien à corriger" : log;
    }

    [MenuItem("DungeonCore/Configurer pour mobile (paysage)")]
    public static string ConfigureMobile()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.productName = "Dungeon Core";
        AssetDatabase.SaveAssets();
        return "Orientation paysage uniquement";
    }
}
#endif
