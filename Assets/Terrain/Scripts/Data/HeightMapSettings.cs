using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[CreateAssetMenu(menuName = "HeightMapSettings")]
public class HeightMapSettings : UpdatableData
{    
    public NoiseSettings noiseSettings;
    
    [Header("Map Variants")]
    public bool useFalloff;
    
    [Header("Mesh")]
    public float heightMulyiplier;
    public AnimationCurve heightCurve;

    public float minHeight 
    {
        get {
            return heightMulyiplier * heightCurve.Evaluate(0);
        }
    }
    
    public float maxHeight 
    {
        get {
            return heightMulyiplier * heightCurve.Evaluate(1);
        }
    }
    
    
    #if UNITY_EDITOR
    protected override void OnValidate()
    {        
        noiseSettings.ValidateValues();
        base.OnValidate();
    }
    #endif
}
