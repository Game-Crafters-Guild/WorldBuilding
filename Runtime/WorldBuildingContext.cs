using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using float2 = Unity.Mathematics.float2;

namespace GameCraftersGuild.WorldBuilding
{
    /// <summary>
    /// Context data for world generation operations
    /// </summary>
    public class WorldBuildingContext
    {
        // Making mask render texture public to allow access from GPU placement code
        public RenderTexture MaskRenderTexture => m_MaskRenderTexture;
        internal RenderTexture m_MaskRenderTexture;

        private float3 m_TerrainPosition;
        private float2 m_TerrainSize;
        public MaskFalloff MaskFalloff { get; set; }
        public bool MaintainMaskAspectRatio { get; set; } = false;

        private RenderTexture m_HeightmapRenderTexture;
        private float MaxTerrainHeight { get; set; }
        public TerrainData TerrainData { get; private set; }
        public Matrix4x4 CurrentTransform { get; internal set; }
        public Dictionary<TerrainLayer, int> TerrainLayersIndexMap { get; internal set; }
        public RenderTexture[] SplatRenderTextures { get; private set; }
        public RenderTexture HeightmapRenderTexture => m_HeightmapRenderTexture;

        internal Mesh m_Quad;
        internal Material m_ApplyHeightmapMaterial;
        internal Material m_ApplySplatmapMaterial;

        private float4 FallOffVector => new Vector4(1.0f - MaskFalloff.Max, 1.0f - MaskFalloff.Min, 0.0f, 0.0f);
        public float3 TerrainPosition => m_TerrainPosition;
        public float2 TerrainSize => m_TerrainSize;

        // Vegetation data containers
        private List<TreeInstance> m_TreeInstances = new List<TreeInstance>();
        private Dictionary<int, int[,]> m_DetailLayers = new Dictionary<int, int[,]>();
        
        // Tree and detail prototype collections.
        private List<TreePrototype> m_TreePrototypes = new List<TreePrototype>();
        private List<DetailPrototype> m_DetailPrototypes = new List<DetailPrototype>();

        private HashSet<int> m_RegisteredTreeIndices = new HashSet<int>();
        private HashSet<int> m_RegisteredDetailIndices = new HashSet<int>();

        public void ApplyRegionTransformsToHeightmap(Bounds worldBounds, Texture mask,
            HeightWriteMode mode = HeightWriteMode.Replace)
        {
            float3 scaledSplineExtents = CurrentTransform.MultiplyVector(worldBounds.extents);

            float4 splineMeshBoundsY = new Vector4(worldBounds.center.y - scaledSplineExtents.y,
                worldBounds.center.y + scaledSplineExtents.y, 0.0f, 0.0f);
            float4 terrainWorldHeightRange =
                new float4(m_TerrainPosition.y, m_TerrainPosition.y + MaxTerrainHeight, 0.0f, 0.0f);
            MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
            materialPropertyBlock.SetTexture("_Mask", mask);
            materialPropertyBlock.SetTexture("_Data", Texture2D.blackTexture);
            materialPropertyBlock.SetVector("_HeightRange", Vector4.zero);
            materialPropertyBlock.SetVector("_Falloff", FallOffVector);
            materialPropertyBlock.SetVector("_SplineMeshBoundsY", splineMeshBoundsY);
            materialPropertyBlock.SetVector("_TerrainWorldHeightRange", terrainWorldHeightRange);
            SetBlendMode(mode, m_ApplyHeightmapMaterial);
            DrawQuad(worldBounds, HeightmapRenderTexture, m_ApplyHeightmapMaterial, materialPropertyBlock, 1);
        }

