using UnityEngine;

/// <summary>Animation image par image sans Animator (bateaux, torches, feux, pièges).</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAnim : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 8f;
    public bool randomStart;
    [Tooltip("Joue une seule fois puis s'arrête sur la dernière image")]
    public bool once;

    private SpriteRenderer sr;
    private float t;

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    private void Start()
    {
        if (randomStart && frames != null && frames.Length > 0) t = Random.Range(0f, frames.Length);
        if (sr != null && frames != null && frames.Length > 0) sr.sprite = frames[0];
    }

    public void Restart() => t = 0f;

    private void Update()
    {
        if (frames == null || frames.Length == 0 || sr == null) return;
        t += Time.deltaTime * fps;
        int i = once ? Mathf.Min((int)t, frames.Length - 1) : (int)t % frames.Length;
        sr.sprite = frames[i];
    }
}
