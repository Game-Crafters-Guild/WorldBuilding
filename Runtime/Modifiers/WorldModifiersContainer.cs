using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
  [Serializable]
  public class WorldModifiersContainer
  {
    [SerializeReference] public List<ITerrainHeightModifier> TerrainHeightModifiers = new();
    [SerializeReference] public List<ITerrainSplatModifier> TerrainSplatModifiers = new();
    [SerializeReference] public List<ITerrainVegetationModifier> TerrainVegetationModifiers = new();
    [SerializeReference] public List<IGameObjectModifier> GameObjectModifiers = new();

    public void OnValidate()
    {
        // Remove any null modifiers from the lists
        for (int i = TerrainHeightModifiers.Count - 1; i >= 0; i--)
        {
            if (TerrainHeightModifiers[i] == null)
                TerrainHeightModifiers.RemoveAt(i);
        }

        for (int i = TerrainSplatModifiers.Count - 1; i >= 0; i--)
        {
            if (TerrainSplatModifiers[i] == null)
                TerrainSplatModifiers.RemoveAt(i);
        }

        for (int i = TerrainVegetationModifiers.Count - 1; i >= 0; i--)
        {
            if (TerrainVegetationModifiers[i] == null)
                TerrainVegetationModifiers.RemoveAt(i);
        }

        for (int i = GameObjectModifiers.Count - 1; i >= 0; i--)
        {
            if (GameObjectModifiers[i] == null)
                GameObjectModifiers.RemoveAt(i);
        }
    }
  }
}