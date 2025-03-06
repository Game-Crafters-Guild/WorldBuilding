using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    /// <summary>
    /// Modifier for placing tree vegetation on terrain
    /// </summary>
    [Serializable]
    public class TreesModifier : BaseVegetationModifier
    {
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

        public List<TreeSettings> Trees = new List<TreeSettings>();
        
        [Tooltip("Number of trees per unit at full density")]
        [Range(0.001f, 10.0f)]
        public float TreesPerUnit = 0.005f;
        
        // Calculate the number of trees based on area and density
        private int CalculateTreeCount(float area)
        {
            // Calculate number of trees based on area and density
            float baseCount = area * TreesPerUnit;
            
            // Apply density (0-5 range)
            return Mathf.RoundToInt(baseCount * Density);
        }
        
        public override int GetNumPrototypes()
        {
            return Trees.Count;
        }

        public override object GetPrototype(int index)
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
            
            ApplyTrees(context, worldBounds, mask);
        }

        private void ApplyTrees(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {
            // Get terrain data and mesh for bounds
            TerrainData terrainData = context.TerrainData;
            if (terrainData == null) return;

            // Convert world bounds to terrain space
            Bounds terrainBounds = ConvertWorldBoundsToTerrainSpace(worldBounds, terrainData);
            
            // Get tree prototype index mapping
            Dictionary<int, int> prototypeMap = GetTreePrototypeIndices(terrainData);
            if (prototypeMap.Count == 0)
            {
                Debug.LogWarning("No tree prototypes were registered with the terrain. Make sure prototypes are registered before calling ApplyTrees.");
                return;
            }
            
            // Calculate tree count based on area and density
            int treeCount = CalculateTreeCount(terrainBounds.size.x * terrainBounds.size.z);
            
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

                // Create constraint context
                var constraintContext = CreateConstraintContext(
                    terrainData, normX, normZ, boundsNormX, boundsNormZ, alphamaps, mask);
                
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

                // Add random offset if enabled
                if (RandomOffset > 0)
                {
                    float offsetX = GetRandomOffset();
                    float offsetZ = GetRandomOffset();
                    treeInstance.position += new Vector3(offsetX, 0, offsetZ);
                    
                    // Ensure position stays within bounds
                    treeInstance.position.x = Mathf.Clamp01(treeInstance.position.x);
                    treeInstance.position.z = Mathf.Clamp01(treeInstance.position.z);
                }

                // Add tree instance to context instead of directly to terrain
                context.AddTreeInstance(treeInstance);
            }
        }

        private Dictionary<int, int> GetTreePrototypeIndices(TerrainData terrainData)
        {
            Dictionary<int, int> map = new Dictionary<int, int>();
            
            if (terrainData == null || Trees.Count == 0)
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
    }
} 