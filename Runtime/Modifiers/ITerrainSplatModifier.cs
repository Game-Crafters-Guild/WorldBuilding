using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public abstract class ITerrainSplatModifier : WorldModifier
    {
        public virtual void ApplySplatmap(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {
        }

        public abstract int GetNumTerrainLayers();
        public abstract TerrainLayer GetTerrainLayer(int index);

    }
}