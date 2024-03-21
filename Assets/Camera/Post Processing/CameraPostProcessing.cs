using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraPostProcessing : MonoBehaviour
{
    [Header("Depth Parameters")]
    [SerializeField] private Transform mainCamera;
    [SerializeField] private int depth;

    [Header("Post Processing Volume")]
    [SerializeField] private Volume postProcessingVolume;

    [Header("Post Processing Profiles")]
    [SerializeField] private VolumeProfile surfacePostProcessing;
    [SerializeField] private VolumeProfile underwaterPostProcessing;


    void Update()
    {            
        if (mainCamera.position.y > depth)
        {
            postProcessingVolume.profile = surfacePostProcessing;
            // RenderSettings.fog = true;
        }
        else
        {
            postProcessingVolume.profile = underwaterPostProcessing;
            // RenderSettings.fog = false;
        }
    }
}
