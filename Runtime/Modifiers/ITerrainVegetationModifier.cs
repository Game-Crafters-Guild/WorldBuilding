using System;
using UnityEngine;
using System.Collections.Generic;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public abstract class ITerrainVegetationModifier : WorldModifier
    {
        /// <summary>
        /// Method for registering tree and detail prototypes with the terrain.
        /// This is called by the WorldBuildingSystem before applying vegetation.
        /// </summary>
        public virtual void RegisterPrototypes(WorldBuildingContext context, TerrainData terrainData)
        {
            int prototypeCount = GetNumPrototypes();
            if (prototypeCount <= 0)
                return;
                
            // Determine the prototype type from the first prototype
            if (prototypeCount > 0)
            {
                object firstPrototype = GetPrototype(0);
                
                if (firstPrototype is TreePrototype)
                {
                    RegisterTreePrototypes(context);
                }
                else if (firstPrototype is DetailPrototype)
                {
                    RegisterDetailPrototypes(context);
                }
            }
        }
        
        protected virtual void RegisterTreePrototypes(WorldBuildingContext context)
        {
            int count = GetNumPrototypes();
            if (count <= 0)
                return;
                
            // Get existing prototypes to prevent duplicates
            var existingPrototypes = new List<TreePrototype>(context.GetTreePrototypes());
            
            // Add new prototypes
            for (int i = 0; i < count; i++)
            {
                var prototype = GetPrototype(i) as TreePrototype;
                if (prototype == null)
                    continue;
                    
                // Check if this prototype already exists (by comparing prefab)
                bool exists = false;
                foreach (var existing in existingPrototypes)
                {
                    if (existing.prefab == prototype.prefab)
                    {
                        exists = true;
                        break;
                    }
                }
                
                if (!exists)
                {
                    context.AddTreePrototype(prototype);
                    existingPrototypes.Add(prototype); // Add to our local list to avoid duplicates
                }
            }
        }
        
        protected virtual void RegisterDetailPrototypes(WorldBuildingContext context)
        {
            int count = GetNumPrototypes();
            if (count <= 0)
                return;
                
            // Get existing prototypes to prevent duplicates
            var existingPrototypes = new List<DetailPrototype>(context.GetDetailPrototypes());
            
            // Add new prototypes
            for (int i = 0; i < count; i++)
            {
                var prototype = GetPrototype(i) as DetailPrototype;
                if (prototype == null)
                    continue;
                    
                // Check if this prototype already exists (by comparing prefab or texture)
                bool exists = false;
                foreach (var existing in existingPrototypes)
                {
                    if ((existing.usePrototypeMesh && existing.prototype == prototype.prototype) ||
                        (!existing.usePrototypeMesh && existing.prototypeTexture == prototype.prototypeTexture))
                    {
                        exists = true;
                        break;
                    }
                }
                
                if (!exists)
                {
                    context.AddDetailPrototype(prototype);
                    existingPrototypes.Add(prototype); // Add to our local list to avoid duplicates
                }
            }
        }
        
        public virtual void ApplyVegetation(WorldBuildingContext context, Bounds worldBounds, Texture mask)
        {
        }
        
        public abstract int GetNumPrototypes();
        public abstract object GetPrototype(int index);
    }
}