using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding.Editor
{
#if UNITY_2023_1_OR_NEWER
    [EditorTool("Create Rectangle Stamp", toolPriority = 11)]
#else
    [EditorTool("Create Rectangle Stamp")]
#endif
    public class CreateRectangleStampTool : BaseStampTool
    {
        private Vector2 m_Size = new Vector2(10f, 10f);
        
        // Size adjustment parameters
        private const float m_SizeAdjustSpeed = 0.5f;
        private const float m_MinSize = 0.5f;
        private const float m_MaxSize = 1000.0f;
        
        // Adjustment mode enum
        private enum AdjustmentMode
        {
            Width,
            Height,
            Uniform,
            Rotation
        }
        
        private AdjustmentMode m_CurrentMode = AdjustmentMode.Width;
        
        public override GUIContent toolbarIcon => m_IconContent;
        
        protected override void OnToolEnable()
        {
            m_IconContent = new GUIContent
            {
                image = EditorGUIUtility.IconContent("RectangleShape Icon").image,
                text = "Create Rectangle Stamp",
                tooltip = "Create a rectangle stamp"
            };
        }
        
        protected override void OnLeftMouseDown()
        {
            CreateRectangleStamp();
        }
        
        protected override bool HandleScrollWheelEvent(Event evt)
        {
            // Only adjust if a modifier key is pressed to avoid conflicts with Unity's navigation
            if (evt.control) // Use only Control check first
            {
                // Fixed: Apply fast multiplier if both Control and Shift are held
                float speed = m_SizeAdjustSpeed;
                if (evt.shift)
                {
                    speed *= m_FastAdjustMultiplier;
                }
                
                float delta = Mathf.Abs(evt.delta.y) > Mathf.Abs(evt.delta.x) ? -evt.delta.y : -evt.delta.x;
                float adjustment = delta * speed;
                
                if (m_CurrentMode == AdjustmentMode.Rotation)
                {
                    // Adjust rotation
                    m_Rotation = (m_Rotation + adjustment * m_RotationAdjustSpeed) % 360f;
                    if (m_Rotation < 0) m_Rotation += 360f;
                }
                else if (m_CurrentMode == AdjustmentMode.Width)
                {
                    // Adjust width
                    m_Size.x = Mathf.Clamp(m_Size.x + adjustment, m_MinSize, m_MaxSize);
                }
                else if (m_CurrentMode == AdjustmentMode.Height)
                {
                    // Adjust height
                    m_Size.y = Mathf.Clamp(m_Size.y + adjustment, m_MinSize, m_MaxSize);
                }
                else if (m_CurrentMode == AdjustmentMode.Uniform)
                {
                    // Adjust both dimensions
                    m_Size.x = Mathf.Clamp(m_Size.x + adjustment, m_MinSize, m_MaxSize);
                    m_Size.y = Mathf.Clamp(m_Size.y + adjustment, m_MinSize, m_MaxSize);
                }
                
                return true; // Event was handled
            }
            
            return false;
        }
        
        protected override bool HandleKeyDownEvent(Event evt)
        {
            bool handled = false;
            
            // Mode selection keys - 1, 2, 3, 4 to select adjustment modes
            if (evt.keyCode >= KeyCode.Alpha1 && evt.keyCode <= KeyCode.Alpha4)
            {
                m_CurrentMode = (AdjustmentMode)(evt.keyCode - KeyCode.Alpha1);
                handled = true;
            }
            // Quick access keys
            else if (evt.keyCode == KeyCode.U || evt.keyCode == KeyCode.G)
            {
                // U for Uniform or G for Global
                m_CurrentMode = AdjustmentMode.Uniform;
                handled = true;
            }
            else if (evt.keyCode == KeyCode.X || evt.keyCode == KeyCode.W)
            {
                // X or W for Width mode
                m_CurrentMode = AdjustmentMode.Width;
                handled = true;
            }
            else if (evt.keyCode == KeyCode.Z || evt.keyCode == KeyCode.H)
            {
                // Z or H for Height mode
                m_CurrentMode = AdjustmentMode.Height;
                handled = true;
            }
            else if (evt.keyCode == KeyCode.R)
            {
                // R for Rotation
                m_CurrentMode = AdjustmentMode.Rotation;
                handled = true;
            }
            // Size/rotation adjustments with keyboard arrows
            else if (evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.DownArrow ||
                     evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.RightArrow)
            {
                float speed = evt.shift ? m_SizeAdjustSpeed * m_FastAdjustMultiplier : m_SizeAdjustSpeed;
                float adjustment = 0;
                
                // Adjustment depends on arrow key direction
                bool isHorizontal = (evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.RightArrow);
                bool isPositive = (evt.keyCode == KeyCode.RightArrow || evt.keyCode == KeyCode.UpArrow);
                
                adjustment = isPositive ? speed : -speed;
                
                if (m_CurrentMode == AdjustmentMode.Rotation)
                {
                    // In rotation mode, any arrow key adjusts rotation
                    m_Rotation = (m_Rotation + adjustment * m_RotationAdjustSpeed) % 360f;
                    if (m_Rotation < 0) m_Rotation += 360f;
                }
                else if (m_CurrentMode == AdjustmentMode.Uniform)
                {
                    // In uniform mode, any arrow key adjusts both dimensions
                    m_Size.x = Mathf.Clamp(m_Size.x + adjustment, m_MinSize, m_MaxSize);
                    m_Size.y = Mathf.Clamp(m_Size.y + adjustment, m_MinSize, m_MaxSize);
                }
                else
                {
                    // In width/height mode, adjust the appropriate dimension based on arrow direction
                    // Horizontal keys primarily adjust width, vertical keys primarily adjust height
                    // But allow all keys to work in all modes
                    if (isHorizontal)
                    {
                        // Left/Right primarily adjusts width
                        if (m_CurrentMode == AdjustmentMode.Width || evt.control)
                        {
                            m_Size.x = Mathf.Clamp(m_Size.x + adjustment, m_MinSize, m_MaxSize);
                        }
                        else if (m_CurrentMode == AdjustmentMode.Height)
                        {
                            // Also allow left/right to work in height mode
                            m_Size.y = Mathf.Clamp(m_Size.y + adjustment, m_MinSize, m_MaxSize);
                        }
                    }
                    else // Vertical arrow keys
                    {
                        // Up/Down primarily adjusts height
                        if (m_CurrentMode == AdjustmentMode.Height || evt.control)
                        {
                            m_Size.y = Mathf.Clamp(m_Size.y + adjustment, m_MinSize, m_MaxSize);
                        }
                        else if (m_CurrentMode == AdjustmentMode.Width)
                        {
                            // Also allow up/down to work in width mode
                            m_Size.x = Mathf.Clamp(m_Size.x + adjustment, m_MinSize, m_MaxSize);
                        }
                    }
                }
                
                handled = true;
            }
            
            return handled;
        }
        
        protected override void DrawPreview()
        {
            // Draw rectangle outline at placement position
            using (new Handles.DrawingScope(Color.green))
            {
                Matrix4x4 transformMatrix = Matrix4x4.TRS(
                    m_PlacementPosition,
                    Quaternion.Euler(0, m_Rotation, 0),
                    Vector3.one
                );
                
                using (new Handles.DrawingScope(transformMatrix))
                {
                    // Calculate rectangle corners
                    Vector3 halfSize = new Vector3(m_Size.x / 2f, 0, m_Size.y / 2f);
                    Vector3[] corners = new Vector3[4];
                    corners[0] = new Vector3(-halfSize.x, 0, -halfSize.z);
                    corners[1] = new Vector3(halfSize.x, 0, -halfSize.z);
                    corners[2] = new Vector3(halfSize.x, 0, halfSize.z);
                    corners[3] = new Vector3(-halfSize.x, 0, halfSize.z);
                    
                    // Draw wire rectangle
                    Handles.color = Color.green;
                    Handles.DrawLine(corners[0], corners[1]);
                    Handles.DrawLine(corners[1], corners[2]);
                    Handles.DrawLine(corners[2], corners[3]);
                    Handles.DrawLine(corners[3], corners[0]);
                    
                    // Draw semi-transparent filled rectangle
                    Color fillColor = new Color(0, 1, 0, 0.2f);
                    Handles.color = fillColor;
                    Handles.DrawAAConvexPolygon(corners);
                    
                    // SWAPPED: Draw dimension lines with colors based on current mode
                    // Now using white for active axis and yellow for normal
                    Color normalColor = Color.yellow;
                    Color activeColor = Color.white;
                    
                    // Draw all edges in yellow first
                    Handles.color = normalColor;
                    Handles.DrawLine(corners[0], corners[1]); // Bottom width
                    Handles.DrawLine(corners[2], corners[3]); // Top width
                    Handles.DrawLine(corners[1], corners[2]); // Right height
                    Handles.DrawLine(corners[3], corners[0]); // Left height
                    
                    // Highlight active edges with white - FIXED to match the correct dimension labels
                    if (m_CurrentMode == AdjustmentMode.Width)
                    {
                        // In width mode, highlight the horizontal edges (along X axis)
                        Handles.color = activeColor;
                        Handles.DrawLine(corners[1], corners[2]); // Right height
                        Handles.DrawLine(corners[3], corners[0]); // Left height
                    }
                    else if (m_CurrentMode == AdjustmentMode.Height)
                    {
                        // In height mode, highlight the vertical edges (along Z axis)
                        Handles.color = activeColor;
                        Handles.DrawLine(corners[0], corners[1]); // Bottom width
                        Handles.DrawLine(corners[2], corners[3]); // Top width
                    }
                    else if (m_CurrentMode == AdjustmentMode.Uniform)
                    {
                        // In uniform mode, highlight all edges
                        Handles.color = activeColor;
                        Handles.DrawLine(corners[0], corners[1]);
                        Handles.DrawLine(corners[1], corners[2]);
                        Handles.DrawLine(corners[2], corners[3]);
                        Handles.DrawLine(corners[3], corners[0]);
                    }
                    
                    // If in rotation mode, draw a rotation handle
                    if (m_CurrentMode == AdjustmentMode.Rotation)
                    {
                        Handles.color = activeColor;
                        float size = Mathf.Min(Mathf.Abs(m_Size.x), Mathf.Abs(m_Size.y)) * 0.25f;
                        Vector3 cameraRight = SceneView.lastActiveSceneView.camera.transform.right;
                        cameraRight.y = 0.0f;
                        cameraRight = Quaternion.AngleAxis(-m_Rotation, Vector3.up) * cameraRight;
                        Vector3 direction = cameraRight * size;
                        
                        Handles.DrawLine(Vector3.zero, direction);
                        Handles.DrawWireArc(Vector3.zero, Vector3.up, direction, m_Rotation, size);
                    }
                }
                
                // Calculate the text position to appear just above the placement position
                Camera camera = SceneView.lastActiveSceneView.camera;
                Vector3 textPosition = CalculateScreenSpaceTextPosition(camera, m_PlacementPosition);
                
                // Show the dimensions and mode as text
                GUIStyle style = CreateLabelStyle();
                
                string modeText = "";
                string dimensionsText = "";
                
                switch (m_CurrentMode)
                {
                    case AdjustmentMode.Width: modeText = "Width Mode"; break;
                    case AdjustmentMode.Height: modeText = "Height Mode"; break;
                    case AdjustmentMode.Uniform: modeText = "Uniform Mode"; break;
                    case AdjustmentMode.Rotation: modeText = "Rotation Mode"; break;
                }
                
                dimensionsText = $"Width: {m_Size.x:F1}  Height: {m_Size.y:F1}  Rotation: {m_Rotation:F0}°";
                
                // Create a better formatted shortcuts section with each shortcut on a new line
                string shortcutsText = "Shortcuts:\n" +
                                      "G/U/1: Uniform\n" +
                                      "X/W/2: Width\n" +
                                      "Z/H/3: Height\n" +
                                      "R/4: Rotation\n" +
                                      "CTRL+Scroll: Adjust\n" +
                                      "CTRL+Shift+Scroll: Fast Adjust\n" +
                                      "Shift+Arrows: Fast Adjust";
                
                Handles.Label(textPosition, $"{modeText}\n{dimensionsText}\n\n{shortcutsText}", style);
            }
        }
        
        private void CreateRectangleStamp()
        {
            // Create a new GameObject for the rectangle stamp
            GameObject stampObject = new GameObject("Rectangle Stamp");
            stampObject.transform.position = m_PlacementPosition;
            stampObject.transform.rotation = Quaternion.Euler(0, m_Rotation, 0);
            
            // Add the RectangleShape component
            RectangleShape rectangleShape = stampObject.AddComponent<RectangleShape>();
            rectangleShape.Size = m_Size;
            
            // Add the Stamp component
            Stamp stamp = stampObject.AddComponent<Stamp>();
            
            // Set the shape on the stamp
            stamp.Shape = rectangleShape;
            
            // Generate the mask
            rectangleShape.GenerateMask();
            
            // Select the created object
            Selection.activeGameObject = stampObject;
        }
    }
}
