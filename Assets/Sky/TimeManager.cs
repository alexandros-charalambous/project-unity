using Unity.VisualScripting;
using UnityEngine;

[ExecuteAlways]
public class LightingManager : MonoBehaviour
{
    [Header("Lights")]
    [SerializeField] private Light DayDirectionalLight;
    [SerializeField] private Light NightDirectionalLight;
    [SerializeField] private LightingPreset GradientPreset;

    [Header("Time Of Day")]
    [SerializeField, Range(0, 24)] private float TimeOfDay;
    public int hours;
    public int minutes;
    public bool pauseTimer;
    
    [Header("Light Intensity")]
    [SerializeField] [Range(0,1)] private float dayIntensity;
    [SerializeField] [Range(0,1)] private float nightIntensity;

    private void Update()
    {
        if (GradientPreset == null)
            return;

        if (!pauseTimer)
        {
            if (Application.isPlaying)
            {
                TimeOfDay += Time.deltaTime / 60f;
                TimeOfDay %= 24; //Modulus to ensure always between 0-24
                UpdateLighting(TimeOfDay / 24f);
                UpdateLightIntensity();
                UpdateSkybox(TimeOfDay / 24f);
                hours = (int)TimeOfDay;
                minutes = (int)((TimeOfDay - hours) * 60);
            }
        }
    }


    private void UpdateLighting(float timePercent)  
    {
        //Set ambient and fog
        RenderSettings.ambientLight = GradientPreset.AmbientColor.Evaluate(timePercent);
        RenderSettings.fogColor = GradientPreset.FogColor.Evaluate(timePercent);        

        //If the directional light is set then rotate and set it's color
        if (DayDirectionalLight != null && NightDirectionalLight != null)
        {
            DayDirectionalLight.color = GradientPreset.DirectionalColor.Evaluate(timePercent);
            DayDirectionalLight.transform.localRotation = Quaternion.Euler(new Vector3((timePercent * 360f) - 90f, 170f, 0));
            NightDirectionalLight.color = GradientPreset.DirectionalColor.Evaluate(timePercent);
            NightDirectionalLight.transform.localRotation = Quaternion.Euler(new Vector3((timePercent * 360f) + 90f, 170f, 0));

        }
    }

    private void UpdateLightIntensity()
    {
        // DayDirectionalLight.enabled = hours >= 6 && hours < 18 ? true : false;
        // NightDirectionalLight.enabled = hours < 6 || hours >= 18 ? true : false;
        if (hours >= 6 && hours < 18)
        {
            DayDirectionalLight.enabled = true;
            if (DayDirectionalLight.intensity < dayIntensity)
            {
                DayDirectionalLight.intensity += Time.deltaTime / 2f;
            } else {
                DayDirectionalLight.intensity = dayIntensity;
            }
        } else {
            if (DayDirectionalLight.intensity > 0)
            {                    
                DayDirectionalLight.intensity -= Time.deltaTime / 2f;
            }
            else
            {
                DayDirectionalLight.intensity = 0f;
                DayDirectionalLight.enabled = false;
            }
        }

        if (hours < 6 || hours >= 18)
        {
            NightDirectionalLight.enabled = true;
            if (NightDirectionalLight.intensity < nightIntensity)
            {
                NightDirectionalLight.intensity += Time.deltaTime / 2f;
            } else {
                NightDirectionalLight.intensity = nightIntensity;
            }
        } else {
            if (NightDirectionalLight.intensity > 0)
            {                    
                NightDirectionalLight.intensity -= Time.deltaTime / 2f;
            }
            else
            {
                NightDirectionalLight.intensity = 0;
                NightDirectionalLight.enabled = false;
            }
        }
    }

    private void UpdateSkybox(float timePercent)
    {
        RenderSettings.skybox.SetColor("_TopColor", GradientPreset.TopColor.Evaluate(timePercent));
        RenderSettings.skybox.SetColor("_HorizonColor", GradientPreset.HorizonColor.Evaluate(timePercent));
        RenderSettings.skybox.SetColor("_BottomColor", GradientPreset.BottomColor.Evaluate(timePercent));
        RenderSettings.skybox.SetColor("_CloudColor", GradientPreset.CloudColor.Evaluate(timePercent));
        RenderSettings.skybox.SetVector("_SunLightDirection", DayDirectionalLight.transform.forward);
        RenderSettings.skybox.SetVector("_MoonLightDirection", NightDirectionalLight.transform.forward);
    }

    //Try to find a directional light to use if we haven't set one
    private void OnValidate()
    {
        if (DayDirectionalLight != null)
            return;

        //Search for lighting tab sun
        if (RenderSettings.sun != null)
        {
            DayDirectionalLight = RenderSettings.sun;
        }
        //Search scene for light that fits criteria (directional)
        else
        {
            Light[] lights = GameObject.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional)
                {                    
                    DayDirectionalLight = RenderSettings.sun;
                    return;
                }
            }
        }
    }

    void OnApplicationQuit()
    {
        TimeOfDay = 10f;
        DayDirectionalLight.transform.localRotation = Quaternion.Euler(new Vector3((TimeOfDay / 24 * 360f) - 90f, 170f, 0));
        NightDirectionalLight.transform.localRotation = Quaternion.Euler(new Vector3((TimeOfDay / 24 * 360f) + 90f, 170f, 0));
        UpdateSkybox(TimeOfDay / 24f);
    }
}