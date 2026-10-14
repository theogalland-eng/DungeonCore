using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Torche : lumière vacillante.</summary>
public class TorchFlicker : MonoBehaviour
{
    public float baseIntensity = 1f;
    public float amplitude = 0.18f;
    private Light2D light2D;
    private float seed;

    private void Start()
    {
        light2D = GetComponentInChildren<Light2D>();
        if (light2D != null) baseIntensity = light2D.intensity;
        seed = Random.value * 100f;
    }

    private void Update()
    {
        if (light2D == null) return;
        float n = Mathf.PerlinNoise(seed, Time.time * 6f) - 0.5f;
        light2D.intensity = baseIntensity * (1f + n * 2f * amplitude);
    }
}
