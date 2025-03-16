using UnityEditor;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    public class StampOrderingWindow : EditorWindow
    {
        private StampOrderingControl m_StampOrderingControl;

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
            m_StampOrderingControl = new StampOrderingControl
            {
                ShowHeader = true,
                ShowAutoRefreshToggle = true,
                ListHeight = 300f
            };
            m_StampOrderingControl.AutoAdjustListHeight = true;

            // Add it to the window's root
            rootVisualElement.Add(m_StampOrderingControl);
        }

        private void OnDisable()
        {
            // Clean up event handlers
            if (m_StampOrderingControl != null)
            {
                // No need for manual cleanup as the control handles its own cleanup
                // when it's detached from the panel
                m_StampOrderingControl = null;
            }
        }
    }
} 