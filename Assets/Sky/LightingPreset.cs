using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TextCore.Text;

[System.Serializable]
[CreateAssetMenu(fileName ="Lighting Preset", menuName ="Scriptables/Lighting Preset",order =1)]
public class LightingPreset : ScriptableObject
{
    [Header("Light Presets")]
    public Gradient AmbientColor;
    public Gradient DirectionalColor;
    public Gradient FogColor;

    
    [Header("Sky Presets")]
    public Gradient TopColor;
    public Gradient HorizonColor;
    public Gradient BottomColor;
    public Gradient CloudColor;
}