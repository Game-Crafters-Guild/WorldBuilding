using System;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public class MaskFalloff
    {
        [UnityEngine.Range(0.0f, 1.0f)] public float Min;
        [UnityEngine.Range(0.0f, 1.0f)] public float Max = 1.0f;
    }

    public enum FalloffType
    {
        Linear,
        Smoothstep,
        EaseIn,
        EaseOut,
        SmoothEaseInOut
    }
} 