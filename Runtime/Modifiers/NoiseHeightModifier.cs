using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public class NoiseHeightModifier : ITerrainHeightModifier
{
    [SerializeReference]
    public NoiseProperties NoiseProperties = new NoiseProperties();
    
    public Texture2D NoiseTexture;
    public HeightWriteMode Mode;
    public MaskFalloff Fallof;
    public override string FilePath => GetFilePath();
    
    public override void ApplyHeightmap(WorldBuildingContext context, Bounds worldBounds, Texture mask)
    {
        if (NoiseTexture == null || NoiseProperties.IsDirty)
        {
            NoiseProperties.IsDirty = false;
            var noiseMap = NoiseGenerator.FromNoiseProperties(NoiseProperties, Allocator.Temp);
            NoiseTexture = NoiseGenerator.GenerateNoiseTexture(noiseMap, new int2(NoiseProperties.NoiseResolution, NoiseProperties.NoiseResolution));
            noiseMap.Dispose();
        }
        context.MaskFalloff = Fallof;
        context.ApplyHeightmap(worldBounds, NoiseTexture, mask, Mode, NoiseProperties.HeightMin, NoiseProperties.HeightMax);
    }
}