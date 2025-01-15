using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public abstract class ITerrainHeightModifier : WorldModifier
{
    public abstract void ApplyHeightmap(WorldBuildingContext context, Bounds worldBounds, Texture mask);
}
