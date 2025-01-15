using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.Splines;

[ExecuteInEditMode]
public class WorldBuildingSystem : MonoBehaviour
{ 
    private List<IWorldBuilder> m_WorldBuilders = new List<IWorldBuilder>();
    private Dictionary<TerrainLayer, int> m_TerrainLayersIndexMap = new Dictionary<TerrainLayer, int>();

    [SerializeField] private bool m_IsDirty;
    public float m_LODUpdateDelay = -1.0f;

    [Serializable]
    internal struct TerrainHeightData
    {
        [SerializeField] internal int Width;
        [SerializeField] internal int Height;

        private float[,] m_HeightData;

        [SerializeField] [HideInInspector] private float[] m_FlattenedData;

        public float[,] HeightData
        {
            get
            {
                if (m_HeightData == null && m_FlattenedData != null)
                {
                    m_HeightData = new float[Width, Height];
                    int index = 0;
                    for (int i = 0; i < Width; i++)
                    {
                        for (int j = 0; j < Height; j++)
                        {
                            m_HeightData[i, j] = m_FlattenedData[index++];
                        }
                    }
                }
                return m_HeightData;
            }
        }
        
        public static TerrainHeightData Create(float[,] data)
        {
            TerrainHeightData  terrainHeightData = new TerrainHeightData();
            terrainHeightData.Width = data.GetLength(0);
            terrainHeightData.Height = data.GetLength(1);
            terrainHeightData.m_HeightData = data;
            terrainHeightData.m_FlattenedData = new float[terrainHeightData.Width * terrainHeightData.Height];
            int index = 0;
            for (int i = 0; i < terrainHeightData.Width; i++)
            {
                for (int j = 0; j < terrainHeightData.Height; j++)
                {
                    terrainHeightData.m_FlattenedData[index++] = data[i, j];
                }
            }
            return terrainHeightData;
        }
    }

    [Serializable]
    internal struct TerrainSplatData
    {
        [SerializeField] public int Width;
        [SerializeField] public int Height;
        [SerializeField] public int Layers;

        private float[,,] m_LayersData;

        [SerializeField] [HideInInspector] private float[] m_FlattenedData;

        public float[,,] LayersData
        {
            get
            {
                if (m_LayersData == null && m_FlattenedData != null)
                {
                    m_LayersData = new float[Width, Height, Layers];
                    int index = 0;
                    for (int i = 0; i < Width; i++)
                    {
                        for (int j = 0; j < Height; j++)
                        {
                            for (int k = 0; k < Layers; k++)
                            {
                                m_LayersData[i, j, k] = m_FlattenedData[index++];
                            }
                        }
                    }
                }
                return m_LayersData;
            }
        }

        public static TerrainSplatData CreateExplicitSize(float[,,] data, int width, int height, int layers)
        {
            TerrainSplatData  terrainSplatData = new TerrainSplatData();
            terrainSplatData.Width = width;
            terrainSplatData.Height = height;
            terrainSplatData.Layers = layers;
            terrainSplatData.m_FlattenedData = new float[terrainSplatData.Width * terrainSplatData.Height * terrainSplatData.Layers];
            int index = 0;
            
            int minWidth = Mathf.Min(width, data.GetLength(0));
            int minHeight = Mathf.Min(height, data.GetLength(1));
            int minLayer = Mathf.Min(layers, data.GetLength(2));
            for (int i = 0; i < minWidth; i++)
            {
                for (int j = 0; j < minHeight; j++)
                {
                    for (int k = 0; k < minLayer; k++)
                    {
                        terrainSplatData.m_FlattenedData[index++] = data[i, j, k];
                    }
                }
            }
            return terrainSplatData;
        }

        public static TerrainSplatData Create(float[,,] data)
        {
            TerrainSplatData  terrainSplatData = new TerrainSplatData();
            terrainSplatData.Width = data.GetLength(0);
            terrainSplatData.Height = data.GetLength(1);
            terrainSplatData.Layers = data.GetLength(2);
            terrainSplatData.m_LayersData = data;
            terrainSplatData.m_FlattenedData = new float[terrainSplatData.Width * terrainSplatData.Height * terrainSplatData.Layers];
            int index = 0;
            for (int i = 0; i < terrainSplatData.Width; i++)
            {
                for (int j = 0; j < terrainSplatData.Height; j++)
                {
                    for (int k = 0; k < terrainSplatData.Layers; k++)
                    {
                        terrainSplatData.m_FlattenedData[index++] = data[i, j, k];
                    }
                }
            }
            return terrainSplatData;
        }
    }

    [SerializeField] [HideInInspector]
    private TerrainHeightData m_OriginalTerrainHeights;
    [SerializeField] [HideInInspector]
    private TerrainSplatData m_OriginalTerrainSplatmap;

    [SerializeField] public Material m_HeightmapMaterial;
    [SerializeField] public Material m_SplatmapMaterial;
    [SerializeField] private Mesh m_Quad;
    
    private HashSet<TerrainLayer> m_TerrainLayersHashset = new HashSet<TerrainLayer>();

    public void RestoreTerrainData()
    {
        TerrainData terrainData = Terrain.activeTerrain.terrainData;
        terrainData.SetHeights(0, 0, m_OriginalTerrainHeights.HeightData);
        if (m_OriginalTerrainSplatmap.LayersData.GetLength(2) != terrainData.alphamapLayers)
        {
            m_OriginalTerrainSplatmap = TerrainSplatData.CreateExplicitSize(m_OriginalTerrainSplatmap.LayersData, terrainData.alphamapWidth, terrainData.alphamapHeight, terrainData.alphamapLayers);
        }
        terrainData.SetAlphamaps(0, 0, m_OriginalTerrainSplatmap.LayersData);
    }

