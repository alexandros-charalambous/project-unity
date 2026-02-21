using UnityEngine;

[CreateAssetMenu(fileName = "SurfaceHeightSettings", menuName = "Terrain/Surface Height Settings")]
public class SurfaceHeightSettings : UpdatableData
{
    [Header("Noise")]
    [Tooltip("Noise configuration used to generate the base 2D surface heightmap (world XZ → normalized value 0..1).\n\nTip: For larger-scale, calmer terrain: increase scale and/or reduce octaves.")]
    public NoiseSettings noiseSettings = new NoiseSettings { normalizeMode = Noise.NormalizeMode.Global };

    [Header("Height")]
    [Tooltip("World-space base elevation (Y). Think: sea level / average ground level.\n\nRaising this lifts the entire terrain up uniformly.")]
    public float baseHeight = 0f;

    [Tooltip("Maximum height added above baseHeight after applying the height curve.\n\nHigher values = taller mountains / deeper valleys from the same noise.")]
    [Min(0f)]
    public float heightMultiplier = 650f;

    [Tooltip("Shapes normalized noise (0..1) into a height fraction (0..1).\n\nUse this to bias terrain towards flatter plains (gentle early curve) or sharper peaks (steeper late curve).")]
    public AnimationCurve heightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        if (noiseSettings == null)
        {
            noiseSettings = new NoiseSettings();
        }

        if (heightCurve == null)
        {
            heightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        noiseSettings.ValidateValues();
        heightMultiplier = Mathf.Max(0f, heightMultiplier);
        base.OnValidate();
    }
#endif
}
