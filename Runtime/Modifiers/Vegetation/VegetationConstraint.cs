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
        
        // Object placement data
        public float MinimumDistance { get; set; }
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
        
        [SerializeReference] 
        public NoiseProperties NoiseProperties = new NoiseProperties();

        public bool CheckConstraint(TerrainData terrainData, float normX, float normZ, VegetationConstraintContext context)
        {
            var noiseTexture = NoiseProperties.NoiseTexture;
            if (noiseTexture == null)
                return true;
                
            // Sample noise texture
            Color noiseColor;
            
            int x = Mathf.FloorToInt(context.BoundsNormX * noiseTexture.width);
            int y = Mathf.FloorToInt(context.BoundsNormZ * noiseTexture.height);
            noiseColor = noiseTexture.GetPixel(x, y);
            
            return noiseColor.grayscale >= Threshold;
        }
    }
} 