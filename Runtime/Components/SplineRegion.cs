using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(SplineContainer))]
[ExecuteInEditMode]
public class SplineRegion : BaseWorldBuilder
{
    [SerializeReference][HideInInspector]
    SplineContainer m_SplineContainer;
    public override SplineContainer SplinContainer => m_SplineContainer;

    // Mask Texture.
    [SerializeField] private Texture m_MaskTexture;
    protected override Texture MaskTexture => m_MaskTexture;
    private const int kMaskTextureWidth = 256;
    private const int kMaskTextureHeight = 256;
    
    // Shader Parameters.
    [SerializeField] private ComputeShader m_CreateSplineAreaTextureComputeShader;
    private static readonly int kComputeResultId = Shader.PropertyToID("Result");
    private static readonly int kComputeRegionMinId = Shader.PropertyToID("RegionMin");
    private static readonly int kComputeRegionSizeId = Shader.PropertyToID("RegionSize");
    private static readonly int kComputeNumPositionsId = Shader.PropertyToID("NumPositions");
    private static readonly int kComputeSplinePositions = Shader.PropertyToID("SplinePositions");

    private void CalculateWorldBounds()
    {
        if (m_SplineContainer.Splines.Count == 0)
        {
            LocalBounds = new Bounds();
            return;
        }
        Bounds splineBounds = m_SplineContainer.Splines[0].GetBounds();
        for (int i = 1; i < m_SplineContainer.Splines.Count; i++)
        {
            splineBounds.Encapsulate(m_SplineContainer.Splines[i].GetBounds());
        }
        LocalBounds = splineBounds;
    }
    
    /*public float Intervals = 1.5f;

    [Range(0.0f, 1.0f)]
    public float Density = 0.5f;
    public float MinScale = 1.2f;
    public float MaxScale = 0.8f;
    public Vector3 MinRotation = Vector3.zero;
    public Vector3 MaxRotation = new Vector3(0, 360, 0);
    [Range(1, 10000000)]
    public uint Seed = 1;
    
    public List<GameObject> m_Prefabs = new List<GameObject>();
    private float2 m_NoiseOffset;
    private NativeList<ColliderPositionRadius> m_CollidersInArea;
    Collider[] m_HitColliders = new Collider[100];
    private Random m_Random;*/
    
    //SplineCache m_SplineCache;

    private void OnValidate()
    {
        if (m_SplineContainer == null)
        {
            m_SplineContainer = GetComponent<SplineContainer>();
        }

        if (!m_SplineContainer.Spline.Closed)
        {
            m_SplineContainer.Spline.Closed = true;
        }

        FindComputeShader();
    }

    private void FindComputeShader()
    {
#if UNITY_EDITOR
        if (m_CreateSplineAreaTextureComputeShader == null)
        {
            m_CreateSplineAreaTextureComputeShader = Resources.Load<ComputeShader>("Shaders/SplineAreaComputeShader");
        }
#endif
    }

    protected override void OnEnable()
    {
        m_SplineContainer = GetComponent<SplineContainer>();
        base.OnEnable();

        FindComputeShader();
    }

    public override void SpawnGameObjects(WorldBuildingContext context)
    {
        //BakeRegionMap();
    }

