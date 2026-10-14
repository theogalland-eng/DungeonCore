using UnityEngine;

/// <summary>Objet exposé sur un présentoir : léger flottement et scintillement selon sa rareté.</summary>
public class TrophyShine : MonoBehaviour
{
    public int rarity;
    private Vector3 basePos;
    private float phase;
    private float nextSparkle;

    private void Start()
    {
        basePos = transform.localPosition;
        phase = Random.value * 6f;
        nextSparkle = Time.time + Random.Range(1f, 4f);
    }

    private void Update()
    {
        transform.localPosition = basePos + Vector3.up * (0.03f * Mathf.Sin(Time.time * 2f + phase));
        if (rarity >= 2 && Time.time > nextSparkle)
        {
            nextSparkle = Time.time + Random.Range(2.5f, 6f) / rarity;
            FloatingText.Spawn(transform.position + (Vector3)(Random.insideUnitCircle * 0.2f), "+", LootSystem.RarityColors[rarity], 0.45f);
        }
    }
}
