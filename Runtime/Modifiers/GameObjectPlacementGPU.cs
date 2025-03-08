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
        
        // References to kernel IDs
        private int generatePositionsKernelId;
        
        // Buffers for GPU data
        private ComputeBuffer resultsBuffer;
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
        private struct PlacementResult
        {
            public Vector3 position;
            public float scale;
            public float rotation;
            public uint prefabIndex;
            public uint isValid;
        }
        
        // Structure for prefab settings in compute shader
        private struct PrefabSettings
        {
            public float minScale;
            public float maxScale;
            public float yOffset;
            public uint alignToNormal;
            public uint randomYRotation;
            public float minRotation;
            public float maxRotation;
            public float minimumDistance;
        }
        
        // Array to hold results
        private PlacementResult[] results;
        
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
        private RenderTexture normalTexture;
        
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
        }
        
        /// <summary>
        /// Release all GPU resources
        /// </summary>
        public void Release()
        {
            // Release buffers
            ReleaseBuffer(ref resultsBuffer);
            ReleaseBuffer(ref heightConstraintsBuffer);
            ReleaseBuffer(ref slopeConstraintsBuffer);
            ReleaseBuffer(ref noiseConstraintsBuffer);
            ReleaseBuffer(ref placedObjectsBuffer);
            ReleaseBuffer(ref minimumDistancesBuffer);
            ReleaseBuffer(ref prefabSettingsBuffer);
            ReleaseBuffer(ref maskConstraintThresholdsBuffer);
            ReleaseBuffer(ref layerConstraintIndicesBuffer);
            
            // Release textures
            ReleaseTexture(ref normalTexture);
            
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
                                Debug.Log($"Layer constraint: Found matching layer at index {i} - {allowedLayer.name}");
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
                
                Debug.Log($"GPU Placement: Using {collisionConstraint.PlacedObjects.Count} placed objects for collision constraint");
                
                for (int i = 0; i < collisionConstraint.PlacedObjects.Count; i++)
                {
                    var obj = collisionConstraint.PlacedObjects[i];
                    placedObjectPositions[i * 3] = obj.Position.x;
                    placedObjectPositions[i * 3 + 1] = obj.Position.y;
                    placedObjectPositions[i * 3 + 2] = obj.Position.z;
                    minimumDistances[i] = obj.MinDistance;
                    
                    // Debug the first few placed objects
                    if (i < 5)
                    {
                        Debug.Log($"Placed object {i}: Position={obj.Position}, MinDistance={obj.MinDistance}");
                    }
                }
            }
            else
            {
                // Empty arrays if no objects placed yet
                placedObjectPositions = new float[0];
                minimumDistances = new float[0];
                
                if (collisionConstraint != null)
                {
                    Debug.Log("GPU Placement: CollisionConstraint exists but has no placed objects");
                }
                else
                {
                    Debug.Log("GPU Placement: No CollisionConstraint found");
                }
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
                
                prefabSettings[i] = new PrefabSettings
                {
                    minScale = gameObj.MinScale,
                    maxScale = gameObj.MaxScale,
                    yOffset = gameObj.YOffset,
                    alignToNormal = gameObj.AlignToNormal ? 1u : 0u,
                    randomYRotation = gameObj.RandomYRotation ? 1u : 0u,
                    minRotation = gameObj.MinRotation,
                    maxRotation = gameObj.MaxRotation,
                    minimumDistance = gameObj.MinimumDistance > 0 ? gameObj.MinimumDistance : modifier.DefaultMinDistance
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
            // Create normal map texture if needed
            if (normalTexture == null || 
                normalTexture.width != terrainData.heightmapResolution || 
                normalTexture.height != terrainData.heightmapResolution)
            {
                ReleaseTexture(ref normalTexture);
                
                normalTexture = new RenderTexture(
                    terrainData.heightmapResolution,
                    terrainData.heightmapResolution,
                    0,
                    RenderTextureFormat.ARGBFloat);
                normalTexture.enableRandomWrite = true;
                normalTexture.Create();
                
                // Create normals from heightmap
                Texture2D tempNormals = new Texture2D(terrainData.heightmapResolution, terrainData.heightmapResolution, TextureFormat.RGBAFloat, false);
                
                // Calculate normals based on heightmap - a simplified approach
                for (int y = 0; y < terrainData.heightmapResolution; y++)
                {
                    for (int x = 0; x < terrainData.heightmapResolution; x++)
                    {
                        Vector3 normal = terrainData.GetInterpolatedNormal(
                            (float)x / terrainData.heightmapResolution, 
                            (float)y / terrainData.heightmapResolution);
                        
                        // Convert from -1,1 to 0,1 range for storage in texture
                        normal = normal * 0.5f + new Vector3(0.5f, 0.5f, 0.5f);
                        tempNormals.SetPixel(x, y, new Color(normal.x, normal.y, normal.z, 1));
                    }
                }
                
                tempNormals.Apply();
                Graphics.Blit(tempNormals, normalTexture);
                
                // Safely destroy the temporary texture
                SafeDestroy(tempNormals);
            }
        }
        
        /// <summary>
        /// Gets a white texture to use as a default mask
        /// </summary>
        private Texture GetWhiteTexture()
        {
            return Texture2D.whiteTexture;
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
                return new List<GameObjectPlacementInfo>();
            }
            
            // Setup constraints from modifier
            SetupConstraints(modifier);
            
            // Get terrain data
            TerrainData terrainData = context.TerrainData;
            if (terrainData == null || modifier.GameObjects.Count == 0)
                return new List<GameObjectPlacementInfo>();
                
            // Get terrain
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
                return new List<GameObjectPlacementInfo>();
            
            // Setup normal texture which isn't in WorldBuildingContext
            SetupNormalTexture(terrainData);
            
            // Calculate bounds in terrain space
            Vector3 terrainPos = context.TerrainPosition;
            Vector3 terrainSize = terrainData.size;
            
            float boundsMinX = (worldBounds.min.x - terrainPos.x) / terrainSize.x;
            float boundsMinZ = (worldBounds.min.z - terrainPos.z) / terrainSize.z;
            float boundsMaxX = (worldBounds.max.x - terrainPos.x) / terrainSize.x;
            float boundsMaxZ = (worldBounds.max.z - terrainPos.z) / terrainSize.z;
            
            // Clamp to terrain bounds
            boundsMinX = Mathf.Clamp01(boundsMinX);
            boundsMinZ = Mathf.Clamp01(boundsMinZ);
            boundsMaxX = Mathf.Clamp01(boundsMaxX);
            boundsMaxZ = Mathf.Clamp01(boundsMaxZ);
            
            // Calculate the area in square units
            float areaWidth = (boundsMaxX - boundsMinX) * terrainSize.x;
            float areaDepth = (boundsMaxZ - boundsMinZ) * terrainSize.z;
            float areaSize = areaWidth * areaDepth;
            
            // Calculate number of objects to place
            int numObjects = Mathf.FloorToInt(areaSize * modifier.ObjectsPerSquareUnit * modifier.Density);
            
            // Apply max objects limit if set
            if (modifier.MaxObjects > 0)
            {
                numObjects = Mathf.Min(numObjects, modifier.MaxObjects);
            }
            
            // Limit the number of parallel calculations for performance
            int numThreads = Mathf.Min(maxAttempts, 1000000);
            
            // Create buffers
            CreateBuffers(numThreads);
            
            // Set compute shader parameters using context
            SetShaderParameters(
                context, 
                terrainData,
                boundsMinX, boundsMinZ, boundsMaxX, boundsMaxZ,
                modifier.Density, modifier.MaxObjects, 
                modifier.GameObjects.Count, 
                modifier.RandomSeed, 
                modifier.RandomOffset,
                modifier.DefaultMinDistance > 0f ? modifier.DefaultMinDistance : 2.0f,
                mask
            );
            
            // Dispatch the compute shader
            int threadGroupsX = Mathf.CeilToInt(numThreads / 64f);
            placementComputeShader.Dispatch(generatePositionsKernelId, threadGroupsX, 1, 1);
            
            // Get results back from GPU
            resultsBuffer.GetData(results);
            
            // Process results
            List<GameObjectPlacementInfo> placements = new List<GameObjectPlacementInfo>();
            
            // First pass: collect all valid placements returned by GPU
            List<GameObjectPlacementInfo> validGPUResults = new List<GameObjectPlacementInfo>();
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i].isValid == 1 && validGPUResults.Count < numObjects)
                {
                    GameObjectPlacementInfo info = new GameObjectPlacementInfo
                    {
                        Position = results[i].position,
                        Scale = results[i].scale,
                        Rotation = results[i].rotation,
                        PrefabIndex = (int)results[i].prefabIndex % modifier.GameObjects.Count
                    };
                    
                    validGPUResults.Add(info);
                }
            }
            
            Debug.Log($"GPU returned {validGPUResults.Count} valid positions before collision filtering");
            
            // Second pass: CPU-side collision detection between objects generated in this batch
            // Get existing collision constraint
            var collisionConstraint = FindConstraint<GameObjectModifier.ObjectCollisionConstraint>(modifier.ConstraintsContainer.Constraints);
            if (collisionConstraint == null)
            {
                Debug.LogWarning("No collision constraint found. Creating a new one.");
                collisionConstraint = new GameObjectModifier.ObjectCollisionConstraint { DefaultMinDistance = modifier.DefaultMinDistance };
                modifier.ConstraintsContainer.Constraints.Add(collisionConstraint);
            }
            
            // Keep track of objects we've added in this pass
            List<Vector3> newlyAddedPositions = new List<Vector3>();
            List<float> newlyAddedDistances = new List<float>();
            
            // Process each placement from GPU
            foreach (var info in validGPUResults)
            {
                if (placements.Count >= numObjects)
                    break;
                    
                float minimumDistance = modifier.GameObjects[info.PrefabIndex].MinimumDistance;
                if (minimumDistance <= 0)
                    minimumDistance = modifier.DefaultMinDistance;
                
                // Check against previously added objects in this batch
                bool validPosition = true;
                for (int i = 0; i < newlyAddedPositions.Count; i++)
                {
                    Vector3 existingPos = newlyAddedPositions[i];
                    float requiredDist = Mathf.Max(minimumDistance, newlyAddedDistances[i]);
                    
                    if (Vector3.Distance(info.Position, existingPos) < requiredDist)
                    {
                        validPosition = false;
                        break;
                    }
                }
                
                // Check against previously placed objects (from previous batches)
                if (validPosition && collisionConstraint.PlacedObjects.Count > 0)
                {
                    foreach (var placedObj in collisionConstraint.PlacedObjects)
                    {
                        float requiredDist = Mathf.Max(minimumDistance, placedObj.MinDistance);
                        if (Vector3.Distance(info.Position, placedObj.Position) < requiredDist)
                        {
                            validPosition = false;
                            break;
                        }
                    }
                }
                
                // If it passed all checks, add to final placements
                if (validPosition)
                {
                    placements.Add(info);
                    
                    // Track this position for checking against subsequent objects
                    newlyAddedPositions.Add(info.Position);
                    newlyAddedDistances.Add(minimumDistance);
                    
                    // Add to collision constraint for future passes
                    collisionConstraint.AddObject(info.Position, minimumDistance);
                    
                    // Debug - only for the first few objects
                    if (placements.Count <= 5)
                    {
                        Debug.Log($"Added object {placements.Count-1} to collision constraint: Position={info.Position}, MinDistance={minimumDistance}");
                    }
                    
                    // Log total count periodically
                    if (placements.Count % 50 == 0)
                    {
                        Debug.Log($"Collision constraint now has {collisionConstraint.PlacedObjects.Count} objects");
                    }
                }
            }
            
            Debug.Log($"GPU Placement: Generated {placements.Count} valid placements after CPU-side collision filtering (rejected {validGPUResults.Count - placements.Count})");
            
            return placements;
        }
        
        /// <summary>
        /// Create or resize buffers for compute shader
        /// </summary>
        private void CreateBuffers(int numThreads)
        {
            // Create/resize results buffer
            if (resultsBuffer == null || resultsBuffer.count != numThreads)
            {
                ReleaseBuffer(ref resultsBuffer);
                resultsBuffer = new ComputeBuffer(numThreads, sizeof(float) * 3 + sizeof(float) + sizeof(float) + sizeof(uint) + sizeof(uint));
                results = new PlacementResult[numThreads];
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
                Debug.Log($"Created placedObjectsBuffer with {requiredElementCount} elements (for {requiredElementCount/3} objects), stride={sizeof(float)}");
            }
            
            if (placedObjectPositions.Length > 0)
            {
                Debug.Log("Buffer contents before SetData:");
                for (int i = 0; i < Math.Min(9, placedObjectPositions.Length); i++)
                {
                    Debug.Log($"  placedObjectPositions[{i}] = {placedObjectPositions[i]}");
                }
                
                // Create a temporary host-side buffer to verify data
                float[] debugBuffer = new float[placedObjectPositions.Length];
                Array.Copy(placedObjectPositions, debugBuffer, placedObjectPositions.Length);
                
                // Set the data to the GPU buffer
                placedObjectsBuffer.SetData(placedObjectPositions);
                
                // For debugging, read back the data
                float[] readbackData = new float[placedObjectPositions.Length];
                placedObjectsBuffer.GetData(readbackData);
                
                // Verify the data matches
                bool dataMatches = true;
                for (int i = 0; i < placedObjectPositions.Length; i++)
                {
                    if (Math.Abs(debugBuffer[i] - readbackData[i]) > 0.0001f)
                    {
                        Debug.LogError($"Data mismatch at index {i}: {debugBuffer[i]} vs {readbackData[i]}");
                        dataMatches = false;
                        break;
                    }
                }
                
                if (dataMatches)
                {
                    Debug.Log("Buffer data verification successful!");
                }
                else
                {
                    Debug.LogError("Buffer data verification failed!");
                }
                
                Debug.Log($"Set placedObjectsBuffer data with {placedObjectPositions.Length} elements (for {placedObjectPositions.Length/3} objects)");
                
                // Debug some values
                if (placedObjectPositions.Length >= 3)
                {
                    Debug.Log($"First object position: ({placedObjectPositions[0]}, {placedObjectPositions[1]}, {placedObjectPositions[2]})");
                }
            }
            
            // Setup minimum distances buffer - for each object we need 1 float
            int minDistancesCount = Mathf.Max(1, minimumDistances.Length);
            
            if (minimumDistancesBuffer == null || minimumDistancesBuffer.count != minDistancesCount)
            {
                ReleaseBuffer(ref minimumDistancesBuffer);
                minimumDistancesBuffer = new ComputeBuffer(minDistancesCount, sizeof(float));
                Debug.Log($"Created minimumDistancesBuffer with {minDistancesCount} elements, stride={sizeof(float)}");
            }
            if (minimumDistances.Length > 0)
            {
                minimumDistancesBuffer.SetData(minimumDistances);
                Debug.Log($"Set minimumDistancesBuffer data with {minimumDistances.Length} elements");
                
                // Debug some values
                if (minimumDistances.Length >= 1)
                {
                    Debug.Log($"First minimum distance: {minimumDistances[0]}");
                }
            }
            
            // Setup prefab settings buffer
            if (prefabSettingsBuffer == null || prefabSettingsBuffer.count != prefabSettings.Length)
            {
                ReleaseBuffer(ref prefabSettingsBuffer);
                // 5 floats + 3 uints 
                prefabSettingsBuffer = new ComputeBuffer(Mathf.Max(1, prefabSettings.Length), sizeof(float) * 5 + sizeof(uint) * 3);
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
            
            // Debug logging
            Debug.Log($"GPU Placement: _PlacedObjectCount={objectCount}, _DefaultMinDistance={defaultMinDistance}");
            Debug.Log($"GPU Placement: Using buffers - placedObjectsBuffer.count={placedObjectsBuffer.count}, minimumDistancesBuffer.count={minimumDistancesBuffer.count}");
            
            // Set texture samplers - use the context render textures directly
            placementComputeShader.SetTexture(generatePositionsKernelId, "_HeightmapTexture", context.HeightmapRenderTexture);
            
            // For alphamap/splatmap, use the first splat texture from the context if available
            if (context.SplatRenderTextures != null && context.SplatRenderTextures.Length > 0)
            {
                placementComputeShader.SetTexture(generatePositionsKernelId, "_AlphamapTexture", context.SplatRenderTextures[0]);
            }
            
            // Set the normal texture we created
            placementComputeShader.SetTexture(generatePositionsKernelId, "_NormalTexture", normalTexture);
            
            // Set mask texture - use context mask if available
            placementComputeShader.SetTexture(generatePositionsKernelId, "_MaskTexture", mask == null ? Texture2D.whiteTexture : mask);
            
            // Set noise texture if available
            placementComputeShader.SetTexture(generatePositionsKernelId, "_NoiseTexture", 
                noiseTexture != null ? noiseTexture : Texture2D.whiteTexture);
            
            // Set buffers
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
    }
} 