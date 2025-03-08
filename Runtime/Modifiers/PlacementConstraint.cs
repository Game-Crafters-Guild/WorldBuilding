using System;
using Unity.Mathematics;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    /// <summary>
    /// Interface for placement constraints that can be checked during object placement
    /// </summary>
    public interface IPlacementConstraint
    {
        /// <summary>
        /// Check if an object can be placed at the specified position
        /// </summary>
        /// <param name="terrainData">The terrain data</param>
        /// <param name="normX">Normalized X position on terrain (0-1)</param>
        /// <param name="normZ">Normalized Z position on terrain (0-1)</param>
        /// <param name="context">Additional context data for constraint checking</param>
        /// <returns>True if the constraint is satisfied, false otherwise</returns>
        bool CheckConstraint(TerrainData terrainData, float normX, float normZ, PlacementConstraintContext context);
    }

    /// <summary>
    /// Context data for placement constraint checking
    /// </summary>
    public class PlacementConstraintContext
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
    /// Height-based placement constraint
    /// </summary>
    [Serializable]
    public class HeightConstraint : IPlacementConstraint
    {
        public float MinHeight = 0f;
        public float MaxHeight = 1000f;

        public bool CheckConstraint(TerrainData terrainData, float normX, float normZ, PlacementConstraintContext context)
        {
            return context.TerrainHeight >= MinHeight && context.TerrainHeight <= MaxHeight;
        }
    }

    /// <summary>
    /// Slope-based placement constraint
    /// </summary>
    [Serializable]
    public class SlopeConstraint : IPlacementConstraint
    {
        [Range(0f, 90f)]
        public float MinSlope = 0f;
        
        [Range(0f, 90f)]
        public float MaxSlope = 45f;

        public bool CheckConstraint(TerrainData terrainData, float normX, float normZ, PlacementConstraintContext context)
        {
            return context.TerrainSlope >= MinSlope && context.TerrainSlope <= MaxSlope;
        }
    }

    /// <summary>
    /// Layer-based placement constraint
    /// </summary>
    [Serializable]
    public class LayerConstraint : IPlacementConstraint
    {
        public TerrainLayer[] AllowedLayers;

        public bool CheckConstraint(TerrainData terrainData, float normX, float normZ, PlacementConstraintContext context)
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
    /// Mask-based placement constraint
    /// </summary>
    [Serializable]
    public class MaskConstraint : IPlacementConstraint
    {
        [Range(0f, 1f)]
        public float Threshold = 0.1f;

        public bool CheckConstraint(TerrainData terrainData, float normX, float normZ, PlacementConstraintContext context)
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
            
            return maskColor.r >= Threshold;
        }
    }

    /// <summary>
    /// Noise-based placement constraint
    /// </summary>
    [Serializable]
    public class NoiseConstraint : IPlacementConstraint
    {
        [Range(0f, 1f)]
        public float Threshold = 0.3f;
        
        [SerializeReference] 
        public NoiseProperties NoiseProperties = new NoiseProperties();

        public bool CheckConstraint(TerrainData terrainData, float normX, float normZ, PlacementConstraintContext context)
        {
            // Try to get or generate the noise texture
            var noiseTexture = NoiseProperties?.NoiseTexture;
            if (noiseTexture == null)
            {
                // Generate procedural noise if texture isn't available
                // This matching the GPU implementation
                float2 position = new float2(context.BoundsNormX, context.BoundsNormZ);
                float scale = NoiseProperties != null ? Mathf.Clamp(NoiseProperties.NoiseScale / 10f, 0.1f, 10f) : 1f;
                float offset = NoiseProperties != null ? (NoiseProperties.Seed % 1000) / 1000f : 0f;
                
                // Simple Perlin-like noise that matches GPU implementation
                Vector2 scaledPos = new Vector2(
                    position.x * scale + offset,
                    position.y * scale + offset
                );
                
                Vector2 i = new Vector2(Mathf.Floor(scaledPos.x), Mathf.Floor(scaledPos.y));
                Vector2 f = new Vector2(scaledPos.x - i.x, scaledPos.y - i.y);
                Vector2 u = new Vector2(
                    f.x * f.x * (3.0f - 2.0f * f.x),
                    f.y * f.y * (3.0f - 2.0f * f.y)
                );
                
                float n00 = Mathf.Repeat(Mathf.Sin(Vector2.Dot(i, new Vector2(127.1f, 311.7f))) * 43758.5453f, 1f);
                float n01 = Mathf.Repeat(Mathf.Sin(Vector2.Dot(i + new Vector2(0, 1), new Vector2(127.1f, 311.7f))) * 43758.5453f, 1f);
                float n10 = Mathf.Repeat(Mathf.Sin(Vector2.Dot(i + new Vector2(1, 0), new Vector2(127.1f, 311.7f))) * 43758.5453f, 1f);
                float n11 = Mathf.Repeat(Mathf.Sin(Vector2.Dot(i + new Vector2(1, 1), new Vector2(127.1f, 311.7f))) * 43758.5453f, 1f);
                
                float nx0 = Mathf.Lerp(n00, n10, u.x);
                float nx1 = Mathf.Lerp(n01, n11, u.x);
                float noiseValue = Mathf.Lerp(nx0, nx1, u.y);
                
                return noiseValue >= Threshold;
            }
                
            // Sample noise texture
            Color noiseColor;
            
            int x = Mathf.FloorToInt(context.BoundsNormX * noiseTexture.width);
            int y = Mathf.FloorToInt(context.BoundsNormZ * noiseTexture.height);
            
            // Clamp to valid range
            x = Mathf.Clamp(x, 0, noiseTexture.width - 1);
            y = Mathf.Clamp(y, 0, noiseTexture.height - 1);
            
            noiseColor = noiseTexture.GetPixel(x, y);
            
            return noiseColor.grayscale >= Threshold;
        }
    }
}