using System;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public abstract class ITerrainHeightModifier : WorldModifier
    {
        public abstract void ApplyHeightmap(WorldBuildingContext context, Bounds worldBounds, Texture mask);
    }
}