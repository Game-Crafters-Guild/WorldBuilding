using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    public class RectangleShape : StampShape
    {
        public Vector2 Size = new Vector2(100.0f, 100.0f);

        public override void GenerateMask()
        {
            LocalBounds = new Bounds(Vector3.zero, new Vector3(Size.x, 0.0f, Size.y));
            if (MaskTexture == null)
            {
                MaskTexture = Resources.Load<Texture2D>($"GameCraftersGuild/WorldBuilding/SquareMask");
            }
        }
    }
}