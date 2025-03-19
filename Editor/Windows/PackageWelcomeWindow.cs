using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    /// <summary>
    /// Welcome window that automatically opens when the package is first imported.
    /// </summary>
    public class PackageWelcomeWindow : EditorWindow
    {
        private const string WindowTitle = "Welcome to World Building";
        private const int WindowWidth = 600;
        private const int WindowHeight = 500;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // Wait until the first editor update to ensure everything is loaded
            EditorApplication.delayCall += () =>
            {
                if (!WorldBuildingUserPreferences.instance.HasShownWelcomeWindow)
                {
                    ShowWindow();
                    WorldBuildingUserPreferences.instance.HasShownWelcomeWindow = true;
                }
            };
        }

        /// <summary>
        /// Show the welcome window. Can be called manually if needed.
        /// </summary>
        [MenuItem("Window/Game Crafters Guild/Welcome to World Building")]
        public static void ShowWindow()
        {
            var window = GetWindow<PackageWelcomeWindow>(true, WindowTitle, true);
            
            // Center window on screen
            window.position = new Rect(
                (Screen.currentResolution.width - WindowWidth) / 2,
                (Screen.currentResolution.height - WindowHeight) / 2,
                WindowWidth,
                WindowHeight);
                
            window.minSize = new Vector2(WindowWidth, WindowHeight);
        }

        private void OnEnable()
        {
            // Add stylesheet
            var styleSheet = Resources.Load<StyleSheet>("PackageWelcomeWindowStyle");
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }
            
            // Load UI from UXML
            var uxmlAsset = Resources.Load<VisualTreeAsset>("PackageWelcomeWindowLayout");
            if (uxmlAsset != null)
            {
                uxmlAsset.CloneTree(rootVisualElement);
                SetupButtonCallbacks();
            }
            else
            {
                // If UXML file is not found, create UI programmatically
                Debug.LogWarning("PackageWelcomeWindowLayout.uxml not found.");
            }
        }

        private void SetupButtonCallbacks()
        {
            // Set up button callbacks
            var docsButton = rootVisualElement.Q<Button>("docs-button");
            if (docsButton != null)
            {
                docsButton.clicked += () => Application.OpenURL("https://github.com/YourOrganization/WorldBuilding/wiki");
            }

            var closeButton = rootVisualElement.Q<Button>("close-button");
            if (closeButton != null)
            {
                closeButton.clicked += Close;
            }
        }
    }
} 