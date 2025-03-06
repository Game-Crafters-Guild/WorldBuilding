using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GameCraftersGuild.WorldBuilding
{
    /// <summary>
    /// Base class for vegetation modifiers that provides common functionality
    /// </summary>
    [Serializable]
    public abstract class BaseVegetationModifier : ITerrainVegetationModifier
    {
        // Vegetation constraints
        public VegetationConstraintsContainer ConstraintsContainer = new VegetationConstraintsContainer();
        
        // Density settings
        [Range(0f, 5f)]
        [Tooltip("Density of vegetation. Higher values = more vegetation.")]
        public float Density = 1f;
        
        [Range(0f, 1f)]
        public float RandomOffset = 0.1f;
        
        // Seed for random generation
        public int RandomSeed = 0;
        protected System.Random m_SeededRandom;
        
        // Return a random value between 0 and 1 using either the seeded random or Unity's random
        protected float GetRandomValue()
        {
            if (RandomSeed != 0)
            {
                if (m_SeededRandom == null)
                {
                    m_SeededRandom = new System.Random(RandomSeed);
                }
                return (float)m_SeededRandom.NextDouble();
            }
            else
            {
                return Random.value;
            }
        }
        
        // Return a random value in range using either the seeded random or Unity's random
        protected float GetRandomRange(float min, float max)
        {
            return min + GetRandomValue() * (max - min);
        }
        
        // Return a random integer in range using either the seeded random or Unity's random
        protected int GetRandomRange(int min, int max)
        {
            if (RandomSeed != 0)
            {
                if (m_SeededRandom == null)
                {
                    m_SeededRandom = new System.Random(RandomSeed);
                }
                return m_SeededRandom.Next(min, max);
            }
            else
            {
                return Random.Range(min, max);
            }
        }
        
        // Return a random rotation around y-axis
        protected float GetRandomRotation()
        {
            return GetRandomValue() * 2f * Mathf.PI;
        }
        
        // Get random offset based on the defined random offset value
        protected float GetRandomOffset()
        {
            return (GetRandomValue() * 2 - 1) * RandomOffset * 0.01f;
        }
        
        // Check if vegetation should be placed based on density
        protected bool CheckDensity()
        {
            return GetRandomValue() <= Density * 0.2f; // Scale to make density more intuitive
        }

        // Create default constraints if none exist
        protected void CreateDefaultConstraints()
        {
            if (ConstraintsContainer.Constraints.Count == 0)
            {
                // Create default constraints
                ConstraintsContainer.Constraints.Add(new HeightConstraint());
                ConstraintsContainer.Constraints.Add(new SlopeConstraint());
            }
        }

        public override string FilePath => GetFilePath();
        
        // Abstract methods to be implemented by derived classes
        public abstract override int GetNumPrototypes();
        public abstract override object GetPrototype(int index);
        public abstract override void ApplyVegetation(WorldBuildingContext context, Bounds worldBounds, Texture mask);
        
        // Common helper methods
        
        protected TerrainData GetTerrainDataFromContext(WorldBuildingContext context)
        {
            return context.TerrainData;
        }
        
        protected Bounds ConvertWorldBoundsToTerrainSpace(Bounds worldBounds, TerrainData terrainData)
        {
            // Convert world bounds to local terrain space (assuming terrain is at origin)
            // This is a simplification - you may need to adjust based on your terrain position
            return worldBounds;
        }

        protected float GetTerrainSlope(TerrainData terrainData, float normX, float normZ)
        {
            // Get slope at the specified normalized position
            int heightMapX = Mathf.RoundToInt(normX * terrainData.heightmapResolution);
            int heightMapZ = Mathf.RoundToInt(normZ * terrainData.heightmapResolution);
            
            // Clamp to valid range
            heightMapX = Mathf.Clamp(heightMapX, 0, terrainData.heightmapResolution - 1);
            heightMapZ = Mathf.Clamp(heightMapZ, 0, terrainData.heightmapResolution - 1);
            
            // Get terrain normal
            Vector3 normal = terrainData.GetInterpolatedNormal(normX, normZ);
            
            // Calculate angle between normal and up vector (in degrees)
            float angle = Vector3.Angle(normal, Vector3.up);
            return angle;
        }

        protected float SampleTexture(Texture texture, float normX, float normZ)
        {
            // Sample texture at normalized position
            if (texture is Texture2D texture2D)
            {
                int x = Mathf.FloorToInt(normX * texture2D.width);
                int y = Mathf.FloorToInt(normZ * texture2D.height);
                
                x = Mathf.Clamp(x, 0, texture2D.width - 1);
                y = Mathf.Clamp(y, 0, texture2D.height - 1);
                
                return texture2D.GetPixel(x, y).grayscale;
            }
            
            // Fallback
            return 1.0f;
        }
        
        protected VegetationConstraintContext CreateConstraintContext(
            TerrainData terrainData, 
            float normX, 
            float normZ, 
            float boundsNormX, 
            float boundsNormZ, 
            float[,,] alphamaps, 
            Texture mask)
        {
            // Get height at position
            float height = terrainData.GetHeight(
                Mathf.RoundToInt(normX * terrainData.heightmapResolution), 
                Mathf.RoundToInt(normZ * terrainData.heightmapResolution)
            );

            // Get slope at this position
            float slope = GetTerrainSlope(terrainData, normX, normZ);
            
            // Create context for constraint checking
            return new VegetationConstraintContext
            {
                TerrainHeight = height,
                TerrainSlope = slope,
                AlphaMaps = alphamaps,
                BoundsNormX = boundsNormX,
                BoundsNormZ = boundsNormZ,
                MaskTexture = mask
            };
        }
    }
} 