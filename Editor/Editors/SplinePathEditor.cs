using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(SplinePath))]
public class SplinePathEditor : BaseWorldBuilderEditor
{
    SplinePath Target => target as SplinePath;
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement inspector = base.CreateInspectorGUI();
        inspector.Q<PropertyField>("PropertyField:Width").RegisterValueChangeCallback(OnWidthValueChanged);
        return inspector;
    }

    private void OnWidthValueChanged(SerializedPropertyChangeEvent evt)
    {
        Target.GenerateMask();
        Target.IsDirty = true;
    }
}
