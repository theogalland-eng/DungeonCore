using UnityEngine;

/// <summary>Animation en boucle (temps réel : elle continue quand le jeu est en pause).</summary>
public class LoopAnim : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 8f;
    public float phase;
    private SpriteRenderer sr;

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    private void Update()
    {
        if (frames == null || frames.Length == 0 || sr == null) return;
        sr.sprite = frames[(int)((Time.unscaledTime + phase) * fps) % frames.Length];
    }
}
