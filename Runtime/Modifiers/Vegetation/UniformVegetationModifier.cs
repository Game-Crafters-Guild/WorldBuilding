using System;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public class UniformVegetationModifier : ITerrainVegetationModifier
    {
        public override string FilePath => GetFilePath();
        
        public override int GetNumPrototypes()
        {
            return 0;
        }

        public override object GetPrototype(int index)
        {
            return null;
        }
    }
}