        public void ApplyHeightmap(Bounds worldBounds, Texture heightmap, Texture mask, HeightWriteMode mode,
            float minHeight = 0.0f, float maxHeight = 10.0f)
        {
            float4 heightRangeNormalized = new float4( /*Mathf.Clamp01*/
                (minHeight / MaxTerrainHeight), /*Mathf.Clamp01*/(maxHeight / MaxTerrainHeight), 0.0f, 0.0f);
            float3 scaledSplineExtents = CurrentTransform.MultiplyVector(worldBounds.extents);

            float4 splineMeshBoundsY = new Vector4(worldBounds.center.y - scaledSplineExtents.y,
                worldBounds.center.y + scaledSplineExtents.y, 0.0f, 0.0f);
            float4 terrainWorldHeightRange =
                new float4(m_TerrainPosition.y, m_TerrainPosition.y + MaxTerrainHeight, 0.0f, 0.0f);
            MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
            materialPropertyBlock.SetTexture("_Mask", mask);
            materialPropertyBlock.SetTexture("_Data", heightmap != null ? heightmap : Texture2D.whiteTexture);
            materialPropertyBlock.SetVector("_HeightRange", heightRangeNormalized);
            materialPropertyBlock.SetVector("_Falloff", FallOffVector);
            materialPropertyBlock.SetVector("_SplineMeshBoundsY", splineMeshBoundsY);
            materialPropertyBlock.SetVector("_TerrainWorldHeightRange", terrainWorldHeightRange);
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

        private void DrawQuad(Bounds worldBounds, RenderTexture renderTexture, Material material,
            MaterialPropertyBlock materialPropertyBlock, int shaderPass = 0)
        {
            float2 positionToTerrainSpace = WorldPositionToTerrainSpace(worldBounds.center) - new float2(0.5f, 0.5f);
            float2 sizeToTerrainSpace = new float2(worldBounds.size.x, worldBounds.size.z) / m_TerrainSize;
            Matrix4x4 worldTransform = CurrentTransform;
            worldTransform.m03 = worldTransform.m13 = worldTransform.m23 = 0.0f;

            float3 aspectRatio;
            if (MaintainMaskAspectRatio)
            {
                aspectRatio = Vector3.one;
            }
            else if (worldBounds.size.x > worldBounds.size.z)
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

            Matrix4x4 scaleMatrix =
                Matrix4x4.Scale(new Vector3(sizeToTerrainSpace.x, 1.0f, sizeToTerrainSpace.y) * aspectRatio);
            Matrix4x4 translationMatrix = Matrix4x4.Translate(new Vector3(positionToTerrainSpace.x, 0.0f,
                positionToTerrainSpace.y));
            Matrix4x4 transform = translationMatrix * worldTransform * scaleMatrix;
            cmd.DrawMesh(m_Quad, transform, material, 0, shaderPass, properties: materialPropertyBlock);
            Graphics.ExecuteCommandBuffer(cmd);
        }

        private void DrawQuadSplat(Bounds worldBounds, RenderTexture renderTexture, Material material,
            MaterialPropertyBlock materialPropertyBlock)
        {
            float2 positionToTerrainSpace = WorldPositionToTerrainSpace(worldBounds.center) - new float2(0.5f, 0.5f);
            float2 sizeToTerrainSpace = new float2(worldBounds.size.x, worldBounds.size.z) / m_TerrainSize;
            Matrix4x4 worldTransform = CurrentTransform;
            worldTransform.m03 = worldTransform.m13 = worldTransform.m23 = 0.0f;

            float3 aspectRatio;
            if (MaintainMaskAspectRatio)
            {
                aspectRatio = Vector3.one;
            }
            else if (worldBounds.size.x > worldBounds.size.z)
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

            Matrix4x4 scaleMatrix =
                Matrix4x4.Scale(new Vector3(sizeToTerrainSpace.x, 1.0f, sizeToTerrainSpace.y) * aspectRatio);
            Matrix4x4 translationMatrix = Matrix4x4.Translate(new Vector3(positionToTerrainSpace.x, 0.0f,
                positionToTerrainSpace.y));
            Matrix4x4 transform = translationMatrix * worldTransform * scaleMatrix;

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
            materialPropertyBlock.SetVector("_Falloff", FallOffVector);
            DrawQuadSplat(worldBounds, layerRenderTarget, m_ApplySplatmapMaterial, materialPropertyBlock);
        }

        public static WorldBuildingContext Create(Terrain terrain)
        {
            WorldBuildingContext context = new WorldBuildingContext();
            TerrainData terrainData = terrain.terrainData;
            context.TerrainData = terrainData;
            context.m_TerrainPosition = terrain.transform.position;
            context.m_TerrainSize = new float2(terrainData.size.x, terrainData.size.z);
            context.MaxTerrainHeight = terrainData.heightmapScale.y;

            context.m_HeightmapRenderTexture = RenderTexture.GetTemporary(terrainData.heightmapResolution,
                terrainData.heightmapResolution, 0, Terrain.heightmapRenderTextureFormat,
                RenderTextureReadWrite.Linear);
            int numLayersTextures = terrainData.alphamapTextureCount;
            RenderTexture[] splatmapRenderTextures = new RenderTexture[numLayersTextures];
            for (int i = 0; i < numLayersTextures; i++)
            {
                splatmapRenderTextures[i] = RenderTexture.GetTemporary(terrainData.alphamapWidth,
                    terrainData.alphamapHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
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
            
            ClearVegetation();
        }

        float2 WorldPositionToTerrainSpace(float3 worldPosition)
        {
            float2 positionOnTerrain =
                new Vector2(worldPosition.x - m_TerrainPosition.x, worldPosition.z - m_TerrainPosition.z);
            float2 posTerrainSpace = positionOnTerrain / m_TerrainSize;

            return posTerrainSpace;
        }

        public int GetTerrainLayerIndex(TerrainLayer layer)
        {
            return TerrainLayersIndexMap.GetValueOrDefault(layer, -1);
        }

        /// <summary>
        /// Adds a tree instance to be applied to the terrain.
        /// </summary>
        public void AddTreeInstance(TreeInstance treeInstance)
        {
            m_TreeInstances.Add(treeInstance);
            m_RegisteredTreeIndices.Add(treeInstance.prototypeIndex);
        }
        
        /// <summary>
        /// Sets a detail density at the specified position.
        /// </summary>
        public void SetDetailDensity(int prototypeIndex, int x, int y, int density)
        {
            // Make sure we have a layer for this prototype
            if (!m_DetailLayers.TryGetValue(prototypeIndex, out int[,] detailLayer))
            {
                if (TerrainData != null)
                {
                    detailLayer = new int[TerrainData.detailWidth, TerrainData.detailHeight];
                    m_DetailLayers[prototypeIndex] = detailLayer;
                }
            }
            
            if (detailLayer != null && x >= 0 && y >= 0 && x < detailLayer.GetLength(1) && y < detailLayer.GetLength(0))
            {
                detailLayer[y, x] = density;
                m_RegisteredDetailIndices.Add(prototypeIndex);
            }
        }
        
        /// <summary>
        /// Sets detail densities for a region of the terrain.
        /// </summary>
        public void SetDetailLayer(int prototypeIndex, int xBase, int yBase, int[,] detailPatch)
        {
            // Make sure we have a layer for this prototype
            if (!m_DetailLayers.TryGetValue(prototypeIndex, out int[,] detailLayer))
            {
                if (TerrainData != null)
                {
                    detailLayer = new int[TerrainData.detailWidth, TerrainData.detailHeight];
                    m_DetailLayers[prototypeIndex] = detailLayer;
                }
            }
            
            if (detailLayer != null && detailPatch != null)
            {
                int patchWidth = detailPatch.GetLength(1);
                int patchHeight = detailPatch.GetLength(0);
                
                // Copy patch to the main detail layer
                for (int y = 0; y < patchHeight; y++)
                {
                    for (int x = 0; x < patchWidth; x++)
                    {
                        int targetX = xBase + x;
                        int targetY = yBase + y;
                        
                        if (targetX >= 0 && targetY >= 0 && targetX < detailLayer.GetLength(1) && targetY < detailLayer.GetLength(0))
                        {
                            detailLayer[targetY, targetX] = detailPatch[y, x];
                        }
                    }
                }
                
                m_RegisteredDetailIndices.Add(prototypeIndex);
            }
        }
        
        /// <summary>
        /// Applies all collected vegetation data to the terrain.
        /// </summary>
        public void ApplyVegetationToTerrain()
        {
            if (TerrainData == null)
                return;
            
            // Apply tree prototypes and instances to the terrain
            if (m_TreePrototypes.Count > 0)
            {
                TerrainData.treePrototypes = m_TreePrototypes.ToArray();
                TerrainData.RefreshPrototypes();
            }
            
            if (m_TreeInstances.Count > 0)
            {
                TerrainData.treeInstances = m_TreeInstances.ToArray();
            }

            // Apply detail prototypes and layers to the terrain
            if (m_DetailPrototypes.Count > 0)
            {
                TerrainData.detailPrototypes = m_DetailPrototypes.ToArray();
                TerrainData.RefreshPrototypes();
            }

            // Apply detail layers
            foreach (var detailLayer in m_DetailLayers)
            {
                int detailIndex = detailLayer.Key;
                int[,] detailData = detailLayer.Value;
                
                TerrainData.SetDetailLayer(0, 0, detailIndex, detailData);
            }
        }
        
        /// <summary>
        /// Clears all vegetation data stored in the context.
        /// </summary>
        public void ClearVegetation()
        {
            m_TreeInstances.Clear();
            m_DetailLayers.Clear();
            m_RegisteredTreeIndices.Clear();
            m_RegisteredDetailIndices.Clear();
            m_TreePrototypes.Clear();
            m_DetailPrototypes.Clear();
        }

        public void AddTreePrototype(TreePrototype prototype)
        {
            m_TreePrototypes.Add(prototype);
        }

        public void AddDetailPrototype(DetailPrototype prototype)
        {
            m_DetailPrototypes.Add(prototype);
        }

        public List<TreePrototype> GetTreePrototypes()
        {
            return m_TreePrototypes;
        }

        public List<DetailPrototype> GetDetailPrototypes()
        {
            return m_DetailPrototypes;
        }
    }
}