    void CreateDefaultTerrain()
    {
        TerrainData newTerrainData = new TerrainData();
        newTerrainData.heightmapResolution = 2049;
        newTerrainData.baseMapResolution = 2048;
        newTerrainData.alphamapResolution = 2048;
        newTerrainData.size = new Vector3(200, 256, 200);
        GameObject newTerrain = Terrain.CreateTerrainGameObject(newTerrainData);
        newTerrain.transform.position = Vector3.zero;
    }

    public void BackupTerrainData()
    {
        if (Terrain.activeTerrain == null)
        {
            CreateDefaultTerrain();
        }
        TerrainData terrainData = Terrain.activeTerrain.terrainData;
        m_OriginalTerrainHeights = TerrainHeightData.Create(terrainData.GetHeights(0, 0, terrainData.heightmapResolution, terrainData.heightmapResolution));
        m_OriginalTerrainSplatmap = TerrainSplatData.Create(terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight));
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
            worldBuildingSystem.BackupTerrainData();
        }
        return worldBuildingSystem;
    }
    
    private void OnEnable()
    {
        m_IsGenerating = false;
#if UNITY_EDITOR
        UnityEditor.Splines.EditorSplineUtility.AfterSplineWasModified += AfterSplineWasModified;
        //UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
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

        if (m_MaskRenderTexture == null)
        {
            m_MaskRenderTexture = new RenderTexture(kMaskTextureWidth, kMaskTextureHeight, 0, RenderTextureFormat.ARGB32);
            m_MaskRenderTexture.enableRandomWrite = true;
        }
    }

    private void CreateFullScreenQuad()
    {
        if (m_Quad != null)
        {
            //return;
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
        
        m_Quad = new Mesh();
        m_Quad.vertices = vertices;
        m_Quad.uv = uv;
        m_Quad.triangles = tris;
        m_Quad.UploadMeshData(true);
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.Splines.EditorSplineUtility.AfterSplineWasModified -= AfterSplineWasModified;
        //UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        UnityEditor.AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
#endif
    }
    
    /*public void OnBeforeAssemblyReload()
    {
    }*/

    public void OnAfterAssemblyReload()
    {
        m_IsDirty = false;
    }

    
    private void AfterSplineWasModified(Spline modifiedSpline)
    {
        IWorldBuilder modifiedBuilder = null;
        foreach (var builder in m_WorldBuilders)
        {
            var splines = builder.Splines;
            if (splines == null) continue;
            foreach (var spline in splines)
            {
                if (spline == modifiedSpline)
                {
                    //builder.ProcessSpline(spline);
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
        else if (m_LODUpdateDelay > 0.0f)
        {
            m_LODUpdateDelay -= Time.unscaledDeltaTime;
            if (m_LODUpdateDelay < 0.0f)
            {
                TerrainData terrainData = Terrain.activeTerrain.terrainData;
                terrainData.SyncHeightmap();
                terrainData.SyncTexture(TerrainData.AlphamapTextureName);
            }
        }
    }

    internal void AddWorldBuilder(IWorldBuilder worldBuilder)
    {
        if (m_WorldBuilders.Contains(worldBuilder))
            return;
        
        m_WorldBuilders.Add(worldBuilder);
        worldBuilder.ProcessSpline(null);
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
    [SerializeField]
    private bool m_IsGenerating;

    [SerializeField]
    private RenderTexture m_MaskRenderTexture;
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

        if (Terrain.activeTerrain == null)
        {
            CreateDefaultTerrain();
        }
        m_IsDirty = false;
        m_IsGenerating = true;
        CreateFullScreenQuad();

        Terrain terrain = Terrain.activeTerrain;
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

        
        
        
        //await Task.Run(GenerateTask);
        GenerateTask();

        RenderTexture.active = m_WorldBuildingContext.HeightmapRenderTexture;
        RectInt heightmapRect = new RectInt(0, 0, m_WorldBuildingContext.HeightmapRenderTexture.width,
            m_WorldBuildingContext.HeightmapRenderTexture.height);
        terrainData.CopyActiveRenderTextureToHeightmap(heightmapRect, heightmapRect.position, TerrainHeightmapSyncControl.None);

        RectInt splatmapRect = new RectInt(0, 0, terrainData.alphamapWidth,
            terrainData.alphamapHeight);
        int index = 0;
        foreach (var splatRenderTexture in m_WorldBuildingContext.SplatRenderTextures)
        {
            RenderTexture.active = splatRenderTexture;
            terrainData.CopyActiveRenderTextureToTexture(TerrainData.AlphamapTextureName, 0, splatmapRect, splatmapRect.position, false);
            ++index;
        }
        m_WorldBuildingContext.Release();
        m_LODUpdateDelay = 1.0f;
        m_IsGenerating = false;
    }

    private void GenerateTask()
    {
        // Sort builders by priority.
        m_WorldBuilders.Sort((worldBuilder1, worldBuilder2) => worldBuilder1.Priority.CompareTo(worldBuilder2.Priority));
        
        foreach (var builder in m_WorldBuilders)
        {
            builder.IsDirty = false;
            builder.GenerateMask(m_MaskRenderTexture);
        }
        
        
        foreach (var builder in m_WorldBuilders)
        {
            m_WorldBuildingContext.CurrentTransform = builder.TransformMatrix;
            builder.ApplyHeights(m_WorldBuildingContext);
        }
        
        foreach (var builder in m_WorldBuilders)
        {
            m_WorldBuildingContext.CurrentTransform = builder.TransformMatrix;
            builder.ApplySplatmap(m_WorldBuildingContext);
        }
        
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

        var terrainLayers =  Terrain.activeTerrain.terrainData.terrainLayers;
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
