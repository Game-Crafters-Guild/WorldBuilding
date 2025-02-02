using UnityEditor;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    public class CircleShape : StampShape
    {
        public float Radius = 10.0f;

        public override void GenerateMask()
        {
            if (MaskTexture == null)
            {
                MaskTexture = Resources.Load<Texture2D>($"GameCraftersGuild/WorldBuilding/CircleMask");
            }
            float Diameter = Radius * 2.0f;
            LocalBounds = new Bounds(Vector3.zero, new Vector3(Diameter, 0.0f, Diameter));
        }

        public void OnDrawGizmosSelected()
        {
            using (new Handles.DrawingScope(Color.blue, transform.localToWorldMatrix))
            {
                Handles.DrawWireDisc(Vector3.zero, transform.up, Radius);
            }
        }
    }
}