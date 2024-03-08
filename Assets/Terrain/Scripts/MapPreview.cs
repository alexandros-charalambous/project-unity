using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapPreview : MonoBehaviour
{

    public enum DrawMode {NoiseMap, Mesh, FalloffMap};
    public DrawMode drawMode;

    [Header("Attributes")]
    public Renderer textureRenderer;
    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;
    public Material terrainMaterial;

    [Header("Settings")]
    public MeshSettings meshSettings;
    public HeightMapSettings heightMapSettings;

    [Header("Map")]
    [Range(0, MeshSettings.numSupportedLODs - 1)]public int editorPreviewLOD;

    [Header("Editor")]
    public bool autoUpdate;

    public void DrawMapInEditor()
    {        
        HeightMap heightMap = HeightMapGenerator.GenerateHeightMap(meshSettings.numberVerticesPerLine, meshSettings.numberVerticesPerLine, heightMapSettings, Vector2.zero);
        if (drawMode == DrawMode.NoiseMap) {
            DrawTexture(TextureGenerator.TextureFromHeightMap(heightMap));
        }
        else if (drawMode == DrawMode.Mesh) {
            DrawMesh(MeshGenerator.GenerateTerrainMesh(heightMap.values, meshSettings, editorPreviewLOD));
        }
        else if (drawMode == DrawMode.FalloffMap) {
            DrawTexture(TextureGenerator.TextureFromHeightMap(new HeightMap(FalloffGenerator.GenerateFalloffMap(meshSettings.numberVerticesPerLine), 0, 1)));
        }
    }

    public void DrawTexture(Texture2D texture)
    {
        textureRenderer.sharedMaterial.mainTexture = texture;
        textureRenderer.transform.localScale = new Vector3(texture.width, 1, texture.height) / 10f;

        textureRenderer.gameObject.SetActive(true);
        meshFilter.gameObject.SetActive(false);
    }

    public void DrawMesh(MeshData meshData)
    {
        meshFilter.sharedMesh = meshData.CreateMesh();

        textureRenderer.gameObject.SetActive(false);
        meshFilter.gameObject.SetActive(true);
    }
    
    void OnValuesUpdated()
    {
        if (!Application.isPlaying)
        {
            DrawMapInEditor();
        }
    }

    void OnValidate()
    {
        if (meshSettings != null)
        {            
            meshSettings.OnValuesUpdated -= OnValuesUpdated;
            meshSettings.OnValuesUpdated += OnValuesUpdated;
        }
        
        if (heightMapSettings != null)
        {
            heightMapSettings.OnValuesUpdated -= OnValuesUpdated;
            heightMapSettings.OnValuesUpdated += OnValuesUpdated;
        }
    }
}
