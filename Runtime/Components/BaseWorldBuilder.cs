using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

[ExecuteInEditMode]
public abstract class BaseWorldBuilder : MonoBehaviour, IWorldBuilder
{
    public float4x4 TransformMatrix { get; set; }

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
    public virtual Bounds WorldBounds
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
            return m_IsDirty;
        }
        set => m_IsDirty = value;
    }

    [HideInInspector]
    [SerializeField] private bool m_IsDirty = true;

    private void OnDestroy()
    {
        WorldBuildingSystem worldBuildingSystem = WorldBuildingSystem.FindSystemInScene();
        if (worldBuildingSystem != null)
        {
            worldBuildingSystem.RemoveWorldBuilder(this);
        }
    }

    protected BaseWorldBuilder()
    {
        m_Modifiers.TerrainHeightModifiers.Add(new ApplyTransformToHeightmap());
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
    public virtual void ApplyHeights(WorldBuildingContext context)
    {
        context.MaskFalloff = new MaskFalloff();
        foreach (var heightModifier in m_Modifiers.TerrainHeightModifiers)
        {
            heightModifier.ApplyHeightmap(context, this.WorldBounds, MaskTexture);
        }
    }

    public virtual void ApplySplatmap(WorldBuildingContext context)
    {
        context.MaskFalloff = new MaskFalloff();
        foreach (var splatModifier in m_Modifiers.TerrainSplatModifiers)
        {
            splatModifier.ApplySplatmap(context, WorldBounds, MaskTexture);
        }
    }

    public virtual void SpawnGameObjects(WorldBuildingContext context)
    {
        
    }

    public abstract void GenerateMask();
    protected abstract Texture MaskTexture { get; }
    public List<ITerrainSplatModifier> TerrainSplatModifiers => m_Modifiers.TerrainSplatModifiers;
    public virtual bool ContainsSplineData(SplineData<float> splineData)
    {
        return false;
    }

    /*public void AddModifier(IWorldModifier modifier)
    {
        m_Modifiers.Add(modifier);
        IsDirty = true;
    }*/
}
