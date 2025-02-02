using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    public class WorldModifiersFactoryUI
    {
        public static void ShowWorldModifiersContextMenu<ModifierType>(Action<ModifierType> callback)
        {
            var worldModifierTypes = TypeCache.GetTypesDerivedFrom<ModifierType>();
            GenericMenu menu = new UnityEditor.GenericMenu();
            foreach (var type in worldModifierTypes)
            {
                menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(type.Name)), false, data =>
                {
                    var instance = Activator.CreateInstance(type);
                    callback?.Invoke((ModifierType)instance);
                }, type);
            }

            menu.ShowAsContext();
        }
    }
}