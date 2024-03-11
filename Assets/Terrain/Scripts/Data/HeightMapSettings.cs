using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[CreateAssetMenu(menuName = "HeightMapSettings")]
public class HeightMapSettings : UpdatableData
{    
    public NoiseSettings noiseSettings;
    
    [Header("Mesh")]
    public float heightMultyplier;
    public AnimationCurve heightCurve;

    public float minHeight 
    {
        get {
            return heightMultyplier * heightCurve.Evaluate(0);
        }
    }
    
    public float maxHeight 
    {
        get {
            return heightMultyplier * heightCurve.Evaluate(1);
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
