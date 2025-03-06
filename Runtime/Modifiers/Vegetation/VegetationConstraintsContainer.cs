using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    /// <summary>
    /// Container class for vegetation constraints to allow for a custom property drawer
    /// </summary>
    [Serializable]
    public class VegetationConstraintsContainer
    {
        [SerializeReference]
        public List<IVegetationConstraint> Constraints = new List<IVegetationConstraint>();

        /// <summary>
        /// Check if all constraints are satisfied
        /// </summary>
        public bool CheckConstraints(TerrainData terrainData, float normX, float normZ, VegetationConstraintContext context)
        {
            // Check each constraint, fail fast if any constraint is not met
            foreach (var constraint in Constraints)
            {
                if (constraint == null)
                    continue;
                    
                if (!constraint.CheckConstraint(terrainData, normX, normZ, context))
                {
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Set random seed for constraints that need it
        /// </summary>
        public void SetRandomSeed(int seed)
        {
            foreach (var constraint in Constraints)
            {
                if (constraint is DensityConstraint densityConstraint)
                {
                    densityConstraint.SetRandomSeed(seed);
                }
            }
        }
        
        /// <summary>
        /// Find a constraint of the specified type
        /// </summary>
        public T FindConstraint<T>() where T : IVegetationConstraint
        {
            return (T)Constraints.Find(c => c is T);
        }
    }
} 