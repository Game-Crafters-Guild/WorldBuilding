using System;
using UnityEngine;

[Serializable]
public class MaskFalloff
{
    [Range(0.0f, 1.0f)] public float Min = 0.75f;
    [Range(0.0f, 1.0f)] public float Max = 1.0f;
}
