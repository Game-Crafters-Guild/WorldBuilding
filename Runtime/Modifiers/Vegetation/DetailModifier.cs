using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    /// <summary>
    /// Modifier for placing detail vegetation (grass, plants) on terrain
    /// </summary>
    [Serializable]
    public class DetailModifier : BaseVegetationModifier
    {
        [Serializable]
        public class DetailSettings
        {
            public GameObject Prefab;
            public Texture2D DetailTexture;
            public bool UseMeshForGrass = false;
            [Range(0f, 2f)]
            public float MinWidth = 0.8f;
            [Range(0f, 2f)]
            public float MaxWidth = 1.2f;
            [Range(0f, 2f)]
            public float MinHeight = 0.8f;
            [Range(0f, 2f)]
            public float MaxHeight = 1.2f;
            public Color HealthyColor = Color.green;
            public Color DryColor = new Color(0.8f, 0.7f, 0.2f);
            public bool AlignToGround = true;
            public bool UseGPUInstancing = true;
            [Range(0, 10)]
            public int NoiseSpread = 3;
        }

        public List<DetailSettings> Details = new List<DetailSettings>();
        
        public override int GetNumPrototypes()
        {
            return Details.Count;
        }

        public override object GetPrototype(int index)
        {
            if (index < 0 || index >= Details.Count)
                return null;

            DetailSettings detailSettings = Details[index];
            DetailPrototype prototype = new DetailPrototype();
            
            if (detailSettings.UseMeshForGrass && detailSettings.Prefab != null)
            {
                prototype.prototype = detailSettings.Prefab;
                prototype.usePrototypeMesh = true;
            }
            else if (detailSettings.DetailTexture != null)
            {
                prototype.prototypeTexture = detailSettings.DetailTexture;
                prototype.usePrototypeMesh = false;
            }
            
            prototype.minWidth = detailSettings.MinWidth;
            prototype.maxWidth = detailSettings.MaxWidth;
            prototype.minHeight = detailSettings.MinHeight;
            prototype.maxHeight = detailSettings.MaxHeight;
            prototype.healthyColor = detailSettings.HealthyColor;
            prototype.dryColor = detailSettings.DryColor;
            prototype.noiseSpread = detailSettings.NoiseSpread;
            prototype.renderMode = detailSettings.UseGPUInstancing ? 
                DetailRenderMode.GrassBillboard : DetailRenderMode.Grass;
            
            return prototype;
        }

        public override void ApplyVegetation(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {
            // Ensure we have constraints
            if (ConstraintsContainer.Constraints.Count == 0)
            {
                CreateDefaultConstraints();
            }
            
            // Initialize random if needed
            if (RandomSeed != 0)
            {
                m_SeededRandom = new System.Random(RandomSeed);
            }
            
            ApplyDetails(context, worldBounds, mask);
        }

        private void ApplyDetails(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {
            // Get terrain data and mesh for bounds
            TerrainData terrainData = context.TerrainData;
            if (terrainData == null) return;

            // Convert world bounds to terrain space
            Bounds terrainBounds = ConvertWorldBoundsToTerrainSpace(worldBounds, terrainData);
            
            // Get detail prototype index mapping
            Dictionary<int, int> prototypeMap = GetDetailPrototypeIndices(terrainData);
            if (prototypeMap.Count == 0)
            {
                Debug.LogWarning("No detail prototypes were registered with the terrain. Make sure prototypes are registered before calling ApplyDetails.");
                return;
            }

            // Process each detail prototype
            for (int localProtoIndex = 0; localProtoIndex < Details.Count; localProtoIndex++)
            {
                // Convert to terrain prototype index
                if (!prototypeMap.TryGetValue(localProtoIndex, out int terrainProtoIndex))
                    continue;
                    
                // Get the detail map dimensions
                int detailMapWidth = terrainData.detailWidth;
                int detailMapHeight = terrainData.detailHeight;
                
                // Calculate map region that corresponds to the bounds
                int startX = Mathf.Max(0, Mathf.FloorToInt((terrainBounds.min.x / terrainData.size.x) * detailMapWidth));
                int startY = Mathf.Max(0, Mathf.FloorToInt((terrainBounds.min.z / terrainData.size.z) * detailMapHeight));
                int endX = Mathf.Min(detailMapWidth - 1, Mathf.CeilToInt((terrainBounds.max.x / terrainData.size.x) * detailMapWidth));
                int endY = Mathf.Min(detailMapHeight - 1, Mathf.CeilToInt((terrainBounds.max.z / terrainData.size.z) * detailMapHeight));
                int width = endX - startX + 1;
                int height = endY - startY + 1;

                // Create a detail layer patch for this region
                int[,] detailPatch = new int[height, width];
                
                // Get terrain parameters for constraints
                float[,,] alphamaps = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
            
                // Apply details based on constraints
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Calculate position in terrain space
                        float normX = (startX + x) / (float)detailMapWidth;
                        float normZ = (startY + y) / (float)detailMapHeight;
                        
                        // Calculate position in bounds space (0-1)
                        float boundsNormX = (float)x / width;
                        float boundsNormZ = (float)y / height;
                        
                        // Create constraint context
                        var constraintContext = CreateConstraintContext(
                            terrainData, normX, normZ, boundsNormX, boundsNormZ, alphamaps, mask);
                        
                        // Check all constraints
                        bool shouldApply = ConstraintsContainer.CheckConstraints(terrainData, normX, normZ, constraintContext);
                        
                        // Check density
                        if (shouldApply && !CheckDensity())
                        {
                            shouldApply = false;
                        }
                        
                        // Apply detail
                        if (shouldApply)
                        {
                            // Calculate density based on density value (1-16 range)
                            int maxDensity = Mathf.Clamp(Mathf.RoundToInt(8 * Density), 1, 16);
                            detailPatch[y, x] = GetRandomRange(1, maxDensity);
                        }
                        else
                        {
                            detailPatch[y, x] = 0;
                        }
                    }
                }
                
                // Add detail patch to context instead of directly to terrain
                context.SetDetailLayer(terrainProtoIndex, startX, startY, detailPatch);
            }
        }

        private Dictionary<int, int> GetDetailPrototypeIndices(TerrainData terrainData)
        {
            Dictionary<int, int> map = new Dictionary<int, int>();
            
            if (terrainData == null || Details.Count == 0)
                return map;
                
            // Map our prototype indices to terrain prototype indices
            for (int localIndex = 0; localIndex < Details.Count; localIndex++)
            {
                DetailSettings detailSettings = Details[localIndex];
                
                // Find the matching prototype in terrain data
                for (int terrainIndex = 0; terrainIndex < terrainData.detailPrototypes.Length; terrainIndex++)
                {
                    DetailPrototype terrainProto = terrainData.detailPrototypes[terrainIndex];
                    
                    // Match based on prefab for mesh or texture for images
                    if ((terrainProto.usePrototypeMesh && terrainProto.prototype == detailSettings.Prefab) ||
                        (!terrainProto.usePrototypeMesh && terrainProto.prototypeTexture == detailSettings.DetailTexture))
                    {
                        map[localIndex] = terrainIndex;
                        break;
                    }
                }
            }
            
            return map;
        }
    }
} 