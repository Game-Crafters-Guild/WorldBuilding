using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GameCraftersGuild.WorldBuilding
{
    /// <summary>
    /// Modifier for placing game objects on terrain
    /// </summary>
    [Serializable]
    public class GameObjectModifier : IGameObjectModifier
    {
        [Serializable]
        public class GameObjectSettings
        {
            public GameObject Prefab;
            
            [Range(0f, 10f)]
            public float MinScale = 0.8f;
            
            [Range(0f, 10f)]
            public float MaxScale = 1.2f;
            
            [Range(0f, 10f)]
            public float YOffset = 0f;
            
            [Tooltip("Align rotation to terrain normal")]
            public bool AlignToNormal = false;
            
            [Tooltip("Random rotation around Y axis")]
            public bool RandomYRotation = false;
            
            [Range(0f, 180f)]
            public float MinRotation = 0f;
            
            [Range(0f, 180f)]
            public float MaxRotation = 360f;
            
            [Tooltip("Minimum distance between this object and other spawned objects")]
            [Range(0f, 10f)]
            public float MinimumDistance = 0f;
        }

        [Serializable]
        public class ObjectCollisionConstraint : IPlacementConstraint
        {
            [Tooltip("Default minimum distance between objects when not specified")]
            [Range(0f, 10f)]
            public float DefaultMinDistance = 2.0f;

            // Structure to store both position and distance
            public struct PlacedObject
            {
                public Vector3 Position;
                public float MinDistance;
            }

            // List to store positions and distances of placed objects
            [NonSerialized]
            public List<PlacedObject> PlacedObjects = new List<PlacedObject>();

            public bool CheckConstraint(TerrainData terrainData, float normX, float normZ, PlacementConstraintContext context)
            {
                if (PlacedObjects.Count == 0)
                    return true;

                // Convert normalized position to world space
                Vector3 terrainPos = Terrain.activeTerrain?.transform.position ?? Vector3.zero;
                Vector3 terrainSize = terrainData.size;
                
                float worldX = terrainPos.x + normX * terrainSize.x;
                float worldZ = terrainPos.z + normZ * terrainSize.z;
                
                // Get height at position (use context's terrain height)
                float worldY = context.TerrainHeight;
                
                Vector3 proposedPosition = new Vector3(worldX, worldY, worldZ);
                
                // Get the minimum distance for the current object (if available in context)
                float currentObjectMinDistance = context.MinimumDistance > 0 ? 
                    context.MinimumDistance : DefaultMinDistance;
                
                // Check distance against all placed objects
                foreach (var placedObj in PlacedObjects)
                {
                    // Use the maximum of the two minimum distances
                    float requiredDistance = Mathf.Max(currentObjectMinDistance, placedObj.MinDistance);
                    
                    float distance = Vector3.Distance(placedObj.Position, proposedPosition);
                    if (distance < requiredDistance)
                    {
                        return false; // Too close to an existing object
                    }
                }
                
                return true; // No collision detected
            }
            
            public void AddObject(Vector3 position, float minimumDistance)
            {
                PlacedObjects.Add(new PlacedObject { 
                    Position = position, 
                    MinDistance = minimumDistance > 0 ? minimumDistance : DefaultMinDistance 
                });
            }
            
            public void Clear()
            {
                PlacedObjects.Clear();
            }
        }
        
        [Header("Game Objects")]
        [Tooltip("List of game objects to spawn")]
        public List<GameObjectSettings> GameObjects = new List<GameObjectSettings>();
        
        [Header("Placement Constraints")]
        [Tooltip("Container for all placement constraints")]
        public PlacementConstraintsContainer ConstraintsContainer = new PlacementConstraintsContainer();
        
        [Header("Placement Settings")]
        [Tooltip("Overall density factor for object placement")]
        [Range(0f, 1.0f)]
        public float Density = 1.0f;
        
        [Tooltip("Random position offset factor")]
        [Range(0f, 1f)]
        public float RandomOffset = 0.0f;
        
        [Tooltip("Base number of objects per square unit")]
        [Range(1, 100)]
        public int ObjectsPerSquareUnit = 10;
        
        [Tooltip("Maximum number of objects to spawn. If set to 0, there is no limit.")]
        public int MaxObjects = 0;
        
        [Tooltip("Default minimum distance between objects")]
        [Range(0f, 10f)]
        public float DefaultMinDistance = 2.0f;
        
        [Header("GPU Placement")]
        [Tooltip("Use GPU-based placement for better performance")]
        public bool UseGPUPlacement = false;
        
        [Tooltip("Compute shader for GPU-based placement")]
        public ComputeShader PlacementComputeShader;
        
        [Header("Randomization")]
        [Tooltip("Random seed for deterministic generation. 0 for random.")]
        public int RandomSeed = 0;
        protected System.Random m_SeededRandom;
        
        [Header("Advanced Settings")]
        [Tooltip("Maximum number of objects to instantiate per frame when using async instantiation")]
        public int MaxObjectsPerFrame = 10;
        
        // Collection to track spawned objects
        private List<GameObject> m_SpawnedObjects = new List<GameObject>();
        
        // Collision constraint reference
        private ObjectCollisionConstraint m_CollisionConstraint;
        
        private GameObjectPlacementGPU m_GPUPlacement;
        
        // Queue for async instantiation
        private Queue<InstantiationRequest> m_InstantiationQueue = new Queue<InstantiationRequest>();
        private bool m_ProcessingQueue = false;
        
        // Structure to hold pending instantiation requests
        private struct InstantiationRequest
        {
            public GameObject prefab;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public float minimumDistance;
        }
        
        public override string FilePath => GetFilePath();

        public GameObjectModifier()
        {
            CreateDefaultConstraints();
        }
        
        // Override OnCleanup to remove objects when disabled or removed
        public override void OnCleanup()
        {
            base.OnCleanup();
            ClearSpawnedObjects();
        }
        
        // OnEnable method to handle editor and runtime enabling
        public void OnEnable()
        {
            // Make sure we have a collision constraint
            if (m_CollisionConstraint == null)
            {
                m_CollisionConstraint = ConstraintsContainer.FindConstraint<ObjectCollisionConstraint>();
            }
            
            // Initialize GPU placement if needed
            if (UseGPUPlacement && PlacementComputeShader != null && m_GPUPlacement == null)
            {
                m_GPUPlacement = new GameObjectPlacementGPU(PlacementComputeShader);
            }
        }
        
        // OnDisable method to handle editor and runtime disabling
        public void OnDisable()
        {
            // Clean up GPU resources
            if (m_GPUPlacement != null)
            {
                m_GPUPlacement.Release();
                m_GPUPlacement = null;
            }
        }
        
        // Called when the object is destroyed
        public void OnDestroy()
        {
            // Make sure to clean up GPU resources
            if (m_GPUPlacement != null)
            {
                m_GPUPlacement.Release();
                m_GPUPlacement = null;
            }
            
            ClearSpawnedObjects();
        }
        
        // Return a random value between 0 and 1 using either the seeded random or Unity's random
        protected float GetRandomValue()
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
        protected float GetRandomRange(float min, float max)
        {
            return min + GetRandomValue() * (max - min);
        }
        
        // Return a random integer in range using either the seeded random or Unity's random
        protected int GetRandomRange(int min, int max)
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
        
        // Return a random rotation around y-axis in degrees
        protected float GetRandomRotation(float min, float max)
        {
            return GetRandomRange(min, max);
        }
        
        // Check if object should be placed based on density
        protected bool CheckDensity()
        {
            return GetRandomValue() <= Density;
        }

        // Create default constraints if none exist
        protected void CreateDefaultConstraints()
        {
            if (ConstraintsContainer.Constraints.Count == 0)
            {
                // Create default constraints
                ConstraintsContainer.Constraints.Add(new HeightConstraint());
                ConstraintsContainer.Constraints.Add(new SlopeConstraint());
                ConstraintsContainer.Constraints.Add(new MaskConstraint());
                
                // Add default collision constraint
                ConstraintsContainer.Constraints.Add(new ObjectCollisionConstraint { DefaultMinDistance = DefaultMinDistance });
                
                // Cache collision constraint reference for performance
                m_CollisionConstraint = ConstraintsContainer.FindConstraint<ObjectCollisionConstraint>();
            }
            else if (m_CollisionConstraint == null)
            {
                // Try to find existing collision constraint
                m_CollisionConstraint = ConstraintsContainer.FindConstraint<ObjectCollisionConstraint>();
            }
        }
        
        public void ClearSpawnedObjects()
        {
            foreach (var obj in m_SpawnedObjects)
            {
                if (obj != null)
                {
                    #if UNITY_EDITOR
                    if (Application.isEditor && !Application.isPlaying)
                    {
                        UnityEngine.Object.DestroyImmediate(obj);
                    }
                    else
                    #endif
                    {
                        UnityEngine.Object.Destroy(obj);
                    }
                }
            }
            
            m_SpawnedObjects.Clear();
            
            // Clear placed object positions if collision constraint exists
            if (m_CollisionConstraint != null)
            {
                m_CollisionConstraint.Clear();
            }
        }
        
        public void SpawnGameObjects(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {
            // Try to find the collision constraint if we don't have a reference yet
            m_CollisionConstraint = ConstraintsContainer.FindConstraint<ObjectCollisionConstraint>();
            
            // Clear positions from previous runs if we have a collision constraint
            if (m_CollisionConstraint != null)
            {
                m_CollisionConstraint.Clear();
            }
            
            // Initialize random if needed
            if (RandomSeed != 0)
            {
                m_SeededRandom = new System.Random(RandomSeed);
            }
            
            // First clear any previously spawned objects
            ClearSpawnedObjects();
            
            // Get terrain data
            TerrainData terrainData = context.TerrainData;
            if (terrainData == null || GameObjects.Count == 0) 
                return;
                
            // Get terrain
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
                return;
                
            // Use GPU-based placement if enabled and available
            if (UseGPUPlacement && PlacementComputeShader != null)
            {
                SpawnGameObjectsGPU(context, worldBounds, mask);
                return;
            }
            
            // Otherwise use CPU-based placement
            SpawnGameObjectsCPU(context, worldBounds, mask);
        }
        
        /// <summary>
        /// Process the instantiation queue over multiple frames
        /// </summary>
        private IEnumerator ProcessInstantiationQueue()
        {
            m_ProcessingQueue = true;
            
            while (m_InstantiationQueue.Count > 0)
            {
                int count = 0;
                
                // Process a batch of instantiations
                while (count < MaxObjectsPerFrame && m_InstantiationQueue.Count > 0)
                {
                    var request = m_InstantiationQueue.Dequeue();
                    
                    // Create the async operation
                    AsyncInstantiateOperation asyncOp = UnityEngine.Object.InstantiateAsync(
                        request.prefab, 
                        request.position, 
                        request.rotation);
                    
                    // We need to wait for completion to set scale and add to tracked objects
                    asyncOp.completed += (op) => {
                        GameObject newObject = asyncOp.Result[0] as GameObject;
                        if (newObject != null)
                        {
                            // Set scale
                            newObject.transform.localScale = request.scale;
                            
                            // Keep track of spawned objects
                            m_SpawnedObjects.Add(newObject);
                            
                            // Add to collision tracking if constraint exists
                            if (m_CollisionConstraint != null)
                            {
                                // Add the object with its minimum distance
                                m_CollisionConstraint.AddObject(request.position, request.minimumDistance);
                            }
                        }
                    };
                    
                    count++;
                }
                
                // Wait for next frame before processing more
                yield return null;
            }
            
            m_ProcessingQueue = false;
        }
        
        private void SpawnGameObjectsGPU(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {
            // Initialize GPU placement if needed
            if (m_GPUPlacement == null)
            {
                m_GPUPlacement = new GameObjectPlacementGPU(PlacementComputeShader);
            }
            
            // Generate placements on GPU
            List<GameObjectPlacementInfo> placements = m_GPUPlacement.GenerateObjectPlacements(
                this, context, worldBounds, mask);
                
            // Prepare objects for instantiation
            foreach (var placement in placements)
            {
                if (placement.PrefabIndex < 0 || placement.PrefabIndex >= GameObjects.Count)
                    continue;
                    
                GameObjectSettings objectSettings = GameObjects[placement.PrefabIndex];
                if (objectSettings.Prefab == null)
                    continue;
                
                // Create rotation based on settings
                Quaternion rotation = Quaternion.identity;
                    
                if (objectSettings.AlignToNormal)
                {
                    // Get terrain normal at position
                    Vector3 terrainPosition = context.TerrainPosition;
                    Vector3 terrainSize = context.TerrainData.size;
                    
                    Vector3 normal = context.TerrainData.GetInterpolatedNormal(
                        (placement.Position.x - terrainPosition.x) / terrainSize.x,
                        (placement.Position.z - terrainPosition.z) / terrainSize.z
                    );
                    Quaternion normalRotation = Quaternion.FromToRotation(Vector3.up, normal);
                    
                    if (objectSettings.RandomYRotation)
                    {
                        Quaternion yRot = Quaternion.Euler(0, placement.Rotation, 0);
                        rotation = normalRotation * yRot;
                    }
                    else
                    {
                        rotation = normalRotation;
                    }
                }
                else if (objectSettings.RandomYRotation)
                {
                    rotation = Quaternion.Euler(0, placement.Rotation, 0);
                }
                
                // Add to instantiation queue
                m_InstantiationQueue.Enqueue(new InstantiationRequest {
                    prefab = objectSettings.Prefab,
                    position = placement.Position,
                    rotation = rotation,
                    scale = Vector3.one * placement.Scale,
                    minimumDistance = objectSettings.MinimumDistance
                });
            }
            
            // Start processing the queue if not already processing
            if (!m_ProcessingQueue && m_InstantiationQueue.Count > 0)
            {
                if (Application.isPlaying)
                {
                    MonoBehaviourHelper.Instance.StartCoroutine(ProcessInstantiationQueue());
                }
                else
                {
                    // In Edit mode, instantiate synchronously
                    while (m_InstantiationQueue.Count > 0)
                    {
                        var request = m_InstantiationQueue.Dequeue();
                        
                        GameObject newObject = UnityEngine.Object.Instantiate(
                            request.prefab,
                            request.position,
                            request.rotation
                        );
                        
                        // Set scale
                        newObject.transform.localScale = request.scale;
                        
                        // Keep track of spawned objects
                        m_SpawnedObjects.Add(newObject);
                        
                        // Add to collision tracking if constraint exists
                        if (m_CollisionConstraint != null)
                        {
                            // Add the object with its minimum distance
                            m_CollisionConstraint.AddObject(request.position, request.minimumDistance);
                        }
                    }
                }
            }
        }
        
        private void SpawnGameObjectsCPU(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {
            // Calculate bounds in terrain space
            TerrainData terrainData = context.TerrainData;
            Terrain terrain = Terrain.activeTerrain;
            Vector3 terrainPos = terrain.transform.position;
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
            int numObjects = Mathf.FloorToInt(areaSize * ObjectsPerSquareUnit * Density);
            
            // Apply max objects limit if set
            if (MaxObjects > 0)
            {
                numObjects = Mathf.Min(numObjects, MaxObjects);
            }
            
            // Get alphamaps for layer constraints
            float[,,] alphamaps = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
            
            // Place objects randomly within bounds
            for (int i = 0; i < numObjects; i++)
            {
                // Try several times to find a valid position
                bool positionFound = false;
                const int maxAttempts = 10;
                
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    // Choose a random position within the bounds
                    float normX = boundsMinX + GetRandomValue() * (boundsMaxX - boundsMinX);
                    float normZ = boundsMinZ + GetRandomValue() * (boundsMaxZ - boundsMinZ);
                    
                    // Apply random offset, but make sure to stay within worldBounds
                    if (RandomOffset > 0)
                    {
                        // Scale the offset by the size of the bounds to keep it proportional
                        float scaledOffsetX = RandomOffset * (boundsMaxX - boundsMinX);
                        float scaledOffsetZ = RandomOffset * (boundsMaxZ - boundsMinZ);
                        
                        // Apply a scaled random offset
                        normX += (GetRandomValue() * 2 - 1) * scaledOffsetX;
                        normZ += (GetRandomValue() * 2 - 1) * scaledOffsetZ;
                        
                        // Reclamp to worldBounds (not just terrain bounds)
                        normX = Mathf.Clamp(normX, boundsMinX, boundsMaxX);
                        normZ = Mathf.Clamp(normZ, boundsMinZ, boundsMaxZ);
                    }
                    
                    // Ensure we're still within terrain bounds (in case worldBounds extends beyond terrain)
                    normX = Mathf.Clamp01(normX);
                    normZ = Mathf.Clamp01(normZ);
                    
                    // Calculate normalized position within the worldBounds (0-1 relative to worldBounds, not terrain)
                    float boundsNormX = (normX - boundsMinX) / (boundsMaxX - boundsMinX);
                    float boundsNormZ = (normZ - boundsMinZ) / (boundsMaxZ - boundsMinZ);
                    
                    // Check if we're within the mask
                    if (mask != null)
                    {
                        float maskValue = SampleTexture(mask, boundsNormX, boundsNormZ);
                        if (maskValue < 0.1f) // Skip if outside mask
                            continue;
                    }
                    
                    // Choose a random game object setting
                    GameObjectSettings objectSettings = GameObjects[GetRandomRange(0, GameObjects.Count)];
                    if (objectSettings.Prefab == null)
                        continue;
                    
                    // Create constraint context
                    PlacementConstraintContext constraintContext = CreateConstraintContext(
                        terrainData, normX, normZ, 
                        boundsNormX, boundsNormZ,
                        alphamaps, mask,
                        objectSettings.MinimumDistance);
                    
                    // Check constraints
                    if (!ConstraintsContainer.CheckConstraints(terrainData, normX, normZ, constraintContext))
                        continue;
                    
                    // Convert normalized position to world space
                    float worldX = terrainPos.x + normX * terrainSize.x;
                    float worldZ = terrainPos.z + normZ * terrainSize.z;
                    
                    // Get height at position
                    float height = terrain.SampleHeight(new Vector3(worldX, 0, worldZ));
                    float worldY = height + objectSettings.YOffset;
                    
                    Vector3 position = new Vector3(worldX, worldY, worldZ);
                    
                    // Create game object
                    GameObject newObject = UnityEngine.Object.Instantiate(
                        objectSettings.Prefab, 
                        position, 
                        Quaternion.identity
                    );
                    
                    // Set scale
                    float randomScale = GetRandomRange(objectSettings.MinScale, objectSettings.MaxScale);
                    newObject.transform.localScale = Vector3.one * randomScale;
                    
                    // Set rotation
                    if (objectSettings.AlignToNormal)
                    {
                        // Get terrain normal at position
                        Vector3 normal = terrainData.GetInterpolatedNormal(normX, normZ);
                        Quaternion normalRotation = Quaternion.FromToRotation(Vector3.up, normal);
                        
                        if (objectSettings.RandomYRotation)
                        {
                            float yRotation = GetRandomRotation(objectSettings.MinRotation, objectSettings.MaxRotation);
                            Quaternion yRot = Quaternion.Euler(0, yRotation, 0);
                            newObject.transform.rotation = normalRotation * yRot;
                        }
                        else
                        {
                            newObject.transform.rotation = normalRotation;
                        }
                    }
                    else if (objectSettings.RandomYRotation)
                    {
                        float yRotation = GetRandomRotation(objectSettings.MinRotation, objectSettings.MaxRotation);
                        newObject.transform.rotation = Quaternion.Euler(0, yRotation, 0);
                    }
                    
                    // Keep track of spawned objects
                    m_SpawnedObjects.Add(newObject);
                    
                    // Add to collision tracking if constraint exists
                    if (m_CollisionConstraint != null)
                    {
                        // Add the object with its minimum distance
                        m_CollisionConstraint.AddObject(position, objectSettings.MinimumDistance);
                    }
                    
                    positionFound = true;
                    break;
                }
                
                // If we can't find a valid position after several attempts, skip this object
                if (!positionFound)
                {
                    continue;
                }
            }
        }
        
        protected PlacementConstraintContext CreateConstraintContext(
            TerrainData terrainData, 
            float normX, 
            float normZ, 
            float boundsNormX, 
            float boundsNormZ, 
            float[,,] alphamaps, 
            Texture mask,
            float minimumDistance = 0)
        {
            // Get height at position
            float height = terrainData.GetHeight(
                Mathf.RoundToInt(normX * terrainData.heightmapResolution), 
                Mathf.RoundToInt(normZ * terrainData.heightmapResolution)
            );

            // Get slope at this position
            float slope = GetTerrainSlope(terrainData, normX, normZ);
            
            // Create context for constraint checking
            return new PlacementConstraintContext
            {
                TerrainHeight = height,
                TerrainSlope = slope,
                AlphaMaps = alphamaps,
                BoundsNormX = boundsNormX,
                BoundsNormZ = boundsNormZ,
                MaskTexture = mask,
                MinimumDistance = minimumDistance
            };
        }
        
        protected float GetTerrainSlope(TerrainData terrainData, float normX, float normZ)
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
        
        // Helper method to sample a texture at normalized coordinates
        protected float SampleTexture(Texture texture, float normX, float normZ)
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
    }
    
    /// <summary>
    /// Helper MonoBehaviour to run coroutines even without a MonoBehaviour attached
    /// </summary>
    public class MonoBehaviourHelper : MonoBehaviour
    {
        private static MonoBehaviourHelper _instance;
        
        public static MonoBehaviourHelper Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("MonoBehaviourHelper");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    _instance = go.AddComponent<MonoBehaviourHelper>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
    }
} 