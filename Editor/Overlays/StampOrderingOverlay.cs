using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    [Overlay(typeof(SceneView), "Stamp Order Manager")]
    public class StampOrderingOverlay : Overlay
    {
        private StampOrderingControl stampOrdering;
        
        // Called when the overlay is created to construct the UI
        public override VisualElement CreatePanelContent()
        {
            // Create a parent container for the panel
            var root = new VisualElement();
            root.style.flexGrow = 1;
            
            // Create and configure the stamp ordering control
            stampOrdering = new StampOrderingControl
            {
                ShowHeader = true,
                ShowAutoRefreshToggle = true,
                ListHeight = 250f // Smaller height for overlay
            };
            
            // Add the control to the root
            root.Add(stampOrdering);
            
            return root;
        }

        public override void OnCreated()
        {
            // Set a nice icon for the overlay
            this.displayName = "Stamp Order";
            this.collapsed = false;
        }
    }
} 