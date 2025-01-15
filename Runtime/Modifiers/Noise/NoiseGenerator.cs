using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public static class NoiseGenerator
{
    public static NativeArray<float> FromNoiseProperties(NoiseProperties noiseProperties, Allocator allocator, bool isMainThread = true)
    {
        float scale = noiseProperties.NoiseScale;
        if (scale <= 0)
        {
            scale = 0.0001f;
            Debug.LogWarning("Scale must be greater than 0.");
        }
        Random random = new Random(noiseProperties.Seed);
        NativeArray<float2> octaveOffsets = new NativeArray<float2>(noiseProperties.NumOctaves, isMainThread ? Allocator.Temp : Allocator.TempJob);
        for (int i = 0; i < noiseProperties.NumOctaves; i++)
        {
            octaveOffsets[i] = random.NextFloat2(-100000, 100000) + (float2)noiseProperties.NoiseOffset;
        }
        
        NativeArray<float> noiseMap = new NativeArray<float>(noiseProperties.NoiseResolution * noiseProperties.NoiseResolution, allocator);
        float minNoiseValue = float.MaxValue;
        float maxNoiseValue = float.MinValue;
        float halfWidth = noiseProperties.NoiseResolution / 2f;
        float halfHeight = noiseProperties.NoiseResolution / 2f;
        for (int x = 0; x < noiseProperties.NoiseResolution; ++x)
        {
            for (int y = 0; y < noiseProperties.NoiseResolution; ++y)
            {
                float amplitude = 1.0f;
                float frequency = 1.0f;
                float noiseHeight = 0.0f;
                float2 uv = new float2((x - halfWidth) / noiseProperties.NoiseResolution * scale, (y - halfHeight) / noiseProperties.NoiseResolution * scale);
                int index = x * noiseProperties.NoiseResolution + y;
                for (int i = 0; i < noiseProperties.NumOctaves; ++i)
                {
                    float2 sampleUV = uv * frequency + octaveOffsets[i];
                    float noiseValue = 0.0f;
                    switch (noiseProperties.NoiseType)
                    {
                        case NoiseProperties.NoiseFunction.Perlin:
                            noiseValue = noise.cnoise(sampleUV);
                            break;
                        
                        case NoiseProperties.NoiseFunction.Cellular:
                            noiseValue = noise.cellular(sampleUV).x;
                            break;
                        
                        case NoiseProperties.NoiseFunction.Simplex:
                            noiseValue = noise.snoise(sampleUV);
                            break;
                    }
                    noiseHeight += noiseValue * amplitude;
                    
                    amplitude *= noiseProperties.Persistence;
                    frequency *= noiseProperties.Lacunarity;
                }
                noiseMap[index] = noiseHeight;
                if (noiseHeight < minNoiseValue)
                {
                    minNoiseValue = noiseHeight;
                }
                else if (noiseHeight > maxNoiseValue)
                {
                    maxNoiseValue = noiseHeight;
                }
            }
        }
        octaveOffsets.Dispose();
        
        // Normalize the noise.
        for (int i = 0; i < noiseMap.Length; ++i)
        {
            noiseMap[i] = math.unlerp(minNoiseValue, maxNoiseValue, noiseMap[i]);
        }
        return noiseMap;
    }
    
    public static Texture2D GenerateNoiseTexture(NativeArray<float> noiseMap, int2 resolution, Texture2D texture = null)
    {
        Color[] colors = new Color[noiseMap.Length];
        for (int i = 0; i < noiseMap.Length; ++i) {
            colors[i] = Color.Lerp(Color.black, Color.white, noiseMap[i]);
        }

        if (texture == null)
        {
            texture = new Texture2D(resolution.x, resolution.y);
        }
        else if (texture.width != resolution.x || texture.height != resolution.y)
        {
            texture.Reinitialize(resolution.x, resolution.y);
        }

        texture.SetPixels(colors);
        texture.Apply();
        
        return texture;
    }
}
