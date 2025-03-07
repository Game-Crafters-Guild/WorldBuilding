using System;

namespace GameCraftersGuild.WorldBuilding
{
    [Serializable]
    public abstract class WorldModifier
    {
        public bool Enabled = true;
        
        // Private backing field for enabled property to detect changes
        private bool m_PreviousEnabled = true;

        protected string GetFilePath([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
            path;

        public abstract string FilePath { get; }
        
        /// <summary>
        /// Called when a modifier is first added or when being validated
        /// </summary>
        public virtual void OnInitialize() { }
        
        /// <summary>
        /// Called when the modifier is disabled or removed
        /// </summary>
        public virtual void OnCleanup() { }
        
        /// <summary>
        /// Returns true if the enabled state has changed
        /// </summary>
        public virtual bool HasEnabledStateChanged()
        {
            bool stateChanged = m_PreviousEnabled != Enabled;
            m_PreviousEnabled = Enabled;
            return stateChanged;
        }
    }
}