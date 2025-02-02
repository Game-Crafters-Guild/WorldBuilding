using UnityEditor;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    public class CircleShape : StampShape
    {
        public float Radius = 10.0f;

        public override void GenerateMask()
        {
            MaskTexture = Texture2D.whiteTexture;
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