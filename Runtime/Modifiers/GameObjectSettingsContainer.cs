using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding
{
    /// <summary>
    /// Container class for GameObjectSettings to allow for proper initialization of new items
    /// through a custom property drawer
    /// </summary>
    [Serializable]
    public class GameObjectSettingsContainer
    {
        [SerializeField]
        public List<GameObjectModifier.GameObjectSettings> GameObjects = new List<GameObjectModifier.GameObjectSettings>();

        /// <summary>
        /// Adds a new GameObjectSettings instance with proper initialization
        /// </summary>
        public void AddGameObjectSettings()
        {
            // Create a new instance with proper constructor initialization
            var settings = new GameObjectModifier.GameObjectSettings();
            GameObjects.Add(settings);
        }

        /// <summary>
        /// Removes a GameObjectSettings at the specified index
        /// </summary>
        public void RemoveGameObjectSettings(int index)
        {
            if (index >= 0 && index < GameObjects.Count)
            {
                GameObjects.RemoveAt(index);
            }
        }
    }
} 