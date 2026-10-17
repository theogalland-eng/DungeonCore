using System.Collections;
using UnityEngine;

/// <summary>
/// Retour d'impact : silhouette blanche d'un instant sur le sprite touché (shader DungeonCore/HitFlash),
/// micro-pause du jeu sur les coups qui comptent, étincelles.
/// </summary>
[DefaultExecutionOrder(1100)]
public class HitFlashFx : MonoBehaviour
{
    private static Material mat;
    private SpriteRenderer src, flash;
    private float until;

    /// <summary>Flash d'impact sur ce sprite.</summary>
    public static void Flash(SpriteRenderer sr, float duration = 0.08f)
    {
        if (sr == null || !sr.gameObject.activeInHierarchy) return;
        var fx = sr.GetComponent<HitFlashFx>();
        if (fx == null) fx = sr.gameObject.AddComponent<HitFlashFx>();
        fx.Play(duration);
    }

    /// <summary>Le temps s'arrête un instant : le coup « pèse ».</summary>
    public static void Hitstop(float seconds)
    {
        if (GameManager.Instance == null || Time.timeScale <= 0.01f) return;
        GameManager.Instance.StartCoroutine(HitstopRoutine(seconds));
    }

    private static bool stopping;

    private static IEnumerator HitstopRoutine(float seconds)
    {
        if (stopping) yield break;
        stopping = true;
        float prev = Time.timeScale;
        Time.timeScale = 0.04f;
        yield return new WaitForSecondsRealtime(seconds);
        // si une pause est arrivée entre-temps, on ne la défait pas
        if (Mathf.Approximately(Time.timeScale, 0.04f)) Time.timeScale = prev;
        stopping = false;
    }

    public static void Sparks(Vector3 at, Color c, float size = 0.6f)
    {
        Fx.Play("shine", at, size, 26f, 40, c);
    }

    private void Play(float duration)
    {
        src = GetComponent<SpriteRenderer>();
        if (flash == null)
        {
            if (mat == null)
            {
                var sh = Resources.Load<Shader>("Shaders/HitFlash");
                if (sh == null) return;
                mat = new Material(sh);
            }
            var go = new GameObject("Flash");
            go.transform.SetParent(transform, false);
            flash = go.AddComponent<SpriteRenderer>();
            flash.sharedMaterial = mat;
        }
        until = Time.time + duration;
        Sync();
    }

    private void LateUpdate()
    {
        if (flash == null) return;
        Sync();
    }

    private void Sync()
    {
        bool on = Time.time < until && src != null && src.enabled;
        flash.enabled = on;
        if (!on) return;
        flash.sprite = src.sprite;
        flash.flipX = src.flipX;
        flash.flipY = src.flipY;
        flash.sortingLayerID = src.sortingLayerID;
        flash.sortingOrder = src.sortingOrder + 1;
        flash.color = new Color(1f, 1f, 1f, 0.85f * Mathf.Clamp01((until - Time.time) / 0.05f + 0.3f));
    }
}
