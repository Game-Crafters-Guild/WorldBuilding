using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(SplineRegion))]
public class SplineRegionEditor : BaseWorldBuilderEditor
{
    SplineRegion Target => target as SplineRegion;
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement inspector = base.CreateInspectorGUI();
        return inspector;
    }
}
