using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using static System.String;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    [CustomEditor(typeof(Stamp), editorForChildClasses: true)]
    public class StampEditor : UnityEditor.Editor
    {
        Stamp Target => target as Stamp;
        private ListView m_ModifiersListView;
        private VisualElement m_ShapeInspector;

        public override VisualElement CreateInspectorGUI()
        {
            // Create a new VisualElement to be the root of the Inspector UI.
            VisualElement inspector = new VisualElement();

            // Attach a default Inspector to the Foldout.
            InspectorElement.FillDefaultInspector(inspector, serializedObject, this);

            // Add a button next to Priority field to open Stamp Order Manager
            CustomizePriorityField(inspector);

            m_ShapeInspector = new Foldout() { text = "Shape Properties" };
            CreateShapeDropdown(inspector);
            FillShapeInspector(m_ShapeInspector, inspector);
            inspector.Add(m_ShapeInspector);

            /*Button markDirtyButton = new Button();
            markDirtyButton.text = "Mark Dirty\n(Will become automatic later)";
            markDirtyButton.clicked += () =>
            {
                Target.IsDirty = true;
                EditorUtility.SetDirty(Target);
            };
            inspector.Add(markDirtyButton);*/

            PropertyField modfiersField = inspector.Q<PropertyField>("PropertyField:m_Modifiers");
            if (modfiersField != null)
            {
                inspector.Remove(modfiersField);
                inspector.Add(modfiersField);
            }

            inspector.RegisterCallbackOnce<GeometryChangedEvent>(evt =>
            {
                VisualElement target = evt.target as VisualElement;
                target?.schedule.Execute(() =>
                {
                    foreach (var child in inspector.Children())
                    {
                        if (child is PropertyField propertyField)
                        {
                            propertyField.RegisterValueChangeCallback(OnPropertyFieldValueChanged);
                        }
                    }
                });
            });
            // Return the finished Inspector UI.
            return inspector;
        }

        private List<string> m_ShapeNames;
        private List<Type> m_ShapeTypes;

        private void CreateShapeDropdown(VisualElement inspector)
        {
            var shapeProperty = serializedObject.FindProperty("m_Shape");
            m_ShapeInspector.TrackPropertyValue(shapeProperty, _ => FillShapeInspector(m_ShapeInspector, inspector));

            var shape = shapeProperty.objectReferenceValue;
            Type currentShapeType = null;
            if (shape != null)
            {
                currentShapeType = shape.GetType();
            }

            TypeCache.TypeCollection shapeTypesCollection = TypeCache.GetTypesDerivedFrom<StampShape>();
            m_ShapeNames = new List<string>();
            m_ShapeTypes = new List<Type>(shapeTypesCollection.Count);
            string selectedShapeName = null;
            foreach (var shapeType in shapeTypesCollection)
            {
                m_ShapeTypes.Add(shapeType);
            }

            m_ShapeTypes.Sort((x, y) => CompareOrdinal(x.Name, y.Name));
            foreach (var shapeType in m_ShapeTypes)
            {
                string shapeName = shapeType.Name;
                if (shapeName.EndsWith("Shape"))
                {
                    shapeName = shapeName.Substring(0, shapeName.Length - "Shape".Length);
                }

                shapeName = ObjectNames.NicifyVariableName(shapeName);
                if (currentShapeType == shapeType)
                {
                    selectedShapeName = shapeName;
                }

                m_ShapeNames.Add(shapeName);
            }

            DropdownField dropdownField = new DropdownField() { label = "Stamp Shape" };
            dropdownField.choices = m_ShapeNames;
            dropdownField.RegisterValueChangedCallback(OnShapeDropdownValueChanged);
            if (selectedShapeName != null)
            {
                dropdownField.SetValueWithoutNotify(selectedShapeName);
            }

            dropdownField.AddToClassList("unity-base-field__aligned");

            inspector.Add(dropdownField);
        }

        private void OnShapeDropdownValueChanged(ChangeEvent<string> evt)
        {
            int shapeIndex = m_ShapeNames.IndexOf(evt.newValue);
            Type newShapeType = m_ShapeTypes[shapeIndex];
            var shapeProperty = serializedObject.FindProperty("m_Shape");
            var oldComponent = shapeProperty.objectReferenceValue;
            if (!Target.gameObject.TryGetComponent(newShapeType, out Component shapeComponent))
            {
                shapeComponent = Target.gameObject.AddComponent(newShapeType);
            }

            shapeProperty.objectReferenceValue = shapeComponent;
            var stampShape = shapeComponent as StampShape;
            stampShape.GenerateMask();

            if (oldComponent != null)
            {
                DestroyImmediate(oldComponent);
            }

            serializedObject.ApplyModifiedProperties();
            Target.IsDirty = true;
        }

        private void FillShapeInspector(VisualElement shapeInspector, VisualElement inspector)
        {
            shapeInspector.Clear();
            var shapeProperty = serializedObject.FindProperty("m_Shape");
            if (shapeProperty.objectReferenceValue == null)
            {
                shapeInspector.style.display = DisplayStyle.None;
                return;
            }

            var shapeSO = new SerializedObject(shapeProperty.objectReferenceValue);
            InspectorElement.FillDefaultInspector(m_ShapeInspector, shapeSO, this);
            m_ShapeInspector.Q("PropertyField:m_Script")?.RemoveFromHierarchy();
            m_ShapeInspector.Bind(shapeSO);
            m_ShapeInspector?.schedule.Execute(() =>
            {
                foreach (var child in m_ShapeInspector.Children())
                {
                    if (child is PropertyField propertyField)
                    {
                        propertyField.RegisterValueChangeCallback(OnShapePropertyFieldValueChanged);
                    }
                }
            });
            shapeInspector.style.display = m_ShapeInspector.childCount == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnShapePropertyFieldValueChanged(SerializedPropertyChangeEvent evt)
        {
            var so = evt.changedProperty.serializedObject;
            if (so is { targetObject: StampShape stampShape })
            {
                stampShape.GenerateMask();
            }

            Target.IsDirty = true;
        }

        private void OnPropertyFieldValueChanged(SerializedPropertyChangeEvent evt)
        {
            Target.IsDirty = true;
        }

        private void CustomizePriorityField(VisualElement inspector)
        {
            // Find the Priority field
            PropertyField priorityField = inspector.Q<PropertyField>("PropertyField:m_Priority");
            if (priorityField == null) return;

            // Create a container for field and button
            VisualElement container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            
            // Remove the original field from the inspector
            priorityField.RemoveFromHierarchy();
            
            // Add the field to our container
            container.Add(priorityField);
            priorityField.style.flexGrow = 1;
            
            // Create a button to open the Stamp Order Manager
            Button openOrderManagerButton = new Button(() => StampOrderingWindow.ShowWindow());
            openOrderManagerButton.text = "Order Manager";
            openOrderManagerButton.tooltip = "Open the Stamp Order Manager to view and reorder all stamps";
            openOrderManagerButton.style.marginLeft = 5;
            container.Add(openOrderManagerButton);
            
            // Find where to insert the container (after the script field)
            var scriptField = inspector.Q<PropertyField>("PropertyField:m_Script");
            if (scriptField != null)
            {
                int scriptIndex = inspector.IndexOf(scriptField);
                inspector.Insert(scriptIndex + 1, container);
            }
            else
            {
                inspector.Insert(0, container);
            }
        }
    }
}