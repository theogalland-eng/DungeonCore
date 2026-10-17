using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Lueur d'une coulée de lave : l'intensité palpite lentement, chaque lueur à son rythme.</summary>
[RequireComponent(typeof(Light2D))]
public class LavaGlow : MonoBehaviour
{
    private Light2D l;
    private float baseIntensity, phase, speed;

    private void Start()
    {
        l = GetComponent<Light2D>();
        baseIntensity = l.intensity;
        phase = (transform.position.x * 0.37f + transform.position.y * 0.61f) % 6.28f;
        speed = 0.8f + Mathf.Abs(Mathf.Sin(phase * 3.1f)) * 0.9f;
    }

    private void Update()
    {
        float t = Time.time * speed + phase;
        l.intensity = baseIntensity * (0.8f + 0.2f * Mathf.Sin(t) + 0.08f * Mathf.Sin(t * 3.7f));
    }
}
