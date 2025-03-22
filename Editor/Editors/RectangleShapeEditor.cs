using UnityEngine;
using UnityEditor;
using Unity.Collections;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    [CustomEditor(typeof(RectangleShape))]
    public class RectangleShapeEditor : UnityEditor.Editor
    {
        private RectangleShape m_RectangleShape;
        private const float kHandleSize = 0.15f; // Handle size as proportion of view size
        private int m_ActiveHandleId = -1;
        private int m_HoveredHandleIndex = -1;
        private Vector2 m_StartSize; // Store initial size for proportional scaling
        private Vector3 m_DragStartPosition; // Store position where drag started
        
        // Cache handle positions to prevent GC
        private NativeArray<Vector3> m_HandlePositions;
        // Track which dimension each handle controls
        private NativeArray<Vector2> m_HandleDimensions;
        
        // Cache tooltip resources to prevent GC allocations
        private GUIStyle m_TooltipStyle;
        private GUIContent m_CornerTooltipContent;
        private GUIContent m_EdgeTooltipContent;
        private Vector3[] m_TooltipBorderPoints;
        private readonly Color m_BorderColor = new Color(1f, 1f, 1f, 0.5f);
        private readonly Color m_BgColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        
        private void OnEnable()
        {
            m_RectangleShape = (RectangleShape)target;
            m_StartSize = m_RectangleShape.Size;
            
            // Initialize handle positions once (8 corner/edge handles)
            m_HandlePositions = new NativeArray<Vector3>(8, Allocator.Persistent);
            // Store which dimensions (x,y) each handle affects
            m_HandleDimensions = new NativeArray<Vector2>(8, Allocator.Persistent);
            
            // Set fixed dimension flags for each handle
            m_HandleDimensions[0] = new Vector2(1, 1); // Top-Right
            m_HandleDimensions[1] = new Vector2(1, 1); // Top-Left
            m_HandleDimensions[2] = new Vector2(1, 1); // Bottom-Left
            m_HandleDimensions[3] = new Vector2(1, 1); // Bottom-Right
            m_HandleDimensions[4] = new Vector2(1, 0); // Right
            m_HandleDimensions[5] = new Vector2(0, 1); // Top
            m_HandleDimensions[6] = new Vector2(1, 0); // Left
            m_HandleDimensions[7] = new Vector2(0, 1); // Bottom
            
            // Initialize tooltip resources
            InitTooltipResources();
            
            UpdateHandlePositions();
            
            // Ensure SceneView repaint to show tooltips properly
            SceneView.duringSceneGui += OnSceneGUIDelegate;
        }
        
        private void InitTooltipResources()
        {
            // Create tooltip style once
            m_TooltipStyle = new GUIStyle(EditorStyles.helpBox);
            m_TooltipStyle.normal.textColor = Color.white;
            m_TooltipStyle.fontSize = 12;
            m_TooltipStyle.fontStyle = FontStyle.Bold;
            m_TooltipStyle.alignment = TextAnchor.MiddleCenter;
            m_TooltipStyle.padding = new RectOffset(10, 10, 6, 6);
            m_TooltipStyle.stretchWidth = true;
            
            // Create tooltip content objects once
            m_CornerTooltipContent = new GUIContent("Drag to resize\nHold SHIFT for uniform scaling");
            m_EdgeTooltipContent = new GUIContent("Drag to resize");
            
            // Initialize border points array once
            m_TooltipBorderPoints = new Vector3[8];
        }
        
        private void OnDisable()
        {
            // Clean up native arrays when editor is disabled
            if (m_HandlePositions.IsCreated)
            {
                m_HandlePositions.Dispose();
            }
            
            if (m_HandleDimensions.IsCreated)
            {
                m_HandleDimensions.Dispose();
            }
            
            SceneView.duringSceneGui -= OnSceneGUIDelegate;
        }
        
        private void OnSceneGUIDelegate(SceneView sceneView)
        {
            // This ensures the tooltip gets rendered properly
            if (m_HoveredHandleIndex >= 0 && m_HoveredHandleIndex < m_HandlePositions.Length)
            {
                ShowTooltipNearHandle(m_HandlePositions[m_HoveredHandleIndex], m_HoveredHandleIndex);
                sceneView.Repaint();
            }
        }
        
        private void UpdateHandlePositions()
        {
            if (!m_HandlePositions.IsCreated || m_RectangleShape == null) return;
            
            float halfWidth = m_RectangleShape.Size.x * 0.5f;
            float halfDepth = m_RectangleShape.Size.y * 0.5f;
            
            // Update positions without creating new arrays
            // Corner handles
            m_HandlePositions[0] = new Vector3(halfWidth, 0, halfDepth);     // Top-Right
            m_HandlePositions[1] = new Vector3(-halfWidth, 0, halfDepth);    // Top-Left
            m_HandlePositions[2] = new Vector3(-halfWidth, 0, -halfDepth);   // Bottom-Left
            m_HandlePositions[3] = new Vector3(halfWidth, 0, -halfDepth);    // Bottom-Right
            
            // Edge handles (middle of each edge)
            m_HandlePositions[4] = new Vector3(halfWidth, 0, 0);             // Right
            m_HandlePositions[5] = new Vector3(0, 0, halfDepth);             // Top
            m_HandlePositions[6] = new Vector3(-halfWidth, 0, 0);            // Left
            m_HandlePositions[7] = new Vector3(0, 0, -halfDepth);            // Bottom
        }

        private void OnSceneGUI()
        {
            if (m_RectangleShape == null) return;
            
            // Store starting size to check for changes
            Vector2 currentSize = m_RectangleShape.Size;
            m_HoveredHandleIndex = -1;
            
            // Update handle positions with current size
            UpdateHandlePositions();
            
            // Draw wire rectangle using the full transform matrix
            using (new Handles.DrawingScope(Color.blue, m_RectangleShape.transform.localToWorldMatrix))
            {
                // Draw rectangle outline
                Handles.DrawWireCube(Vector3.zero, new Vector3(m_RectangleShape.Size.x, 0.0f, m_RectangleShape.Size.y));
            }
            
            // Get object's local scale to adjust handle positions correctly
            Vector3 objectScale = m_RectangleShape.transform.localScale;
            
            // Draw handles directly in world space
            for (int i = 0; i < m_HandlePositions.Length; i++)
            {
                // Create a unique control ID for this handle
                int controlId = GUIUtility.GetControlID(FocusType.Passive);
                
                // Get the position in world space directly
                Vector3 worldHandlePos = m_RectangleShape.transform.TransformPoint(m_HandlePositions[i]);
                
                // Calculate handle size based on view
                float size = HandleUtility.GetHandleSize(worldHandlePos) * kHandleSize;
                
                // Process input events for this handle
                Event evt = Event.current;
                
                // The current dimensions for the handle.
                Vector2 dimensions = m_HandleDimensions[i];
                
                switch (evt.type)
                {
                    case EventType.MouseDown:
                        // Check if this handle was clicked
                        if (HandleUtility.nearestControl == controlId && evt.button == 0)
                        {
                            m_ActiveHandleId = controlId;
                            GUIUtility.hotControl = controlId;
                            
                            // Store initial size and drag start position
                            m_StartSize = m_RectangleShape.Size;
                            
                            // Get initial drag position in world space
                            Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                            Plane plane = new Plane(m_RectangleShape.transform.up, m_RectangleShape.transform.position);
                            
                            if (plane.Raycast(ray, out float hitDistance))
                            {
                                Vector3 hitPoint = ray.GetPoint(hitDistance);
                                m_DragStartPosition = m_RectangleShape.transform.InverseTransformPoint(hitPoint);
                            }
                            
                            evt.Use();
                            EditorGUIUtility.SetWantsMouseJumping(1);
                        }
                        break;
                        
                    case EventType.MouseDrag:
                        // If this handle is being dragged
                        if (GUIUtility.hotControl == controlId)
                        {
                            // Convert mouse delta to world space
                            Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                            Plane plane = new Plane(m_RectangleShape.transform.up, m_RectangleShape.transform.position);
                            
                            if (plane.Raycast(ray, out float hitDistance))
                            {
                                Vector3 hitPoint = ray.GetPoint(hitDistance);
                                
                                // Transform hit-point to local space - this already accounts for scale
                                Vector3 localHitPoint = m_RectangleShape.transform.InverseTransformPoint(hitPoint);
                                
                                // Record for undo and update
                                Undo.RecordObject(m_RectangleShape, "Change Rectangle Size");
                                
                                // Check if shift is held for proportional scaling
                                bool uniformScale = evt.shift && i < 4; // Only for corner handles
                                
                                Vector2 newSize = m_RectangleShape.Size;
                                
                                if (uniformScale)
                                {
                                    // For uniform scaling, use the ratio of distances from the drag start
                                    // to the current position relative to the corner's quadrant
                                    
                                    // Determine which quadrant we're in for this corner
                                    float signX = (i == 1 || i == 2) ? -1 : 1;
                                    float signZ = (i == 2 || i == 3) ? -1 : 1;
                                    
                                    // Get the drag vector components in the appropriate directions
                                    float dragX = Mathf.Abs(localHitPoint.x);
                                    float dragZ = Mathf.Abs(localHitPoint.z);
                                    
                                    // Find the larger scale factor to maintain aspect ratio
                                    float scaleFactorX = dragX / (m_StartSize.x * 0.5f);
                                    float scaleFactorZ = dragZ / (m_StartSize.y * 0.5f);
                                    float scaleFactor = Mathf.Max(scaleFactorX, scaleFactorZ);
                                    
                                    // Apply the scale factor to both dimensions
                                    newSize.x = m_StartSize.x * scaleFactor;
                                    newSize.y = m_StartSize.y * scaleFactor;
                                    
                                    // Ensure minimum size
                                    newSize.x = Mathf.Max(0.1f, newSize.x);
                                    newSize.y = Mathf.Max(0.1f, newSize.y);
                                }
                                else
                                {
                                    // Non-uniform scaling - handle each dimension separately
                                    
                                    // Update width (x) if this handle affects it
                                    if (dimensions.x > 0)
                                    {
                                        // Use absolute value for consistent behavior regardless of handle position
                                        float width = Mathf.Abs(localHitPoint.x) * 2f;
                                        newSize.x = Mathf.Max(0.1f, width);
                                    }
                                    
                                    // Update depth (y) if this handle affects it
                                    if (dimensions.y > 0)
                                    {
                                        // Use absolute value for consistent behavior regardless of handle position
                                        float depth = Mathf.Abs(localHitPoint.z) * 2f;
                                        newSize.y = Mathf.Max(0.1f, depth);
                                    }
                                }
                                
                                // Apply the new size
                                m_RectangleShape.Size = newSize;
                                m_RectangleShape.GenerateMask();
                                EditorUtility.SetDirty(m_RectangleShape);
                            }
                            
                            evt.Use();
                            GUIUtility.hotControl = controlId;
                        }
                        break;
                        
                    case EventType.MouseUp:
                        // Release control when mouse is released
                        if (GUIUtility.hotControl == controlId && evt.button == 0)
                        {
                            GUIUtility.hotControl = 0;
                            m_ActiveHandleId = -1;
                            evt.Use();
                            EditorGUIUtility.SetWantsMouseJumping(0);
                        }
                        break;
                        
                    case EventType.Layout:
                        // Set the distance to cursor for handle selection priority
                        HandleUtility.AddControl(controlId, HandleUtility.DistanceToCircle(worldHandlePos, size));
                        break;
                        
                    case EventType.Repaint:
                        // Change color based on whether handle is active
                        Color originalColor = Handles.color;
                        
                        // All handles are blue, highlighted yellow when active
                        if (GUIUtility.hotControl == controlId || HandleUtility.nearestControl == controlId)
                        {
                            Handles.color = Color.yellow;
                            
                            // Track which handle is being hovered for tooltip display
                            if (HandleUtility.nearestControl == controlId)
                            {
                                m_HoveredHandleIndex = i;
                            }
                        }
                        else
                        {
                            Handles.color = Color.blue;
                        }
                        
                        // Draw the sphere handle directly in world space
                        Handles.SphereHandleCap(
                            controlId,
                            worldHandlePos,
                            Quaternion.identity,
                            size,
                            Event.current.type
                        );
                        
                        Handles.color = originalColor;
                        break;
                }
            }
            
            // Force repaint on changes
            if (currentSize != m_RectangleShape.Size)
            {
                SceneView.RepaintAll();
            }
        }
        
        private void ShowTooltipNearHandle(Vector3 handlePos, int handleIndex)
        {
            // Convert handle position to screen space
            Vector3 worldPos = m_RectangleShape.transform.TransformPoint(handlePos);
            Vector2 screenPos = HandleUtility.WorldToGUIPoint(worldPos);
            
            // Choose tooltip content based on handle type - reuse cached content
            GUIContent content = handleIndex < 4 ? m_CornerTooltipContent : m_EdgeTooltipContent;
            
            // Calculate tooltip size with padding
            Vector2 tooltipSize = m_TooltipStyle.CalcSize(content);
            
            // Use consistent dimensions for all tooltips
            float minWidth = 180f;
            float minHeight = handleIndex < 4 ? 50f : 30f;
            
            tooltipSize.x = Mathf.Max(tooltipSize.x, minWidth);
            tooltipSize.y = Mathf.Max(tooltipSize.y, minHeight);
            
            // Adjust position based on which side of shape the handle is on
            float xOffset = 15f;
            if (handleIndex == 1 || handleIndex == 2 || handleIndex == 6) // Left side handles
            {
                xOffset = -tooltipSize.x - 15f; // Position to the left of the handle
            }
            
            // Position tooltip with adjusted offset
            Rect tooltipRect = new Rect(
                screenPos.x + xOffset,
                screenPos.y - tooltipSize.y * 0.5f,
                tooltipSize.x,
                tooltipSize.y
            );
            
            // Update existing points array values
            // Top edge
            m_TooltipBorderPoints[0] = new Vector3(tooltipRect.x, tooltipRect.y);
            m_TooltipBorderPoints[1] = new Vector3(tooltipRect.x + tooltipRect.width, tooltipRect.y);
            
            // Right edge
            m_TooltipBorderPoints[2] = new Vector3(tooltipRect.x + tooltipRect.width, tooltipRect.y);
            m_TooltipBorderPoints[3] = new Vector3(tooltipRect.x + tooltipRect.width, tooltipRect.y + tooltipRect.height);
            
            // Bottom edge
            m_TooltipBorderPoints[4] = new Vector3(tooltipRect.x + tooltipRect.width, tooltipRect.y + tooltipRect.height);
            m_TooltipBorderPoints[5] = new Vector3(tooltipRect.x, tooltipRect.y + tooltipRect.height);
            
            // Left edge
            m_TooltipBorderPoints[6] = new Vector3(tooltipRect.x, tooltipRect.y + tooltipRect.height);
            m_TooltipBorderPoints[7] = new Vector3(tooltipRect.x, tooltipRect.y);
            
            // Draw the tooltip with GUI
            Handles.BeginGUI();
            
            // Draw dark background with consistent size
            EditorGUI.DrawRect(tooltipRect, m_BgColor);
            
            // Draw border with cached color and points
            Handles.color = m_BorderColor;
            Handles.DrawLines(m_TooltipBorderPoints);
            
            // Draw the tooltip text with white color
            m_TooltipStyle.normal.background = null;
            GUI.color = Color.white;
            GUI.Label(tooltipRect, content, m_TooltipStyle);
            
            Handles.EndGUI();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }
} 