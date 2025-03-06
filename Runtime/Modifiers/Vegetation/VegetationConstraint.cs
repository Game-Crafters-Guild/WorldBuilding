using System;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    /// <summary>
    /// Interface for vegetation constraints that can be checked during vegetation placement
    /// </summary>
    public interface IVegetationConstraint
    {
        /// <summary>
        /// Check if the vegetation can be placed at the specified position
        /// </summary>
        /// <param name="terrainData">The terrain data</param>
        /// <param name="normX">Normalized X position on terrain (0-1)</param>
        /// <param name="normZ">Normalized Z position on terrain (0-1)</param>
        /// <param name="context">Additional context data for constraint checking</param>
        /// <returns>True if the constraint is satisfied, false otherwise</returns>
        bool CheckConstraint(TerrainData terrainData, float normX, float normZ, VegetationConstraintContext context);
    }

    /// <summary>
    /// Context data for vegetation constraint checking
    /// </summary>
    public class VegetationConstraintContext
    {
        // Terrain data
        public float TerrainHeight { get; set; }
        public float TerrainSlope { get; set; }
        public float[,,] AlphaMaps { get; set; }
        
        // Position data
        public float BoundsNormX { get; set; }
        public float BoundsNormZ { get; set; }
        
        // Texture data
        public Texture MaskTexture { get; set; }
        public Texture NoiseTexture { get; set; }
    }

    /// <summary>
    /// Height-based vegetation constraint
    /// </summary>
    [Serializable]
    public class HeightConstraint : IVegetationConstraint
    {
        public float MinHeight = 0f;
        public float MaxHeight = 1000f;

        public bool CheckConstraint(TerrainData terrainData, float normX, float normZ, VegetationConstraintContext context)
        {
            return context.TerrainHeight >= MinHeight && context.TerrainHeight <= MaxHeight;
        }
    }

    /// <summary>
    /// Slope-based vegetation constraint
    /// </summary>
    [Serializable]
    public class SlopeConstraint : IVegetationConstraint
    {
        [Range(0f, 90f)]
        public float MinSlope = 0f;
        
        [Range(0f, 90f)]
        public float MaxSlope = 45f;

        public bool CheckConstraint(TerrainData terrainData, float normX, float normZ, VegetationConstraintContext context)
        {
            return context.TerrainSlope >= MinSlope && context.TerrainSlope <= MaxSlope;
        }
    }

    /// <summary>
    /// Layer-based vegetation constraint
    /// </summary>
    [Serializable]
    public class LayerConstraint : IVegetationConstraint
    {
        public TerrainLayer[] AllowedLayers;

        public bool CheckConstraint(TerrainData terrainData, float normX, float normZ, VegetationConstraintContext context)
        {
            if (AllowedLayers == null || AllowedLayers.Length == 0)
                return true;

            // Convert to alpha map coordinates
            int alphamapX = Mathf.FloorToInt(normX * terrainData.alphamapWidth);
            int alphamapZ = Mathf.FloorToInt(normZ * terrainData.alphamapHeight);
            
            // Clamp to valid range
            alphamapX = Mathf.Clamp(alphamapX, 0, terrainData.alphamapWidth - 1);
            alphamapZ = Mathf.Clamp(alphamapZ, 0, terrainData.alphamapHeight - 1);
            
            // Check each allowed layer to see if it's the dominant one at this position
            for (int layerIdx = 0; layerIdx < terrainData.terrainLayers.Length; layerIdx++)
            {
                foreach (TerrainLayer allowedLayer in AllowedLayers)
                {
                    if (terrainData.terrainLayers[layerIdx] == allowedLayer)
                    {
                        // Check if this layer has significant weight at this position
                        if (context.AlphaMaps[alphamapZ, alphamapX, layerIdx] > 0.5f)
                        {
                            return true;
                        }
                    }
                }
            }
            
            return false;
        }
    }

    /// <summary>
    /// Mask-based vegetation constraint
    /// </summary>
    [Serializable]
    public class MaskConstraint : IVegetationConstraint
    {
        [Range(0f, 1f)]
        public float Threshold = 0.1f;

        public bool CheckConstraint(TerrainData terrainData, float normX, float normZ, VegetationConstraintContext context)
        {
            if (context.MaskTexture == null)
                return true;
                
            // Sample mask texture
            Color maskColor;
            if (context.MaskTexture is Texture2D texture2D)
            {
                int x = Mathf.FloorToInt(context.BoundsNormX * texture2D.width);
                int y = Mathf.FloorToInt(context.BoundsNormZ * texture2D.height);
                maskColor = texture2D.GetPixel(x, y);
            }
            else
            {
                // For other texture types, use a simpler approach (this is not as accurate)
                maskColor = Color.white;
            }
            
            return maskColor.grayscale >= Threshold;
        }
    }

    /// <summary>
    /// Noise-based vegetation constraint
    /// </summary>
    [Serializable]
    public class NoiseConstraint : IVegetationConstraint
    {
        [Range(0f, 1f)]
        public float Threshold = 0.3f;

        public bool CheckConstraint(TerrainData terrainData, float normX, float normZ, VegetationConstraintContext context)
        {
            if (context.NoiseTexture == null)
                return true;
                
            // Sample noise texture
            Color noiseColor;
            if (context.NoiseTexture is Texture2D texture2D)
            {
                int x = Mathf.FloorToInt(context.BoundsNormX * texture2D.width);
                int y = Mathf.FloorToInt(context.BoundsNormZ * texture2D.height);
                noiseColor = texture2D.GetPixel(x, y);
            }
            else
            {
                // For other texture types, use a simpler approach
                noiseColor = Color.white;
            }
            
            return noiseColor.grayscale >= Threshold;
        }
    }

    /// <summary>
    /// Density-based vegetation constraint
    /// </summary>
    [Serializable]
    public class DensityConstraint : IVegetationConstraint
    {
        [Range(0f, 5f)]
        [Tooltip("Density of vegetation. Higher values = more vegetation.")]
        public float Density = 1f;
        
        [Range(0f, 1f)]
        public float RandomOffset = 0.1f;
        
        [Tooltip("Number of trees per unit at full density")]
        [Min(0.001f)]
        public float TreesPerUnit = 0.1f;

        private System.Random m_Random;

        public bool CheckConstraint(TerrainData terrainData, float normX, float normZ, VegetationConstraintContext context)
        {
            if (m_Random == null)
                m_Random = new System.Random();
                
            // Use random value compared against density to determine if vegetation should be placed
            double randomValue = m_Random.NextDouble();
            return randomValue <= Density * 0.2f; // Scale to make density more intuitive
        }
        
        public void SetRandomSeed(int seed)
        {
            if (seed != 0)
                m_Random = new System.Random(seed);
            else
                m_Random = new System.Random();
        }
        
        public float GetRandomOffset()
        {
            if (m_Random == null)
                m_Random = new System.Random();
                
            return (float)(m_Random.NextDouble() * 2 - 1) * RandomOffset * 0.01f;
        }
        
        public int CalculateTreeCount(float area)
        {
            // Calculate number of trees based on area and density
            float baseCount = area * TreesPerUnit;
            
            // Apply density (0-5 range)
            return Mathf.RoundToInt(baseCount * Density);
        }
    }
} 