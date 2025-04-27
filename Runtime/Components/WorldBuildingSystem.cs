using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace GameCraftersGuild.WorldBuilding
{
    [ExecuteInEditMode]
    public class WorldBuildingSystem : MonoBehaviour
    {
        private List<IWorldBuilder> m_WorldBuilders = new List<IWorldBuilder>();
        private Dictionary<TerrainLayer, int> m_TerrainLayersIndexMap = new Dictionary<TerrainLayer, int>();

        [HideInInspector] [SerializeField] private bool m_IsDirty;
        [HideInInspector] [SerializeField] private bool m_IsReloadingDomain;
        public float m_LODUpdateDelay = -1.0f;

        [SerializeField] public Material m_HeightmapMaterial;
        [SerializeField] public Material m_SplatmapMaterial;
        [SerializeField] private Mesh m_Quad;
        [SerializeField] private ComputeShader m_NormalMapGenerationShader;
        private List<Terrain> m_ActiveTerrains = new List<Terrain>();
        private Queue<Terrain> m_TerrainsToUpdate = new Queue<Terrain>();

        private HashSet<TerrainLayer> m_TerrainLayersHashset = new HashSet<TerrainLayer>();
        // Pre-allocated list for relevant world builders to avoid GC allocations.
        private List<IWorldBuilder> m_RelevantWorldBuilders = new List<IWorldBuilder>();
        // Collection to store dirty world builders.
        private HashSet<IWorldBuilder> m_DirtyBuilders = new HashSet<IWorldBuilder>();
        // Dictionary to track previous bounds of builders
        private Dictionary<IWorldBuilder, Bounds> m_PreviousBounds = new Dictionary<IWorldBuilder, Bounds>();
        // Collection to store builders that modified heights or splatmaps in the current terrain.
        private HashSet<IWorldBuilder> m_ModifiedRegionBuilders = new HashSet<IWorldBuilder>();
        void CreateDefaultTerrain()
        {
            TerrainData newTerrainData = new TerrainData();
            newTerrainData.heightmapResolution = 2049;
            newTerrainData.baseMapResolution = 2048;
            newTerrainData.alphamapResolution = 2048;
            newTerrainData.size = new Vector3(2000, 256, 2000);
            GameObject newTerrain = Terrain.CreateTerrainGameObject(newTerrainData);
            newTerrain.transform.position = Vector3.zero;
        }

        public static WorldBuildingSystem FindSystemInScene()
        {
            var systems = GameObject.FindObjectsByType<WorldBuildingSystem>(FindObjectsSortMode.None);
            WorldBuildingSystem worldBuildingSystem = null;
            Scene activeScene = SceneManager.GetActiveScene();
            foreach (var system in systems)
            {
                if (system.gameObject.scene == activeScene)
                {
                    worldBuildingSystem = system;
                    break;
                }
            }

            return worldBuildingSystem;
        }

        public static WorldBuildingSystem GetOrCreate()
        {
            WorldBuildingSystem worldBuildingSystem = FindSystemInScene();
            if (worldBuildingSystem == null)
            {
                worldBuildingSystem = new GameObject("WorldBuilding System").AddComponent<WorldBuildingSystem>();
            }

            return worldBuildingSystem;
        }

        private void Start()
        {
#if UNITY_EDITOR
            foreach (var builder in m_WorldBuilders)
            {
                builder.GenerateMask();
            }
#endif
        }

        private void OnEnable()
        {
            m_IsGenerating = false;
#if UNITY_EDITOR
            UnityEditor.Splines.EditorSplineUtility.AfterSplineWasModified += AfterSplineWasModified;
            UnityEditor.Splines.EditorSplineUtility.RegisterSplineDataChanged<float>(OnAfterSplineDataWasModified);

            SplineContainer.SplineAdded += OnSplineContainerAdded;
            SplineContainer.SplineRemoved += OnSplineContainerRemoved;
            SplineContainer.SplineReordered += OnSplineContainerReordered;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            UnityEditor.AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;


            if (m_HeightmapMaterial == null)
            {
                m_HeightmapMaterial = new Material(Shader.Find("Hidden/GameCraftersGuild/TerrainGen/WriteHeightmap"));
            }

            if (m_SplatmapMaterial == null)
            {
                m_SplatmapMaterial = new Material(Shader.Find("Hidden/GameCraftersGuild/TerrainGen/WriteSplatmap"));
            }

            FindShaders();
#endif

            CreateFullScreenQuad();
        }

        private void FindShaders()
        {
            if (m_NormalMapGenerationShader == null)
            {
                m_NormalMapGenerationShader = Resources.Load<ComputeShader>("NormalMapGenerator");
                if (m_NormalMapGenerationShader == null)
                {
                    Debug.LogWarning("Could not find NormalMapGenerator compute shader in Resources folder!");
                }
            }
        }

        private void CreateFullScreenQuad()
        {
            if (m_Quad != null)
            {
                return;
            }

            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-0.5f, -0.0f, -0.5f),
                new Vector3(0.5f, -0.0f, -0.5f),
                new Vector3(-0.5f, 0.0f, 0.5f),
                new Vector3(0.5f, 0.0f, 0.5f)
            };
            Vector2[] uv = new Vector2[4]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            int[] tris = new int[6]
            {
                // lower left triangle
                0, 2, 1,
                // upper right triangle
                2, 3, 1
            };

            m_Quad = new Mesh
            {
                vertices = vertices,
                uv = uv,
                triangles = tris
            };
            m_Quad.UploadMeshData(true);
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.Splines.EditorSplineUtility.AfterSplineWasModified -= AfterSplineWasModified;
            UnityEditor.Splines.EditorSplineUtility.UnregisterSplineDataChanged<float>(OnAfterSplineDataWasModified);
            SplineContainer.SplineAdded -= OnSplineContainerAdded;
            SplineContainer.SplineRemoved -= OnSplineContainerRemoved;
            SplineContainer.SplineReordered -= OnSplineContainerReordered;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            UnityEditor.AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
#endif
        }

        private void OnBeforeAssemblyReload()
        {
            m_IsReloadingDomain = true;
        }

        private void OnAfterAssemblyReload()
        {
            m_IsReloadingDomain = false;
        }

        private void AfterSplineWasModified(Spline modifiedSpline)
        {
            IWorldBuilder modifiedBuilder = null;
            foreach (var builder in m_WorldBuilders)
            {
                var splineContainer = builder.SplineContainer;
                if (splineContainer == null) continue;
                foreach (var spline in splineContainer.Splines)
                {
                    if (spline == modifiedSpline)
                    {
                        builder.GenerateMask();
                        builder.IsDirty = true;
                        modifiedBuilder = builder;
                        
                        // Add to dirty builders collection (HashSet automatically prevents duplicates)
                        m_DirtyBuilders.Add(builder);
                        break;
                    }
                }
            }

            // If possible, update from modified builder only.
            if (modifiedBuilder == null)
            {
                return;
            }

            m_IsDirty = true;
            Generate();
        }

        void OnAfterSplineDataWasModified(SplineData<float> splineData)
        {
            IWorldBuilder modifiedBuilder = null;
            foreach (var builder in m_WorldBuilders)
            {
                if (builder.ContainsSplineData(splineData))
                {
                    modifiedBuilder = builder;
                    builder.GenerateMask();
                    builder.IsDirty = true;
                    
                    // Add to dirty builders collection (HashSet automatically prevents duplicates)
                    m_DirtyBuilders.Add(builder);
                    break;
                }
            }

            m_IsDirty = true;
            Generate();
        }

        private void OnSplineContainerModified(SplineContainer modifiedSplineContainer)
        {
            IWorldBuilder modifiedBuilder = null;
            foreach (var builder in m_WorldBuilders)
            {
                if (builder.SplineContainer == modifiedSplineContainer)
                {
                    builder.GenerateMask();
                    builder.IsDirty = true;
                    modifiedBuilder = builder;
                    
                    // Add to dirty builders collection (HashSet automatically prevents duplicates)
                    m_DirtyBuilders.Add(builder);
                    break;
                }
            }

            // If possible, update from modified builder only.
            if (modifiedBuilder == null)
            {
                return;
            }

            m_IsDirty = true;
            Generate();
        }

        private void OnSplineContainerAdded(SplineContainer splineContainer, int splineIndex)
        {
            OnSplineContainerModified(splineContainer);
        }

        private void OnSplineContainerRemoved(SplineContainer splineContainer, int splineIndex)
        {
            OnSplineContainerModified(splineContainer);
        }

        void OnSplineContainerReordered(SplineContainer container, int previousIndex, int newIndex)
        {

        }

        private void Update()
        {
            // Check for dirty builders and collect them
            for (int i = m_WorldBuilders.Count - 1; i >= 0; i--)
            {
                var builder = m_WorldBuilders[i];
                if (builder == null)
                {
                    m_WorldBuilders.RemoveAt(i);
                    continue;
                }
                else if (builder.IsDirty)
                {
                    // This builder is dirty, add it to our collection
                    m_DirtyBuilders.Add(builder);
                    m_IsDirty = true;
                }
            }

            if (m_IsDirty && m_DirtyBuilders.Count > 0)
            {
                Generate();
            }
            else if (m_LODUpdateDelay > 0.0f && m_TerrainsToUpdate.Count > 0)
            {
                m_LODUpdateDelay -= Time.unscaledDeltaTime;
                if (m_LODUpdateDelay < 0.0f)
                {
                    Terrain terrain = m_TerrainsToUpdate.Dequeue();
                    TerrainData terrainData = terrain.terrainData;
                    terrainData.SyncHeightmap();
                    terrainData.SyncTexture(TerrainData.AlphamapTextureName);
                    m_LODUpdateDelay = 1.0f;
                }
            }
        }

        internal void AddWorldBuilder(IWorldBuilder worldBuilder)
        {
            if (m_WorldBuilders.Contains(worldBuilder))
                return;

            m_WorldBuilders.Add(worldBuilder);
            if (m_IsReloadingDomain)
            {
                return;
            }

            // Add to dirty builders collection
            m_DirtyBuilders.Add(worldBuilder);

            m_IsDirty = true;
        }

        internal void RemoveWorldBuilder(IWorldBuilder worldBuilder)
        {
            if (!m_WorldBuilders.Contains(worldBuilder))
                return;
            
            // First add to dirty builders collection before removing from main list
            // This ensures the Generate method will process terrains affected by this builder
            // using its previous bounds stored in m_PreviousBounds
            m_DirtyBuilders.Add(worldBuilder);
            
            // Now remove from the main world builders list
            m_WorldBuilders.Remove(worldBuilder);
            
            // Mark system as dirty to ensure Generate gets called on next Update
            m_IsDirty = true;
            
            #if UNITY_EDITOR
            //Debug.Log($"World builder {worldBuilder.GetType().Name} removed and marked for terrain update");
            #endif
        }

        private WorldBuildingContext m_WorldBuildingContext;
        [SerializeField] private bool m_IsGenerating;

        public void Generate()
        {
            if (m_IsGenerating)
            {
                m_LODUpdateDelay = -1.0f;
                m_IsDirty = true;
                return;
            }

            FindShaders();

            // If there are no dirty builders, nothing to do
            if (m_DirtyBuilders.Count == 0)
            {
                m_IsDirty = false;
                return;
            }

            // Cleanup disabled modifiers before regenerating
            CleanupDisabledModifiers();

            // Sort builders by their position in the scene hierarchy instead of priority
            m_WorldBuilders.Sort((worldBuilder1, worldBuilder2) => {
                // Cast to MonoBehaviour to access transform (all IWorldBuilder implementations are MonoBehaviours in this case)
                var mb1 = worldBuilder1 as MonoBehaviour;
                var mb2 = worldBuilder2 as MonoBehaviour;
                
                if (mb1 == null || mb2 == null)
                    return worldBuilder1.Priority.CompareTo(worldBuilder2.Priority); // Fallback to priority
                
                // If they're siblings, sort by sibling index
                if (mb1.transform.parent == mb2.transform.parent)
                    return mb1.transform.GetSiblingIndex().CompareTo(mb2.transform.GetSiblingIndex());
                
                // If they're not siblings, compare hierarchy depth (lower depth = higher in hierarchy)
                int depth1 = GetHierarchyDepth(mb1.transform);
                int depth2 = GetHierarchyDepth(mb2.transform);
                if (depth1 != depth2)
                    return depth1.CompareTo(depth2);
                
                // If all else is equal, fall back to priority
                return worldBuilder1.Priority.CompareTo(worldBuilder2.Priority);
            });

            if (Terrain.activeTerrain == null)
            {
                CreateDefaultTerrain();
            }

            m_IsDirty = false;
            m_IsGenerating = true;
            m_TerrainsToUpdate.Clear();
            CreateFullScreenQuad();

            Terrain.GetActiveTerrains(m_ActiveTerrains);
            
            #if UNITY_EDITOR
            int totalTerrains = m_ActiveTerrains.Count;
            int updatedTerrains = 0;
            #endif
            
            // Process each terrain
            foreach (var terrain in m_ActiveTerrains)
            {
                TerrainData terrainData = terrain.terrainData;
                
                // Skip terrains with no terrain data
                if (terrainData == null)
                {
                    Debug.LogWarning($"Terrain {terrain.name} has no terrain data, skipping");
                    continue;
                }
                
                // Calculate terrain bounds
                Vector3 terrainPosition = terrain.transform.position;
                Vector3 terrainSize = terrainData.size;
                Bounds terrainBounds = new Bounds(
                    terrainPosition + new Vector3(terrainSize.x * 0.5f, terrainSize.y * 0.5f, terrainSize.z * 0.5f), 
                    terrainSize
                );
                
                // Check if this terrain intersects with any dirty builder's current or previous bounds
                bool shouldUpdate = false;
                foreach (var dirtyBuilder in m_DirtyBuilders)
                {
                    // If the builder is still in the world builders list, check its current bounds
                    if (m_WorldBuilders.Contains(dirtyBuilder))
                    {
                        Bounds currentBounds = dirtyBuilder.WorldBounds;
                        
                        // Check if terrain intersects with current bounds
                        if (terrainBounds.Intersects(currentBounds))
                        {
                            shouldUpdate = true;
                            break;
                        }
                    }
                    
                    // Check if terrain intersects with previous bounds (if available)
                    // This handles both moved and removed builders
                    if (m_PreviousBounds.TryGetValue(dirtyBuilder, out Bounds previousBounds))
                    {
                        if (terrainBounds.Intersects(previousBounds))
                        {
                            shouldUpdate = true;
                            break;
                        }
                    }
                }
                
                // Skip this terrain if it doesn't need updating
                if (!shouldUpdate)
                {
                    continue;
                }
                
                if (terrain.drawInstanced == false)
                {
                    terrain.drawInstanced = true;
                }

                // Process terrain layers only from relevant world builders
                GenerateTerrainLayers(terrainData, terrainBounds);
                
                m_WorldBuildingContext = WorldBuildingContext.Create(terrain);
                m_WorldBuildingContext.TerrainLayersIndexMap = m_TerrainLayersIndexMap;
                m_WorldBuildingContext.m_ApplyHeightmapMaterial = m_HeightmapMaterial;
                m_WorldBuildingContext.m_ApplySplatmapMaterial = m_SplatmapMaterial;
                m_WorldBuildingContext.m_Quad = m_Quad;

                // Only process world builders that intersect with this terrain
                GenerateTask(terrainBounds);

                RenderTexture.active = m_WorldBuildingContext.HeightmapRenderTexture;
                RectInt heightmapRect = new RectInt(0, 0, m_WorldBuildingContext.HeightmapRenderTexture.width,
                    m_WorldBuildingContext.HeightmapRenderTexture.height);
                terrainData.CopyActiveRenderTextureToHeightmap(heightmapRect, heightmapRect.position,
                    TerrainHeightmapSyncControl.None);

                RectInt splatmapRect = new RectInt(0, 0, terrainData.alphamapWidth,
                    terrainData.alphamapHeight);
                int index = 0;
                int alphamapTextureCount = terrainData.alphamapTextureCount;
                foreach (var splatRenderTexture in m_WorldBuildingContext.SplatRenderTextures)
                {
                    // Make sure we don't exceed the number of alphamap textures in the terrain
                    if (index >= alphamapTextureCount)
                        break;
                        
                    RenderTexture.active = splatRenderTexture;
                    terrainData.CopyActiveRenderTextureToTexture(TerrainData.AlphamapTextureName, index, splatmapRect,
                        splatmapRect.position, false);
                    ++index;
                }

                m_WorldBuildingContext.Release();
                m_TerrainsToUpdate.Enqueue(terrain);

                #if UNITY_EDITOR
                updatedTerrains++;
                #endif
            }

            // First, keep track of previous bounds for any removed builders that were dirty
            // (so we can update terrains they affected)
            Dictionary<IWorldBuilder, Bounds> tempBounds = new Dictionary<IWorldBuilder, Bounds>();
            foreach (var dirtyBuilder in m_DirtyBuilders)
            {
                if (!m_WorldBuilders.Contains(dirtyBuilder) && m_PreviousBounds.TryGetValue(dirtyBuilder, out Bounds bounds))
                {
                    tempBounds[dirtyBuilder] = bounds;
                }
            }
            
            // Now clear and rebuild the previous bounds dictionary
            m_PreviousBounds.Clear();
            
            // Add entries for all current builders
            foreach (var builder in m_WorldBuilders)
            {
                m_PreviousBounds[builder] = builder.WorldBounds;
                
                // Reset the dirty flag for all builders after all terrains have been processed
                if (m_DirtyBuilders.Contains(builder))
                {
                    builder.IsDirty = false;
                }
            }
            
            // Re-add the removed dirty builders with their previous bounds
            foreach (var kvp in tempBounds)
            {
                m_PreviousBounds[kvp.Key] = kvp.Value;
            }
            
            // Now that all terrains are processed and all cleanup is done,
            // we can safely clear the dirty builders collection
            m_DirtyBuilders.Clear();

            m_LODUpdateDelay = 1.0f;
            m_IsGenerating = false;

            #if UNITY_EDITOR
            //Debug.Log($"Updated {updatedTerrains} out of {totalTerrains} terrains");
            #endif
        }

        private void GenerateTask(Bounds terrainBounds)
        {
            // Clear and reuse the pre-allocated list to avoid GC allocations
            m_RelevantWorldBuilders.Clear();
            // Clear the set of builders that modified regions on this terrain
            m_ModifiedRegionBuilders.Clear();
            
            // Filter world builders that intersect with the terrain bounds
            // Only consider builders that are still in the world builders list
            foreach (var builder in m_WorldBuilders)
            {
                if (builder.WorldBounds.Intersects(terrainBounds))
                {
                    m_RelevantWorldBuilders.Add(builder);
                }
            }
            
            // Track which builders applied height changes
            foreach (var builder in m_RelevantWorldBuilders)
            {
                m_WorldBuildingContext.CurrentTransform = builder.TransformMatrix;
                m_WorldBuildingContext.CurrentTransformComponent = builder.Transform;
                bool heightChangesApplied = builder.ApplyHeights(m_WorldBuildingContext);
                
                // Track builders that modified the terrain
                if (heightChangesApplied)
                {
                    m_ModifiedRegionBuilders.Add(builder);
                }
            }

            // Generate normal map from the heightmap once all height changes are applied
            m_WorldBuildingContext.NormalGenerationShader = m_NormalMapGenerationShader;
            m_WorldBuildingContext.GenerateNormalMap();

            // Track which builders applied splatmap changes
            foreach (var builder in m_RelevantWorldBuilders)
            {
                m_WorldBuildingContext.CurrentTransform = builder.TransformMatrix;
                m_WorldBuildingContext.CurrentTransformComponent = builder.Transform;
                bool splatChangesApplied = builder.ApplySplatmap(m_WorldBuildingContext);
                
                // Track builders that modified the terrain
                if (splatChangesApplied)
                {
                    m_ModifiedRegionBuilders.Add(builder);
                }
            }
            
            // Register vegetation prototypes first for current terrain
            TerrainData terrainData = m_WorldBuildingContext.TerrainData;
            
            // Clear existing tree instances 
            terrainData.treeInstances = new TreeInstance[0];
            
            // Register all vegetation prototypes from relevant builders
            foreach (var builder in m_RelevantWorldBuilders)
            {
                if (builder is ITerrainVegetationProvider vegetationProvider)
                {
                    if (vegetationProvider.TerrainVegetationModifiers == null) continue;
                    foreach (var vegetationModifier in vegetationProvider.TerrainVegetationModifiers)
                    {
                        if (vegetationModifier.Enabled)
                        {
                            vegetationModifier.RegisterPrototypes(m_WorldBuildingContext, terrainData);
                        }
                    }
                }
            }
            
            // Now have relevant modifiers generate vegetation data into the context
            foreach (var builder in m_RelevantWorldBuilders)
            {
                m_WorldBuildingContext.CurrentTransform = builder.TransformMatrix;
                m_WorldBuildingContext.CurrentTransformComponent = builder.Transform;
                builder.GenerateVegetation(m_WorldBuildingContext);
            }
            
            // Apply all vegetation at once
            m_WorldBuildingContext.ApplyVegetationToTerrain();

            // Only spawn game objects from builders that:
            // 1. Are in areas where terrain was modified (bounds intersect with any modified builder), OR
            // 2. Are explicitly dirty (in m_DirtyBuilders)
            foreach (var builder in m_RelevantWorldBuilders)
            {
                bool shouldSpawn = m_DirtyBuilders.Contains(builder);
                
                // If not already determined to spawn, check if it intersects with any modified regions
                if (!shouldSpawn && m_ModifiedRegionBuilders.Count > 0)
                {
                    Bounds builderBounds = builder.WorldBounds;
                    
                    // Check if this builder's bounds intersect with any builder that modified the terrain
                    foreach (var modifiedBuilder in m_ModifiedRegionBuilders)
                    {
                        if (builderBounds.Intersects(modifiedBuilder.WorldBounds))
                        {
                            shouldSpawn = true;
                            break;
                        }
                    }
                }
                
                if (shouldSpawn)
                {
                    m_WorldBuildingContext.CurrentTransform = builder.TransformMatrix;
                    m_WorldBuildingContext.CurrentTransformComponent = builder.Transform;
                    // Directly use builder.Shape as guaranteed by the interface
                    builder.SpawnGameObjects(m_WorldBuildingContext, builder.Shape); 
                }
            }
            
            // After processing all world builders for this terrain, reset their dirty flags
            foreach (var builder in m_RelevantWorldBuilders)
            {
                if (m_DirtyBuilders.Contains(builder))
                {
                    // Don't clear the dirty flag yet, wait until all terrains are processed
                    builder.IsDirty = false;
                }
            }
        }

        private void GenerateTerrainLayers(TerrainData terrainData, Bounds terrainBounds)
        {
            m_TerrainLayersHashset.Clear();
            
            // Only process terrain layers from builders that intersect with this terrain
            foreach (var builder in m_WorldBuilders)
            {
                // Skip builders that don't intersect with the terrain bounds
                if (!builder.WorldBounds.Intersects(terrainBounds))
                    continue;
                    
                foreach (var splatModifier in builder.TerrainSplatModifiers)
                {
                    int numTerrainLayers = splatModifier.GetNumTerrainLayers();
                    for (int i = 0; i < numTerrainLayers; i++)
                    {
                        TerrainLayer layer = splatModifier.GetTerrainLayer(i);
                        m_TerrainLayersHashset.Add(layer);
                    }
                }
            }

            var terrainLayers = terrainData.terrainLayers;
            if (terrainLayers.Length != m_TerrainLayersHashset.Count)
            {
                terrainLayers = new TerrainLayer[m_TerrainLayersHashset.Count];
                int index = 0;
                foreach (var terrainLayer in m_TerrainLayersHashset)
                {
                    terrainLayers[index++] = terrainLayer;
                }
            }
            else
            {
                bool changed = false;
                for (int i = 0; i < terrainLayers.Length; ++i)
                {
                    if (m_TerrainLayersHashset.Contains(terrainLayers[i]))
                    {
                        m_TerrainLayersHashset.Remove(terrainLayers[i]);
                        continue;
                    }

                    terrainLayers[i] = null;
                    changed = true;
                }

                if (changed)
                {
                    for (int i = 0; i < terrainLayers.Length; ++i)
                    {
                        if (terrainLayers[i] == null)
                        {
                            var enumerator = m_TerrainLayersHashset.GetEnumerator();
                            enumerator.MoveNext();
                            terrainLayers[i] = enumerator.Current;
                            m_TerrainLayersHashset.Remove(enumerator.Current);
                        }
                    }
                }
            }

            m_TerrainLayersHashset.Clear();
            terrainData.terrainLayers = terrainLayers;

            m_TerrainLayersIndexMap.Clear();
            for (int i = 0; i < terrainLayers.Length; ++i)
            {
                m_TerrainLayersIndexMap.Add(terrainLayers[i], i);
            }
        }

        public struct CopyFloatArrayJob : IJobParallelFor
        {
            public NativeArray<float> SourceData;
            public NativeArray<float> DestinationData;

            public void Execute(int index)
            {
                DestinationData[index] = SourceData[index];
            }
        }

        /// <summary>
        /// Clean up all disabled modifiers to ensure proper removal of objects
        /// </summary>
        private void CleanupDisabledModifiers()
        {
            foreach (var builder in m_WorldBuilders)
            {
                if (builder is Stamp stamp)
                {
                    // Clean up any disabled modifiers
                    stamp.m_Modifiers.CleanupDisabledModifiers();
                }
            }
        }

        // Helper method to get the depth of a transform in the hierarchy
        private int GetHierarchyDepth(Transform transform)
        {
            int depth = 0;
            Transform current = transform;
            while (current.parent != null)
            {
                depth++;
                current = current.parent;
            }
            return depth;
        }
    }
}
