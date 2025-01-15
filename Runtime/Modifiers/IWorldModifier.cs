using System;
using UnityEngine;

[Serializable]
public abstract class WorldModifier
{
    protected string GetFilePath([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        path;

    public abstract string FilePath { get; }
}
