using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    [CustomEditor(typeof(WorldBuildingSystem))]
    public class WorldBuildingSystemEditor : UnityEditor.Editor
    {
        WorldBuildingSystem Target => target as WorldBuildingSystem;
        private StampOrderingControl stampOrderingControl;

        public override VisualElement CreateInspectorGUI()
        {
            // Create a new VisualElement to be the root of the Inspector UI.
            VisualElement inspector = new VisualElement();

            // Attach a default Inspector to the Foldout.
            InspectorElement.FillDefaultInspector(inspector, serializedObject, this);

            // Add generate button
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.marginTop = 8;
            buttonContainer.style.marginBottom = 10;
            
            Button generateButton = new Button() { text = "Generate" };
            generateButton.clicked += () => Target.Generate();
            generateButton.style.flexGrow = 1;
            buttonContainer.Add(generateButton);
            
            // Add Stamp Order Manager button
            Button openOrderManagerButton = new Button(() => {
                // Show a dropdown menu with options
                GenericMenu menu = new GenericMenu();
                
                menu.AddItem(new GUIContent("Open as Window"), false, () => {
                    StampOrderingWindow.ShowWindow();
                });
                
                /*menu.AddItem(new GUIContent("Toggle Overlay"), false, () => {
                    // Get the overlay type
                    var overlayType = typeof(StampOrderingOverlay);
                    
                    // Get the current scene view
                    var sceneView = SceneView.lastActiveSceneView;
                    if (sceneView != null)
                    {
                        // Toggle the overlay
                        var overlays = sceneView.overlays;
                        var overlay = overlays.Find(overlayType) as StampOrderingOverlay;
                        
                        if (overlay != null)
                        {
                            if (overlay.displayed)
                                overlay.collapsed = !overlay.collapsed;
                            else
                                overlay.displayed = true;
                        }
                        else
                        {
                            overlays.Add(overlayType);
                        }
                    }
                });*/
                
                menu.ShowAsContext();
            });
            openOrderManagerButton.text = "Stamp Order Manager";
            openOrderManagerButton.tooltip = "Open the Stamp Order Manager to view and reorder all stamps";
            openOrderManagerButton.style.marginLeft = 5;
            buttonContainer.Add(openOrderManagerButton);
            
            inspector.Add(buttonContainer);
            
            // Add the stylesheet for the order management
            StyleSheet stampOrderWindowStylesheet = Resources.Load<StyleSheet>("StampOrderingControlStylesheet");
            if (stampOrderWindowStylesheet != null)
            {
                inspector.styleSheets.Add(stampOrderWindowStylesheet);
            }
            
            // Add stamp management section
            AddStampManagementSection(inspector);

            // Return the finished Inspector UI.
            return inspector;
        }

        private void AddStampManagementSection(VisualElement inspector)
        {
            // Stamp ordering section
            var stampOrderingSection = new Foldout
            {
                text = "Stamp Order Manager",
                value = false // Start collapsed
            };
            
            // Create our stamp ordering control
            stampOrderingControl = new StampOrderingControl
            {
                ShowHeader = false, // No header in the inspector
                ShowAutoRefreshToggle = true,
                ListHeight = 200f
            };
            
            stampOrderingSection.Add(stampOrderingControl);
            inspector.Add(stampOrderingSection);
        }
    }
}