using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

[Serializable]
public class SplineCache : IEnumerable<SplinePositionsData>
{
    //[SerializeReference] internal SplineContainer m_SplineContainer;

    //[SerializeField][HideInInspector]
    private List<SplinePositionsData> m_SplinePositions = new List<SplinePositionsData>();

    public void BakePath(SplineContainer splineContainer, float resolution)
    {
        m_SplinePositions.Clear();
        foreach (var spline in splineContainer.Splines)
        {
            SplinePositionsData data = new SplinePositionsData();
            data.Spline = spline;
            data.BakePath(resolution);
            m_SplinePositions.Add(data);
        }  
    }
    public void BakeRegion(SplineContainer splineContainer, float resolution)
    {
        m_SplinePositions.Clear();
        foreach (var spline in splineContainer.Splines)
        {
            SplinePositionsData data = new SplinePositionsData();
            data.Spline = spline;
            data.BakeRegion(resolution);
            m_SplinePositions.Add(data);
        }  
    }

    /*public void RemoveStaleSplines()
    {
        // Remove stale splines.
        for (int i = m_SplinePositions.Count - 1; i >= 0; i--)
        {
            bool found = false;
            foreach (var spline in m_SplineContainer.Splines)
            {
                if (m_SplinePositions[i].Spline == spline)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                m_SplinePositions.RemoveAt(i);
            }
        }
    }
    public void OnBeforeSerialize()
    {
        if (m_SplineContainer == null) return;
        RemoveStaleSplines();
        
        // Order splines according to m_SplineContainer.
        int index = 0;
        foreach (var spline in m_SplineContainer.Splines)
        {
            if (m_SplinePositions.Count >= index)
            {
                var splinePositionsData = new SplinePositionsData() { Spline = spline };
                m_SplinePositions.Add(splinePositionsData);
            }
            else if (m_SplinePositions[index].Spline != spline)
            {
                Debug.Log("Spline out of order");
                bool found = false;
                for (int i = index; i < m_SplinePositions.Count; i++)
                {
                    if (m_SplinePositions[i].Spline == spline)
                    {
                        (m_SplinePositions[index], m_SplinePositions[i]) = (m_SplinePositions[i], m_SplinePositions[index]);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    var splinePositionsData = new SplinePositionsData() { Spline = spline };
                    var temp = m_SplinePositions[index];
                    m_SplinePositions[index] = splinePositionsData;
                    m_SplinePositions.Add(temp);
                }
            }
            ++index;
        }

    }

    public void OnAfterDeserialize()
    {
        if (m_SplineContainer == null) return;
        int index = 0;
        foreach (var spline in m_SplineContainer.Splines)
        {
            m_SplinePositions[index].Spline = spline;
            ++index;
        }
    }

    public void Add(SplinePositionsData data)
    {
        m_SplinePositions.Add(data);
    }*/

    public IEnumerator<SplinePositionsData> GetEnumerator()
    {
        //RemoveStaleSplines();
        return m_SplinePositions.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        //RemoveStaleSplines();
        return m_SplinePositions.GetEnumerator();
    }
}
