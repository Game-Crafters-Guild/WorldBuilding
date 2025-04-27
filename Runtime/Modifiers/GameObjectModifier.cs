using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor;
#if UNITY_2019_1_OR_NEWER
using Unity.EditorCoroutines.Editor;
#endif
#endif

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
            
            [Tooltip("Minimum normal alignment factor (0 = no alignment, 1 = full alignment)")]
            [Range(0f, 1f)]
            public float MinNormalAlignment = 0.7f;
            
            [Tooltip("Maximum normal alignment factor (0 = no alignment, 1 = full alignment)")]
            [Range(0f, 1f)]
            public float MaxNormalAlignment = 1.0f;
            
            [Tooltip("Random rotation around Y axis")]
            public bool RandomYRotation = true;
            
            [Range(0f, 180f)]
            public float MinRotation = 0f;
            
            [Range(0f, 360f)]
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
                    // The stored MinDistance already includes the global scale at the time it was added
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
        
        //[Header("Game Objects")]
        [Tooltip("List of game objects to spawn")]
        public GameObjectSettingsContainer GameObjectsContainer = new GameObjectSettingsContainer();
        
        /// <summary>
        /// Backward compatibility method to access the GameObjects list
        /// </summary>
        public List<GameObjectSettings> GameObjects
        {
            get { return GameObjectsContainer.GameObjects; }
        }
        
        //[Header("Placement Constraints")]
        [Tooltip("Container for all placement constraints")]
        public PlacementConstraintsContainer ConstraintsContainer = new PlacementConstraintsContainer();
        
        [Header("Placement Settings")]
        [Tooltip("Overall density factor for object placement")]
        [Range(0f, 1.0f)]
        public float Density = 0.005f;
        
        [Tooltip("Random position offset factor")]
        [Range(0f, 1f)]
        public float RandomOffset = 0.0f;
        
        [Tooltip("Base number of objects per square unit")]
        [Range(1, 100)]
        public int ObjectsPerSquareUnit = 10;
        
        [Tooltip("Maximum number of objects to spawn. If set to 0, there is no limit.")]
        public int MaxObjects = 1000;
        
        [Tooltip("Global scale factor applied to all spawned objects")]
        [Range(0.1f, 10f)]
        public float GlobalScale = 1.0f;
        
        /*[Header("GPU Placement")]
        [Tooltip("Use GPU-based placement for better performance")]
        public bool UseGPUPlacement = true;*/
        
        [Tooltip("Compute shader for GPU-based placement")]
        public ComputeShader PlacementComputeShader;
        
        [Header("Randomization")]
        [Tooltip("Random seed for deterministic generation. 0 for random.")]
        public int RandomSeed = 0;
        protected System.Random m_SeededRandom;
        
        [Header("Advanced Settings")]
        [Tooltip("Maximum number of objects to instantiate per frame when using async instantiation")]
        public int MaxObjectsPerFrame = 10;
        
        [Tooltip("Use async instantiation for better performance. If disabled, objects will be created using coroutines.")]
        public bool UseAsyncInstantiation = true;
        
        // Collection to track spawned objects
        private List<GameObject> m_SpawnedObjects = new List<GameObject>();
        
        // Collision constraint reference
        private ObjectCollisionConstraint m_CollisionConstraint;
        
        private GameObjectPlacementGPU m_GPUPlacement;
        
        // Queue for async instantiation
        private Queue<InstantiationRequest> m_InstantiationQueue = new Queue<InstantiationRequest>();
        private bool m_ProcessingQueue = false;
        
        // References for coroutines
        private Coroutine m_RunningCoroutine;
        
        #if UNITY_EDITOR
        private EditorCoroutine m_EditorCoroutine;
        #endif
        
        // Add a reference for the container GameObject
        private GameObject m_ObjectContainer;
        
        // Batch counter for creating child containers
        private int m_BatchCounter = 0;
        
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
            if (PlacementComputeShader != null && m_GPUPlacement == null)
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
                
                // Add default collision constraint with user's value (without scale applied)
                ConstraintsContainer.Constraints.Add(new ObjectCollisionConstraint { DefaultMinDistance = 2.0f });
                
                // Cache collision constraint reference for performance
                m_CollisionConstraint = ConstraintsContainer.FindConstraint<ObjectCollisionConstraint>();
            }
            else if (m_CollisionConstraint == null)
            {
                // Try to find existing collision constraint
                m_CollisionConstraint = ConstraintsContainer.FindConstraint<ObjectCollisionConstraint>();
            }
        }
        
        // Cancel any running queue processing
        private void CancelQueueProcessing()
        {
            if (m_ProcessingQueue)
            {
                if (Application.isPlaying && m_RunningCoroutine != null)
                {
                    MonoBehaviourHelper.Instance.StopCoroutine(m_RunningCoroutine);
                    m_RunningCoroutine = null;
                }
                #if UNITY_EDITOR
                else if (!Application.isPlaying && m_EditorCoroutine != null)
                {
                    EditorCoroutineUtility.StopCoroutine(m_EditorCoroutine);
                    m_EditorCoroutine = null;
                }
                #endif
                
                m_ProcessingQueue = false;
            }
        }
        
        public void ClearSpawnedObjects()
        {
            // Cancel any running coroutines first
            CancelQueueProcessing();
            
            // Clear the instantiation queue
            m_InstantiationQueue.Clear();
            
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
            
            // Destroy the container GameObject if it exists
            if (m_ObjectContainer != null)
            {
                #if UNITY_EDITOR
                if (Application.isEditor && !Application.isPlaying)
                {
                    UnityEngine.Object.DestroyImmediate(m_ObjectContainer);
                }
                else
                #endif
                {
                    UnityEngine.Object.Destroy(m_ObjectContainer);
                }
                
                m_ObjectContainer = null;
            }
            
            // Clear placed object positions if collision constraint exists
            if (m_CollisionConstraint != null)
            {
                m_CollisionConstraint.Clear();
            }
        }
        
        // Modified signature to accept StampShape
        public void SpawnGameObjects(WorldBuildingContext context, Bounds worldBounds, Texture mask, StampShape shape)
        {
            // Cancel any in-progress spawning operations
            CancelQueueProcessing();
            
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
            
            // Create a container for spawned objects if needed
            string containerName = "SpawnedObjects";
            if (shape != null) // Use shape.name if available
            {
                containerName = $"{shape.gameObject.name}_Objects";
            }
            m_ObjectContainer = new GameObject(containerName);
            
            // Parent the container to the Stamp if available
            if (shape != null)
            {
                m_ObjectContainer.transform.SetParent(shape.transform); // Use shape.transform
            }
            
            // Get terrain data
            TerrainData terrainData = context.TerrainData;
            if (terrainData == null || GameObjectsContainer.GameObjects.Count == 0) 
                return;

            bool hasPrefabsToSpawn = false;
            foreach (var item in GameObjectsContainer.GameObjects)
            {
                if (item.Prefab != null)
                {
                    hasPrefabsToSpawn = true;
                    break;
                }
            }
            if (!hasPrefabsToSpawn) return;
                
            // Get terrain
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
                return;
                
            // Use GPU-based placement if enabled and available
            if (HasPlacementShader())
            {                
                SpawnGameObjectsGPU(context, worldBounds, mask, shape);
            }
        }

        private bool HasPlacementShader()
        {
            if (PlacementComputeShader == null)
            {
                PlacementComputeShader =
                    Resources.Load<ComputeShader>("GameCraftersGuild/WorldBuilding/Shaders/GameObjectPlacement");
            }
            return PlacementComputeShader != null;
        }
        
        /// <summary>
        /// Process the instantiation queue over multiple frames
        /// </summary>
        private IEnumerator ProcessInstantiationQueue()
        {
            m_ProcessingQueue = true;
            m_BatchCounter = 0;
            
            // Track pending async operations
            List<AsyncInstantiateOperation> pendingOperations = new List<AsyncInstantiateOperation>();
            List<InstantiationRequest> pendingRequests = new List<InstantiationRequest>();
            GameObject currentBatchParent = null;
            
            // Progress tracking
            int totalObjects = m_InstantiationQueue.Count;
            int completedObjects = 0;
            int skippedObjects = 0;
            int lastReportedPercentage = 0;
            int batchSize = 0;
            
            // Cache real-time object positions for collision checking
            List<Vector3> placedPositions = new List<Vector3>();
            List<float> placedMinDistances = new List<float>();
            
            // Get collision constraint if we have one
            var collisionConstraint = ConstraintsContainer.FindConstraint<ObjectCollisionConstraint>();
            
            //Debug.Log($"Starting to spawn {totalObjects} objects (max {MaxObjectsPerFrame} per frame)");
            
            while (m_InstantiationQueue.Count > 0 || pendingOperations.Count > 0)
            {
                // Create a new batch parent if needed
                if (currentBatchParent == null)
                {
                    m_BatchCounter++;
                    string batchName = $"Batch_{m_BatchCounter}";
                    currentBatchParent = new GameObject(batchName);
                    currentBatchParent.transform.SetParent(m_ObjectContainer.transform);
                    currentBatchParent.SetActive(false); // Start disabled to prevent popping
                    batchSize = 0;
                }
                
                // Check if we can start more async operations
                bool processingNewBatch = pendingOperations.Count == 0;
                int maxOpsThisFrame = processingNewBatch ? MaxObjectsPerFrame : 0;
                
                while (pendingOperations.Count < MaxObjectsPerFrame * 2 && 
                       m_InstantiationQueue.Count > 0 && 
                       batchSize < maxOpsThisFrame)
                {
                    // Pull requests from the queue up to our per-frame limit
                    int batchCount = Mathf.Min(MaxObjectsPerFrame - batchSize, m_InstantiationQueue.Count);
                    List<InstantiationRequest> validRequests = new List<InstantiationRequest>();
                    
                    // For each request, check runtime collisions before instantiating
                    for (int i = 0; i < batchCount; i++)
                    {
                        if (m_InstantiationQueue.Count == 0)
                            break;
                            
                        var request = m_InstantiationQueue.Dequeue();
                        
                        // Check if this position would collide with objects already placed during this session
                        bool validPosition = true;
                        
                        // Calculate effective minimum distance with scale
                        float effectiveMinDistance = request.minimumDistance;
                        if (effectiveMinDistance <= 0 && collisionConstraint != null)
                            effectiveMinDistance = collisionConstraint.DefaultMinDistance;
                        
                        // Apply scale factor to minimum distance
                        effectiveMinDistance *= request.scale.x;
                        
                        // Check against objects placed during this coroutine
                        for (int j = 0; j < placedPositions.Count; j++)
                        {
                            // Use the maximum of the two distances
                            float requiredDistance = Mathf.Max(effectiveMinDistance, placedMinDistances[j]);
                            
                            if (Vector3.Distance(request.position, placedPositions[j]) < requiredDistance)
                            {
                                validPosition = false;
                                break;
                            }
                        }
                        
                        // If position is valid, add to batch for instantiation
                        if (validPosition)
                        {
                            validRequests.Add(request);
                            batchSize++;
                            
                            // Immediately add to our collision tracking to prevent future placements from overlapping
                            placedPositions.Add(request.position);
                            placedMinDistances.Add(effectiveMinDistance);
                            
                            // Also add to permanent constraint tracking if constraint exists
                            if (collisionConstraint != null)
                            {
                                collisionConstraint.AddObject(request.position, effectiveMinDistance);
                            }
                        }
                        else
                        {
                            skippedObjects++;
                        }
                    }
                    
                    // Instantiate valid requests
                    foreach (var request in validRequests)
                    {
                        if (UseAsyncInstantiation)
                        {
                            // Use the overload that takes parent, position, and rotation directly
                            AsyncInstantiateOperation asyncOp = UnityEngine.Object.InstantiateAsync(
                                request.prefab, 
                                currentBatchParent.transform, // Use the batch parent
                                request.position,
                                request.rotation);
                            
                            // Track the operation and its request data
                            pendingOperations.Add(asyncOp);
                            pendingRequests.Add(request);
                        }
                        else
                        {
                            // Use synchronous instantiation with coroutine
                            GameObject newObject = InstantiateInternal(request.prefab,
                                request.position,
                                request.rotation,
                                currentBatchParent.transform);
                            
                            // Set scale
                            newObject.transform.localScale = request.scale;
                            
                            // Keep track of spawned objects
                            m_SpawnedObjects.Add(newObject);
                            
                            // Update progress immediately
                            completedObjects++;
                        }
                    }
                }
                
                // For non-async mode, check if we need to activate the batch parent
                if (!UseAsyncInstantiation && batchSize > 0 && (batchSize >= MaxObjectsPerFrame || m_InstantiationQueue.Count == 0))
                {
                    // Update progress reporting
                    int currentPercentage = Mathf.RoundToInt((float)completedObjects / totalObjects * 100);
                    if (currentPercentage >= lastReportedPercentage + 10)
                    {
                        lastReportedPercentage = currentPercentage / 10 * 10; // Round to nearest 10%
                        //Debug.Log($"Spawning progress: {lastReportedPercentage}% ({completedObjects}/{totalObjects})");
                    }
                    
                    // Enable the batch once it's full or we're done with all objects
                    currentBatchParent.SetActive(true);
                    currentBatchParent = null; // Create a new batch next time
                    
                    // Wait for next frame before processing more
                    yield return null;
                    continue;
                }
                
                // Process completed operations for async mode
                if (UseAsyncInstantiation && pendingOperations.Count > 0)
                {
                    bool allDone = true;
                    int completedInBatch = 0;
                    
                    // Process completed operations
                    for (int i = pendingOperations.Count - 1; i >= 0; i--)
                    {
                        var asyncOp = pendingOperations[i];
                        if (asyncOp.isDone)
                        {
                            var request = pendingRequests[i];
                            GameObject newObject = asyncOp.Result[0] as GameObject;
                            
                            if (newObject != null)
                            {
                                // Set scale
                                newObject.transform.localScale = request.scale;
                                
                                // Keep track of spawned objects
                                m_SpawnedObjects.Add(newObject);
                            }
                            
                            // Remove from pending lists
                            pendingOperations.RemoveAt(i);
                            pendingRequests.RemoveAt(i);
                            
                            // Update progress
                            completedObjects++;
                            completedInBatch++;
                        }
                        else
                        {
                            allDone = false;
                        }
                    }
                    
                    // Update progress reporting
                    if (completedInBatch > 0)
                    {
                        int currentPercentage = Mathf.RoundToInt((float)completedObjects / totalObjects * 100);
                        if (currentPercentage >= lastReportedPercentage + 10)
                        {
                            lastReportedPercentage = currentPercentage / 10 * 10; // Round to nearest 10%
                            //Debug.Log($"Spawning progress: {lastReportedPercentage}% ({completedObjects}/{totalObjects})");
                        }
                    }
                    
                    // If all operations in current batch are done, activate the batch parent and reset
                    if (allDone && batchSize > 0)
                    {
                        currentBatchParent.SetActive(true);
                        currentBatchParent = null; // Create a new batch next time
                    }
                }
                
                // Wait for next frame before processing more
                yield return null;
            }
            
            // Ensure the last batch is activated
            if (currentBatchParent != null)
            {
                currentBatchParent.SetActive(true);
            }
            
            //Debug.Log($"Completed spawning {completedObjects} objects, skipped {skippedObjects} due to collisions");
            m_ProcessingQueue = false;
            m_RunningCoroutine = null;
            #if UNITY_EDITOR
            m_EditorCoroutine = null;
            #endif
        }

        private static GameObject InstantiateInternal(GameObject requestPrefab, Vector3 requestPosition, Quaternion requestRotation, Transform transform)
        {
#if UNITY_EDITOR
            GameObject go = PrefabUtility.InstantiatePrefab(requestPrefab, transform) as GameObject;
            go.transform.position = requestPosition;
            go.transform.rotation = requestRotation;
            return go;
#endif
            return UnityEngine.Object.Instantiate(requestPrefab, requestPrefab.transform.position, requestPrefab.transform.rotation, transform);
        }

        // Modified signature to accept StampShape
        private void SpawnGameObjectsGPU(WorldBuildingContext context, Bounds worldBounds, Texture mask, StampShape shape)
        {
            // Initialize GPU placement if needed
            if (m_GPUPlacement == null)
            {
                m_GPUPlacement = new GameObjectPlacementGPU(PlacementComputeShader);
            }
            m_GPUPlacement.PlacementComputeShader = PlacementComputeShader;
            
            // Generate placements on GPU - Pass shape transform info, scale, and mask gen bounds
            List<GameObjectPlacementInfo> placements = m_GPUPlacement.GenerateObjectPlacements(
                this, 
                context, 
                worldBounds, 
                mask, 
                shape.transform.worldToLocalMatrix, // Pass worldToLocalMatrix
                shape.transform.lossyScale,           // Pass lossyScale
                shape.MaskGenerationBoundsMin,        // Pass mask gen min
                shape.MaskGenerationBoundsSize        // Pass mask gen size
            );
            
            if (placements == null) return;
                
            // Prepare objects for instantiation
            foreach (var placement in placements)
            {
                if (placement.PrefabIndex < 0 || placement.PrefabIndex >= GameObjectsContainer.GameObjects.Count)
                    continue;
                    
                GameObjectSettings objectSettings = GameObjectsContainer.GameObjects[placement.PrefabIndex];
                if (objectSettings.Prefab == null)
                    continue;
                
                // Create rotation based on settings
                Quaternion rotation = Quaternion.identity;
                    
                if (objectSettings.AlignToNormal)
                {
                    // Instead of getting the terrain normal, use the normal from the GPU
                    Vector3 normal = placement.Normal;
                    
                    // Apply normal alignment factor (lerp between identity and full normal alignment)
                    Quaternion normalRotation = Quaternion.identity;
                    
                    if (placement.NormalAlignmentFactor > 0)
                    {
                        // Full normal alignment
                        Quaternion fullNormalRot = Quaternion.FromToRotation(Vector3.up, normal);
                        
                        // Interpolate between identity and full normal alignment
                        normalRotation = Quaternion.Slerp(Quaternion.identity, fullNormalRot, placement.NormalAlignmentFactor);
                    }
                    
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
                    scale = Vector3.one * placement.Scale, // Note: Scale from GPU already includes GlobalScale
                    minimumDistance = objectSettings.MinimumDistance
                });
            }
            
            // Start processing the queue if not already processing
            if (m_InstantiationQueue.Count > 0)
            {
                if (Application.isPlaying)
                {
                    m_RunningCoroutine = MonoBehaviourHelper.Instance.StartCoroutine(ProcessInstantiationQueue());
                }
                #if UNITY_EDITOR
                else if (!Application.isPlaying)
                {
                    // Use EditorCoroutine in Edit mode
                    #if UNITY_2019_1_OR_NEWER
                    m_EditorCoroutine = EditorCoroutineUtility.StartCoroutineOwnerless(ProcessInstantiationQueue());
                    #else
                    m_EditorCoroutine = EditorCoroutineUtility.StartCoroutine(ProcessInstantiationQueue(), this);
                    #endif
                }
                #endif
                else
                {
                    // Fallback for non-editor builds when not playing
                    ProcessSynchronously();
                }
            }
        }
        
        /// <summary>
        /// Process queue synchronously when coroutines aren't available
        /// </summary>
        private void ProcessSynchronously()
        {
            //Debug.Log($"Processing {m_InstantiationQueue.Count} objects synchronously");
            m_BatchCounter = 0;
            
            // Simple list for tracking placed objects and distances
            List<Vector3> placedPositions = new List<Vector3>();
            List<float> placedMinDistances = new List<float>();
            
            // Create a parent for the whole batch since we're doing this all at once
            string batchName = $"Batch_Sync";
            GameObject batchParent = new GameObject(batchName);
            batchParent.transform.SetParent(m_ObjectContainer.transform);
            batchParent.SetActive(false); // Start disabled to prevent popping
            
            // Process the entire queue synchronously
            while (m_InstantiationQueue.Count > 0)
            {
                var request = m_InstantiationQueue.Dequeue();
                
                // Check for collision with already placed objects
                bool validPosition = true;
                float effectiveMinDistance = request.minimumDistance > 0 ? 
                    request.minimumDistance : 
                    (m_CollisionConstraint != null ? m_CollisionConstraint.DefaultMinDistance : 2.0f);
                
                // Apply scale factor
                effectiveMinDistance *= request.scale.x;
                
                // Check against objects already placed
                for (int i = 0; i < placedPositions.Count; i++)
                {
                    float requiredDistance = Mathf.Max(effectiveMinDistance, placedMinDistances[i]);
                    if (Vector3.Distance(request.position, placedPositions[i]) < requiredDistance)
                    {
                        validPosition = false;
                        break;
                    }
                }
                
                // Skip invalid positions
                if (!validPosition)
                    continue;
                
                // Add to collision tracking before instantiation
                placedPositions.Add(request.position);
                placedMinDistances.Add(effectiveMinDistance);
                
                // Add to collision tracking if constraint exists
                if (m_CollisionConstraint != null)
                {
                    m_CollisionConstraint.AddObject(request.position, effectiveMinDistance);
                }
                
                GameObject newObject = InstantiateInternal(
                    request.prefab,
                    request.position,
                    request.rotation,
                    batchParent.transform // Parent to batch container
                );
                
                // Set scale
                newObject.transform.localScale = request.scale;
                
                // Keep track of spawned objects
                m_SpawnedObjects.Add(newObject);
            }
            
            // Enable the batch when all objects are ready
            batchParent.SetActive(true);
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
    
    #if UNITY_EDITOR && !UNITY_2019_1_OR_NEWER
    /// <summary>
    /// Editor coroutine wrapper for non-play mode
    /// </summary>
    public class EditorCoroutine
    {
        public readonly IEnumerator _routine;
        
        public EditorCoroutine(IEnumerator routine)
        {
            _routine = routine;
        }
    }
    
    /// <summary>
    /// Utility class for editor coroutines
    /// </summary>
    public static class EditorCoroutineUtility
    {
        private static Dictionary<EditorCoroutine, bool> _runningCoroutines = new Dictionary<EditorCoroutine, bool>();
        
        public static EditorCoroutine StartCoroutine(IEnumerator routine, object owner)
        {
            // In the editor, we'll use our custom EditorCoroutine implementation
            var coroutine = new EditorCoroutine(routine);
            _runningCoroutines[coroutine] = true;
            UnityEditor.EditorApplication.update += () => EditorUpdate(routine, coroutine);
            return coroutine;
        }
        
        public static void StopCoroutine(EditorCoroutine coroutine)
        {
            if (coroutine == null) return;
            
            // Mark this coroutine as stopped
            if (_runningCoroutines.ContainsKey(coroutine))
            {
                _runningCoroutines[coroutine] = false;
            }
        }
        
        private static void EditorUpdate(IEnumerator routine, EditorCoroutine coroutine)
        {
            // If coroutine is marked as stopped, remove the update callback
            if (_runningCoroutines.TryGetValue(coroutine, out bool isRunning) && !isRunning)
            {
                _runningCoroutines.Remove(coroutine);
                UnityEditor.EditorApplication.update -= () => EditorUpdate(routine, coroutine);
                return;
            }
            
            if (routine.MoveNext())
            {
                // Continue execution next frame
                return;
            }
            
            // Coroutine finished, remove update callback
            _runningCoroutines.Remove(coroutine);
            UnityEditor.EditorApplication.update -= () => EditorUpdate(routine, coroutine);
        }
    }
    #endif
} 