using UnityEditor;
using UnityEngine;

/// <summary>
/// Réglages d'import des sons (mobile) : musiques en streaming, ambiances compressées en mémoire.
/// Les fichiers déposés dans Assets/Resources/Audio/... sont réglés automatiquement.
/// </summary>
public class AudioImport : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (!assetPath.Replace('\\', '/').Contains("Resources/Audio/")) return;
        var imp = (AudioImporter)assetImporter;
        imp.forceToMono = assetPath.Contains("/Ambience/") || assetPath.Contains("/Sfx/");
        imp.loadInBackground = !assetPath.Contains("/Sfx/");
        var s = imp.defaultSampleSettings;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.6f;
        s.loadType = assetPath.Contains("/Music/") ? AudioClipLoadType.Streaming
            : assetPath.Contains("/Sfx/") ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
        s.preloadAudioData = !assetPath.Contains("/Music/");
        imp.defaultSampleSettings = s;
    }
}