    /*private void DestroyObjects()
    {
        for(int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }

    private void SpawnObjects()
    {
        if (m_SplineCache == null)
            return;

        GatherCollidersInArea();
        
        m_Random = new Unity.Mathematics.Random(Seed == 0 ? 1 : Seed);
        m_NoiseOffset = new float2(m_Random.NextFloat(-100.0f, 100.0f), m_Random.NextFloat(-100.0f, 100.0f));

        foreach (var splinePositionData in m_SplineCache)
        {
            Bounds splineBounds = splinePositionData.SplineBounds;
            foreach (var point in splinePositionData.RegionPoints)
            {
                Vector3 worldPositionPoint = transform.TransformPoint(point);
                Vector3 randomScaleUniform = Vector3.one * m_Random.NextFloat(MinScale, MaxScale);
                Vector3 randomRotation = m_Random.NextFloat3(MinRotation, MaxRotation);
                Quaternion randomRotationQuat = Quaternion.Euler(randomRotation);
                Vector3 randomOffset =
                    m_Random.NextFloat3(new float3(-Intervals * 0.5f, 0.0f, -Intervals * 0.5f),
                        new float3(Intervals * 0.5f, 0.0f, Intervals * 0.5f));
                worldPositionPoint += randomOffset;
                worldPositionPoint.y = Terrain.activeTerrain.SampleHeight(worldPositionPoint) +
                                       Terrain.activeTerrain.transform.position.y;
                GameObject prefab = m_Prefabs[m_Random.NextInt(m_Prefabs.Count)];
                if (IsLegalSpawnLocation(prefab, worldPositionPoint, randomRotationQuat, randomScaleUniform, point, splineBounds))
                {
                    SpawnObjectAt(prefab, worldPositionPoint, randomRotationQuat, randomScaleUniform);
                }
            }
        }

        m_CollidersInArea.Dispose();
    }

    /*private void SpawnObjectAt(GameObject prefab, Vector3 worldPositionPoint, Quaternion randomRotationQuat, Vector3 randomScaleUniform)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, transform) as GameObject;
        instance.transform.position = worldPositionPoint;
        instance.transform.rotation = prefab.transform.rotation * randomRotationQuat;
        instance.transform.localScale = Vector3.Scale(randomScaleUniform, prefab.transform.localScale);

        float radius = GetGameObjectRadius(instance) * randomScaleUniform.x;
        ColliderPositionRadius positionRadius = new ColliderPositionRadius()
        {
            Position = worldPositionPoint,
            Radius = radius
            
        };
        m_CollidersInArea.Add(positionRadius);
    }

    private struct ColliderPositionRadius
    {
        public float Radius;
        public Vector3 Position;
    }
    
    private void GatherCollidersInArea()
    {
        m_CollidersInArea = new NativeList<ColliderPositionRadius>(Allocator.Temp);
        foreach (var splinePositionData in m_SplineCache)
        {
            Bounds bounds = splinePositionData.SplineBounds;
            float splineRadius = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.5f;
            Vector3 center = transform.TransformPoint(bounds.center);
            int numHits = Physics.OverlapSphereNonAlloc(center, splineRadius, m_HitColliders,
                PrefabBrushSettings.instance.BrushRaycastLayerMask);
            for (int i = 0; i < numHits; i++)
            {
                Collider collider = m_HitColliders[i];
                if (collider.TryGetComponent(out Terrain terrain))
                    continue;

                ColliderPositionRadius positionRadius = new ColliderPositionRadius()
                {
                    Radius = Mathf.Max(collider.bounds.extents.x * collider.transform.lossyScale.x,
                        collider.bounds.extents.z * collider.transform.lossyScale.z), // * 2.0f, //
                    //Radius = collider.transform.GetRadius() * Mathf.Max(collider.transform.localScale.x, collider.transform.localScale.z),
                    Position = collider.transform.position
                };
                m_CollidersInArea.Add(positionRadius);
            }
        }
    }

    private bool IsLegalSpawnLocation(GameObject prefab, Vector3 worldPosition, Quaternion randomRotationQuat, Vector3 randomScale, Vector3 regionPointPosition, Bounds bounds)
    {
        float2 positionSplineSpace = new float2(regionPointPosition.x, regionPointPosition.z) / new float2(bounds.size.x, bounds.size.y);

        if (worldPosition.y < -2.0f)
            return false;

        //float noiseValue = noise.cnoise(m_NoiseOffset + positionSplineSpace);
        float2 worldPosition2D = new Vector2(worldPosition.x, worldPosition.z);
        //float noiseValue = noise.cnoise(m_NoiseOffset + worldPosition2D);
        //noiseValue = (noiseValue * 0.5f) + 0.5f; // Normalize noise value from [-1, 1] to [0, 1].
        
        //noiseValue = Mathf.Clamp01(noiseValue + m_Random.NextFloat(-0.1f, 0.1f));
        float noiseValue = m_Random.NextFloat(0.0f, 1.0f);
        if (noiseValue < 1.0f - Density) return false;

        

        Terrain terrain = Terrain.activeTerrain;
        float2 terrainPosition = new Vector2(terrain.transform.position.x, terrain.transform.position.z);
        float2 terrainSize = new Vector2(terrain.terrainData.size.x, terrain.terrainData.size.z);
        float2 posTerrainSpace = (worldPosition2D - terrainPosition) / terrainSize;
        float steepness = terrain.terrainData.GetSteepness(posTerrainSpace.x, posTerrainSpace.y);
        if (steepness >= 30)
            return false;

        float radius = GetGameObjectRadius(prefab);
        foreach (var collider in m_CollidersInArea)
        {
            float distance = Vector3.Distance(worldPosition, collider.Position);
            if (distance < radius + collider.Radius) return false;
        }

        return true;
    }

    private static float GetGameObjectRadius(GameObject gameObject)
    {
        Collider collider = gameObject.GetComponentInChildren<Collider>();
        float radius = collider == null
            ? gameObject.transform.GetRadius()
            : Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z) * 0.5f;

        return radius;
    }

    private void BakeRegionMap()
    {
        return;
        DestroyObjects();
        SpawnObjects();
    }*/

