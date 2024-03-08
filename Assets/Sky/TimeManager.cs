using UnityEngine;

[ExecuteAlways]
public class LightingManager : MonoBehaviour
{
    //Scene References
    [SerializeField] private Light DayDirectionalLight;
    [SerializeField] private LightingPreset DayPreset;
    //Variables
    [SerializeField, Range(0, 24)] private float TimeOfDay;
    public int hours;
    public int minutes;
    public bool pauseTimer;


    private void Update()
    {
        if (DayPreset == null)
            return;

        if (!pauseTimer)
        {
            if (Application.isPlaying)
            {
                //(Replace with a reference to the game time)
                TimeOfDay += Time.deltaTime / 60f;
                TimeOfDay %= 24; //Modulus to ensure always between 0-24
                UpdateLighting(TimeOfDay / 24f);
                hours = (int)TimeOfDay;
                minutes = (int)((TimeOfDay - hours) * 60);
            }
        }
    }


    private void UpdateLighting(float timePercent)  
    {
        //Set ambient and fog
        RenderSettings.ambientLight = DayPreset.AmbientColor.Evaluate(timePercent);
        RenderSettings.fogColor = DayPreset.FogColor.Evaluate(timePercent);        

        //If the directional light is set then rotate and set it's color
        if (DayDirectionalLight != null)
        {
            DayDirectionalLight.color = DayPreset.DirectionalColor.Evaluate(timePercent);
            DayDirectionalLight.transform.localRotation = Quaternion.Euler(new Vector3((timePercent * 360f) - 90f, 170f, 0));          

        }
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
}