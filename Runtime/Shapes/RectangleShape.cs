using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace GameCraftersGuild.WorldBuilding
{
    public class RectangleShape : StampShape
    {
        public Vector2 Size = new Vector2(10.0f, 10.0f);

        public override void GenerateMask()
        {
            LocalBounds = new Bounds(Vector3.zero, new Vector3(Size.x, 0.0f, Size.y));
            if (MaskTexture == null)
            {
                MaskTexture = Resources.Load<Texture2D>($"GameCraftersGuild/WorldBuilding/SquareMask");
            }
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