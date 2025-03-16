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
            // Create a container for the entire stamp management section
            var stampManagementContainer = new VisualElement();
            
            // Create a container for the header row
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.marginTop = 10;
            
            // Stamp ordering section foldout
            var stampOrderingFoldout = new Foldout
            {
                text = "Stamp Order Manager",
                value = false // Start collapsed
            };
            stampOrderingFoldout.style.flexGrow = 1;
            stampOrderingFoldout.style.marginRight = 0;
            stampOrderingFoldout.style.marginBottom = 0;
            headerRow.Add(stampOrderingFoldout);
            
            // Add Order Manager button
            Button openOrderManagerButton = new Button(() => {
                // Show a dropdown menu with options
                GenericMenu menu = new GenericMenu();
                
                menu.AddItem(new GUIContent("Open as Window"), false, () => {
                    StampOrderingWindow.ShowWindow();
                });
                
                menu.AddItem(new GUIContent("Toggle Overlay"), false, () => {
                    // Get the overlay type
                    var overlayType = typeof(StampOrderingOverlay);
                    
                    // Get the current scene view
                    var sceneView = SceneView.lastActiveSceneView;
                    if (sceneView != null)
                    {
                        // Toggle the overlay
                        sceneView.TryGetOverlay(StampOrderingOverlay.kId, out var overlay);
                        if (overlay != null)
                        {
                            if (overlay.displayed)
                                overlay.collapsed = !overlay.collapsed;
                            else
                                overlay.displayed = true;
                        }
                        else
                        {
                            overlay = new StampOrderingOverlay();
                            SceneView.AddOverlayToActiveView(overlay);
                            overlay.displayed = true;
                            overlay.collapsed = false;
                        }
                    }
                });
                
                menu.ShowAsContext();
            });
            openOrderManagerButton.text = "Order Manager";
            openOrderManagerButton.tooltip = "Open the Stamp Order Manager to view and reorder all stamps";
            openOrderManagerButton.style.marginLeft = 5;
            headerRow.Add(openOrderManagerButton);
            
            // Add the header row to the container
            stampManagementContainer.Add(headerRow);
            
            // Create a container for the foldout content
            var foldoutContent = new VisualElement();
            foldoutContent.style.display = DisplayStyle.None;
            
            // Create our stamp ordering control
            stampOrderingControl = new StampOrderingControl
            {
                ShowHeader = false, // No header in the inspector
                ShowAutoRefreshToggle = true,
                ListHeight = 200f
            };
            
            // Add the control to the foldout content
            foldoutContent.Add(stampOrderingControl);
            
            // Add the foldout content to the container
            stampManagementContainer.Add(foldoutContent);
            
            // Connect the foldout toggle to show/hide the content
            stampOrderingFoldout.RegisterValueChangedCallback(evt => {
                foldoutContent.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            
            // Add the container to the inspector
            inspector.Add(stampManagementContainer);
        }
    }
}