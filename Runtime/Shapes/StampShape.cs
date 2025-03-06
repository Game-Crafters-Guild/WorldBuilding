using UnityEngine;
using UnityEngine.Splines;

namespace GameCraftersGuild.WorldBuilding
{
    public abstract class StampShape : MonoBehaviour
    {
        [SerializeField, HideInInspector] private Bounds m_LocalBounds;

        [SerializeField, HideInInspector] private Texture2D m_MaskTexture;

        public virtual Texture2D MaskTexture
        {
            get => m_MaskTexture;
            protected set => m_MaskTexture = value;
        }

        public virtual bool MaintainMaskAspectRatio { get; } = true;

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

        public virtual SplineContainer SplineContainer => null;

        public virtual bool ContainsSplineData(SplineData<float> splineData)
        {
            return false;
        }

        public abstract void GenerateMask();
    }
}