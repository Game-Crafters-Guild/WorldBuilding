using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using GameCraftersGuild.WorldBuilding;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    [CustomPropertyDrawer(typeof(PlacementConstraintsContainer))]
    public class PlacementConstraintsContainerPropertyDrawer : PropertyDrawer
    {
        private static readonly List<Type> s_ConstraintTypes = new List<Type>();
        private static readonly Dictionary<Type, string> s_ConstraintTypeNames = new Dictionary<Type, string>();
        
        static PlacementConstraintsContainerPropertyDrawer()
        {
            // Find all constraint types using TypeCache
            var types = TypeCache.GetTypesDerivedFrom<IPlacementConstraint>();
            foreach (var type in types)
            {
                if (type.IsInterface || type.IsAbstract)
                    continue;
                
                s_ConstraintTypes.Add(type);
                
                // Get a friendly name for the constraint type
                string name = type.Name;
                if (name.EndsWith("Constraint"))
                    name = name.Substring(0, name.Length - "Constraint".Length);
                
                // Add spaces before capitals for better readability
                name = System.Text.RegularExpressions.Regex.Replace(name, "([A-Z])", " $1").Trim();
                
                s_ConstraintTypeNames[type] = name;
            }
        }
        
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
                text = "Placement Constraints",
                value = property.isExpanded
            };
            foldout.RegisterValueChangedCallback(evt => property.isExpanded = evt.newValue);
            container.Add(foldout);
            
            var contentContainer = new VisualElement();
            foldout.Add(contentContainer);
            
            // Add constraints list
            var constraintsProp = property.FindPropertyRelative("Constraints");
            var constraintsContainer = new VisualElement();
            contentContainer.Add(constraintsContainer);
            
            RefreshConstraintsList(constraintsProp, constraintsContainer);
            
            // Add button to add constraints
            var addConstraintBtn = new Button(() => ShowAddConstraintMenu(constraintsProp, constraintsContainer))
            {
                text = "Add Constraint"
            };
            addConstraintBtn.style.marginTop = 5;
            contentContainer.Add(addConstraintBtn);
            
            return container;
        }
        
        private void RefreshConstraintsList(SerializedProperty constraintsProp, VisualElement container)
        {
            container.Clear();
            
            int count = constraintsProp.arraySize;
            
            if (count == 0)
            {
                var helpBox = new HelpBox("No constraints have been added.", HelpBoxMessageType.Info);
                container.Add(helpBox);
            }
            
            for (int i = 0; i < count; i++)
            {
                var elementProp = constraintsProp.GetArrayElementAtIndex(i);
                if (elementProp == null) continue;
                
                var boxContainer = new Box();
                boxContainer.style.marginTop = 5;
                boxContainer.style.marginBottom = 5;
                
                // Header with constraint type and remove button
                var header = new VisualElement();
                header.style.flexDirection = FlexDirection.Row;
                header.style.justifyContent = Justify.SpaceBetween;
                
                // Get the constraint type for the header label
                string typeName = "Unknown Constraint";
                var managedReferenceFullTypeName = elementProp.managedReferenceFullTypename;
                if (!string.IsNullOrEmpty(managedReferenceFullTypeName))
                {
                    var parts = managedReferenceFullTypeName.Split(' ');
                    if (parts.Length > 1)
                    {
                        var assemblyAndType = parts[1].Split('.');
                        if (assemblyAndType.Length > 0)
                        {
                            string className = assemblyAndType[assemblyAndType.Length - 1];
                            if (className.EndsWith("Constraint"))
                                className = className.Substring(0, className.Length - "Constraint".Length);
                            
                            typeName = className + " Constraint";
                        }
                    }
                }
                
                var constraintLabel = new Label(typeName);
                constraintLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                header.Add(constraintLabel);
                
                int index = i; // Capture index for closure
                var removeBtn = new Button(() => RemoveConstraint(constraintsProp, index, container))
                {
                    text = "Remove"
                };
                removeBtn.style.width = 70;
                header.Add(removeBtn);
                
                boxContainer.Add(header);
                
                // Constraint property fields
                /*var constraintFields = new PropertyField(elementProp);
                constraintFields.Bind(constraintsProp.serializedObject);
                boxContainer.Add(constraintFields);*/
                
                VisualElement fieldsContainer = new VisualElement();
                //fieldsContainer.style.paddingLeft = 2;
                //fieldsContainer.style.paddingRight = 2;
                boxContainer.Add(fieldsContainer);

                var childProperty = elementProp.Copy();
                var endProperty = childProperty.GetEndProperty();
                childProperty.NextVisible(true);
                while (!SerializedProperty.EqualContents(childProperty, endProperty))
                {
                    var propertyField = new PropertyField(childProperty);
                    propertyField.Bind(childProperty.serializedObject);
                    fieldsContainer.Add(propertyField);
                    childProperty.NextVisible(false);
                }
                container.Add(boxContainer);
            }
        }
        
        private void ShowAddConstraintMenu(SerializedProperty constraintsProp, VisualElement constraintsContainer)
        {
            var menu = new GenericMenu();
            
            // Get the current constraints to check for duplicates
            var existingTypes = new HashSet<Type>();
            
            for (int i = 0; i < constraintsProp.arraySize; i++)
            {
                var elementProp = constraintsProp.GetArrayElementAtIndex(i);
                var managedReferenceFullTypeName = elementProp.managedReferenceFullTypename;
                if (!string.IsNullOrEmpty(managedReferenceFullTypeName))
                {
                    var parts = managedReferenceFullTypeName.Split(' ');
                    if (parts.Length > 1)
                    {
                        Type type = Type.GetType(parts[1]);
                        if (type != null)
                            existingTypes.Add(type);
                    }
                }
            }
            
            // Add menu items for each constraint type
            foreach (var type in s_ConstraintTypes)
            {
                string menuItemName = s_ConstraintTypeNames[type];
                bool isDuplicate = existingTypes.Contains(type);
                
                if (isDuplicate)
                {
                    menu.AddDisabledItem(new GUIContent(menuItemName + " (Already Added)"));
                }
                else
                {
                    menu.AddItem(new GUIContent(menuItemName), false, () => AddConstraint(constraintsProp, type, constraintsContainer));
                }
            }
            
            menu.ShowAsContext();
        }
        
        private void AddConstraint(SerializedProperty constraintsProp, Type constraintType, VisualElement constraintsContainer)
        {
            // Record undo
            constraintsProp.serializedObject.Update();
            Undo.RecordObject(constraintsProp.serializedObject.targetObject, "Add Placement Constraint");
            
            // Add new constraint
            int index = constraintsProp.arraySize;
            constraintsProp.arraySize++;
            var elementProp = constraintsProp.GetArrayElementAtIndex(index);
            elementProp.managedReferenceValue = Activator.CreateInstance(constraintType);
            
            constraintsProp.serializedObject.ApplyModifiedProperties();
            
            // Refresh the constraints list
            RefreshConstraintsList(constraintsProp, constraintsContainer);
        }
        
        private void RemoveConstraint(SerializedProperty constraintsProp, int index, VisualElement constraintsContainer)
        {
            // Record undo
            constraintsProp.serializedObject.Update();
            Undo.RecordObject(constraintsProp.serializedObject.targetObject, "Remove Placement Constraint");
            
            // Remove the constraint
            constraintsProp.DeleteArrayElementAtIndex(index);
            
            constraintsProp.serializedObject.ApplyModifiedProperties();
            
            // Refresh the constraints list
            RefreshConstraintsList(constraintsProp, constraintsContainer);
        }
    }
} 