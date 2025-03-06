using System;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public class HeightMapTextureModifier : ITerrainHeightModifier
    {
        public HeightWriteMode Mode;
        public MaskFalloff Fallof;
        public override string FilePath => GetFilePath();
        public Texture2D HeightMapTexture;
        public float MinWorldHeight = 0.0f;
        public float MaxWorldHeight = 80.0f;

        public override void ApplyHeightmap(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {
            context.MaskFalloff = Fallof;
            context.ApplyHeightmap(worldBounds, HeightMapTexture, mask, Mode, MinWorldHeight, MaxWorldHeight);
        }
    }
}