using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public class SetHeightModifier : ITerrainHeightModifier
    {
        public float Height = 0.0f;
        public override string FilePath => GetFilePath();
        public HeightWriteMode Mode;
        public MaskFalloff Fallof;
        public FalloffType FalloffFunction = FalloffType.Linear;

        public override void ApplyHeightmap(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {
            context.MaskFalloff = Fallof;
            context.FalloffFunction = FalloffFunction;
            context.ApplyHeightmap(worldBounds, null, mask, Mode, 0.0f, Height);
        }
    }
}