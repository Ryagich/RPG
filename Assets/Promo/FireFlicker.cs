
using UnityEngine;

[RequireComponent(typeof(Light))]
public class FireFlicker : MonoBehaviour
{
    [Header("Base Flicker")]
    [SerializeField, Range(0.1f, 20f)]
    private float flickerSpeed = 3f;

    [Header("Random Flashes")]
    [SerializeField, Min(0f)]
    private float minFlashDelay = 0.2f;

    [SerializeField, Min(0f)]
    private float maxFlashDelay = 1.2f;

    [SerializeField, Min(0.01f)]
    private float flashDuration = 0.2f;

    [SerializeField, Range(1f, 30f)]
    private float flashSharpness = 10f;

    [SerializeField, Range(0f, 1f)]
    private float flashStrength = 0.6f;

    [Header("Intensity")]
    [SerializeField] private float minIntensity = 1.5f;
    [SerializeField] private float maxIntensity = 3f;

    [Header("Light Range")]
    [SerializeField] private float minRange = 5f;
    [SerializeField] private float maxRange = 8f;

    [Header("Light Color")]
    [SerializeField] private bool changeColor = true;
    [SerializeField] private Color minColor = new Color(1f, 0.35f, 0.1f);
    [SerializeField] private Color maxColor = new Color(1f, 0.65f, 0.25f);

    private Light fireLight;
    private float noiseOffset;

    private float flashTimer;
    private bool isFlashing;

    private void Awake()
    {
        fireLight = GetComponent<Light>();
        noiseOffset = Random.Range(0f, 1000f);

        ScheduleNextFlash();
    }

    private void ScheduleNextFlash()
    {
        isFlashing = false;

        float min = Mathf.Min(minFlashDelay, maxFlashDelay);
        float max = Mathf.Max(minFlashDelay, maxFlashDelay);

        flashTimer = Random.Range(min, max);
    }

    private void Update()
    {
        // Smooth base flicker
        float noise = Mathf.PerlinNoise(
            Time.time * flickerSpeed + noiseOffset, 0f
        );

        // Random flash timing
        float flash = 0f;

        if (isFlashing)
        {
            flashTimer += Time.deltaTime;

            float progress = Mathf.Clamp01(
                flashTimer / Mathf.Max(0.01f, flashDuration)
            );

            // Smooth rise and fall
            float wave = Mathf.Sin(progress * Mathf.PI);
            flash = Mathf.Pow(wave, flashSharpness);

            if (progress >= 1f)
                ScheduleNextFlash();
        }
        else
        {
            flashTimer -= Time.deltaTime;

            if (flashTimer <= 0f)
            {
                isFlashing = true;
                flashTimer = 0f;
            }
        }

        // Combine flicker and flashes
        float value = Mathf.Lerp(
            noise,
            Mathf.Max(noise, flash),
            flashStrength
        );

        fireLight.intensity = Mathf.Lerp(
            minIntensity, maxIntensity, value
        );

        fireLight.range = Mathf.Lerp(
            minRange, maxRange, value
        );

        if (changeColor)
        {
            fireLight.color = Color.Lerp(
                minColor, maxColor, value
            );
        }
    }
}
