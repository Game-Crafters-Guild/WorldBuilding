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
        return inspector;
    }
}
