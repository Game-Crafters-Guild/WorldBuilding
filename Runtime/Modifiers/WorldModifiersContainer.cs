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

    // Flag to track if any modifiers have changed enabled state
    public bool HasModifiersEnabledStateChanged { get; private set; }

    public void OnValidate()
    {
        // Remove any null modifiers from the lists
        for (int i = TerrainHeightModifiers.Count - 1; i >= 0; i--)
        {
            if (TerrainHeightModifiers[i] == null)
                TerrainHeightModifiers.RemoveAt(i);
            else if (TerrainHeightModifiers[i].HasEnabledStateChanged())
                HasModifiersEnabledStateChanged = true;
        }

        for (int i = TerrainSplatModifiers.Count - 1; i >= 0; i--)
        {
            if (TerrainSplatModifiers[i] == null)
                TerrainSplatModifiers.RemoveAt(i);
            else if (TerrainSplatModifiers[i].HasEnabledStateChanged())
                HasModifiersEnabledStateChanged = true;
        }

        for (int i = TerrainVegetationModifiers.Count - 1; i >= 0; i--)
        {
            if (TerrainVegetationModifiers[i] == null)
                TerrainVegetationModifiers.RemoveAt(i);
            else if (TerrainVegetationModifiers[i].HasEnabledStateChanged())
                HasModifiersEnabledStateChanged = true;
        }

        for (int i = GameObjectModifiers.Count - 1; i >= 0; i--)
        {
            if (GameObjectModifiers[i] == null)
                GameObjectModifiers.RemoveAt(i);
            else if (GameObjectModifiers[i].HasEnabledStateChanged())
                HasModifiersEnabledStateChanged = true;
        }
    }

    /// <summary>
    /// Reset the changed flag after it's been processed
    /// </summary>
    public void ResetChangedFlag()
    {
        HasModifiersEnabledStateChanged = false;
    }

    /// <summary>
    /// Clean up all modifiers in this container
    /// </summary>
    public void CleanupAllModifiers()
    {
        foreach (var modifier in TerrainHeightModifiers)
            if (modifier != null) modifier.OnCleanup();
            
        foreach (var modifier in TerrainSplatModifiers)
            if (modifier != null) modifier.OnCleanup();
            
        foreach (var modifier in TerrainVegetationModifiers)
            if (modifier != null) modifier.OnCleanup();
            
        foreach (var modifier in GameObjectModifiers)
            if (modifier != null) modifier.OnCleanup();
    }

    /// <summary>
    /// Clean up disabled modifiers
    /// </summary>
    public void CleanupDisabledModifiers()
    {
        foreach (var modifier in TerrainHeightModifiers)
            if (modifier != null && !modifier.Enabled) modifier.OnCleanup();
            
        foreach (var modifier in TerrainSplatModifiers)
            if (modifier != null && !modifier.Enabled) modifier.OnCleanup();
            
        foreach (var modifier in TerrainVegetationModifiers)
            if (modifier != null && !modifier.Enabled) modifier.OnCleanup();
            
        foreach (var modifier in GameObjectModifiers)
            if (modifier != null && !modifier.Enabled) modifier.OnCleanup();
    }
  }
}