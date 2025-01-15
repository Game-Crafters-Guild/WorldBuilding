using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using float2 = Unity.Mathematics.float2;

public class WorldBuildingContext
{
    internal RenderTexture m_MaskRenderTexture;

    private float3 m_TerrainPosition;
    private float2 m_TerrainSize;
    public MaskFalloff MaskFalloff { get; set; }

    private RenderTexture m_HeightmapRenderTexture;
    private float MaxTerrainHeight { get; set; }
    public Matrix4x4 CurrentTransform { get; internal set; }
    public Dictionary<TerrainLayer, int> TerrainLayersIndexMap { get; internal set; }
    public RenderTexture[] SplatRenderTextures { get; private set; }
    public RenderTexture HeightmapRenderTexture => m_HeightmapRenderTexture;

    internal Mesh m_Quad;
    internal Material m_ApplyHeightmapMaterial;
    internal Material m_ApplySplatmapMaterial;
    public void ApplyHeightmap(Bounds worldBounds, Texture heightmap, Texture mask, HeightWriteMode mode, float minHeight = 0.0f, float maxHeight = 10.0f)
    {
        float4 heightRangeNormalized = new float4(/*Mathf.Clamp01*/(minHeight / MaxTerrainHeight), /*Mathf.Clamp01*/(maxHeight / MaxTerrainHeight), 0.0f, 0.0f);

        MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
        materialPropertyBlock.SetTexture("_Mask", mask);
        materialPropertyBlock.SetTexture("_Data", heightmap != null ? heightmap : Texture2D.whiteTexture);
        materialPropertyBlock.SetVector("_HeightRange", heightRangeNormalized);
        materialPropertyBlock.SetVector("_Falloff", new Vector4(MaskFalloff.Min, MaskFalloff.Max));
        SetBlendMode(mode, m_ApplyHeightmapMaterial);
        DrawQuad(worldBounds, HeightmapRenderTexture, m_ApplyHeightmapMaterial, materialPropertyBlock);
        
    }

    private void SetBlendMode(HeightWriteMode mode, Material material)
    {
        switch (mode)
        {
            case HeightWriteMode.Add:
                m_ApplyHeightmapMaterial.SetInt("_BlendOp", (int)BlendOp.Add);
                m_ApplyHeightmapMaterial.SetInt("_SrcMode", (int)BlendMode.SrcAlpha);
                m_ApplyHeightmapMaterial.SetInt("_DstMode", (int)BlendMode.One);
                m_ApplyHeightmapMaterial.SetInt("_AlphaSrcMode", (int)BlendMode.SrcAlpha);
                m_ApplyHeightmapMaterial.SetInt("_AlphaDstMode", (int)BlendMode.One);
                break;
            case HeightWriteMode.Subtract:
                m_ApplyHeightmapMaterial.SetInt("_BlendOp", (int)BlendOp.ReverseSubtract);
                m_ApplyHeightmapMaterial.SetInt("_SrcMode", (int)BlendMode.SrcAlpha);
                m_ApplyHeightmapMaterial.SetInt("_DstMode", (int)BlendMode.One);
                m_ApplyHeightmapMaterial.SetInt("_AlphaSrcMode", (int)BlendMode.SrcAlpha);
                m_ApplyHeightmapMaterial.SetInt("_AlphaDstMode", (int)BlendMode.One);
                break;
            
            case HeightWriteMode.Replace:
                m_ApplyHeightmapMaterial.SetInt("_BlendOp", (int)BlendOp.Add);
                m_ApplyHeightmapMaterial.SetInt("_SrcMode", (int)BlendMode.SrcAlpha);
                m_ApplyHeightmapMaterial.SetInt("_DstMode", (int)BlendMode.OneMinusSrcAlpha);
                m_ApplyHeightmapMaterial.SetInt("_AlphaSrcMode", (int)BlendMode.SrcAlpha);
                m_ApplyHeightmapMaterial.SetInt("_AlphaDstMode", (int)BlendMode.OneMinusSrcAlpha);
            break;
        }
    }

    private void DrawQuad(Bounds worldBounds, RenderTexture renderTexture, Material material, MaterialPropertyBlock materialPropertyBlock)
    {
        float2 positionToTerrainSpace = WorldPositionToTerrainSpace(worldBounds.center) - new float2(0.5f, 0.5f);
        float2 sizeToTerrainSpace = new float2(worldBounds.size.x, worldBounds.size.z) / m_TerrainSize;
        Matrix4x4 worldTransform = CurrentTransform;
        worldTransform.m03 = worldTransform.m13 = worldTransform.m23 = 0.0f;

        float3 aspectRatio;
        if (worldBounds.size.x > worldBounds.size.z)
        {
            aspectRatio = new Vector3(1.0f, 1.0f, worldBounds.size.x / worldBounds.size.z);
        }
        else
        {
            aspectRatio = new Vector3(worldBounds.size.z / worldBounds.size.x, 1.0f, 1.0f);
        }
        Matrix4x4 projectionMatrix = Matrix4x4.Ortho(-0.5f, 0.5f, -0.5f, 0.5f, -1.0f, 1.0f);

        CommandBuffer cmd = new CommandBuffer();
        cmd.SetRenderTarget(renderTexture);
        cmd.SetProjectionMatrix(projectionMatrix);
        Matrix4x4 lookAtMatrix = Matrix4x4.LookAt(Vector3.up * 0.5f, Vector3.zero, Vector3.forward).inverse;
        cmd.SetViewMatrix(lookAtMatrix);
        
        Matrix4x4 transform = Matrix4x4.TRS(new Vector3(positionToTerrainSpace.x, 0.0f, positionToTerrainSpace.y),
            quaternion.identity, new Vector3(sizeToTerrainSpace.x, 1.0f, sizeToTerrainSpace.y) * aspectRatio) * worldTransform;
        cmd.DrawMesh(m_Quad, transform, material, 0, 0, properties: materialPropertyBlock);
        Graphics.ExecuteCommandBuffer(cmd);
    }
    
