using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(BaseWorldBuilder), editorForChildClasses: false)]
public class BaseWorldBuilderEditor : Editor
{ 
    //public List<
    BaseWorldBuilder Target => target as BaseWorldBuilder;
    private ListView m_ModifiersListView;
    public override VisualElement CreateInspectorGUI()
    {
        // Create a new VisualElement to be the root of the Inspector UI.
        VisualElement inspector = new VisualElement();
    
        // Attach a default Inspector to the Foldout.
        InspectorElement.FillDefaultInspector(inspector, serializedObject, this);

        Button markDirtyButton = new Button();
        markDirtyButton.text = "Mark Dirty\n(Will become automatic later)";
        markDirtyButton.clicked += () =>
        {
            Target.IsDirty = true;
            EditorUtility.SetDirty(Target);
        };
        inspector.Add(markDirtyButton);

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

    private void OnPropertyFieldValueChanged(SerializedPropertyChangeEvent evt)
    {
        Target.IsDirty = true;
    }
}