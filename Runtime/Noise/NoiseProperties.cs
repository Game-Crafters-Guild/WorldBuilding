using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Serialization;

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
    [Range(1, 10000000)]
    public uint Seed = 1;
    [Min(0.01f)]
    public float NoiseScale = 10.0f;
    public Vector2 NoiseOffset;
    [Range(4, 2048)]
    public int NoiseResolution = 64;
    [Range(1, 12)]
    public int NumOctaves = 4;
    [Range(0.0f, 1.0f)]
    public float Persistence = 0.5f;
    [Min(1.0f)]
    public float Lacunarity = 2.0f;
    [Min(0.0f)]
    public float HeightMin = 0.0f;
    //[Range(0.0f, 1.0f)]
    [Min(0.0f)]
    public float HeightMax = 10.0f;

    [SerializeField][HideInInspector]
    private bool m_IsDirty;
    public bool IsDirty
    {
        get => m_IsDirty;
        set => m_IsDirty = value; 
    }
}
