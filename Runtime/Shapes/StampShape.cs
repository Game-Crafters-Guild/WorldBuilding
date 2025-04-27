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

        public Bounds LocalBounds
        {
            protected set => m_LocalBounds = value;
            get => m_LocalBounds;
        }

        // Bounds used specifically for mask generation UV mapping
        // Subclasses like SplineAreaShape can override this if their mask generation uses different bounds
        public virtual Vector3 MaskGenerationBoundsMin => m_LocalBounds.min;
        public virtual Vector3 MaskGenerationBoundsSize => m_LocalBounds.size;

        public virtual Bounds WorldBounds
        {
            get
            {
                // Calculate accurate world AABB by transforming corners
                Bounds localBounds = this.LocalBounds;
                if (localBounds.size == Vector3.zero) return new Bounds(transform.position, Vector3.zero);

                Vector3 center = localBounds.center;
                Vector3 extents = localBounds.extents;
                Matrix4x4 matrix = transform.localToWorldMatrix;

                // Transform all 8 corners to world space
                Vector3[] corners = new Vector3[8] {
                    matrix.MultiplyPoint3x4(center + new Vector3(extents.x, extents.y, extents.z)),
                    matrix.MultiplyPoint3x4(center + new Vector3(extents.x, extents.y, -extents.z)),
                    matrix.MultiplyPoint3x4(center + new Vector3(extents.x, -extents.y, extents.z)),
                    matrix.MultiplyPoint3x4(center + new Vector3(extents.x, -extents.y, -extents.z)),
                    matrix.MultiplyPoint3x4(center + new Vector3(-extents.x, extents.y, extents.z)),
                    matrix.MultiplyPoint3x4(center + new Vector3(-extents.x, extents.y, -extents.z)),
                    matrix.MultiplyPoint3x4(center + new Vector3(-extents.x, -extents.y, extents.z)),
                    matrix.MultiplyPoint3x4(center + new Vector3(-extents.x, -extents.y, -extents.z))
                };

                // Find min and max world coordinates among corners
                Vector3 min = corners[0];
                Vector3 max = corners[0];
                for (int i = 1; i < 8; i++)
                {
                    min = Vector3.Min(min, corners[i]);
                    max = Vector3.Max(max, corners[i]);
                }

                // Create AABB from min/max
                Bounds worldAABB = new Bounds();
                worldAABB.SetMinMax(min, max);
                return worldAABB;
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