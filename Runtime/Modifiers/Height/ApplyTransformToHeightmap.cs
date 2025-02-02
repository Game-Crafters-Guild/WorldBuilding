using System;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public class ApplyTransformToHeightmap : ITerrainHeightModifier
    {
        public override string FilePath => GetFilePath();
        public MaskFalloff Fallof;

        public override void ApplyHeightmap(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {
            context.MaskFalloff = Fallof;
            context.ApplyRegionTransformsToHeightmap(worldBounds, mask);
        }
    }
}