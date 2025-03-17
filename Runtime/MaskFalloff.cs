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