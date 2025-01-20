using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

[ExecuteInEditMode]
public abstract class BaseWorldBuilder : MonoBehaviour, IWorldBuilder
{
    public float4x4 TransformMatrix { get; set; }
    public float3 Scale { get; set; }
    public Quaternion Rotation { get; set; }

    public int Priority
    {
        get => m_Priority;
        set => m_Priority = value;
    }
    [SerializeField]
    private int m_Priority = 0;

    [SerializeField]
    public WorldModifiersContainer m_Modifiers = new WorldModifiersContainer();
    
    [SerializeField][HideInInspector]
    private Bounds m_LocalBounds;

    protected Bounds LocalBounds
    {
        set => m_LocalBounds = value;
    }
    public Bounds WorldBounds
    {
        get
        {
            Bounds bounds = m_LocalBounds;
            bounds.center = transform.TransformPoint(bounds.center);
            return bounds;
        }
    }

    public bool IsDirty
    {
        get
        {
            if (!transform.hasChanged) return m_IsDirty;
            m_IsDirty = true;
            transform.hasChanged = false;
            TransformMatrix = transform.localToWorldMatrix;
            Scale = transform.lossyScale;
            Rotation = transform.rotation;
            return m_IsDirty;
        }
        set => m_IsDirty = value;
    }

    [SerializeField] private bool m_IsDirty = true;

    private void OnDestroy()
    {
        WorldBuildingSystem worldBuildingSystem = WorldBuildingSystem.FindSystemInScene();
        if (worldBuildingSystem != null)
        {
            worldBuildingSystem.RemoveWorldBuilder(this);
        }
    }

    protected virtual void OnEnable()
    {
        TransformMatrix = transform.localToWorldMatrix;
        if (m_Modifiers == null)
        {
            m_Modifiers = new WorldModifiersContainer();
        }
        WorldBuildingSystem.GetOrCreate().AddWorldBuilder(this);
    }

    protected virtual void OnDisable()
    {
        WorldBuildingSystem worldBuildingSystem = WorldBuildingSystem.FindSystemInScene();
        if (worldBuildingSystem != null)
        {
            worldBuildingSystem.RemoveWorldBuilder(this);
        }
    }

    public virtual SplineContainer SplinContainer => null;
    public abstract void ApplyHeights(WorldBuildingContext context);
    public abstract void ApplySplatmap(WorldBuildingContext context);
    public abstract void SpawnGameObjects(WorldBuildingContext context);
    public abstract void GenerateMask();
    public List<ITerrainSplatModifier> TerrainSplatModifiers => m_Modifiers.TerrainSplatModifiers;

    /*public void AddModifier(IWorldModifier modifier)
    {
        m_Modifiers.Add(modifier);
        IsDirty = true;
    }*/
}
