using UnityEditor;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    public class RectangleShape : StampShape
    {
        public Vector2 Size = new Vector2(10.0f, 10.0f);

        public override void GenerateMask()
        {
            LocalBounds = new Bounds(Vector3.zero, new Vector3(Size.x, 0.0f, Size.y));
            MaskTexture = Texture2D.whiteTexture;
        }

        public void OnDrawGizmosSelected()
        {
            using (new Handles.DrawingScope(Color.blue, transform.localToWorldMatrix))
            {
                Handles.DrawWireCube(Vector3.zero, new Vector3(Size.x, 0.0f, Size.y));
            }
        }
    }
}