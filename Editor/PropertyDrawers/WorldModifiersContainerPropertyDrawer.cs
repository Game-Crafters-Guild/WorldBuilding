using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(WorldModifiersContainer))]
public class WorldModifiersContainerPropertyDrawer : PropertyDrawer
{
    public class ModifierTabView<ModifierType> : Tab where ModifierType : WorldModifier
    {
        //private ListView m_ModifiersListView;
        private ScrollView m_ScrollView;
        private SerializedProperty m_Property;

        public ModifierTabView(SerializedProperty property, string text) : base()
        {
            m_Property = property;
            label = text;

            m_ScrollView = new ScrollView()  { viewDataKey = $"{text}-Modifiers-ScrollView" };
            m_ScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            m_ScrollView.style.maxHeight = 800;
            Add(m_ScrollView);

            RebuildList();
            Button addModifier = new Button() { text = $"Add {text} Modifier" };
            addModifier.style.marginTop = 8;
            addModifier.clicked += () => WorldModifiersFactoryUI.ShowWorldModifiersContextMenu<ModifierType>(OnAddModifier);
            Add(addModifier);
        }

        private void RebuildList()
        {
            m_ScrollView.Clear();
            
            for (int i = 0; i < m_Property.arraySize; i++)
            {
                SerializedProperty property = m_Property.GetArrayElementAtIndex(i);
                AddFieldForProperty(property);
            }
        }
        
        private void OnAddModifier(ModifierType modifier)
        {
            if (modifier == null)
                return;

            ++m_Property.arraySize;
            SerializedProperty property = m_Property.GetArrayElementAtIndex(m_Property.arraySize - 1);
            property.boxedValue = modifier;
            SerializedObject so = m_Property.serializedObject;
            so.ApplyModifiedProperties();
            AddFieldForProperty(property);
        }

        private void AddFieldForProperty(SerializedProperty property)
        {
            var propertyContainer = new VisualElement();
            propertyContainer.style.borderBottomWidth = 1.0f;
            propertyContainer.style.borderBottomColor = Color.black;
            propertyContainer.style.paddingTop = 2;
            propertyContainer.style.paddingBottom = 2;
            m_ScrollView.Add(propertyContainer);
            
            VisualElement headerContainer = new Box();
            headerContainer.style.marginTop = headerContainer.style.marginBottom = 2; 
            propertyContainer.Add(headerContainer);
            
            Label propertyLabel = new Label() { text = ObjectNames.NicifyVariableName(property.boxedValue.GetType().Name) };
            propertyLabel.style.alignSelf = new StyleEnum<Align>(Align.Center);
            propertyLabel.style.unityTextAlign = new StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
            propertyLabel.style.fontSize = 12;
            headerContainer.Add(propertyLabel);

            
            var childProperty = property.Copy();
            var endProperty = childProperty.GetEndProperty();
            childProperty.NextVisible(true);
            while (!SerializedProperty.EqualContents(childProperty, endProperty))
            {
                var propertyField = new PropertyField(childProperty);
                propertyField.Bind(childProperty.serializedObject);
                propertyContainer.Add(propertyField);
                childProperty.NextVisible(false);
            }
            
            WorldModifier modifier = property.boxedValue as WorldModifier;
            
            void PopupMenuCallback(ContextualMenuPopulateEvent obj)
            {
                obj.menu.AppendAction("Edit Modifier Script", OnEditModifierScript);
                obj.menu.AppendAction("Remove", OnRemoveModifier);

                void OnEditModifierScript(DropdownMenuAction dropdownMenuAction)
                {
                    string path = modifier.FilePath.Replace("\\", "/");
                    string relativePath = path.StartsWith(Application.dataPath)
                        ? ("Assets" + path.Substring(Application.dataPath.Length))
                        : path;
                    Unity.CodeEditor.CodeEditor.Editor.CurrentCodeEditor.OpenProject(relativePath);
                }
                
                void OnRemoveModifier(DropdownMenuAction dropdownMenuAction)
                {
                    for (int i = 0; i < m_Property.arraySize; i++)
                    {
                        SerializedProperty arrayProperty = m_Property.GetArrayElementAtIndex(i);
                        if (property.boxedValue == arrayProperty.boxedValue)
                        {
                            property.boxedValue = null;
                            m_Property.DeleteArrayElementAtIndex(i);
                            
                            SerializedObject so = m_Property.serializedObject;
                            so.ApplyModifiedProperties();
                            RebuildList();
                            return;
                        }
                    }
                }
            }
            propertyContainer.AddManipulator(new ContextualMenuManipulator(PopupMenuCallback));
        }
    }
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        WorldModifiersContainer target = property.boxedValue as WorldModifiersContainer; 
        // Create property container element.
        var container = new Box();
        container.style.borderBottomWidth = 1.0f;
        container.style.borderTopWidth = 1.0f;
        container.style.borderLeftWidth = 1.0f;
        container.style.borderRightWidth = 1.0f;
        container.style.borderRightColor = container.style.borderLeftColor = container.style.borderTopColor = container.style.borderBottomColor = Color.black;
        container.style.marginTop = 2.0f;
        container.style.paddingTop = 2;
        container.style.paddingBottom = 2;

        Label header = new Label("Modifiers");
        header.style.alignSelf = new StyleEnum<Align>(Align.Center);
        header.style.unityTextAlign = new StyleEnum<TextAnchor>(TextAnchor.MiddleCenter);
        header.style.fontSize = 14;
        container.Add(header);
        
        TabView view = new TabView()  { viewDataKey = "Modifiers-TabView" };
        view.Add(new ModifierTabView<ITerrainHeightModifier>(property.FindPropertyRelative("TerrainHeightModifiers"), "Height") { viewDataKey = "Height-Modifiers-Tab" });
        view.Add(new ModifierTabView<ITerrainSplatModifier>(property.FindPropertyRelative("TerrainSplatModifiers"), "Splat") { viewDataKey = "Splat-Modifiers-Tab" });
        view.Add(new ModifierTabView<IGameObjectModifier>(property.FindPropertyRelative("GameObjectModifiers"), "GameObject") { viewDataKey = "GameObject-Modifiers-Tab" });
        container.Add(view);
        
        

        return container;
    }
}
