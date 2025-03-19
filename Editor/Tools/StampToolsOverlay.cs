using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    // Base class for stamp tool toggles to handle mutual exclusivity
    abstract class BaseStampToolToggle : EditorToolbarToggle
    {
        private static BaseStampToolToggle s_ActiveToggle;
        private static bool s_ListeningToToolChanged = false;
        
        protected BaseStampToolToggle()
        {
            // Register callback for when the toggle changes
            this.RegisterValueChangedCallback(OnValueChanged);
            
            // Ensure we only register the global event once
            if (!s_ListeningToToolChanged)
            {
                ToolManager.activeToolChanged += OnActiveToolChanged;
                s_ListeningToToolChanged = true;
            }
        }
        
        // Global handler for tool changes from any source
        private static void OnActiveToolChanged()
        {
            if (s_ActiveToggle != null)
            {
                // Get the active tool type
                System.Type activeToolType = ToolManager.activeToolType;
                
                // Check if our tool is still active
                bool shouldBeActive = false;
                if (s_ActiveToggle is CircleStampToolToggle && activeToolType == typeof(CreateCircleStampTool))
                    shouldBeActive = true;
                else if (s_ActiveToggle is RectangleStampToolToggle && activeToolType == typeof(CreateRectangleStampTool))
                    shouldBeActive = true;
                else if (s_ActiveToggle is SplineStampToolToggle && activeToolType == typeof(CreateSplineStampTool))
                    shouldBeActive = true;
                
                // Update toggle state if it doesn't match
                if (!shouldBeActive)
                {
                    s_ActiveToggle.SetValueWithoutNotify(false);
                    s_ActiveToggle = null;
                }
            }
        }
        
        private void OnValueChanged(ChangeEvent<bool> evt)
        {
            if (evt.newValue)
            {
                // Deactivate previous toggle if it exists and isn't this one
                if (s_ActiveToggle != null && s_ActiveToggle != this)
                {
                    s_ActiveToggle.SetValueWithoutNotify(false);
                }
                s_ActiveToggle = this;
                OnActivated();
            }
            else
            {
                if (s_ActiveToggle == this)
                {
                    s_ActiveToggle = null;
                }
                OnDeactivated();
            }
        }
        
        public static void DeactivateActiveToggle()
        {
            if (s_ActiveToggle != null)
            {
                s_ActiveToggle.SetValueWithoutNotify(false);
                s_ActiveToggle = null;
            }
        }
        
        protected abstract void OnActivated();
        protected abstract void OnDeactivated();
    }
    
    // Create the toggle elements for each tool
    [EditorToolbarElement(id, typeof(SceneView))]
    class CircleStampToolToggle : BaseStampToolToggle
    {
        public const string id = "StampTools/CircleStamp";
        
        public CircleStampToolToggle()
        {
            text = "Circle";
            icon = EditorGUIUtility.IconContent("CircleShape Icon").image as Texture2D;
            tooltip = "Create a circle stamp";
            
            // Set initial state based on whether this tool is active
            value = ToolManager.activeToolType == typeof(CreateCircleStampTool);
        }
        
        protected override void OnActivated()
        {
            ToolManager.SetActiveTool<CreateCircleStampTool>();
        }
        
        protected override void OnDeactivated()
        {
            if (ToolManager.activeToolType == typeof(CreateCircleStampTool))
            {
                ToolManager.RestorePreviousTool();
            }
        }
    }
    
    [EditorToolbarElement(id, typeof(SceneView))]
    class RectangleStampToolToggle : BaseStampToolToggle
    {
        public const string id = "StampTools/RectangleStamp";
        
        public RectangleStampToolToggle()
        {
            text = "Rectangle";
            icon = EditorGUIUtility.IconContent("RectangleShape Icon").image as Texture2D;
            tooltip = "Create a rectangle stamp";
            
            // Set initial state based on whether this tool is active
            value = ToolManager.activeToolType == typeof(CreateRectangleStampTool);
        }
        
        protected override void OnActivated()
        {
            ToolManager.SetActiveTool<CreateRectangleStampTool>();
        }
        
        protected override void OnDeactivated()
        {
            if (ToolManager.activeToolType == typeof(CreateRectangleStampTool))
            {
                ToolManager.RestorePreviousTool();
            }
        }
    }
    
    [EditorToolbarElement(id, typeof(SceneView))]
    class SplineStampToolToggle : BaseStampToolToggle
    {
        public const string id = "StampTools/SplineStamp";
        
        public SplineStampToolToggle()
        {
            text = "Spline";
            icon = EditorGUIUtility.IconContent("EditCollider").image as Texture2D;
            tooltip = "Draw a spline stamp";
            
            // Set initial state based on whether this tool is active
            value = ToolManager.activeToolType == typeof(CreateSplineStampTool);
            
            // Make text not squished by increasing width
            style.width = 85;
        }
        
        protected override void OnActivated()
        {
            ToolManager.SetActiveTool<CreateSplineStampTool>();
        }
        
        protected override void OnDeactivated()
        {
            if (ToolManager.activeToolType == typeof(CreateSplineStampTool))
            {
                ToolManager.RestorePreviousTool();
            }
        }
    }
    
    // Create the toolbar overlay that contains all the toggles
    [Overlay(typeof(SceneView), "Stamp Tools")]
    [Icon("EditCollider")]
    public class StampToolsOverlay : ToolbarOverlay
    {
        StampToolsOverlay() : base(
            CircleStampToolToggle.id,
            RectangleStampToolToggle.id,
            SplineStampToolToggle.id)
        {
        }
    }
} 