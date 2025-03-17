using System;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public class MaskFalloff
    {
        [UnityEngine.Range(0.0f, 1.0f)] public float Min;
        [UnityEngine.Range(0.0f, 1.0f)] public float Max = 1.0f;
        
        // Define the range of mask values that will be affected
        [UnityEngine.Header("Mask Range")]
        [UnityEngine.Tooltip("Only mask values between MaskMin and MaskMax will be affected")]
        [UnityEngine.Range(0.0f, 1.0f)] public float MaskMin = 0.0f;
        [UnityEngine.Range(0.0f, 1.0f)] public float MaskMax = 1.0f;
        
        // Define how the inner boundary should be handled
        [UnityEngine.Header("Inner Boundary")]
        [UnityEngine.Tooltip("Controls how smoothly to transition at the high-value boundary (MaskMax)")]
        [UnityEngine.Range(0.0f, 0.5f)] public float InnerFalloff = 0.05f;
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