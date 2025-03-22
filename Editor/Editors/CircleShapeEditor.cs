using UnityEngine;
using UnityEditor;
using Unity.Collections;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    [CustomEditor(typeof(CircleShape))]
    public class CircleShapeEditor : UnityEditor.Editor
    {
        private CircleShape m_CircleShape;
        private const float kHandleSize = 0.15f; // Handle size as proportion of view size
        private int m_ActiveHandleId = -1;
        private int m_HoveredHandleIndex = -1;
        
        // Cache handle positions to prevent GC
        private NativeArray<Vector3> m_HandlePositions;
        
        private void OnEnable()
        {
            m_CircleShape = (CircleShape)target;
            
            // Initialize handle positions once
            m_HandlePositions = new NativeArray<Vector3>(4, Allocator.Persistent);
            UpdateHandlePositions();
            
            // Ensure SceneView repaint to show tooltips properly
            SceneView.duringSceneGui += OnSceneGUIDelegate;
        }
        
        private void OnDisable()
        {
            // Clean up native array when editor is disabled
            if (m_HandlePositions.IsCreated)
            {
                m_HandlePositions.Dispose();
            }
            
            SceneView.duringSceneGui -= OnSceneGUIDelegate;
        }
        
        private void OnSceneGUIDelegate(SceneView sceneView)
        {
            // This ensures the tooltip gets rendered properly
            if (m_HoveredHandleIndex >= 0 && m_HoveredHandleIndex < m_HandlePositions.Length)
            {
                ShowTooltipNearHandle(m_HandlePositions[m_HoveredHandleIndex]);
                sceneView.Repaint();
            }
        }
        
        private void UpdateHandlePositions()
        {
            if (!m_HandlePositions.IsCreated || m_CircleShape == null) return;
            
            // Update positions without creating new arrays
            m_HandlePositions[0] = new Vector3(m_CircleShape.Radius, 0, 0);    // Right
            m_HandlePositions[1] = new Vector3(-m_CircleShape.Radius, 0, 0);   // Left
            m_HandlePositions[2] = new Vector3(0, 0, m_CircleShape.Radius);    // Forward
            m_HandlePositions[3] = new Vector3(0, 0, -m_CircleShape.Radius);   // Back
        }

        private void OnSceneGUI()
        {
            if (m_CircleShape == null) return;
            
            // Store starting radius to check for changes
            float startRadius = m_CircleShape.Radius;
            m_HoveredHandleIndex = -1;
            
            // Update handle positions with current radius
            UpdateHandlePositions();
            
            // Draw wire disc using the full transform matrix
            using (new Handles.DrawingScope(Color.blue, m_CircleShape.transform.localToWorldMatrix))
            {
                Handles.DrawWireDisc(Vector3.zero, m_CircleShape.transform.up, m_CircleShape.Radius);
            }
            
            // Get object's local scale to adjust handle positions correctly
            Vector3 objectScale = m_CircleShape.transform.localScale;
            
            // Draw handles directly in world space
            for (int i = 0; i < m_HandlePositions.Length; i++)
            {
                // Create a unique control ID for this handle
                int controlId = GUIUtility.GetControlID(FocusType.Passive);
                
                // Scale the handle position by object's scale (to position it correctly)
                Vector3 scaledHandlePos = Vector3.Scale(m_HandlePositions[i], objectScale);
                
                // Convert to world position for drawing and hit detection
                Vector3 worldHandlePos = m_CircleShape.transform.TransformPoint(m_HandlePositions[i]);
                
                // Calculate handle size based on view
                float size = HandleUtility.GetHandleSize(worldHandlePos) * kHandleSize;
                
                // Process input events for this handle
                Event evt = Event.current;
                
                switch (evt.type)
                {
                    case EventType.MouseDown:
                        // Check if this handle was clicked
                        if (HandleUtility.nearestControl == controlId && evt.button == 0)
                        {
                            m_ActiveHandleId = controlId;
                            GUIUtility.hotControl = controlId;
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
                            Plane plane = new Plane(m_CircleShape.transform.up, m_CircleShape.transform.position);
                            
                            if (plane.Raycast(ray, out float hitDistance))
                            {
                                Vector3 hitPoint = ray.GetPoint(hitDistance);
                                
                                // Transform hitpoint to local space
                                Vector3 localHitPoint = m_CircleShape.transform.InverseTransformPoint(hitPoint);
                                
                                // Update radius based on distance from center, accounting for scale
                                float scaledRadius = Vector3.Scale(localHitPoint, new Vector3(1f / objectScale.x, 1f / objectScale.y, 1f / objectScale.z)).magnitude;
                                
                                // Record for undo and update
                                Undo.RecordObject(m_CircleShape, "Change Circle Radius");
                                m_CircleShape.Radius = scaledRadius;
                                m_CircleShape.GenerateMask();
                                EditorUtility.SetDirty(m_CircleShape);
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
            if (!Mathf.Approximately(startRadius, m_CircleShape.Radius))
            {
                SceneView.RepaintAll();
            }
        }
        
        private void ShowTooltipNearHandle(Vector3 handlePos)
        {
            // Convert handle position to screen space
            Vector3 worldPos = m_CircleShape.transform.TransformPoint(handlePos);
            Vector2 screenPos = HandleUtility.WorldToGUIPoint(worldPos);
            
            // Create tooltip style
            GUIStyle tooltipStyle = new GUIStyle(EditorStyles.helpBox);
            tooltipStyle.normal.textColor = Color.white;
            tooltipStyle.fontSize = 12;
            tooltipStyle.fontStyle = FontStyle.Bold;
            tooltipStyle.alignment = TextAnchor.MiddleCenter;
            tooltipStyle.padding = new RectOffset(10, 10, 6, 6);
            tooltipStyle.stretchWidth = true;
            
            // Tooltip text
            string tooltipText = "Drag to resize circle";
            
            // Calculate tooltip size with padding
            GUIContent content = new GUIContent(tooltipText);
            Vector2 tooltipSize = tooltipStyle.CalcSize(content);
            
            // Use a consistent minimum width and height
            float minWidth = 180f;
            float minHeight = 30f;
            
            tooltipSize.x = Mathf.Max(tooltipSize.x, minWidth);
            tooltipSize.y = Mathf.Max(tooltipSize.y, minHeight);
            
            // Adjust position based on which side of circle the handle is on
            float xOffset = 15f;
            if (handlePos.x < 0) // Left side handles
            {
                xOffset = -tooltipSize.x - 15f; // Position to the left of the handle
            }
            
            // Position tooltip adjusted for handle position
            Rect tooltipRect = new Rect(
                screenPos.x + xOffset,
                screenPos.y - tooltipSize.y * 0.5f,
                tooltipSize.x,
                tooltipSize.y
            );
            
            // Draw the tooltip with GUI
            Handles.BeginGUI();
            
            // Draw dark background with consistent size
            Color bgColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            EditorGUI.DrawRect(tooltipRect, bgColor);
            
            // Draw border with precise positioning
            Color borderColor = new Color(1f, 1f, 1f, 0.5f);
            Rect borderRect = new Rect(tooltipRect.x, tooltipRect.y, tooltipRect.width, tooltipRect.height);
            
            // Make an array where each pair of consecutive points forms a line segment
            Vector3[] points = new Vector3[]
            {
                // Top edge
                new Vector3(borderRect.x, borderRect.y), 
                new Vector3(borderRect.x + borderRect.width, borderRect.y),
                
                // Right edge
                new Vector3(borderRect.x + borderRect.width, borderRect.y), 
                new Vector3(borderRect.x + borderRect.width, borderRect.y + borderRect.height),
                
                // Bottom edge
                new Vector3(borderRect.x + borderRect.width, borderRect.y + borderRect.height), 
                new Vector3(borderRect.x, borderRect.y + borderRect.height),
                
                // Left edge
                new Vector3(borderRect.x, borderRect.y + borderRect.height), 
                new Vector3(borderRect.x, borderRect.y)
            };
            
            Handles.color = borderColor;
            Handles.DrawLines(points);
            
            // Draw the tooltip text with white color
            tooltipStyle.normal.background = null;
            GUI.color = Color.white;
            GUI.Label(tooltipRect, tooltipText, tooltipStyle);
            
            Handles.EndGUI();
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }
} 