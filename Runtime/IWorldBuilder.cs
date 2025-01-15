using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public interface IWorldBuilder
{
    public float4x4 TransformMatrix { get; set; }
    public float3 Scale { get; set; }
    public Quaternion Rotation { get; set; }
    public int Priority { get; }
    
    public bool IsDirty { get; set; }
    
    IReadOnlyList<Spline> Splines { get; }

    public void ApplyHeights(WorldBuildingContext context);
    public void ApplySplatmap(WorldBuildingContext context);
    public void SpawnGameObjects(WorldBuildingContext context);
    public void ProcessSpline(Spline spline);
    
    public Bounds WorldBounds { get; }
    public void GenerateMask(RenderTexture renderTexture);
    
    public List<ITerrainSplatModifier> TerrainSplatModifiers { get; }
}
