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
                    RegisterTreePrototypes(terrainData);
                }
                else if (firstPrototype is DetailPrototype)
                {
                    RegisterDetailPrototypes(terrainData);
                }
            }
        }
        
        protected virtual void RegisterTreePrototypes(TerrainData terrainData)
        {
            int count = GetNumPrototypes();
            if (count <= 0)
                return;
                
            // Get existing prototypes to prevent duplicates
            var existingPrototypes = new List<TreePrototype>(terrainData.treePrototypes);
            var newPrototypes = new List<TreePrototype>();
            bool hasChanges = false;
            
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
                    newPrototypes.Add(prototype);
                    hasChanges = true;
                }
            }
            
            // If we have new prototypes, update the terrain
            if (hasChanges)
            {
                // Combine existing and new prototypes
                var allPrototypes = new List<TreePrototype>(existingPrototypes);
                allPrototypes.AddRange(newPrototypes);
                
                // Apply to terrain
                terrainData.treePrototypes = allPrototypes.ToArray();
                
                // This is needed to refresh the tree database
                terrainData.RefreshPrototypes();
            }
        }
        
        protected virtual void RegisterDetailPrototypes(TerrainData terrainData)
        {
            int count = GetNumPrototypes();
            if (count <= 0)
                return;
                
            // Get existing prototypes to prevent duplicates
            var existingPrototypes = new List<DetailPrototype>(terrainData.detailPrototypes);
            var newPrototypes = new List<DetailPrototype>();
            bool hasChanges = false;
            
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
                    newPrototypes.Add(prototype);
                    hasChanges = true;
                }
            }
            
            // If we have new prototypes, update the terrain
            if (hasChanges)
            {
                // Combine existing and new prototypes
                var allPrototypes = new List<DetailPrototype>(existingPrototypes);
                allPrototypes.AddRange(newPrototypes);
                
                // Apply to terrain
                terrainData.detailPrototypes = allPrototypes.ToArray();
                
                // Resize the detail maps if needed
                if (terrainData.detailPrototypes.Length > existingPrototypes.Count)
                {
                    int detailWidth = terrainData.detailWidth;
                    int detailHeight = terrainData.detailHeight;
                    
                    // Initialize detail layer for each new prototype
                    for (int i = existingPrototypes.Count; i < terrainData.detailPrototypes.Length; i++)
                    {
                        int[,] emptyDetailMap = new int[detailWidth, detailHeight];
                        terrainData.SetDetailLayer(0, 0, i, emptyDetailMap);
                    }
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