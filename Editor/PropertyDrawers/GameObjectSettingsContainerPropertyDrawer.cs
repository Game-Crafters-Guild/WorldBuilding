using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    [CustomPropertyDrawer(typeof(GameObjectSettingsContainer))]
    public class GameObjectSettingsContainerPropertyDrawer : PropertyDrawer
    {
        private SerializedProperty m_ListProperty; 
            
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement
            {
                style =
                {
                    marginLeft = 12
                }
            };

            // Create a foldout for the property
            var foldout = new Foldout
            {
                //text = property.displayName,
                text = "Prefabs",
                value = property.isExpanded
            };
            foldout.RegisterValueChangedCallback(evt => property.isExpanded = evt.newValue);
            container.Add(foldout);
            
            var contentContainer = new VisualElement();
            foldout.Add(contentContainer);
            
            // Add constraints list
            m_ListProperty = property.FindPropertyRelative("GameObjects");
            var listContainer = new VisualElement();
            contentContainer.Add(listContainer);
            
            RefreshList(m_ListProperty, listContainer);
            
            // Add button to add constraints
            var addConstraintBtn = new Button(() => OnCreateItem(listContainer))
            {
                text = "Add Prefab"
            };
            addConstraintBtn.style.marginTop = 5;
            contentContainer.Add(addConstraintBtn);
            
            return container;
        }

        private void RefreshList(SerializedProperty listProperty, VisualElement listContainer)
        {
            listContainer.Clear();
            
            int count = m_ListProperty.arraySize;
            
            if (count == 0)
            {
                var helpBox = new HelpBox("No Items have been added.", HelpBoxMessageType.Info);
                listContainer.Add(helpBox);
            }
            
            for (int i = 0; i < count; i++)
            {
                var elementProp = m_ListProperty.GetArrayElementAtIndex(i);
                if (elementProp == null) continue;

                var itemElement = CreateListItem(elementProp);
                listContainer.Add(itemElement);
                
                int index = i; // Capture index for closure
                var removeBtn = new Button(() => RemoveItem(index, listContainer))
                {
                    text = "Remove"
                };
                removeBtn.style.width = 70;
                
                itemElement.Add(removeBtn);
            }
        }

        private void RemoveItem(int index, VisualElement listContainer)
        {
            SerializedProperty arrayProperty = m_ListProperty.GetArrayElementAtIndex(index);
            m_ListProperty.DeleteArrayElementAtIndex(index);
            m_ListProperty.serializedObject.ApplyModifiedProperties();
            listContainer.RemoveAt(index);
        }

        private VisualElement CreateListItem(SerializedProperty property)
        {
            VisualElement container = new Box();
            container.style.borderBottomColor = container.style.borderTopColor =
                container.style.borderLeftColor = container.style.borderRightColor = Color.black;
            container.style.borderBottomWidth = container.style.borderTopWidth =
                container.style.borderLeftWidth = container.style.borderRightWidth = 1.0f;
            container.style.paddingBottom = container.style.paddingTop = container.style.paddingLeft = container.style.paddingRight = 4.0f;
            container.style.marginTop = container.style.marginBottom = 5.0f;
            var childProperty = property.Copy();
            var endProperty = childProperty.GetEndProperty();
            childProperty.NextVisible(true);
            while (!SerializedProperty.EqualContents(childProperty, endProperty))
            {
                var propertyField = new PropertyField(childProperty);
                propertyField.Bind(childProperty.serializedObject);
                container.Add(propertyField);
                childProperty.NextVisible(false);
            }
            return container;
        }

        private void OnCreateItem(VisualElement listContainer)
        {
            Undo.RecordObject(m_ListProperty.serializedObject.targetObject as UnityEngine.Object, "Add modifier");
            ++m_ListProperty.arraySize;
            SerializedProperty property = m_ListProperty.GetArrayElementAtIndex(m_ListProperty.arraySize - 1);
            property.boxedValue = new GameObjectModifier.GameObjectSettings();
            m_ListProperty.serializedObject.ApplyModifiedProperties();
            
            RefreshList(m_ListProperty, listContainer);
        }
    }
} 