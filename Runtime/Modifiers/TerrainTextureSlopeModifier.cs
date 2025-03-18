using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public class TerrainTextureSlopeModifier : ITerrainSplatModifier
    {
        [Range(0.0f, 90.0f)] public float SlopeMin = 0.0f;
        public float SlopeMax = 90.0f;

        public TerrainLayer TerrainLayer;

        public override string FilePath => GetFilePath();

        public override void ApplySplatmap(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {

        }

        public override int GetNumTerrainLayers()
        {
            return 1;
        }

        public override TerrainLayer GetTerrainLayer(int index)
        {
            return TerrainLayer;
        }
    }
}