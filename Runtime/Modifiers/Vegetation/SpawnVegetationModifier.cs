using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public class SpawnVegetationModifier : ITerrainVegetationModifier
    {
        public enum VegetationType
        {
            Tree,
            Detail
        }

        [Serializable]
        public class TreeSettings
        {
            public GameObject Prefab;
            [Range(0.1f, 3f)]
            public float MinScale = 0.8f;
            [Range(0.1f, 3f)]
            public float MaxScale = 1.2f;
            [Range(0f, 1f)]
            public float BendFactor = 0.1f;
            public Color LightmapColor = Color.white;
        }

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

        public VegetationType Type = VegetationType.Tree;
        public List<TreeSettings> Trees = new List<TreeSettings>();
        public List<DetailSettings> Details = new List<DetailSettings>();
        
        // Vegetation constraints
        public VegetationConstraintsContainer ConstraintsContainer = new VegetationConstraintsContainer();
        
        public bool UseNoise = false;
        [SerializeReference] public NoiseProperties NoiseProperties = new NoiseProperties();

        // Seed for random generation
        public int RandomSeed = 0;
        private System.Random m_SeededRandom;
        
        // Return a random value between 0 and 1 using either the seeded random or Unity's random
        private float GetRandomValue()
        {
            if (RandomSeed != 0)
            {
                if (m_SeededRandom == null)
                {
                    m_SeededRandom = new System.Random(RandomSeed);
                }
                return (float)m_SeededRandom.NextDouble();
            }
            else
            {
                return Random.value;
            }
        }
        
        // Return a random value in range using either the seeded random or Unity's random
        private float GetRandomRange(float min, float max)
        {
            return min + GetRandomValue() * (max - min);
        }
        
        // Return a random integer in range using either the seeded random or Unity's random
        private int GetRandomRange(int min, int max)
        {
            if (RandomSeed != 0)
            {
                if (m_SeededRandom == null)
                {
                    m_SeededRandom = new System.Random(RandomSeed);
                }
                return m_SeededRandom.Next(min, max);
            }
            else
            {
                return Random.Range(min, max);
            }
        }
        
        // Return a random rotation around y-axis
        private float GetRandomRotation()
        {
            return GetRandomValue() * 2f * Mathf.PI;
        }

        // Create default constraints if none exist
        private void CreateDefaultConstraints()
        {
            if (ConstraintsContainer.Constraints.Count == 0)
            {
                // Create default constraints
                ConstraintsContainer.Constraints.Add(new HeightConstraint());
                ConstraintsContainer.Constraints.Add(new SlopeConstraint());
                ConstraintsContainer.Constraints.Add(new DensityConstraint());
            }
        }

        public override string FilePath => GetFilePath();
        
        public override int GetNumPrototypes()
        {
            if (Type == VegetationType.Tree)
                return Trees.Count;
            else
                return Details.Count;
        }

        public override object GetPrototype(int index)
        {
            if (Type == VegetationType.Tree)
            {
                if (index < 0 || index >= Trees.Count)
                    return null;

                TreeSettings treeSettings = Trees[index];
                TreePrototype prototype = new TreePrototype
                {
                    prefab = treeSettings.Prefab,
                    bendFactor = treeSettings.BendFactor
                };
                return prototype;
            }
            else
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
        }

        public override void ApplyVegetation(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {
            // Ensure we have constraints
            if (ConstraintsContainer.Constraints.Count == 0)
            {
                CreateDefaultConstraints();
            }
            
            // Set random seed for all constraints
            ConstraintsContainer.SetRandomSeed(RandomSeed);
            
            if (Type == VegetationType.Tree)
                ApplyTrees(context, worldBounds, mask);
            else
                ApplyDetails(context, worldBounds, mask);
        }

        private void ApplyTrees(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {
            // Get terrain data and mesh for bounds
            TerrainData terrainData = context.TerrainData;
            if (terrainData == null) return;

            // Initialize seeded random if needed
            if (RandomSeed != 0)
            {
                m_SeededRandom = new System.Random(RandomSeed);
            }

            // Convert world bounds to terrain space
            Bounds terrainBounds = ConvertWorldBoundsToTerrainSpace(worldBounds, terrainData);
            
            // Get tree prototype index mapping
            Dictionary<int, int> prototypeMap = GetTreePrototypeIndices(terrainData);
            if (prototypeMap.Count == 0)
            {
                Debug.LogWarning("No tree prototypes were registered with the terrain. Make sure prototypes are registered before calling ApplyTrees.");
                return;
            }
            
            // Generate/use noise map if needed
            Texture2D noiseTexture = null;
            if (UseNoise && NoiseProperties != null)
            {
                // Convert noise to 2D array.
                noiseTexture = NoiseProperties.NoiseTexture;
            }

            // Find the density constraint
            DensityConstraint densityConstraint = ConstraintsContainer.FindConstraint<DensityConstraint>();
            
            // Create random position samples within the bounds
            int treeCount = 0;
            if (densityConstraint != null)
            {
                treeCount = densityConstraint.CalculateTreeCount(terrainBounds.size.x * terrainBounds.size.z);
            }
            else
            {
                Debug.LogWarning("No density constraint found. Using default tree count.");
                treeCount = 10; // Default fallback
            }
            
            // Get terrain parameters for constraints
            float[,,] alphamaps = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
            
            for (int i = 0; i < treeCount; i++)
            {
                // Generate random position within bounds (normalized 0-1 within bounds)
                float boundsNormX = GetRandomValue(); // Position relative to bounds (0-1)
                float boundsNormZ = GetRandomValue(); // Position relative to bounds (0-1)
                
                // Calculate terrain position from bounds-relative position
                float normX = Mathf.Lerp(terrainBounds.min.x, terrainBounds.max.x, boundsNormX) / terrainData.size.x;
                float normZ = Mathf.Lerp(terrainBounds.min.z, terrainBounds.max.z, boundsNormZ) / terrainData.size.z;
                
                // Clamp to valid range
                normX = Mathf.Clamp01(normX);
                normZ = Mathf.Clamp01(normZ);

                // Get height at position
                float height = terrainData.GetHeight(
                    Mathf.RoundToInt(normX * terrainData.heightmapResolution), 
                    Mathf.RoundToInt(normZ * terrainData.heightmapResolution)
                );

                // Get slope at this position
                float slope = GetTerrainSlope(terrainData, normX, normZ);
                
                // Create context for constraint checking
                VegetationConstraintContext constraintContext = new VegetationConstraintContext
                {
                    TerrainHeight = height,
                    TerrainSlope = slope,
                    AlphaMaps = alphamaps,
                    BoundsNormX = boundsNormX,
                    BoundsNormZ = boundsNormZ,
                    MaskTexture = mask,
                    NoiseTexture = noiseTexture
                };
                
                // Check all constraints
                if (!ConstraintsContainer.CheckConstraints(terrainData, normX, normZ, constraintContext))
                {
                    continue;
                }

                // Select a random tree prototype
                int localProtoIndex = GetRandomRange(0, Trees.Count);
                if (localProtoIndex < 0 || localProtoIndex >= Trees.Count)
                    continue;
                    
                // Convert to terrain prototype index
                if (!prototypeMap.TryGetValue(localProtoIndex, out int terrainProtoIndex))
                    continue;

                TreeSettings treeSettings = Trees[localProtoIndex];

                // Create tree instance
                TreeInstance treeInstance = new TreeInstance
                {
                    position = new Vector3(normX, 0, normZ),
                    prototypeIndex = terrainProtoIndex,
                    widthScale = GetRandomRange(treeSettings.MinScale, treeSettings.MaxScale),
                    heightScale = GetRandomRange(treeSettings.MinScale, treeSettings.MaxScale),
                    rotation = GetRandomRotation(),
                    color = treeSettings.LightmapColor,
                    lightmapColor = treeSettings.LightmapColor
                };

                // Add random offset if the density constraint exists
                if (densityConstraint != null && densityConstraint.RandomOffset > 0)
                {
                    float offsetX = densityConstraint.GetRandomOffset();
                    float offsetZ = densityConstraint.GetRandomOffset();
                    treeInstance.position += new Vector3(offsetX, 0, offsetZ);
                    
                    // Ensure position stays within bounds
                    treeInstance.position.x = Mathf.Clamp01(treeInstance.position.x);
                    treeInstance.position.z = Mathf.Clamp01(treeInstance.position.z);
                }

                // Add tree instance to context instead of directly to terrain
                context.AddTreeInstance(treeInstance);
            }
        }

        private void ApplyDetails(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {
            // Get terrain data and mesh for bounds
            TerrainData terrainData = context.TerrainData;
            if (terrainData == null) return;

            // Initialize seeded random if needed
            if (RandomSeed != 0)
            {
                m_SeededRandom = new System.Random(RandomSeed);
            }

            // Convert world bounds to terrain space
            Bounds terrainBounds = ConvertWorldBoundsToTerrainSpace(worldBounds, terrainData);
            
            // Get detail prototype index mapping
            Dictionary<int, int> prototypeMap = GetDetailPrototypeIndices(terrainData);
            if (prototypeMap.Count == 0)
            {
                Debug.LogWarning("No detail prototypes were registered with the terrain. Make sure prototypes are registered before calling ApplyDetails.");
                return;
            }

            // Generate/use noise map if needed
            Texture2D noiseTexture = null;
            if (UseNoise && NoiseProperties != null)
            {
                // Convert noise to 2D array.
                noiseTexture = NoiseProperties.NoiseTexture;
            }

            // Find the density constraint for scaling
            DensityConstraint densityConstraint = ConstraintsContainer.FindConstraint<DensityConstraint>();
            float density = densityConstraint != null ? densityConstraint.Density : 1.0f;

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
                        
                        // Get height and slope at position
                        float terrainHeight = terrainData.GetHeight(
                            Mathf.RoundToInt(normX * terrainData.heightmapResolution),
                            Mathf.RoundToInt(normZ * terrainData.heightmapResolution)
                        );

                        float slope = GetTerrainSlope(terrainData, normX, normZ);
                        
                        // Create constraint context
                        VegetationConstraintContext constraintContext = new VegetationConstraintContext
                        {
                            TerrainHeight = terrainHeight,
                            TerrainSlope = slope,
                            AlphaMaps = alphamaps,
                            BoundsNormX = boundsNormX,
                            BoundsNormZ = boundsNormZ,
                            MaskTexture = mask,
                            NoiseTexture = noiseTexture
                        };
                        
                        // Check all constraints
                        bool shouldApply = ConstraintsContainer.CheckConstraints(terrainData, normX, normZ, constraintContext);
                        
                        // Apply detail
                        if (shouldApply)
                        {
                            // Calculate density based on density value (1-16 range)
                            int maxDensity = Mathf.Clamp(Mathf.RoundToInt(8 * density), 1, 16);
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

        #region Helper Methods
        
        private TerrainData GetTerrainDataFromContext(WorldBuildingContext context)
        {
            return context.TerrainData;
        }
        
        private Bounds ConvertWorldBoundsToTerrainSpace(Bounds worldBounds, TerrainData terrainData)
        {
            // Convert world bounds to local terrain space (assuming terrain is at origin)
            // This is a simplification - you may need to adjust based on your terrain position
            return worldBounds;
        }

        private float GetTerrainSlope(TerrainData terrainData, float normX, float normZ)
        {
            // Get slope at the specified normalized position
            int heightMapX = Mathf.RoundToInt(normX * terrainData.heightmapResolution);
            int heightMapZ = Mathf.RoundToInt(normZ * terrainData.heightmapResolution);
            
            // Clamp to valid range
            heightMapX = Mathf.Clamp(heightMapX, 0, terrainData.heightmapResolution - 1);
            heightMapZ = Mathf.Clamp(heightMapZ, 0, terrainData.heightmapResolution - 1);
            
            // Get terrain normal
            Vector3 normal = terrainData.GetInterpolatedNormal(normX, normZ);
            
            // Calculate angle between normal and up vector (in degrees)
            float angle = Vector3.Angle(normal, Vector3.up);
            return angle;
        }

        private float SampleTexture(Texture texture, float normX, float normZ)
        {
            // Sample texture at normalized position
            if (texture is Texture2D texture2D)
            {
                int x = Mathf.FloorToInt(normX * texture2D.width);
                int y = Mathf.FloorToInt(normZ * texture2D.height);
                
                x = Mathf.Clamp(x, 0, texture2D.width - 1);
                y = Mathf.Clamp(y, 0, texture2D.height - 1);
                
                return texture2D.GetPixel(x, y).grayscale;
            }
            
            // Fallback
            return 1.0f;
        }

        private Dictionary<int, int> GetTreePrototypeIndices(TerrainData terrainData)
        {
            Dictionary<int, int> map = new Dictionary<int, int>();
            
            if (terrainData == null || Type != VegetationType.Tree || Trees.Count == 0)
                return map;
                
            // Map our prototype indices to terrain prototype indices
            for (int localIndex = 0; localIndex < Trees.Count; localIndex++)
            {
                TreeSettings treeSettings = Trees[localIndex];
                
                // Find the matching prototype in terrain data
                for (int terrainIndex = 0; terrainIndex < terrainData.treePrototypes.Length; terrainIndex++)
                {
                    TreePrototype terrainProto = terrainData.treePrototypes[terrainIndex];
                    
                    if (terrainProto.prefab == treeSettings.Prefab)
                    {
                        map[localIndex] = terrainIndex;
                        break;
                    }
                }
            }
            
            return map;
        }

        private Dictionary<int, int> GetDetailPrototypeIndices(TerrainData terrainData)
        {
            Dictionary<int, int> map = new Dictionary<int, int>();
            
            if (terrainData == null || Type != VegetationType.Detail || Details.Count == 0)
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
        
        #endregion
    }
}
