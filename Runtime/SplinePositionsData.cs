using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

[Serializable]
public class SplinePositionsData// : ISerializationCallbackReceiver
{
    [Serializable]
    public struct PositionData
    {
        public float3 Position;
        public float DistanceFromEdge;
    }
    /*private int m_CurrentVersion = 0;
    [SerializeField][HideInInspector]
    private int m_SerializedVersion = 0;*/
    public Spline Spline;
    private NativeArray<float3> m_SplinePoints;
    private NativeList<PositionData> m_RegionPoints;
    
    public NativeArray<float3> SplinePoints => m_SplinePoints;
    public NativeList<PositionData> RegionPoints => m_RegionPoints;
    
    public Bounds SplineBounds => m_SplineBounds;
    
    public float MaxDistanceFromEdge { get; private set; }
    
    //[SerializeField][HideInInspector]
    private Bounds m_SplineBounds;

    /*[SerializeField] [HideInInspector] private float3[] m_SplinePointsSerialized;
    [SerializeField] [HideInInspector] private float3[] m_RegionPointsSerialized;*/

    ~SplinePositionsData()
    {
        if (m_SplinePoints.IsCreated)
        {
            m_SplinePoints.Dispose();
        }

        if (m_RegionPoints.IsCreated)
        {
            m_RegionPoints.Dispose();
        }
    }

    public void BakePath(float resolution)
    {
        //++m_CurrentVersion;
        EvaluatePointsAlongSpline(resolution);
        
        if (!m_SplinePoints.IsCreated || m_SplinePoints.Length == 0)
            return;

        if (!m_RegionPoints.IsCreated)
        {
            m_RegionPoints = new NativeList<PositionData>(m_SplinePoints.Length, Allocator.Persistent);
        }
        else
        {
            m_RegionPoints.Clear();
            m_RegionPoints.SetCapacity(m_SplinePoints.Length);
        }

        for (int i = 0; i < m_SplinePoints.Length; i++)
        {
            m_RegionPoints.Add(new PositionData() { Position = m_SplinePoints[i], DistanceFromEdge = 0.0f } );
        }

        MaxDistanceFromEdge = 0.0f;
    }

    public void BakeRegion(float resolution)
    {
        //++m_CurrentVersion;
        m_SplineBounds = Spline.GetBounds();
        
        EvaluatePointsAlongSpline(resolution);
        EvaluateRegionPointsInSpline(resolution);
    }

    private void EvaluatePointsAlongSpline(float resolution)
    {
        if (m_SplinePoints.IsCreated)
        {
            m_SplinePoints.Dispose();
        }
        m_SplineBounds = Spline.GetBounds();

        if (Spline.Count == 0)
            return;
        
        float splineLength = Spline.GetLength();
        int numSegments = (int)(splineLength / resolution) + 1;
        if (numSegments <= 1)
        {
            return;
        }

        if (!m_SplinePoints.IsCreated)
        {
            m_SplinePoints = new NativeArray<float3>(numSegments + 1, Allocator.Persistent);
        }

        for (int i = 0; i <= numSegments; i++)
        {
            float t = i / (float)numSegments;
            float3 segmentWorldPosition = Spline.EvaluatePosition(t);
            m_SplinePoints[i] = segmentWorldPosition;
        }
    }
    
    // Job adding two floating point values together
    public struct TestPointsInPolygonJob : IJobParallelFor
    {
        [ReadOnly]
        public int NumStepsZ;
        
        [ReadOnly]
        public Bounds Bounds;

        [ReadOnly] 
        public float Intervals;
        
        [ReadOnly]
        public NativeArray<float3> Polygon;
        
        public NativeList<PositionData>.ParallelWriter Result;


        public void Execute(int index)
        {
            int z = index % NumStepsZ;
            int x = index / NumStepsZ;
            
            float3 point = new float3(Bounds.min.x + x * Intervals, 0.0f, Bounds.min.z + z * Intervals);
            float distanceFromEdge = 0.0f;
            if (IsPointInsidePolygon(point, Polygon, out distanceFromEdge))
            {
                Result.AddNoResize(new PositionData { Position = point, DistanceFromEdge = distanceFromEdge });
            }
        }
    }
    
