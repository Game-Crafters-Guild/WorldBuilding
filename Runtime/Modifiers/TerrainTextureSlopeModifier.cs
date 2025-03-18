using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public class TerrainTextureSlopeModifier : ITerrainSplatModifier
    {
        [Range(0.0f, 90.0f)] public float SlopeMin = 0.0f;
        [Range(0.0f, 90.0f)] public float SlopeMax = 90.0f;
        [Range(0.0f, 1.0f)] public float Intensity = 1.0f;

        public TerrainLayer TerrainLayer;
        public MaskFalloff Falloff;

        public override string FilePath => GetFilePath();

        public override void ApplySplatmap(WorldBuildingContext context, Bounds worldBounds, Texture mask)
{
    // Skip if normal map is not available
    if (context.NormalRenderTexture == null)
    {
        Debug.LogWarning("Normal map not available for slope-based texture application.");
        return;
    }

    context.MaskFalloff = Falloff;
    
    // Calculate slope parameters - convert degrees to cosine values
    // Note: For slopes, we use the cosine of the angle (dot product with up vector)
    // cos(0°) = 1 (flat), cos(90°) = 0 (vertical)
    float minCos = Mathf.Cos(Mathf.Deg2Rad * SlopeMax);
    float maxCos = Mathf.Cos(Mathf.Deg2Rad * SlopeMin);
    
    int terrainLayerIndex = context.GetTerrainLayerIndex(TerrainLayer);
    int layerRenderTextureIndex = terrainLayerIndex / 4;
    RenderTexture layerRenderTarget = context.SplatRenderTextures[layerRenderTextureIndex];
    
    // Set up the material property block
    MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
    materialPropertyBlock.SetTexture("_Mask", mask);
    materialPropertyBlock.SetTexture("_NormalMap", context.NormalRenderTexture);
    
    // Calculate terrain space position and size for proper normal map sampling
    Vector2 terrainPos = context.WorldPositionToTerrainSpace(worldBounds.center);
    Vector2 terrainSize = new float2(worldBounds.size.x, worldBounds.size.z) / context.TerrainSize;
    
    // Pass terrain-space coordinates to the shader
    materialPropertyBlock.SetVector("_TerrainUVParams", new Vector4(
        terrainPos.x, // center X in terrain space (0-1)
        terrainPos.y, // center Y in terrain space (0-1)
        terrainSize.x, // size X in terrain space (0-1)
        terrainSize.y  // size Y in terrain space (0-1)
    ));
    context.m_ApplySplatmapMaterial.SetVector("_TerrainUVParams", new Vector4(
        terrainPos.x, // center X in terrain space (0-1)
        terrainPos.y, // center Y in terrain space (0-1)
        terrainSize.x, // size X in terrain space (0-1)
        terrainSize.y  // size Y in terrain space (0-1)
    ));
    
    // Set slope parameters
    materialPropertyBlock.SetVector("_SlopeRange", new Vector4(minCos, maxCos, 0, 0));
    
    // Set layer intensity
    float4 intensityVector = float4.zero;
    intensityVector[terrainLayerIndex % 4] = Intensity;
    materialPropertyBlock.SetVector("_Intensity", intensityVector);
    
    // Set falloff parameters
    materialPropertyBlock.SetVector("_Falloff", context.MaskFalloff != null ? new float4(
        1.0f - context.MaskFalloff.MaxIntensity,
        1.0f - context.MaskFalloff.MinIntensity,
        (float)context.MaskFalloff.FalloffFunction,
        0.0f) : float4.zero);
            
    materialPropertyBlock.SetVector("_MaskRange", context.MaskFalloff != null ? new float4(
        context.MaskFalloff.MaskMin,
        context.MaskFalloff.MaskMax,
        context.MaskFalloff.InnerFalloff,
        0.0f) : float4.zero);
    
    // First call the inverse slope mask pass
    const int INVERSE_SLOPE_MASK_PASS = 3;
    context.DrawQuad(worldBounds, layerRenderTarget, context.m_ApplySplatmapMaterial, materialPropertyBlock, INVERSE_SLOPE_MASK_PASS);
    
    // Then use the slope-based pass
    const int SLOPE_PASS = 2;
    context.DrawQuad(worldBounds, layerRenderTarget, context.m_ApplySplatmapMaterial, materialPropertyBlock, SLOPE_PASS);
}

        public override int GetNumTerrainLayers()
        {
            return 1;
        }

        public override TerrainLayer GetTerrainLayer(int index)
        {
            return TerrainLayer;
        }
    }
}