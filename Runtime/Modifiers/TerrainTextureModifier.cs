using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public class TerrainTextureModifier : ITerrainSplatModifier
    {
        public float Weight;
        [Range(0.0f, 1.0f)] public float Intensity = 1.0f;
        public TerrainLayer TerrainLayer;
        public MaskFalloff Fallof;
        public override string FilePath => GetFilePath();

        public override void ApplySplatmap(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {
            context.MaskFalloff = Fallof;
            context.ApplySplatmap(worldBounds, TerrainLayer, mask, Intensity);
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