    public override void GenerateMask()
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(kMaskTextureWidth, kMaskTextureHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        renderTexture.enableRandomWrite = true;

        CalculateWorldBounds();
        m_MaskTexture = new Texture2D(kMaskTextureWidth, kMaskTextureHeight, TextureFormat.ARGB32, false, true);
        m_MaskTexture.wrapMode = TextureWrapMode.Clamp;

        FindComputeShader();

        Spline spline = m_SplineContainer.Spline;
        if (spline.Count <= 1)
        {
            return;
        }
        Bounds splineBounds = spline.GetBounds();
        if (splineBounds.size.x > splineBounds.size.z)
        {
            splineBounds.size = new Vector3(splineBounds.size.x, splineBounds.size.y, splineBounds.size.x);
        }
        else
        {
            splineBounds.size = new Vector3(splineBounds.size.z, splineBounds.size.y, splineBounds.size.z);
        }
        
        //
        // Evaluate the positions on the spline.
        //
        int evaluateSplinePositionsKernel = m_CreateSplineAreaTextureComputeShader.FindKernel("EvaluateSplinePositions");
        float kSplineEvaluationResolution = 0.2f;
        float splineLength = spline.GetLength();
        int numSplinePoints = (int)(splineLength / kSplineEvaluationResolution) + 1;
        
        // Create a buffer for the spline points
        ComputeBuffer splinePointsComputeBuffer = new ComputeBuffer(numSplinePoints, sizeof(float) * 3);
        
        // Create the Unity Spline buffer.
        var splineBuffers = new SplineComputeBufferScope<Spline>(spline);
        splineBuffers.Bind(m_CreateSplineAreaTextureComputeShader, evaluateSplinePositionsKernel, "info", "curves", "curveLengths");
        splineBuffers.Upload();
        
        m_CreateSplineAreaTextureComputeShader.SetInt(kComputeNumPositionsId, numSplinePoints);
        m_CreateSplineAreaTextureComputeShader.SetBuffer(evaluateSplinePositionsKernel, kComputeSplinePositions, splinePointsComputeBuffer);
        m_CreateSplineAreaTextureComputeShader.Dispatch(evaluateSplinePositionsKernel, Mathf.CeilToInt(numSplinePoints / 64.0f), 1, 1);
        splineBuffers.Dispose();
    
        //
        // Create the distance field.
        //
        int workgroupsX = Mathf.CeilToInt(kMaskTextureWidth / 8.0f);
        int workgroupsY = Mathf.CeilToInt(kMaskTextureHeight / 8.0f);
        int kernel = m_CreateSplineAreaTextureComputeShader.FindKernel("CSCreateSplineAreaMask");
        ComputeBuffer furthestDistanceBuffer = new ComputeBuffer(1, sizeof(uint));
        NativeArray<uint> furthestDistance = new NativeArray<uint>(1, Allocator.Temp, NativeArrayOptions.ClearMemory);
        furthestDistanceBuffer.SetData(furthestDistance);
        furthestDistance.Dispose();
        m_CreateSplineAreaTextureComputeShader.SetBuffer(kernel, "furthestDistance", furthestDistanceBuffer);
        m_CreateSplineAreaTextureComputeShader.SetTexture(kernel, kComputeResultId, renderTexture);
        m_CreateSplineAreaTextureComputeShader.SetVector(kComputeRegionMinId, splineBounds.min);
        m_CreateSplineAreaTextureComputeShader.SetVector(kComputeRegionSizeId, splineBounds.size);
        m_CreateSplineAreaTextureComputeShader.SetInt(kComputeNumPositionsId, numSplinePoints);
        m_CreateSplineAreaTextureComputeShader.SetBuffer(kernel, kComputeSplinePositions, splinePointsComputeBuffer);
        m_CreateSplineAreaTextureComputeShader.Dispatch(kernel, workgroupsX, workgroupsY, 1);
        splinePointsComputeBuffer.Release();
        
        // Normalize the distances.
        int normalizeDistancesKernel = m_CreateSplineAreaTextureComputeShader.FindKernel("CSCalculateNormalizedDistances");
        m_CreateSplineAreaTextureComputeShader.SetBuffer(normalizeDistancesKernel, "furthestDistance", furthestDistanceBuffer);
        m_CreateSplineAreaTextureComputeShader.SetTexture(normalizeDistancesKernel, kComputeResultId, renderTexture);
        m_CreateSplineAreaTextureComputeShader.Dispatch(normalizeDistancesKernel, workgroupsX, workgroupsY, 1);
        
        furthestDistanceBuffer.Dispose();
        Graphics.CopyTexture(renderTexture, m_MaskTexture);
        
        // Blur the SDF.
        int blurSDFKernel = m_CreateSplineAreaTextureComputeShader.FindKernel("CSBlurSDF");
        m_CreateSplineAreaTextureComputeShader.SetTexture(blurSDFKernel, kComputeResultId, renderTexture);
        m_CreateSplineAreaTextureComputeShader.SetTexture(blurSDFKernel, "SDF", m_MaskTexture);
        m_CreateSplineAreaTextureComputeShader.Dispatch(blurSDFKernel, workgroupsX, workgroupsY, 1);
        
        Graphics.CopyTexture(renderTexture, m_MaskTexture);
        RenderTexture.ReleaseTemporary(renderTexture);
    }
}
