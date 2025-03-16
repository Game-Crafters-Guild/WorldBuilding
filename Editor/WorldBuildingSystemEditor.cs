/*using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    [CustomEditor(typeof(WorldBuildingSystem))]
    public class WorldBuildingSystemEditor : UnityEditor.Editor
    {
        private StampOrderingControl stampOrderingControl;
        
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            
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
            root.Add(stampOrderingSection);
            
            return root;
        }
    }
} */