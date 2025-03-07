using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable, ExecuteInEditMode]
    public class Stamp : MonoBehaviour, IWorldBuilder
    {
        public float4x4 TransformMatrix { get; set; }
        [SerializeField] private int m_Priority = 0;

        public int Priority
        {
            get => m_Priority;
            set => m_Priority = value;
        }

        [SerializeField, HideInInspector] private bool m_IsDirty = true;

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

        [SerializeField] public WorldModifiersContainer m_Modifiers = new WorldModifiersContainer();
        public List<ITerrainSplatModifier> TerrainSplatModifiers => m_Modifiers.TerrainSplatModifiers;
        public List<ITerrainVegetationModifier> TerrainVegetationModifiers => m_Modifiers.TerrainVegetationModifiers;
        public List<IGameObjectModifier> GameObjectModifiers => m_Modifiers.GameObjectModifiers;

        [HideInInspector] [SerializeField] private StampShape m_Shape;

        public StampShape Shape
        {
            get => m_Shape;
            set => m_Shape = value;
        }

        protected Texture MaskTexture => m_Shape.MaskTexture;
        public SplineContainer SplineContainer => m_Shape.SplineContainer;

        public bool ContainsSplineData(SplineData<float> splineData)
        {
            return m_Shape.ContainsSplineData(splineData);
        }

        public Bounds WorldBounds => m_Shape.WorldBounds;

        protected Stamp()
        {
            m_Modifiers.TerrainHeightModifiers.Add(new ApplyTransformToHeightmap());
        }

        private void OnDestroy()
        {
            // Clean up all modifiers
            m_Modifiers.CleanupAllModifiers();
            
            // Remove from world building system
            WorldBuildingSystem worldBuildingSystem = WorldBuildingSystem.FindSystemInScene();
            if (worldBuildingSystem != null)
            {
                worldBuildingSystem.RemoveWorldBuilder(this);
            }
        }

        private void OnEnable()
        {
            TransformMatrix = transform.localToWorldMatrix;
            if (m_Modifiers == null)
            {
                m_Modifiers = new WorldModifiersContainer();
            }

            WorldBuildingSystem.GetOrCreate().AddWorldBuilder(this);
        }

        public void OnDisable()
        {
            // Clean up all modifiers
            m_Modifiers.CleanupAllModifiers();
            
            // Remove from world building system
            WorldBuildingSystem worldBuildingSystem = WorldBuildingSystem.FindSystemInScene();
            if (worldBuildingSystem != null)
            {
                worldBuildingSystem.RemoveWorldBuilder(this);
            }
        }

        private void Awake()
        {
            if (m_Shape != null) return;
            if (TryGetComponent(out m_Shape) == false)
            {
                if (TryGetComponent<SplineContainer>(out var splineContainer))
                {
                    if (splineContainer.Spline.Closed)
                    {
                        m_Shape = gameObject.AddComponent<SplineAreaShape>();    
                    }
                    else
                    {
                        m_Shape = gameObject.AddComponent<SplinePathShape>();
                    }
                }
                else
                {
                    m_Shape = gameObject.AddComponent<CircleShape>();
                }
            }
        }

        private void OnValidate()
        {
            m_Modifiers.OnValidate();
            
            // If modifiers enabled state changed, mark as dirty to trigger regeneration
            if (m_Modifiers.HasModifiersEnabledStateChanged)
            {
                m_IsDirty = true;
                
                // Reset the changed flag after processing
                m_Modifiers.ResetChangedFlag();
            }
        }

        public void GenerateMask()
        {
            m_Shape.GenerateMask();
        }

        public virtual void ApplyHeights(WorldBuildingContext context)
        {
            context.MaskFalloff = new MaskFalloff();
            context.MaintainMaskAspectRatio = m_Shape.MaintainMaskAspectRatio;
            foreach (var heightModifier in m_Modifiers.TerrainHeightModifiers)
            {
                if (!heightModifier.Enabled) continue;
                heightModifier.ApplyHeightmap(context, this.WorldBounds, MaskTexture);
            }
        }

        public virtual void ApplySplatmap(WorldBuildingContext context)
        {
            context.MaskFalloff = new MaskFalloff();
            context.MaintainMaskAspectRatio = m_Shape.MaintainMaskAspectRatio;
            foreach (var splatModifier in m_Modifiers.TerrainSplatModifiers)
            {
                if (!splatModifier.Enabled) continue;
                splatModifier.ApplySplatmap(context, WorldBounds, MaskTexture);
            }
        }

        public void GenerateVegetation(WorldBuildingContext context)
        {
            context.MaskFalloff = new MaskFalloff();
            context.MaintainMaskAspectRatio = m_Shape.MaintainMaskAspectRatio;
            foreach (var vegetationModifier in m_Modifiers.TerrainVegetationModifiers)
            {
                if (!vegetationModifier.Enabled) continue;
                vegetationModifier.ApplyVegetation(context, this.WorldBounds, MaskTexture);
            }
        }

        public virtual void SpawnGameObjects(WorldBuildingContext context)
        {
            context.MaintainMaskAspectRatio = m_Shape.MaintainMaskAspectRatio;
            
            // Apply all GameObject modifiers
            foreach (var modifier in m_Modifiers.GameObjectModifiers)
            {
                if (!modifier.Enabled)
                    continue;
                    
                if (modifier is GameObjectModifier gameObjectModifier)
                {
                    gameObjectModifier.SpawnGameObjects(context, WorldBounds, MaskTexture);
                }
            }
        }
    }
}