using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using System;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    public abstract class BaseStampTool : EditorTool
    {
        [NonSerialized] protected Vector3 m_PlacementPosition;
        [NonSerialized] protected float m_Rotation = 0f;
        [NonSerialized] protected RaycastHit m_RaycastHitInfo;
        [NonSerialized] protected GUIContent m_IconContent;
        
        // Common adjustment parameters
        protected const float m_FastAdjustMultiplier = 5f;
        protected const float m_RotationAdjustSpeed = 5.0f;
        
        public override GUIContent toolbarIcon => m_IconContent;
        
        // Regular method instead of override
        protected void OnEnable()
        {
            // Call the derived class's initialization
            OnToolEnable();
        }
        
        // Virtual method that derived classes can override
        protected virtual void OnToolEnable()
        {
            // Base implementation does nothing
        }
        
        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView sceneView))
                return;
                
            Event evt = Event.current;
            
            // Handle input events
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlID);
            
            // Update placement position based on mouse position
            UpdatePlacementPosition(evt.mousePosition);
            
            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (evt.button == 0) // Left mouse button
                    {
                        OnLeftMouseDown();
                        evt.Use();
                    }
                    break;
                    
                case EventType.MouseMove:
                    // Force repaint on mouse move to update preview
                    sceneView.Repaint();
                    break;
                    
                case EventType.ScrollWheel:
                    if (HandleScrollWheelEvent(evt))
                        evt.Use();
                    break;
                    
                case EventType.KeyDown:
                    if (HandleKeyDownEvent(evt))
                        evt.Use();
                    break;
                    
                case EventType.Repaint:
                    DrawPreview();
                    break;
            }
        }
        
        protected virtual void UpdatePlacementPosition(Vector2 mousePosition)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            
            if (Physics.Raycast(ray, out m_RaycastHitInfo))
            {
                m_PlacementPosition = m_RaycastHitInfo.point;
            }
            else
            {
                // If we didn't hit anything, use a plane with normal = up vector
                // Y position is either 0 or last hit position's Y
                float planeY = m_PlacementPosition.y; // Use the last position's Y if available
                if (float.IsNaN(planeY) || float.IsInfinity(planeY)) // If no valid previous position
                    planeY = 0f;
                
                Plane plane = new Plane(Vector3.up, new Vector3(0, planeY, 0));
                if (plane.Raycast(ray, out float distance))
                {
                    m_PlacementPosition = ray.GetPoint(distance);
                }
            }
        }
        
        protected Vector3 GetMousePositionOnPlane(Vector2 mousePosition, float planeY)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Plane plane = new Plane(Vector3.up, new Vector3(0, planeY, 0));
            
            if (plane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }
            
            return Vector3.zero; // Fallback
        }
        
        protected virtual void OnLeftMouseDown()
        {
            // Default implementation - to be overridden by derived classes
            // e.g., CreateStamp();
        }
        
        protected virtual bool HandleScrollWheelEvent(Event evt)
        {
            // Default implementation - to be overridden by derived classes
            return false; // Return true if the event was handled
        }
        
        protected virtual bool HandleKeyDownEvent(Event evt)
        {
            // Default implementation - to be overridden by derived classes
            return false; // Return true if the event was handled
        }
        
        protected abstract void DrawPreview();
        
        protected Vector3 CalculateScreenSpaceTextPosition(Camera camera, Vector3 worldPos, float yOffset = 50f)
        {
            Vector3 screenPos = camera.WorldToScreenPoint(worldPos);
            screenPos.y += yOffset;
            return camera.ScreenToWorldPoint(screenPos);
        }
        
        protected GUIStyle CreateLabelStyle()
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 14;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.LowerCenter;
            return style;
        }
    }
} 