    private void EvaluateRegionPointsInSpline(float resolution)
    {
        if (!m_SplinePoints.IsCreated || m_SplinePoints.Length == 0)
        {
            return;
        }

        int numIterations = ((int)(m_SplineBounds.size.x / resolution) + 1) * ((int)(m_SplineBounds.size.z / resolution) + 1);
        if (!m_RegionPoints.IsCreated)
        {
            m_RegionPoints = new NativeList<PositionData>(numIterations, Allocator.Persistent);
        }
        else
        {
            m_RegionPoints.Clear();
            m_RegionPoints.SetCapacity(numIterations);
        }

        TestPointsInPolygonJob testPointsInPolygonJob = new TestPointsInPolygonJob();
        testPointsInPolygonJob.NumStepsZ = (int)(m_SplineBounds.size.z / resolution) + 1;
        testPointsInPolygonJob.Intervals = resolution;
        testPointsInPolygonJob.Bounds = m_SplineBounds;
        testPointsInPolygonJob.Polygon = m_SplinePoints;
        testPointsInPolygonJob.Result = m_RegionPoints.AsParallelWriter();
        
        testPointsInPolygonJob.Schedule(numIterations, testPointsInPolygonJob.NumStepsZ).Complete();

        MaxDistanceFromEdge = 0.0f;
        for (int i = 0; i < m_RegionPoints.Length; i++)
        {
            MaxDistanceFromEdge = math.max(MaxDistanceFromEdge, m_RegionPoints[i].DistanceFromEdge);
        }
    }

    /// <summary>
    /// Determines if a point is inside a polygon defined by a spline using the ray-casting algorithm.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <param name="polygon">The polygon represented as a spline.</param>
    /// <returns>True if the point is inside the polygon; otherwise, false.</returns>
    static bool IsPointInsidePolygon(float3 point, Spline polygon)
    {
        int intersections = 0;
        int vertexCount = polygon.Count;
 
        // Iterate over each edge of the polygon
        for (int i = 0, j = vertexCount - 1; i < vertexCount; j = i++)
        {
            float3 vertex1 = polygon[i].Position;
            float3 vertex2 = polygon[j].Position;
 
            // Check if the edge crosses the horizontal ray originating from the point
            bool crossesRay = (vertex1.z > point.z) != (vertex2.z > point.z);
 
            if (crossesRay)
            {
                // Calculate the x-coordinate of the intersection point
                float intersectionX = vertex2.x - (vertex2.z - point.z) * (vertex2.x - vertex1.x) / (vertex2.z - vertex1.z);
 
                // Count the intersection if the x-coordinate is to the right of the point
                if (point.x < intersectionX)
                {
                    intersections++;
                }
            }
        }
        // Odd number of intersections means the point is inside
        return intersections % 2 != 0;
    }
    
    static bool IsPointInsidePolygon(float3 point, NativeArray<float3> polygon, out float distanceFromEdge)
    {
        float distanceSquared = float.MaxValue;
        int intersections = 0;
        int vertexCount = polygon.Length;
 
        // Iterate over each edge of the polygon
        for (int i = 0, j = vertexCount - 1; i < vertexCount; j = i++)
        {
            float3 vertex1 = polygon[i];
            float3 vertex2 = polygon[j];
 
            // Calculate the distance to the first vertex.
            distanceSquared = Mathf.Min(distanceSquared, math.distancesq(new float2(vertex1.x, vertex1.z), new float2(point.x, point.z)));

            // Check if the edge crosses the horizontal ray originating from the point
            bool crossesRay = (vertex1.z > point.z) != (vertex2.z > point.z);
 
            if (crossesRay)
            {
                // Calculate the x-coordinate of the intersection point
                float intersectionX = vertex2.x - (vertex2.z - point.z) * (vertex2.x - vertex1.x) / (vertex2.z - vertex1.z);
 
                // Count the intersection if the x-coordinate is to the right of the point
                if (point.x < intersectionX)
                {
                    intersections++;
                }
            }
        }
        // Calculate the distance to last vertex as that's never calculated in the inner loop.
        float3 lastVertex = polygon[vertexCount - 1];
        distanceSquared = Mathf.Min(distanceSquared, math.distancesq(new float2(lastVertex.x, lastVertex.z), new float2(point.x, point.z)));
        distanceFromEdge = math.sqrt(distanceSquared);
        
        // Odd number of intersections means the point is inside
        return intersections % 2 != 0;
    }

    /*public void OnBeforeSerialize()
    {
        if (m_SerializedVersion == m_CurrentVersion)
            return;

        m_SerializedVersion = m_CurrentVersion;
        if (m_SplinePoints.IsCreated)
        {
            m_SplinePointsSerialized = m_SplinePoints.ToArrayNBC();
        }
        
        if (m_RegionPoints.IsCreated)
        {
            m_RegionPointsSerialized = m_RegionPoints.ToArrayNBC();
        }
    }

    public void OnAfterDeserialize()
    {
        m_CurrentVersion = m_SerializedVersion;
        if (m_SplinePointsSerialized != null && m_SplinePointsSerialized.Length > 0)
        {
            m_SplinePoints = new NativeList<float3>(m_SplinePointsSerialized.Length, Allocator.Persistent);
            m_SplinePoints.CopyFromNBC(m_SplinePointsSerialized);
        }
        if (m_RegionPointsSerialized != null && m_RegionPointsSerialized.Length > 0)
        {
            m_RegionPoints = new NativeList<float3>(m_RegionPointsSerialized.Length, Allocator.Persistent);
            m_RegionPoints.CopyFromNBC(m_RegionPointsSerialized);
        }
    }*/
}
