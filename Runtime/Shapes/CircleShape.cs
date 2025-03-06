using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    public class CircleShape : StampShape
    {
        public float Radius = 50.0f;

        public override void GenerateMask()
        {
            if (MaskTexture == null)
            {
                MaskTexture = Resources.Load<Texture2D>($"GameCraftersGuild/WorldBuilding/CircleMask");
            }
            float Diameter = Radius * 2.0f;
            LocalBounds = new Bounds(Vector3.zero, new Vector3(Diameter, 0.0f, Diameter));
        }

#if UNITY_EDITOR
        public void OnDrawGizmosSelected()
        {
            using (new UnityEditor.Handles.DrawingScope(Color.blue, transform.localToWorldMatrix))
            {
                UnityEditor.Handles.DrawWireDisc(Vector3.zero, transform.up, Radius);
            }
        }
#endif
    }
}