using UnityEditor;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    public class StampOrderingWindow : EditorWindow
    {
        private StampOrderingControl stampOrdering;

        [MenuItem("Window/Game Crafters Guild/Stamp Order Manager")]
        public static void ShowWindow()
        {
            StampOrderingWindow wnd = GetWindow<StampOrderingWindow>();
            wnd.titleContent = new GUIContent("Stamp Order Manager");
            wnd.minSize = new Vector2(500, 300);
        }

        private void OnEnable()
        {
            // Create and configure the stamp ordering control
            stampOrdering = new StampOrderingControl
            {
                ShowHeader = true,
                ShowAutoRefreshToggle = true,
                ListHeight = 300f
            };

            // Add it to the window's root
            rootVisualElement.Add(stampOrdering);
        }

        private void OnDisable()
        {
            // Clean up event handlers
            if (stampOrdering != null)
            {
                // No need for manual cleanup as the control handles its own cleanup
                // when it's detached from the panel
                stampOrdering = null;
            }
        }
    }
} 