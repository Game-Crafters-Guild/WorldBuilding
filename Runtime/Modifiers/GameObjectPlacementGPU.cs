using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameCraftersGuild.WorldBuilding
{
    /// <summary>
    /// GPU-based implementation of game object placement using compute shaders
    /// </summary>
    [Serializable]
    public class GameObjectPlacementGPU
    {
        // Compute shader reference
        [SerializeField] private ComputeShader placementComputeShader;

        public ComputeShader PlacementComputeShader
        {
            set => placementComputeShader = value;
            get => placementComputeShader;
        }
        
        // References to kernel IDs
        private int generatePositionsKernelId;
        private int filterObjectCollisionsKernelId;
        
        // Buffers for GPU data
        private ComputeBuffer resultsBuffer;
        private ComputeBuffer filteredResultsBuffer;
        private ComputeBuffer validCountBuffer;
        private ComputeBuffer heightConstraintsBuffer;
        private ComputeBuffer slopeConstraintsBuffer;
        private ComputeBuffer noiseConstraintsBuffer;
        private ComputeBuffer placedObjectsBuffer;
        private ComputeBuffer minimumDistancesBuffer;
        private ComputeBuffer prefabSettingsBuffer;
        private ComputeBuffer maskConstraintThresholdsBuffer;
        private ComputeBuffer layerConstraintIndicesBuffer;
        
        // Add field for noise texture
        private Texture2D noiseTexture;
        
        // Structure for results from compute shader
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct PlacementResult
        {
            public Vector3 position;       // 12 bytes
            public float scale;            // 4 bytes
            public float rotation;         // 4 bytes
            public uint prefabIndex;       // 4 bytes
            public uint isValid;           // 4 bytes
            public float normalAlignmentFactor; // 4 bytes
            public Vector3 normal;         // 12 bytes
            public float padding;          // 4 bytes padding to match GPU 48-byte alignment
            // Total: 48 bytes
        }
        
        // Structure for prefab settings in compute shader
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct PrefabSettings
        {
            public float minScale;            // 4 bytes
            public float maxScale;            // 4 bytes
            public float yOffset;             // 4 bytes
            public uint alignToNormal;        // 4 bytes
            public uint randomYRotation;      // 4 bytes
            public float minRotation;         // 4 bytes
            public float maxRotation;         // 4 bytes
            public float minimumDistance;     // 4 bytes
            public float minNormalAlignment;  // 4 bytes
            public float maxNormalAlignment;  // 4 bytes
            public float padding1;            // 4 bytes padding
            public float padding2;            // 4 bytes padding
            // Total: 48 bytes to match shader memory layout
        }
        
        // Array to hold results
        private PlacementResult[] results;
        private PlacementResult[] filteredResults;
        private int[] validCountArray;
        
        // Arrays for constraints data
        private Vector2[] heightConstraints;
        private Vector2[] slopeConstraints;
        private Vector4[] noiseConstraints;
        private float[] placedObjectPositions;
        private float[] minimumDistances;
        private PrefabSettings[] prefabSettings;
        private float[] maskConstraintThresholds;
        private int[] layerConstraintIndices;
        
        // Cached properties for efficient reuse
        private Texture2D normalTexture;
        
        // Temporary textures we create (should be destroyed when done)
        private Texture2D tempWhiteTexture;
        
        public GameObjectPlacementGPU(ComputeShader computeShader)
        {
            placementComputeShader = computeShader;
            Initialize();
        }
        
        private void Initialize()
        {
            if (placementComputeShader == null)
            {
                Debug.LogError("GPU Placement: Compute shader is missing!");
                return;
            }
            
            // Get kernel IDs
            generatePositionsKernelId = placementComputeShader.FindKernel("GeneratePositions");
            filterObjectCollisionsKernelId = placementComputeShader.FindKernel("FilterObjectCollisions");
        }
        
        /// <summary>
        /// Release all GPU resources
        /// </summary>
        public void Release()
        {
            // Release buffers
            ReleaseBuffer(ref resultsBuffer);
            ReleaseBuffer(ref filteredResultsBuffer);
            ReleaseBuffer(ref validCountBuffer);
            ReleaseBuffer(ref heightConstraintsBuffer);
            ReleaseBuffer(ref slopeConstraintsBuffer);
            ReleaseBuffer(ref noiseConstraintsBuffer);
            ReleaseBuffer(ref placedObjectsBuffer);
            ReleaseBuffer(ref minimumDistancesBuffer);
            ReleaseBuffer(ref prefabSettingsBuffer);
            ReleaseBuffer(ref maskConstraintThresholdsBuffer);
            ReleaseBuffer(ref layerConstraintIndicesBuffer);
            
            // Release textures
            //ReleaseTexture(ref normalTexture);
            
            // Destroy temporary textures
            if (tempWhiteTexture != null)
            {
                SafeDestroy(tempWhiteTexture);
                tempWhiteTexture = null;
            }
        }
        
        /// <summary>
        /// Safely destroy an object, handling both Editor and Play modes
        /// </summary>
        private void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null)
                return;
                
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(obj);
            else
                UnityEngine.Object.Destroy(obj);
#else
            UnityEngine.Object.Destroy(obj);
#endif
        }
        
        private void ReleaseBuffer(ref ComputeBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }
        }
        
        private void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture != null)
            {
                texture.Release();
                texture = null;
            }
        }
        
        /// <summary>
        /// Setup constraints from the GameObjectModifier
        /// </summary>
        public void SetupConstraints(GameObjectModifier modifier)
        {
            var constraints = modifier.ConstraintsContainer.Constraints;
            
            // Setup height constraints
            var heightConstraint = FindConstraint<HeightConstraint>(constraints);
            if (heightConstraint != null)
            {
                heightConstraints = new Vector2[1] { new Vector2(heightConstraint.MinHeight, heightConstraint.MaxHeight) };
            }
            else
            {
                heightConstraints = new Vector2[1] { new Vector2(0f, 1000f) }; // Default values
            }
            
            // Setup slope constraints
            var slopeConstraint = FindConstraint<SlopeConstraint>(constraints);
            if (slopeConstraint != null)
            {
                slopeConstraints = new Vector2[1] { new Vector2(slopeConstraint.MinSlope, slopeConstraint.MaxSlope) };
            }
            else
            {
                slopeConstraints = new Vector2[1] { new Vector2(0f, 90f) }; // Default values
            }
            
            // Setup noise constraints
            var noiseConstraint = FindConstraint<NoiseConstraint>(constraints);
            if (noiseConstraint != null)
            {
                // Pack noise parameters into a Vector4
                // x: threshold
                // y: scale (normalized from NoiseScale)
                // z: offset seed (normalized from Seed)
                // w: unused for now
                
                float scale = noiseConstraint.NoiseProperties != null ? 
                    Mathf.Clamp(noiseConstraint.NoiseProperties.NoiseScale / 10f, 0.1f, 10f) : 1f;
                
                float offset = noiseConstraint.NoiseProperties != null ? 
                    (noiseConstraint.NoiseProperties.Seed % 1000) / 1000f : 0f;
                
                noiseConstraints = new Vector4[1] { 
                    new Vector4(
                        noiseConstraint.Threshold,  // Threshold
                        scale,                      // Scale
                        offset,                     // Offset/seed
                        0f                          // Reserved
                    ) 
                };
                
                // Get the noise texture from NoiseProperties 
                if (noiseConstraint.NoiseProperties != null)
                {
                    noiseTexture = noiseConstraint.NoiseProperties.NoiseTexture;
                }
                else
                {
                    noiseTexture = null;
                }
            }
            else
            {
                noiseConstraints = new Vector4[1] { new Vector4(0.5f, 1f, 0f, 0f) }; // Default values
                noiseTexture = null;
            }
            
            // Setup mask constraints
            var maskConstraint = FindConstraint<MaskConstraint>(constraints);
            if (maskConstraint != null)
            {
                maskConstraintThresholds = new float[1] { maskConstraint.Threshold };
            }
            else
            {
                maskConstraintThresholds = new float[1] { 0.1f }; // Default value
            }
            
            // Setup layer constraints
            var layerConstraint = FindConstraint<LayerConstraint>(constraints);
            if (layerConstraint != null && layerConstraint.AllowedLayers != null && layerConstraint.AllowedLayers.Length > 0)
            {
                // Convert TerrainLayers to indices (0-3 for the first 4 terrain layers that can fit in RGBA channels)
                TerrainData terrainData = UnityEngine.Terrain.activeTerrain?.terrainData;
                if (terrainData != null && terrainData.terrainLayers != null)
                {
                    List<int> indices = new List<int>();
                    
                    // Find the index of each allowed layer in the terrain's layer array
                    for (int i = 0; i < Mathf.Min(terrainData.terrainLayers.Length, 4); i++)
                    {
                        foreach (var allowedLayer in layerConstraint.AllowedLayers)
                        {
                            if (terrainData.terrainLayers[i] == allowedLayer)
                            {
                                indices.Add(i);
                                //Debug.Log($"Layer constraint: Found matching layer at index {i} - {allowedLayer.name}");
                                break;
                            }
                        }
                    }
                    
                    // If no matching layers were found, add a special "fail" value (-1)
                    // This will cause the constraint to fail rather than be skipped
                    if (indices.Count == 0)
                    {
                        indices.Add(-1); // Special value to indicate "always fail"
                        Debug.LogWarning("Layer constraint: No matching layers found in terrain. The constraint will always fail.");
                        
                        // Debug output to help identify the issue
                        foreach (var allowedLayer in layerConstraint.AllowedLayers)
                        {
                            Debug.LogWarning($"Looking for layer: {allowedLayer?.name ?? "null"}");
                        }
                        
                        for (int i = 0; i < terrainData.terrainLayers.Length; i++)
                        {
                            Debug.LogWarning($"Terrain has layer {i}: {terrainData.terrainLayers[i]?.name ?? "null"}");
                        }
                    }
                    
                    // Store the indices
                    layerConstraintIndices = indices.ToArray();
                }
                else
                {
                    // No terrain data or layers available, but constraint exists
                    // Set special "fail" value since we can't satisfy the constraint
                    layerConstraintIndices = new int[] { -1 };
                }
            }
            else
            {
                // No layer constraints
                layerConstraintIndices = new int[0];
            }
            
            // Get collision constraint if it exists
            var collisionConstraint = FindConstraint<GameObjectModifier.ObjectCollisionConstraint>(constraints);
            if (collisionConstraint != null && collisionConstraint.PlacedObjects.Count > 0)
            {
                // Convert placed objects to flattened arrays for the GPU
                placedObjectPositions = new float[collisionConstraint.PlacedObjects.Count * 3];
                minimumDistances = new float[collisionConstraint.PlacedObjects.Count];
                
                for (int i = 0; i < collisionConstraint.PlacedObjects.Count; i++)
                {
                    var obj = collisionConstraint.PlacedObjects[i];
                    placedObjectPositions[i * 3] = obj.Position.x;
                    placedObjectPositions[i * 3 + 1] = obj.Position.y;
                    placedObjectPositions[i * 3 + 2] = obj.Position.z;
                    minimumDistances[i] = obj.MinDistance;
                }
            }
            else
            {
                // Empty arrays if no objects placed yet
                placedObjectPositions = Array.Empty<float>();
                minimumDistances = Array.Empty<float>();
            }
            
            // Setup prefab settings
            SetupPrefabSettings(modifier);
        }
        
        /// <summary>
        /// Setup prefab-specific settings
        /// </summary>
        private void SetupPrefabSettings(GameObjectModifier modifier)
        {
            if (modifier.GameObjects == null || modifier.GameObjects.Count == 0)
            {
                prefabSettings = new PrefabSettings[1]; // Default empty settings
                return;
            }
            
            prefabSettings = new PrefabSettings[modifier.GameObjects.Count];
            
            for (int i = 0; i < modifier.GameObjects.Count; i++)
            {
                var gameObj = modifier.GameObjects[i];
                
                // Get minimum distance (use default if not specified)
                float minDist = gameObj.MinimumDistance;
                
                // If not specified, try to get from collision constraint
                if (minDist <= 0)
                {
                    var collisionConstraint = FindConstraint<GameObjectModifier.ObjectCollisionConstraint>(modifier.ConstraintsContainer.Constraints);
                    minDist = collisionConstraint != null ? collisionConstraint.DefaultMinDistance : 2.0f;
                }
                
                prefabSettings[i] = new PrefabSettings
                {
                    minScale = gameObj.MinScale * modifier.GlobalScale,
                    maxScale = gameObj.MaxScale * modifier.GlobalScale,
                    yOffset = gameObj.YOffset,
                    alignToNormal = gameObj.AlignToNormal ? 1u : 0u,
                    randomYRotation = gameObj.RandomYRotation ? 1u : 0u,
                    minRotation = gameObj.MinRotation,
                    maxRotation = gameObj.MaxRotation,
                    minimumDistance = minDist, // Store unscaled distance
                    minNormalAlignment = gameObj.MinNormalAlignment,
                    maxNormalAlignment = gameObj.MaxNormalAlignment
                };
            }
        }
        
        /// <summary>
        /// Helper to find constraint of specific type
        /// </summary>
        private T FindConstraint<T>(List<IPlacementConstraint> constraints) where T : IPlacementConstraint
        {
            foreach (var constraint in constraints)
            {
                if (constraint is T typedConstraint)
                {
                    return typedConstraint;
                }
            }
            return default;
        }
        
        /// <summary>
        /// Creates or updates textures needed for GPU computation
        /// </summary>
        private void SetupNormalTexture(TerrainData terrainData)
        {
            // This method is no longer needed as we now use the normal map from WorldBuildingContext
            // It remains for backward compatibility but doesn't do anything
            
            // Old implementation removed
        }
        
        /// <summary>
        /// Main method to spawn game objects using the GPU
        /// </summary>
        public List<GameObjectPlacementInfo> GenerateObjectPlacements(
            GameObjectModifier modifier, 
            WorldBuildingContext context, 
            Bounds worldBounds, 
            Texture mask,
            int maxAttempts = 1000000)
        {
            placementComputeShader = modifier.PlacementComputeShader;
            if (placementComputeShader == null)
            {
                Debug.LogError("GPU Placement: Compute shader is missing!");
                return null;
            }
            
            // Setup constraints from modifier
            SetupConstraints(modifier);
            
            // Get terrain data
            TerrainData terrainData = context.TerrainData;
            if (terrainData == null || modifier.GameObjects.Count == 0) 
                return null;
                
            // Get terrain
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
                return null;
            
            // No longer need to setup normal texture, we'll use the one from context
            // SetupNormalTexture(terrainData);
            
            // Calculate bounds in terrain space
            Vector3 terrainPos = context.TerrainPosition;
            Vector3 terrainSize = terrainData.size;

            // Store the original world bounds for deterministic area calculation
            Vector3 worldBoundsSize = worldBounds.size;
            float worldBoundsWidth = worldBoundsSize.x;
            float worldBoundsDepth = worldBoundsSize.z;

            // Calculate normalized bounds for the shader
            float boundsMinX = (worldBounds.min.x - terrainPos.x) / terrainSize.x;
            float boundsMinZ = (worldBounds.min.z - terrainPos.z) / terrainSize.z;
            float boundsMaxX = (worldBounds.max.x - terrainPos.x) / terrainSize.x;
            float boundsMaxZ = (worldBounds.max.z - terrainPos.z) / terrainSize.z;

            // Clamp to terrain bounds
            boundsMinX = Mathf.Clamp01(boundsMinX);
            boundsMinZ = Mathf.Clamp01(boundsMinZ);
            boundsMaxX = Mathf.Clamp01(boundsMaxX);
            boundsMaxZ = Mathf.Clamp01(boundsMaxZ);

            // Calculate the area in square units directly from world bounds
            float areaWidth = worldBoundsWidth;
            float areaDepth = worldBoundsDepth;
            float areaSize = areaWidth * areaDepth;

            // Calculate number of square units in the area - use a deterministic ceiling function
            int totalSquareUnits = (int)Math.Ceiling(areaSize);

            // Calculate potential number of objects based on ObjectsPerSquareUnit
            int potentialObjectsPerSquareUnit = modifier.ObjectsPerSquareUnit;
            int potentialTotalObjects = totalSquareUnits * potentialObjectsPerSquareUnit;

            // Apply density to determine how many objects to actually spawn
            int numObjects = (int)Math.Floor(potentialTotalObjects * modifier.Density);
            if (numObjects == 0)
                return null;

            // Apply max objects limit if set
            if (modifier.MaxObjects > 0)
            {
                numObjects = Math.Min(numObjects, modifier.MaxObjects);
            }

            // Make sure we at least try to place 1 object if density > 0
            if (modifier.Density > 0 && numObjects == 0)
            {
                numObjects = 1;
            }

            //Debug.Log($"GPU Placement: Area size={areaSize:F2} square units, Total square units={totalSquareUnits}, " +
              //      $"Potential objects={potentialTotalObjects}, Density={modifier.Density:F2}, Objects to place={numObjects}");

            // Calculate grid dimensions for square unit based placement
            // For very large terrains, we may not need a 1:1 grid cell to square unit ratio
            // Limit to a maximum reasonable grid size
            const int MaxGridDimension = 500; // Maximum grid cells in any dimension

            // Create a deterministic grid that doesn't change based on small position differences
            // Round to integer units to make grid cell count more stable
            int baseGridWidth = (int)Math.Ceiling(areaWidth);
            int baseGridDepth = (int)Math.Ceiling(areaDepth);

            int gridWidth, gridDepth;
            if (baseGridWidth > MaxGridDimension || baseGridDepth > MaxGridDimension)
            {
                // Scale down grid for very large terrains - use deterministic math functions
                float scaleFactor = Math.Min(
                    (float)MaxGridDimension / baseGridWidth, 
                    (float)MaxGridDimension / baseGridDepth
                );
                gridWidth = (int)Math.Ceiling(baseGridWidth * scaleFactor);
                gridDepth = (int)Math.Ceiling(baseGridDepth * scaleFactor);
                
                Debug.Log($"GPU Placement: Scaling down grid from {baseGridWidth}x{baseGridDepth} " +
                          $"to {gridWidth}x{gridDepth} for performance");
            }
            else
            {
                gridWidth = baseGridWidth;
                gridDepth = baseGridDepth;
            }

            // Ensure minimum grid dimensions
            gridWidth = Math.Max(1, gridWidth);
            gridDepth = Math.Max(1, gridDepth);

            // Limit the number of parallel calculations for performance
            // Make this calculation deterministic and based on integers
            int numThreads = Math.Max(
                numObjects * 10,  // At least 10x the objects we want to place for better distribution
                Math.Min(totalSquareUnits * potentialObjectsPerSquareUnit * 2, 1000000)  // Double the potential objects but cap at 1M
            );
            
            //Debug.Log($"GPU Placement: Using {numThreads} threads for {gridWidth}x{gridDepth} grid");
            
            // Create buffers
            CreateBuffers(numThreads, numObjects);
            
            // Additional parameters for grid-based placement
            placementComputeShader.SetInt("_GridWidth", gridWidth);
            placementComputeShader.SetInt("_GridDepth", gridDepth);
            placementComputeShader.SetInt("_ObjectsPerSquareUnit", potentialObjectsPerSquareUnit);

            GameObjectModifier.ObjectCollisionConstraint collisionConstraint =
                modifier.ConstraintsContainer.FindConstraint<GameObjectModifier.ObjectCollisionConstraint>();
            
            // Set compute shader parameters using context
            SetShaderParameters(
                context, 
                terrainData,
                boundsMinX, boundsMinZ, boundsMaxX, boundsMaxZ,
                modifier.Density, numObjects, 
                modifier.GameObjects.Count, 
                modifier.RandomSeed, 
                modifier.RandomOffset,
                collisionConstraint != null ? collisionConstraint.DefaultMinDistance : 2.0f, // Get from constraint
                mask
            );
            
            // FIRST PASS: Generate potential positions
            int threadGroupsX = (int)Math.Ceiling(numThreads / 64.0);
            placementComputeShader.Dispatch(generatePositionsKernelId, threadGroupsX, 1, 1);
            
            // Get results back from GPU
            resultsBuffer.GetData(results);
            
            // Initialize valid count - IMPORTANT to reset to zero
            validCountArray[0] = 0;
            validCountBuffer.SetData(validCountArray);
            
            // Set kernel parameters for the filtering pass
            placementComputeShader.SetBuffer(filterObjectCollisionsKernelId, "_Results", resultsBuffer);
            placementComputeShader.SetBuffer(filterObjectCollisionsKernelId, "_FilteredResults", filteredResultsBuffer);
            placementComputeShader.SetBuffer(filterObjectCollisionsKernelId, "_ValidCount", validCountBuffer);
            placementComputeShader.SetInt("_MaxResults", numObjects);
            placementComputeShader.SetInt("_InputResultsCount", numThreads);
            
            // IMPORTANT: Set all required buffers for the second kernel as well
            placementComputeShader.SetBuffer(filterObjectCollisionsKernelId, "_PrefabSettings", prefabSettingsBuffer);
            placementComputeShader.SetFloat("_DefaultMinDistance", collisionConstraint != null ? collisionConstraint.DefaultMinDistance : 2.0f);
            
            // Count valid results from first pass to check if filtering is needed
            int validPreFilterCount = 0;
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i].isValid == 1)
                    validPreFilterCount++;
            }
            
            //Debug.Log($"GPU Placement: Found {validPreFilterCount} valid positions before collision filtering");
            
            // Skip GPU filtering and use valid results directly
            // This allows the coroutine to handle collisions during placement
            int validIdx = 0;
            for (int i = 0; i < results.Length && validIdx < numObjects * 3; i++) // Get more candidates than needed
            {
                if (results[i].isValid == 1)
                {
                    filteredResults[validIdx] = results[i];
                    validIdx++;
                }
            }
            
            validCountArray[0] = validIdx;
            
            int validCount = Mathf.Min(validCountArray[0], numObjects * 3); // Generate extra candidates
            //Debug.Log($"GPU Placement: Generated {validCount} candidates for placement");
            
            // Process results
            List<GameObjectPlacementInfo> placements = new List<GameObjectPlacementInfo>(validCount);
            
            // Add all valid objects directly from the filtered results
            for (int i = 0; i < validCount; i++)
            {
                if (i >= filteredResults.Length)
                    break;
                    
                var result = filteredResults[i];
                if (result.isValid == 1)
                {
                    GameObjectPlacementInfo info = new GameObjectPlacementInfo
                    {
                        Position = result.position,
                        Scale = result.scale,
                        Rotation = result.rotation,
                        PrefabIndex = (int)result.prefabIndex % modifier.GameObjects.Count,
                        Normal = result.normal,
                        NormalAlignmentFactor = result.normalAlignmentFactor
                    };
                    
                    placements.Add(info);
                }
            }
            
            //Debug.Log($"GPU Placement: Returning {placements.Count} candidates for runtime filtering");
            
            return placements;
        }
        
        /// <summary>
        /// Create or resize buffers for compute shader
        /// </summary>
        private void CreateBuffers(int numThreads, int maxResults)
        {
            // For compute shaders, HLSL packs data differently than C#
            // The shader struct is 44 bytes, but with 16-byte alignment, it becomes 48 bytes
            // Vector3/float3 elements are aligned to 16 bytes in the shader
            const int placementResultStride = 48; // Fixed stride to match shader memory layout

            // Create/resize results buffer for first pass
            if (resultsBuffer == null || resultsBuffer.count != numThreads)
            {
                ReleaseBuffer(ref resultsBuffer);
                resultsBuffer = new ComputeBuffer(numThreads, placementResultStride);
                results = new PlacementResult[numThreads];
            }
            
            // Create/resize filtered results buffer for second pass (collision filtering)
            if (filteredResultsBuffer == null || filteredResultsBuffer.count != maxResults)
            {
                ReleaseBuffer(ref filteredResultsBuffer);
                filteredResultsBuffer = new ComputeBuffer(maxResults, placementResultStride);
                filteredResults = new PlacementResult[maxResults];
            }
            
            // Create/resize valid count buffer (used to track how many valid objects we have)
            if (validCountBuffer == null)
            {
                ReleaseBuffer(ref validCountBuffer);
                validCountBuffer = new ComputeBuffer(1, sizeof(int));
                validCountArray = new int[1];
            }
            
            // Create/resize height constraints buffer
            if (heightConstraintsBuffer == null || heightConstraintsBuffer.count != heightConstraints.Length)
            {
                ReleaseBuffer(ref heightConstraintsBuffer);
                heightConstraintsBuffer = new ComputeBuffer(Mathf.Max(1, heightConstraints.Length), sizeof(float) * 2);
            }
            heightConstraintsBuffer.SetData(heightConstraints);
            
            // Setup slope constraints buffer
            if (slopeConstraintsBuffer == null || slopeConstraintsBuffer.count != slopeConstraints.Length)
            {
                ReleaseBuffer(ref slopeConstraintsBuffer);
                slopeConstraintsBuffer = new ComputeBuffer(Mathf.Max(1, slopeConstraints.Length), sizeof(float) * 2);
            }
            slopeConstraintsBuffer.SetData(slopeConstraints);
            
            // Setup noise constraints buffer
            if (noiseConstraintsBuffer == null || noiseConstraintsBuffer.count != noiseConstraints.Length)
            {
                ReleaseBuffer(ref noiseConstraintsBuffer);
                noiseConstraintsBuffer = new ComputeBuffer(Mathf.Max(1, noiseConstraints.Length), sizeof(float) * 4);
            }
            noiseConstraintsBuffer.SetData(noiseConstraints);
            
            // Setup mask constraint thresholds buffer
            if (maskConstraintThresholdsBuffer == null || maskConstraintThresholdsBuffer.count != maskConstraintThresholds.Length)
            {
                ReleaseBuffer(ref maskConstraintThresholdsBuffer);
                maskConstraintThresholdsBuffer = new ComputeBuffer(Mathf.Max(1, maskConstraintThresholds.Length), sizeof(float));
            }
            maskConstraintThresholdsBuffer.SetData(maskConstraintThresholds);
            
            // Setup layer constraint indices buffer
            if (layerConstraintIndicesBuffer == null || layerConstraintIndicesBuffer.count != layerConstraintIndices.Length)
            {
                ReleaseBuffer(ref layerConstraintIndicesBuffer);
                layerConstraintIndicesBuffer = new ComputeBuffer(Mathf.Max(1, layerConstraintIndices.Length), sizeof(int));
            }
            if (layerConstraintIndices.Length > 0)
            {
                layerConstraintIndicesBuffer.SetData(layerConstraintIndices);
            }
            
            // Setup placed objects buffer - for each object we need 3 floats (x,y,z)
            int requiredElementCount = Mathf.Max(1, placedObjectPositions.Length);
            
            if (placedObjectsBuffer == null || placedObjectsBuffer.count != requiredElementCount)
            {
                ReleaseBuffer(ref placedObjectsBuffer);
                placedObjectsBuffer = new ComputeBuffer(requiredElementCount, sizeof(float));
            }
            
            // Setup minimum distances buffer - for each object we need 1 float
            int minDistancesCount = Mathf.Max(1, minimumDistances.Length);
            
            if (minimumDistancesBuffer == null || minimumDistancesBuffer.count != minDistancesCount)
            {
                ReleaseBuffer(ref minimumDistancesBuffer);
                minimumDistancesBuffer = new ComputeBuffer(minDistancesCount, sizeof(float));
            }
            if (minimumDistances.Length > 0)
            {
                minimumDistancesBuffer.SetData(minimumDistances);
            }
            
            // Setup prefab settings buffer with proper struct stride
            // PrefabSettings has floats and uints which should be aligned properly
            const int prefabSettingsStride = 48; // Use fixed stride for consistent memory layout
            if (prefabSettingsBuffer == null || prefabSettingsBuffer.count != prefabSettings.Length)
            {
                ReleaseBuffer(ref prefabSettingsBuffer);
                prefabSettingsBuffer = new ComputeBuffer(Mathf.Max(1, prefabSettings.Length), prefabSettingsStride);
            }
            prefabSettingsBuffer.SetData(prefabSettings);
        }
        
        /// <summary>
        /// Set all parameters for the compute shader
        /// </summary>
        private void SetShaderParameters(
            WorldBuildingContext context,
            TerrainData terrainData,
            float boundsMinX, float boundsMinZ,
            float boundsMaxX, float boundsMaxZ,
            float density, 
            int maxObjectCount, 
            int numberOfGameObjects, 
            int randomSeed, 
            float randomOffset,
            float defaultMinDistance,
            Texture mask)
        {
            Vector3 terrainPosition = context.TerrainPosition;
            Vector3 terrainSize = terrainData.size;
            
            // Set terrain parameters
            placementComputeShader.SetVector("_TerrainParams", new Vector4(terrainSize.x, terrainSize.y, terrainSize.z, 0));
            placementComputeShader.SetVector("_TerrainPosition", new Vector4(terrainPosition.x, terrainPosition.y, terrainPosition.z, 0));
            placementComputeShader.SetVector("_HeightmapResolution", new Vector2(terrainData.heightmapResolution, terrainData.heightmapResolution));
            
            // Additional debug logging for terrain info
            /*Debug.Log($"Terrain Parameters: Size=({terrainSize.x:F1},{terrainSize.y:F1},{terrainSize.z:F1}), " +
                     $"Position=({terrainPosition.x:F1},{terrainPosition.y:F1},{terrainPosition.z:F1}), " +
                     $"HeightmapRes={terrainData.heightmapResolution}");*/
            
            // Set bounds parameters
            placementComputeShader.SetVector("_BoundsMin", new Vector4(boundsMinX, 0, boundsMinZ, 0));
            placementComputeShader.SetVector("_BoundsMax", new Vector4(boundsMaxX, 0, boundsMaxZ, 0));
            
            // Set object placement parameters
            placementComputeShader.SetFloat("_Density", density);
            placementComputeShader.SetInt("_MaxObjectCount", maxObjectCount);
            placementComputeShader.SetInt("_NumberOfGameObjects", numberOfGameObjects);
            placementComputeShader.SetInt("_RandomSeed", randomSeed);
            placementComputeShader.SetFloat("_RandomOffset", randomOffset);
            
            // Set placed objects data
            int objectCount = placedObjectPositions.Length / 3; // Each object has 3 float coordinates
            placementComputeShader.SetInt("_PlacedObjectCount", objectCount);
            placementComputeShader.SetFloat("_DefaultMinDistance", defaultMinDistance);
            
            // For any existing slope constraints, log them for debugging
            /*if (slopeConstraints != null && slopeConstraints.Length > 0)
            {
                Debug.Log($"Setting slope constraint: Min={slopeConstraints[0].x}°, Max={slopeConstraints[0].y}°");
            }*/
            
            // Set texture samplers - use the context render textures directly
            placementComputeShader.SetTexture(generatePositionsKernelId, "_HeightmapTexture", context.HeightmapRenderTexture);
            
            // For alphamap/splatmap, use the first splat texture from the context if available
            if (context.SplatRenderTextures != null && context.SplatRenderTextures.Length > 0)
            {
                placementComputeShader.SetTexture(generatePositionsKernelId, "_AlphamapTexture", context.SplatRenderTextures[0]);
            }
            
            // Set the normal texture from context instead of the one we created
            if (context.NormalRenderTexture != null)
            {
                placementComputeShader.SetTexture(generatePositionsKernelId, "_NormalTexture", context.NormalRenderTexture);
            }
            else
            {
                // Fallback to our old method if context doesn't have a normal map
                if (normalTexture == null)
                {
                    normalTexture = new Texture2D(2, 2);
                    normalTexture.SetPixel(0, 0, new Color(0.5f, 1.0f, 0.5f, 1)); // Default normal pointing up
                    normalTexture.SetPixel(0, 1, new Color(0.5f, 1.0f, 0.5f, 1));
                    normalTexture.SetPixel(1, 0, new Color(0.5f, 1.0f, 0.5f, 1));
                    normalTexture.SetPixel(1, 1, new Color(0.5f, 1.0f, 0.5f, 1));
                    normalTexture.Apply();
                }
                placementComputeShader.SetTexture(generatePositionsKernelId, "_NormalTexture", normalTexture);
                Debug.LogWarning("GPU Placement: Using fallback normal texture - context does not have NormalRenderTexture");
            }
            
            // Set mask texture - use context mask if available
            placementComputeShader.SetTexture(generatePositionsKernelId, "_MaskTexture", mask == null ? Texture2D.whiteTexture : mask);
            
            // Set noise texture if available
            placementComputeShader.SetTexture(generatePositionsKernelId, "_NoiseTexture", 
                noiseTexture != null ? noiseTexture : Texture2D.whiteTexture);
            
            // Set buffers for FIRST kernel (GeneratePositions)
            placementComputeShader.SetBuffer(generatePositionsKernelId, "_Results", resultsBuffer);
            placementComputeShader.SetBuffer(generatePositionsKernelId, "_HeightConstraints", heightConstraintsBuffer);
            placementComputeShader.SetBuffer(generatePositionsKernelId, "_SlopeConstraints", slopeConstraintsBuffer);
            placementComputeShader.SetBuffer(generatePositionsKernelId, "_NoiseConstraints", noiseConstraintsBuffer);
            placementComputeShader.SetBuffer(generatePositionsKernelId, "_MaskConstraintThresholds", maskConstraintThresholdsBuffer);
            placementComputeShader.SetBuffer(generatePositionsKernelId, "_LayerConstraintIndices", layerConstraintIndicesBuffer);
            placementComputeShader.SetInt("_LayerConstraintCount", layerConstraintIndices.Length);
            placementComputeShader.SetBuffer(generatePositionsKernelId, "_PlacedObjectPositions", placedObjectsBuffer);
            placementComputeShader.SetBuffer(generatePositionsKernelId, "_MinimumDistances", minimumDistancesBuffer);
            placementComputeShader.SetBuffer(generatePositionsKernelId, "_PrefabSettings", prefabSettingsBuffer);
            
            // Set buffers for SECOND kernel (FilterObjectCollisions)
            placementComputeShader.SetBuffer(filterObjectCollisionsKernelId, "_PrefabSettings", prefabSettingsBuffer);
        }
    }
    
    /// <summary>
    /// Data structure to store placement information for a game object
    /// </summary>
    public struct GameObjectPlacementInfo
    {
        public Vector3 Position;
        public float Scale;
        public float Rotation;
        public int PrefabIndex;
        public Vector3 Normal;
        public float NormalAlignmentFactor;
    }
} 