using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding.Editor
{
#if UNITY_2023_1_OR_NEWER
    [EditorTool("Create Circle Stamp", toolPriority = 10)]
#else
    [EditorTool("Create Circle Stamp")]
#endif
    public class CreateCircleStampTool : BaseStampTool
    {
        private float m_Radius = 5f;
        private Vector2 m_Scale = Vector2.one; // X and Z scale factors
        
        // Adjustment parameters
        private const float m_RadiusAdjustSpeed = 0.5f;
        private const float m_MinRadius = 0.5f;
        private const float m_MaxRadius = 1000.0f;
        private const float m_MinScale = 0.1f;
        private const float m_MaxScale = 10f;
        
        // Adjustment mode enum
        private enum AdjustmentMode
        {
            Uniform,
            XScale,
            ZScale,
            Rotation
        }
        
        private AdjustmentMode m_CurrentMode = AdjustmentMode.Uniform;
        
        protected override void OnToolEnable()
        {
            m_IconContent = new GUIContent
            {
                image = EditorGUIUtility.IconContent("CircleShape Icon").image,
                text = "Create Circle Stamp",
                tooltip = "Create a circle stamp"
            };
        }
        
        protected override void OnLeftMouseDown()
        {
            CreateCircleStamp();
        }
        
        protected override bool HandleScrollWheelEvent(Event evt)
        {
            // Only adjust if a modifier key is pressed to avoid conflicts with Unity's navigation
            if (evt.control) // Use only Control check first
            {
                // Apply fast multiplier if both Control and Shift are held
                float speed = m_RadiusAdjustSpeed;
                if (evt.shift)
                {
                    speed *= m_FastAdjustMultiplier;
                }
                
                float delta = Mathf.Abs(evt.delta.y) > Mathf.Abs(evt.delta.x) ? -evt.delta.y : -evt.delta.x;
                float adjustment = delta * speed;
                
                if (m_CurrentMode == AdjustmentMode.Uniform)
                {
                    // Adjust radius for uniform scaling
                    m_Radius = Mathf.Clamp(m_Radius + adjustment, m_MinRadius, m_MaxRadius);
                }
                else if (m_CurrentMode == AdjustmentMode.XScale)
                {
                    // Adjust X scale
                    m_Scale.x = Mathf.Clamp(m_Scale.x + adjustment * 0.1f, m_MinScale, m_MaxScale);
                }
                else if (m_CurrentMode == AdjustmentMode.ZScale)
                {
                    // Adjust Z scale
                    m_Scale.y = Mathf.Clamp(m_Scale.y + adjustment * 0.1f, m_MinScale, m_MaxScale);
                }
                else if (m_CurrentMode == AdjustmentMode.Rotation)
                {
                    // Adjust rotation
                    m_Rotation = (m_Rotation + adjustment * m_RotationAdjustSpeed) % 360f;
                    if (m_Rotation < 0) m_Rotation += 360f;
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
            else if (evt.keyCode == KeyCode.X)
            {
                // X for X-axis scaling
                m_CurrentMode = AdjustmentMode.XScale;
                handled = true;
            }
            else if (evt.keyCode == KeyCode.Z)
            {
                // Z for Z-axis scaling
                m_CurrentMode = AdjustmentMode.ZScale;
                handled = true;
            }
            else if (evt.keyCode == KeyCode.R)
            {
                // R for Rotation
                m_CurrentMode = AdjustmentMode.Rotation;
                handled = true;
            }
            // Adjustments with keyboard arrows
            else if (evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.DownArrow ||
                     evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.RightArrow)
            {
                float speed = evt.shift ? m_RadiusAdjustSpeed * m_FastAdjustMultiplier : m_RadiusAdjustSpeed;
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
                    // Any arrow key adjusts the radius in uniform mode
                    m_Radius = Mathf.Clamp(m_Radius + adjustment, m_MinRadius, m_MaxRadius);
                }
                else if (m_CurrentMode == AdjustmentMode.XScale)
                {
                    // In X scale mode, prioritize horizontal arrows but allow all
                    if (isHorizontal || evt.control)
                    {
                        m_Scale.x = Mathf.Clamp(m_Scale.x + adjustment * 0.1f, m_MinScale, m_MaxScale);
                    }
                    else if (!isHorizontal)
                    {
                        // Allow vertical arrows in X mode as a convenience
                        m_Scale.x = Mathf.Clamp(m_Scale.x + adjustment * 0.1f, m_MinScale, m_MaxScale);
                    }
                }
                else if (m_CurrentMode == AdjustmentMode.ZScale)
                {
                    // In Z scale mode, prioritize vertical arrows but allow all
                    if (!isHorizontal || evt.control)
                    {
                        m_Scale.y = Mathf.Clamp(m_Scale.y + adjustment * 0.1f, m_MinScale, m_MaxScale);
                    }
                    else if (isHorizontal)
                    {
                        // Allow horizontal arrows in Z mode as a convenience
                        m_Scale.y = Mathf.Clamp(m_Scale.y + adjustment * 0.1f, m_MinScale, m_MaxScale);
                    }
                }
                
                handled = true;
            }
            
            return handled;
        }
        
        protected override void DrawPreview()
        {
            // Draw circle outline at placement position
            using (new Handles.DrawingScope(Color.green))
            {
                Matrix4x4 transformMatrix = Matrix4x4.TRS(
                    m_PlacementPosition,
                    Quaternion.Euler(0, m_Rotation, 0),
                    new Vector3(m_Scale.x, 1f, m_Scale.y) // Apply X/Z scaling
                );
                
                // Draw dimension lines with colors based on current mode
                // Using white for active axis and yellow for normal
                Color normalColor = Color.yellow;
                Color activeColor = Color.white;
                using (new Handles.DrawingScope(transformMatrix))
                {
                    // Draw wire circle
                    Handles.color = Color.green;
                    Handles.DrawWireDisc(Vector3.zero, Vector3.up, m_Radius);
                    
                    // Draw semi-transparent filled circle
                    Color fillColor = new Color(0, 1, 0, 0.2f);
                    Handles.color = fillColor;
                    Handles.DrawSolidDisc(Vector3.zero, Vector3.up, m_Radius);
                    
                    // Draw axis indicators with default color (yellow)
                    Handles.color = normalColor;
                    Handles.DrawLine(Vector3.zero, new Vector3(m_Radius, 0, 0));
                    Handles.DrawLine(Vector3.zero, new Vector3(0, 0, m_Radius));
                    
                    // Highlight active axes with white
                    if (m_CurrentMode == AdjustmentMode.Uniform)
                    {
                        // In uniform mode, highlight all axes
                        Handles.color = activeColor;
                        Handles.DrawLine(Vector3.zero, new Vector3(m_Radius, 0, 0));
                        Handles.DrawLine(Vector3.zero, new Vector3(0, 0, m_Radius));
                        Handles.DrawLine(Vector3.zero, new Vector3(-m_Radius, 0, 0));
                        Handles.DrawLine(Vector3.zero, new Vector3(0, 0, -m_Radius));
                    }
                    else if (m_CurrentMode == AdjustmentMode.XScale)
                    {
                        // Highlight X axis
                        Handles.color = activeColor;
                        Handles.DrawLine(Vector3.zero, new Vector3(m_Radius, 0, 0));
                    }
                    else if (m_CurrentMode == AdjustmentMode.ZScale)
                    {
                        // Highlight Z axis
                        Handles.color = activeColor;
                        Handles.DrawLine(Vector3.zero, new Vector3(0, 0, m_Radius));
                    }
                }
                
                // If in rotation mode, draw a rotation handle
                if (m_CurrentMode == AdjustmentMode.Rotation)
                {
                    Handles.color = activeColor;
                        
                    // Make rotation visualization larger based on circle radius
                    float rotationIndicatorLength = m_Radius * 0.5f; // 50% of the radius
                    Vector3 cameraRight = SceneView.lastActiveSceneView.camera.transform.right;
                    cameraRight.y = 0.0f;
                    //cameraRight = Quaternion.AngleAxis(-m_Rotation, Vector3.up) * cameraRight;
                    Vector3 direction = cameraRight * rotationIndicatorLength;
                    Handles.DrawLine(m_PlacementPosition, m_PlacementPosition + direction, 2f); // Make the line thicker
                        
                    // Draw larger arc for rotation
                    Handles.DrawWireArc(m_PlacementPosition, Vector3.up, direction, m_Rotation, rotationIndicatorLength, 2f);
                }
                
                // Calculate the text position to appear just above the placement position
                Camera camera = SceneView.lastActiveSceneView.camera;
                Vector3 textPosition = CalculateScreenSpaceTextPosition(camera, m_PlacementPosition);
                
                // Show the radius and controls as text
                GUIStyle style = CreateLabelStyle();
                
                // Generate appropriate dimension text based on mode
                string modeText = "";
                string dimensionsText = "";
                
                switch (m_CurrentMode)
                {
                    case AdjustmentMode.Uniform:
                        modeText = "Uniform Mode";
                        dimensionsText = $"Radius: {m_Radius:F1}";
                        break;
                    case AdjustmentMode.XScale:
                        modeText = "X Scale Mode";
                        dimensionsText = $"Radius: {m_Radius:F1}  X Scale: {m_Scale.x:F1}";
                        break;
                    case AdjustmentMode.ZScale:
                        modeText = "Z Scale Mode";
                        dimensionsText = $"Radius: {m_Radius:F1}  Z Scale: {m_Scale.y:F1}";
                        break;
                    case AdjustmentMode.Rotation:
                        modeText = "Rotation Mode";
                        dimensionsText = $"Radius: {m_Radius:F1}  Rotation: {m_Rotation:F0}°";
                        break;
                }
                
                // Create a better formatted shortcuts section with each shortcut on a new line
                string shortcutsText = "Shortcuts:\n" +
                                      "G/U/1: Uniform\n" +
                                      "X/2: X Scale\n" +
                                      "Z/3: Z Scale\n" +
                                      "R/4: Rotation\n" +
                                      "CTRL+Scroll: Adjust\n" +
                                      "CTRL+Shift+Scroll: Fast Adjust\n" +
                                      "Shift+Arrows: Fast Adjust";
                
                Handles.Label(textPosition, $"{modeText}\n{dimensionsText}\n\n{shortcutsText}", style);
            }
        }
        
        private void CreateCircleStamp()
        {
            // Create a new GameObject for the circle stamp
            GameObject stampObject = new GameObject("Circle Stamp");
            stampObject.transform.position = m_PlacementPosition;
            stampObject.transform.rotation = Quaternion.Euler(0, m_Rotation, 0);
            
            // Apply the non-uniform scaling
            stampObject.transform.localScale = new Vector3(m_Scale.x, 1f, m_Scale.y);
            
            // Add the CircleShape component
            CircleShape circleShape = stampObject.AddComponent<CircleShape>();
            circleShape.Radius = m_Radius;
            
            // Add the Stamp component
            Stamp stamp = stampObject.AddComponent<Stamp>();
            
            // Set the shape on the stamp
            stamp.Shape = circleShape;
            
            // Generate the mask
            circleShape.GenerateMask();
            
            // Select the created object
            Selection.activeGameObject = stampObject;
        }
    }
}
