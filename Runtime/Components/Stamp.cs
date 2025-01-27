using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Stamp : BaseWorldBuilder
{
    protected override Texture MaskTexture => Texture2D.whiteTexture;
    static private List<Terrain> m_ActiveTerrains = new List<Terrain>();
    public enum Volume
    {
        Global,
        Circle,
        Rect
    }
    public override void GenerateMask()
    {
        
    }
    
    public override Bounds WorldBounds
    {
        get
        {
            Bounds bounds = new Bounds();
            if (m_ActiveTerrains == null)
            {
                m_ActiveTerrains = new List<Terrain>();
            }
            Terrain.GetActiveTerrains(m_ActiveTerrains);
            foreach (var terrain in m_ActiveTerrains)
            {
                Bounds terrainBounds = new Bounds(terrain.terrainData.bounds.center + terrain.transform.position, terrain.terrainData.bounds.size);
                bounds.Encapsulate(terrainBounds);
            }
            return bounds;
        }
    }
}
