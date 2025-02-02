using System.Collections.Generic;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    public class GlobalShape : StampShape
    {
        public override Texture MaskTexture => Texture2D.whiteTexture;
        private static List<Terrain> m_ActiveTerrains = new List<Terrain>();

        public override void GenerateMask()
        {
            MaskTexture = Texture2D.whiteTexture;
        }

        public override Bounds WorldBounds
        {
            get
            {
                if (m_ActiveTerrains == null)
                {
                    m_ActiveTerrains = new List<Terrain>();
                }

                Terrain.GetActiveTerrains(m_ActiveTerrains);
                if (m_ActiveTerrains.Count == 0)
                {
                    return new Bounds();
                }

                Terrain terrain = m_ActiveTerrains[0];
                Bounds bounds = new Bounds(terrain.terrainData.bounds.center + terrain.transform.position,
                    terrain.terrainData.bounds.size);
                for (int i = 1; i < m_ActiveTerrains.Count; i++)
                {
                    terrain = m_ActiveTerrains[i];
                    Bounds terrainBounds = new Bounds(terrain.terrainData.bounds.center + terrain.transform.position,
                        terrain.terrainData.bounds.size);
                    bounds.Encapsulate(terrainBounds);
                }

                bounds.center = new Vector3(bounds.center.x, transform.position.y, bounds.center.z);
                bounds.size = new Vector3(bounds.size.x, 0.0f, bounds.size.z);
                return bounds;
            }
        }
    }
}