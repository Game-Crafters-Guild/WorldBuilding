using System;
using UnityEngine;

[Serializable]
public class MaskFalloff
{
    [Range(0.0f, 1.0f)] public float Min = 0.0f;
    [Range(0.0f, 1.0f)] public float Max = 0.25f;
}
