using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WorldModifiersContainer
{
  [SerializeReference]
  public List<ITerrainHeightModifier> TerrainHeightModifiers = new ();
  
  [SerializeReference]
  public List<ITerrainSplatModifier> TerrainSplatModifiers = new ();
  
  [SerializeReference]
  public List<IGameObjectModifier> GameObjectModifiers = new ();
}
