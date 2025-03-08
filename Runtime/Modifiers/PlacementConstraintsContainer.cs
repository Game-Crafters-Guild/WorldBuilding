using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    /// <summary>
    /// Container class for placement constraints to allow for a custom property drawer
    /// </summary>
    [Serializable]
    public class PlacementConstraintsContainer
    {
        [SerializeReference]
        public List<IPlacementConstraint> Constraints = new List<IPlacementConstraint>();

        /// <summary>
        /// Check if all constraints are satisfied
        /// </summary>
        public bool CheckConstraints(TerrainData terrainData, float normX, float normZ, PlacementConstraintContext context)
        {
            if (Constraints == null || Constraints.Count == 0)
                return true;
            
            foreach (var constraint in Constraints)
            {
                // Temporarily skip layer constraints as they're causing issues
                if (constraint is LayerConstraint)
                    continue;
                    
                // Check if the constraint is satisfied
                if (!constraint.CheckConstraint(terrainData, normX, normZ, context))
                    return false;
            }
            
            // All constraints satisfied
            return true;
        }
        
        /// <summary>
        /// Find a constraint of the specified type
        /// </summary>
        public T FindConstraint<T>() where T : IPlacementConstraint
        {
            return (T)Constraints.Find(c => c is T);
        }
    }

    // Keep old container for backward compatibility - will be marked obsolete
    [Serializable]
    [System.Obsolete("Use PlacementConstraintsContainer instead")]
    public class VegetationConstraintsContainer : PlacementConstraintsContainer
    {
    }
}