using System.Collections.Generic;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    /// <summary>
    /// Interface for objects that provide vegetation modifiers.
    /// </summary>
    public interface ITerrainVegetationProvider
    {
        /// <summary>
        /// Gets the list of vegetation modifiers.
        /// </summary>
        List<ITerrainVegetationModifier> TerrainVegetationModifiers { get; }
    }
} 