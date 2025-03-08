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
                noiseConstraints = new Vector4[1] { new Vector4(noiseConstraint.Threshold, 1f, 0f, 0f) };
            }
            else
            {
                noiseConstraints = new Vector4[1] { new Vector4(0.5f, 1f, 0f, 0f) }; // Default values
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
                placedObjectPositions = new float[0];
                minimumDistances = new float[0];
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
            if (tempWhiteTexture == null)
            {
                tempWhiteTexture = new Texture2D(1, 1);
                tempWhiteTexture.SetPixel(0, 0, Color.white);
                tempWhiteTexture.Apply();
            }
            
            return tempWhiteTexture;
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
                mask
            );
            
            // Dispatch the compute shader
            int threadGroupsX = Mathf.CeilToInt(numThreads / 64f);
            placementComputeShader.Dispatch(generatePositionsKernelId, threadGroupsX, 1, 1);
            
            // Get results back from GPU
            resultsBuffer.GetData(results);
            
            // Process results
            List<GameObjectPlacementInfo> placements = new List<GameObjectPlacementInfo>();
            
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i].isValid == 1 && placements.Count < numObjects)
                {
                    // Add valid placement to the list
                    GameObjectPlacementInfo info = new GameObjectPlacementInfo
                    {
                        Position = results[i].position,
                        Scale = results[i].scale,
                        Rotation = results[i].rotation,
                        PrefabIndex = (int)results[i].prefabIndex % modifier.GameObjects.Count
                    };
                    
                    placements.Add(info);
                }
            }
            
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
            
            // Setup placed objects buffer
            if (placedObjectsBuffer == null || placedObjectsBuffer.count != placedObjectPositions.Length)
            {
                ReleaseBuffer(ref placedObjectsBuffer);
                placedObjectsBuffer = new ComputeBuffer(Mathf.Max(1, placedObjectPositions.Length), sizeof(float));
            }
            if (placedObjectPositions.Length > 0)
            {
                placedObjectsBuffer.SetData(placedObjectPositions);
            }
            
            // Setup minimum distances buffer
            if (minimumDistancesBuffer == null || minimumDistancesBuffer.count != minimumDistances.Length)
            {
                ReleaseBuffer(ref minimumDistancesBuffer);
                minimumDistancesBuffer = new ComputeBuffer(Mathf.Max(1, minimumDistances.Length), sizeof(float));
            }
            if (minimumDistances.Length > 0)
            {
                minimumDistancesBuffer.SetData(minimumDistances);
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
            placementComputeShader.SetInt("_PlacedObjectCount", placedObjectPositions.Length / 3);
            placementComputeShader.SetFloat("_DefaultMinDistance", 2.0f); // Default value, can be passed from modifier
            
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
            if (mask != null)
            {
                placementComputeShader.SetTexture(generatePositionsKernelId, "_MaskTexture", mask);
            }
            else if (context.MaskRenderTexture != null)
            {
                placementComputeShader.SetTexture(generatePositionsKernelId, "_MaskTexture", context.MaskRenderTexture);
            }
            else
            {
                // Create a white texture if no mask is provided
                placementComputeShader.SetTexture(generatePositionsKernelId, "_MaskTexture", GetWhiteTexture());
            }
            
            // Set buffers
            placementComputeShader.SetBuffer(generatePositionsKernelId, "_Results", resultsBuffer);
            placementComputeShader.SetBuffer(generatePositionsKernelId, "_HeightConstraints", heightConstraintsBuffer);
            placementComputeShader.SetBuffer(generatePositionsKernelId, "_SlopeConstraints", slopeConstraintsBuffer);
            placementComputeShader.SetBuffer(generatePositionsKernelId, "_NoiseConstraints", noiseConstraintsBuffer);
            placementComputeShader.SetBuffer(generatePositionsKernelId, "_MaskConstraintThresholds", maskConstraintThresholdsBuffer);
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