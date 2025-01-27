using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Stamp : BaseWorldBuilder
{
    protected override Texture MaskTexture => Texture2D.whiteTexture;
    private static List<Terrain> m_ActiveTerrains = new List<Terrain>();
    public enum Volume
    {
        Global,
        Circle,
        Rect
    }
    [SerializeField]
    private Volume m_Volume = Volume.Global; 
    public Volume VolumeType { get => m_Volume; set => m_Volume = value; }
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
            bounds.center = new Vector3(bounds.center.x, transform.position.y, bounds.center.z);
            bounds.size = new Vector3(bounds.size.x, 0.0f, bounds.size.z);
            return bounds;
        }
    }
}
