using UnityEngine;
using UnityEditor;
using System.IO;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    public static class CircleMaskTextureGenerator
    {
        /// <summary>
        /// Creates a high-quality circular mask texture in memory
        /// </summary>
        private static Texture2D CreateCircleMaskTexture(int resolution)
        {
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true);
            
            float centerX = resolution / 2f;
            float centerY = resolution / 2f;
            // Use exactly half the resolution for proper edge-to-edge coverage
            float maxRadius = resolution / 2f;
            
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    
                    // Simple linear gradient from center (1.0) to edge (0.0)
                    float alpha = Mathf.Clamp01(1.0f - (distance / maxRadius));
                    
                    // Ensure exact values: 1.0 at center, 0.0 at or beyond maxRadius
                    if (distance <= 0.1f) alpha = 1.0f;  // Exactly 1.0 at center
                    if (distance >= maxRadius) alpha = 0.0f;  // Exactly 0.0 at edge
                    
                    Color color = new Color(alpha, alpha, alpha, 1.0f);
                    texture.SetPixel(x, y, color);
                }
            }
            
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            
            return texture;
        }

        //[MenuItem("Tools/World Building/Generate High Quality Circle Mask")]
        public static void GenerateHighQualityCircleMask()
        {
            // Higher resolution for better quality
            const int resolution = 1024;
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true);
            
            float centerX = resolution / 2f;
            float centerY = resolution / 2f;
            // Use exactly half the resolution for proper edge-to-edge coverage
            float maxRadius = resolution / 2f;
            
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    // Calculate distance from center
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    
                    // Simple linear gradient from center (1.0) to edge (0.0)
                    float alpha = Mathf.Clamp01(1.0f - (distance / maxRadius));
                    
                    // Ensure exact values: 1.0 at center, 0.0 at or beyond maxRadius
                    if (distance <= 0.1f) alpha = 1.0f;  // Exactly 1.0 at center
                    if (distance >= maxRadius) alpha = 0.0f;  // Exactly 0.0 at edge
                    
                    // Set pixel color
                    Color color = new Color(alpha, alpha, alpha, 1.0f);
                    texture.SetPixel(x, y, color);
                }
            }
            
            texture.Apply();
            
            // Create directory structure if it doesn't exist
            string assetPath = "Assets/GCG/WorldBuilding/Textures";
            string fullPath = Path.Combine(Application.dataPath, assetPath.Substring(7)); // Remove "Assets/" prefix
            
            if (!Directory.Exists(fullPath))
            {
                try
                {
                    Directory.CreateDirectory(fullPath);
                    Debug.Log("Created directory: " + fullPath);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Failed to create directory: " + e.Message);
                    return;
                }
            }
            
            // Save the texture as an asset
            string texturePath = Path.Combine(assetPath, "HighQualityCircleMask.png");
            string fullTexturePath = Path.Combine(Application.dataPath, texturePath.Substring(7));
            
            try
            {
                byte[] pngData = texture.EncodeToPNG();
                File.WriteAllBytes(fullTexturePath, pngData);
                Debug.Log("Texture saved to: " + fullTexturePath);
                AssetDatabase.Refresh();
                
                // Configure import settings for optimal quality
                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.SingleChannel;
                    importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
                    importer.sRGBTexture = false; // Linear texture for better precision
                    importer.mipmapEnabled = true;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                    Debug.Log("Texture import settings configured");
                }
                else
                {
                    Debug.LogWarning("Could not find importer for texture");
                }
                
                Debug.Log("High quality circle mask generated successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError("Failed to save texture: " + e.Message);
            }
        }
    }
} 