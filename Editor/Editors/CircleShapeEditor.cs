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
        
        // Cache handle positions to prevent GC
        private NativeArray<Vector3> m_HandlePositions;
        
        private void OnEnable()
        {
            m_CircleShape = (CircleShape)target;
            
            // Initialize handle positions once
            m_HandlePositions = new NativeArray<Vector3>(4, Allocator.Persistent);
            UpdateHandlePositions();
        }
        
        private void OnDisable()
        {
            // Clean up native array when editor is disabled
            if (m_HandlePositions.IsCreated)
            {
                m_HandlePositions.Dispose();
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
            
            // Update handle positions with current radius
            UpdateHandlePositions();
            
            // Draw wire disc
            using (new Handles.DrawingScope(Color.blue, m_CircleShape.transform.localToWorldMatrix))
            {
                Handles.DrawWireDisc(Vector3.zero, m_CircleShape.transform.up, m_CircleShape.Radius);
                
                // Draw interactive handles at each position
                for (int i = 0; i < m_HandlePositions.Length; i++)
                {
                    // Create a unique control ID for this handle
                    int controlId = GUIUtility.GetControlID(FocusType.Passive);
                    
                    // Calculate handle size based on view
                    float size = HandleUtility.GetHandleSize(m_HandlePositions[i]) * kHandleSize;
                    
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
                                
                                if (plane.Raycast(ray, out float distance))
                                {
                                    Vector3 hitPoint = ray.GetPoint(distance);
                                    
                                    // Transform hitpoint to local space
                                    Vector3 localHitPoint = m_CircleShape.transform.InverseTransformPoint(hitPoint);
                                    
                                    // Update radius based on distance from center
                                    float newRadius = Vector3.Distance(Vector3.zero, localHitPoint);
                                    
                                    // Record for undo and update
                                    Undo.RecordObject(m_CircleShape, "Change Circle Radius");
                                    m_CircleShape.Radius = newRadius;
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
                            HandleUtility.AddControl(controlId, HandleUtility.DistanceToCircle(m_HandlePositions[i], size));
                            break;
                            
                        case EventType.Repaint:
                            // Change color based on whether handle is active
                            Color originalColor = Handles.color;
                            if (GUIUtility.hotControl == controlId || HandleUtility.nearestControl == controlId)
                            {
                                Handles.color = Color.yellow;
                            }
                            
                            // Draw the sphere handle
                            Handles.SphereHandleCap(
                                controlId,
                                m_HandlePositions[i],
                                Quaternion.identity,
                                size,
                                Event.current.type
                            );
                            
                            Handles.color = originalColor;
                            break;
                    }
                }
            }
            
            // Force repaint on changes
            if (!Mathf.Approximately(startRadius, m_CircleShape.Radius))
            {
                SceneView.RepaintAll();
            }
        }

        public override void OnInspectorGUI()
        {
            
        }
    }
} 