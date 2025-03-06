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
        private List<Terrain> m_ActiveTerrains = new List<Terrain>();
        private Queue<Terrain> m_TerrainsToUpdate = new Queue<Terrain>();

        private HashSet<TerrainLayer> m_TerrainLayersHashset = new HashSet<TerrainLayer>();

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
#endif

            CreateFullScreenQuad();
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
                        break;
                    }
                }
            }

            // If possible, update from modified builder only.
            if (modifiedBuilder == null)
            {
                return;
            }

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
                    break;
                }
            }

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
                    break;
                }
            }

            // If possible, update from modified builder only.
            if (modifiedBuilder == null)
            {
                return;
            }

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
                    m_IsDirty = true;
                }
            }

            if (m_IsDirty)
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

            if (worldBuilder.IsDirty)
            {
                worldBuilder.GenerateMask();
            }

            m_IsDirty = true;
        }

        internal void RemoveWorldBuilder(IWorldBuilder worldBuilder)
        {
            if (!m_WorldBuilders.Contains(worldBuilder))
                return;

            m_WorldBuilders.Remove(worldBuilder);
            m_IsDirty = true;
        }

        private WorldBuildingContext m_WorldBuildingContext;
        [SerializeField] private bool m_IsGenerating;

        private const int kMaskTextureWidth = 256;
        private const int kMaskTextureHeight = 256;

        public void Generate()
        {
            if (m_IsGenerating)
            {
                m_LODUpdateDelay = -1.0f;
                m_IsDirty = true;
                return;
            }

            // Sort builders by priority.
            m_WorldBuilders.Sort((worldBuilder1, worldBuilder2) =>
                worldBuilder1.Priority.CompareTo(worldBuilder2.Priority));

            if (Terrain.activeTerrain == null)
            {
                CreateDefaultTerrain();
            }

            m_IsDirty = false;
            m_IsGenerating = true;
            m_TerrainsToUpdate.Clear();
            CreateFullScreenQuad();

            Terrain.GetActiveTerrains(m_ActiveTerrains);
            foreach (var terrain in m_ActiveTerrains)
            {

                TerrainData terrainData = terrain.terrainData;
                if (terrain.drawInstanced == false)
                {
                    terrain.drawInstanced = true;
                }

                GenerateTerrainLayers(terrainData);
                m_WorldBuildingContext = WorldBuildingContext.Create(terrain);
                m_WorldBuildingContext.TerrainLayersIndexMap = m_TerrainLayersIndexMap;
                m_WorldBuildingContext.m_ApplyHeightmapMaterial = m_HeightmapMaterial;
                m_WorldBuildingContext.m_ApplySplatmapMaterial = m_SplatmapMaterial;
                m_WorldBuildingContext.m_Quad = m_Quad;

                GenerateTask();

                RenderTexture.active = m_WorldBuildingContext.HeightmapRenderTexture;
                RectInt heightmapRect = new RectInt(0, 0, m_WorldBuildingContext.HeightmapRenderTexture.width,
                    m_WorldBuildingContext.HeightmapRenderTexture.height);
                terrainData.CopyActiveRenderTextureToHeightmap(heightmapRect, heightmapRect.position,
                    TerrainHeightmapSyncControl.None);

                RectInt splatmapRect = new RectInt(0, 0, terrainData.alphamapWidth,
                    terrainData.alphamapHeight);
                int index = 0;
                foreach (var splatRenderTexture in m_WorldBuildingContext.SplatRenderTextures)
                {
                    RenderTexture.active = splatRenderTexture;
                    terrainData.CopyActiveRenderTextureToTexture(TerrainData.AlphamapTextureName, index, splatmapRect,
                        splatmapRect.position, false);
                    ++index;
                }

                m_WorldBuildingContext.Release();
                m_TerrainsToUpdate.Enqueue(terrain);
            }

            m_LODUpdateDelay = 1.0f;
            m_IsGenerating = false;
        }

        private void GenerateTask()
        {
            foreach (var builder in m_WorldBuilders)
            {
                builder.IsDirty = false;
                m_WorldBuildingContext.CurrentTransform = builder.TransformMatrix;
                builder.ApplyHeights(m_WorldBuildingContext);
            }

            foreach (var builder in m_WorldBuilders)
            {
                m_WorldBuildingContext.CurrentTransform = builder.TransformMatrix;
                builder.ApplySplatmap(m_WorldBuildingContext);
            }
            
            // Register vegetation prototypes first for current terrain
            TerrainData terrainData = m_WorldBuildingContext.TerrainData;
            
            // Clear existing tree instances 
            terrainData.treeInstances = new TreeInstance[0];
            
            // Register all vegetation prototypes
            foreach (var builder in m_WorldBuilders)
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
            
            // Now have all modifiers generate vegetation data into the context
            foreach (var builder in m_WorldBuilders)
            {
                m_WorldBuildingContext.CurrentTransform = builder.TransformMatrix;
                builder.GenerateVegetation(m_WorldBuildingContext);
            }
            
            // Apply all vegetation at once
            m_WorldBuildingContext.ApplyVegetationToTerrain();

            foreach (var builder in m_WorldBuilders)
            {
                m_WorldBuildingContext.CurrentTransform = builder.TransformMatrix;
                //builder.SpawnGameObjects(this);
            }
        }

        private void GenerateTerrainLayers(TerrainData terrainData)
        {
            foreach (var builder in m_WorldBuilders)
            {
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
    }
}