    private void DrawQuadSplat(Bounds worldBounds, RenderTexture renderTexture, Material material, MaterialPropertyBlock materialPropertyBlock)
    {
        float2 positionToTerrainSpace = WorldPositionToTerrainSpace(worldBounds.center) - new float2(0.5f, 0.5f);
        float2 sizeToTerrainSpace = new float2(worldBounds.size.x, worldBounds.size.z) / m_TerrainSize;
        Matrix4x4 worldTransform = CurrentTransform;
        worldTransform.m03 = worldTransform.m13 = worldTransform.m23 = 0.0f;

        float3 aspectRatio;
        if (worldBounds.size.x > worldBounds.size.z)
        {
            aspectRatio = new Vector3(1.0f, 1.0f, worldBounds.size.x / worldBounds.size.z);
        }
        else
        {
            aspectRatio = new Vector3(worldBounds.size.z / worldBounds.size.x, 1.0f, 1.0f);
        }
        Matrix4x4 projectionMatrix = Matrix4x4.Ortho(-0.5f, 0.5f, -0.5f, 0.5f, -1.0f, 1.0f);

        CommandBuffer cmd = new CommandBuffer();
        cmd.SetRenderTarget(renderTexture);
        cmd.SetProjectionMatrix(projectionMatrix);
        Matrix4x4 lookAtMatrix = Matrix4x4.LookAt(Vector3.up * 0.5f, Vector3.zero, Vector3.forward).inverse;
        cmd.SetViewMatrix(lookAtMatrix);
        
        Matrix4x4 transform = Matrix4x4.TRS(new Vector3(positionToTerrainSpace.x, 0.0f, positionToTerrainSpace.y),
            quaternion.identity, new Vector3(sizeToTerrainSpace.x, 1.0f, sizeToTerrainSpace.y) * aspectRatio) * worldTransform;

        cmd.DrawMesh(m_Quad, transform, material, 0, 0, properties: materialPropertyBlock);
        cmd.DrawMesh(m_Quad, transform, material, 0, 1, properties: materialPropertyBlock);
        Graphics.ExecuteCommandBuffer(cmd);
    }

    public void ApplySplatmap(Bounds worldBounds, TerrainLayer layer, Texture mask, float intensity = 1.0f)
    {
        int terrainLayerIndex = TerrainLayersIndexMap[layer];
        int layerRenderTextureIndex = terrainLayerIndex / 4;
        RenderTexture layerRenderTarget = SplatRenderTextures[layerRenderTextureIndex];
        MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
        materialPropertyBlock.SetTexture("_Mask", mask);
        float4 intensityVector = float4.zero;
        intensityVector[terrainLayerIndex % 4] = intensity;
        materialPropertyBlock.SetVector("_Intensity", intensityVector);
        materialPropertyBlock.SetVector("_Falloff", new Vector4(MaskFalloff.Min, MaskFalloff.Max));
        DrawQuadSplat(worldBounds, layerRenderTarget, m_ApplySplatmapMaterial, materialPropertyBlock);
    }
    
    public static WorldBuildingContext Create(Terrain terrain)
    {
        TerrainData terrainData = terrain.terrainData;

        WorldBuildingContext context = new WorldBuildingContext();
        context.m_TerrainPosition = terrain.transform.position;
        context.m_TerrainSize = new float2(terrainData.size.x, terrainData.size.z);
        context.MaxTerrainHeight = terrainData.heightmapScale.y;
        
        context.m_HeightmapRenderTexture = RenderTexture.GetTemporary(terrainData.heightmapResolution, terrainData.heightmapResolution, 0, Terrain.heightmapRenderTextureFormat, RenderTextureReadWrite.Linear);
        int numLayersTextures = terrainData.alphamapTextureCount;
        RenderTexture[] splatmapRenderTextures = new RenderTexture[numLayersTextures];
        for (int i = 0; i < numLayersTextures; i++)
        {
            splatmapRenderTextures[i] = RenderTexture.GetTemporary(terrainData.alphamapWidth, terrainData.alphamapHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        }
        context.SplatRenderTextures = splatmapRenderTextures;
        
        CommandBuffer cmd = new CommandBuffer();
        cmd.SetRenderTarget(context.HeightmapRenderTexture);
        cmd.ClearRenderTarget(false, true, Color.clear);

        for (int i = 0; i < numLayersTextures; i++)
        {
            cmd.SetRenderTarget(splatmapRenderTextures[i]);
            cmd.ClearRenderTarget(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));
        }

        Graphics.ExecuteCommandBuffer(cmd);
        return context;
    }

    public void Release()
    {
        RenderTexture.ReleaseTemporary(m_HeightmapRenderTexture);
        m_HeightmapRenderTexture = null;
        //

        if (SplatRenderTextures == null) return;
        foreach (var renderTexture in SplatRenderTextures)
        {
            RenderTexture.ReleaseTemporary(renderTexture);
        }
        SplatRenderTextures = null;
    }

    
    
    float2 WorldPositionToTerrainSpace(float3 worldPosition)
    {
        float2 positionOnTerrain = new Vector2(worldPosition.x - m_TerrainPosition.x, worldPosition.z - m_TerrainPosition.z);
        float2 posTerrainSpace = positionOnTerrain / m_TerrainSize;

        return posTerrainSpace;
    }

    public int GetTerrainLayerIndex(TerrainLayer layer)
    {
        return TerrainLayersIndexMap.GetValueOrDefault(layer, -1);
    }
}
