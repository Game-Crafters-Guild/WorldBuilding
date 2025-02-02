using System;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public class NoiseHeightModifier : ITerrainHeightModifier
    {
        public HeightWriteMode Mode;
        public MaskFalloff Fallof;
        [SerializeReference] public NoiseProperties NoiseProperties = new NoiseProperties();
        public override string FilePath => GetFilePath();

        public override void ApplyHeightmap(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {
            context.MaskFalloff = Fallof;
            context.ApplyHeightmap(worldBounds, NoiseProperties.NoiseTexture, mask, Mode, NoiseProperties.HeightMin,
                NoiseProperties.HeightMax);
        }
    }
}