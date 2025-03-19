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
        protected const float m_RotationAdjustSpeed = 5f;
        
        public override GUIContent toolbarIcon => m_IconContent;
        
        // Regular method instead of override
        protected void OnEnable()
        {
            // Reset placement position to avoid drawing at old position
            m_PlacementPosition = Vector3.zero;
            m_Rotation = 0f;
            
            // Call the derived class's initialization
            OnToolEnable();
        }
        
        // Virtual method that derived classes can override
        protected virtual void OnToolEnable()
        {
            // Override in derived classes to set up tool-specific initialization
        }
        
        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView sceneView))
                return;
                
            Event evt = Event.current;
            
            // Handle input events
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            
            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (evt.button == 0)
                    {
                        OnLeftMouseDown();
                        evt.Use();
                    }
                    break;
                    
                case EventType.ScrollWheel:
                    if (HandleScrollWheelEvent(evt))
                    {
                        evt.Use();
                    }
                    break;
                    
                case EventType.KeyDown:
                    if (evt.keyCode == KeyCode.Escape)
                    {
                        OnEscapePressed();
                        evt.Use();
                    }
                    else if (HandleKeyDownEvent(evt))
                    {
                        evt.Use();
                    }
                    break;
                    
                case EventType.MouseMove:
                case EventType.MouseDrag:
                    UpdatePlacementPosition(evt);
                    sceneView.Repaint();
                    break;
                    
                case EventType.Repaint:
                    DrawPreview();
                    break;
            }
        }
        
        protected virtual void OnEscapePressed()
        {
            // Deactivate the current tool's toggle
            BaseStampToolToggle.DeactivateActiveToggle();
            // Just set the tool to null instead of restoring previous
            ToolManager.RestorePreviousPersistentTool();
        }
        
        protected virtual void OnToolDisable()
        {
            // Override in derived classes to clean up tool-specific resources
        }
        
        protected virtual void OnLeftMouseDown()
        {
            // Override in derived classes to handle left mouse button click
        }
        
        protected virtual bool HandleScrollWheelEvent(Event evt)
        {
            // Override in derived classes to handle scroll wheel input
            return false;
        }
        
        protected virtual bool HandleKeyDownEvent(Event evt)
        {
            // Override in derived classes to handle key input
            return false;
        }
        
        protected virtual void DrawPreview()
        {
            // Override in derived classes to draw tool preview
        }
        
        protected void UpdatePlacementPosition(Event evt)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            if (Physics.Raycast(ray, out m_RaycastHitInfo))
            {
                m_PlacementPosition = m_RaycastHitInfo.point;
            }
            else
            {
                // If no hit, place on a plane at y=0
                float enter;
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                if (groundPlane.Raycast(ray, out enter))
                {
                    m_PlacementPosition = ray.GetPoint(enter);
                }
            }
        }
        
        protected Vector3 GetMousePositionOnPlane(Vector2 mousePosition, float planeHeight)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Plane plane = new Plane(Vector3.up, new Vector3(0, planeHeight, 0));
            
            float enter;
            if (plane.Raycast(ray, out enter))
            {
                return ray.GetPoint(enter);
            }
            
            return Vector3.zero;
        }
        
        protected GUIStyle CreateLabelStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = Color.white;
            style.fontSize = 12;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.UpperLeft;
            style.wordWrap = true;
            
            // Create a dark, semi-transparent background
            Color backgroundColor = new Color(0, 0, 0, 0.7f);
            style.normal.background = MakeTexture(2, 2, backgroundColor);
            style.padding = new RectOffset(10, 10, 5, 5);
            
            return style;
        }
        
        protected Vector3 CalculateScreenSpaceTextPosition(Camera camera, Vector3 worldPosition, float verticalOffset = 40f)
        {
            if (camera == null)
                return worldPosition + Vector3.up * verticalOffset + Vector3.right * 30f;
                
            // Convert world position to screen position
            Vector3 screenPos = camera.WorldToScreenPoint(worldPosition);
            
            // Add vertical and horizontal offset in screen space
            screenPos.y += verticalOffset;
            screenPos.x += 30f; // Offset text to the right
            
            // Convert back to world position
            return camera.ScreenToWorldPoint(screenPos);
        }
        
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            
            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            
            return texture;
        }
    }
} 