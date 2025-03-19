using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    /// <summary>
    /// Tracks whether the package welcome window has been shown to the user.
    /// Persisted in the Editor preferences.
    /// </summary>
    internal class WorldBuildingUserPreferences : ScriptableSingleton<WorldBuildingUserPreferences>
    {
        [SerializeField] private bool m_HasShownWelcomeWindow = false;

        /// <summary>
        /// Returns whether the welcome window has been shown to the user
        /// </summary>
        public bool HasShownWelcomeWindow
        {
            get => m_HasShownWelcomeWindow;
            set
            {
                m_HasShownWelcomeWindow = value;
                Save(true);
            }
        }

        /// <summary>
        /// Reset the tracker for testing purposes
        /// </summary>
        public void Reset()
        {
            m_HasShownWelcomeWindow = false;
            Save(true);
        }
    }
} 