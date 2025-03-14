using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace GameCraftersGuild.WorldBuilding
{
    public interface IWorldBuilder : ITerrainVegetationProvider
    {
        public float4x4 TransformMatrix { get; set; }
        public int Priority { get; }

        public bool IsDirty { get; set; }

        SplineContainer SplineContainer { get; }

        public bool ApplyHeights(WorldBuildingContext context);
        public bool ApplySplatmap(WorldBuildingContext context);
        public void GenerateVegetation(WorldBuildingContext context);
        public void SpawnGameObjects(WorldBuildingContext context);

        public Bounds WorldBounds { get; }
        public void GenerateMask();

        public List<ITerrainSplatModifier> TerrainSplatModifiers { get; }
        public bool ContainsSplineData(SplineData<float> splineData);
    }
}