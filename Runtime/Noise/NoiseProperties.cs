using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public class NoiseProperties
    {
        public enum NoiseFunction
        {
            Perlin,
            Simplex,
            Cellular,
        }

        public NoiseFunction NoiseType;
        [Range(1, 10000000)] public uint Seed = 1;
        [Range(1, 20)] public float NoiseScale = 10.0f;
        public Vector2 NoiseOffset;
        [Range(1, 12)] public int NumOctaves = 1;
        [Range(0.0f, 1.0f)] public float Persistence = 0.5f;
        [Min(1.0f)] public float Lacunarity = 1.0f;

        [Min(0.0f)] public float HeightMin = 0.0f;

        //[Range(0.0f, 1.0f)]
        [Min(0.0f)] public float HeightMax = 10.0f;

        [FormerlySerializedAs("NoiseResolution")]
        [Range(32, 2048)]
        [Header("High texture resolutions will be more expensive to update in real-time.")]
        public int NoiseTextureResolution = 200;

        [HideInInspector] [SerializeField] internal Texture2D m_NoiseTexture;

        public Texture2D NoiseTexture
        {
            get
            {
                if (m_NoiseTexture == null || IsDirty)
                {
                    var noiseMap = NoiseGenerator.FromNoiseProperties(this, Allocator.Temp);
                    m_NoiseTexture =
                        NoiseGenerator.GenerateNoiseTexture(noiseMap,
                            new int2(NoiseTextureResolution, NoiseTextureResolution), m_NoiseTexture);
                    noiseMap.Dispose();
                    IsDirty = false;
                }

                return m_NoiseTexture;
            }
            internal set => m_NoiseTexture = value;
        }

        [SerializeField] [HideInInspector] private bool m_IsDirty;

        public bool IsDirty
        {
            get => m_IsDirty;
            set => m_IsDirty = value;
        }
    }
}