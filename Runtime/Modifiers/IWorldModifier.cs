using System;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public abstract class WorldModifier
    {
        public bool Enabled = true;

        protected string GetFilePath([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
            path;

        public abstract string FilePath { get; }
    }
}