using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(MaskFalloff))]
public class MaskFalloffPropertyDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        float currentMin = property.FindPropertyRelative("Min").floatValue;
        float currentMax = property.FindPropertyRelative("Max").floatValue;
        MinMaxSlider slider = new MinMaxSlider();
        slider.label = "Falloff";
        slider.SetValueWithoutNotify(new Vector2(currentMin, currentMax));
        slider.lowLimit = 0.0f;
        slider.highLimit = 1.0f;
        slider.RegisterValueChangedCallback(OnValueChanged);
        return slider;
        
        void OnValueChanged(ChangeEvent<Vector2> evt)
        {
            property.FindPropertyRelative("Min").floatValue = evt.newValue.x;
            property.FindPropertyRelative("Max").floatValue = evt.newValue.y;
            property.serializedObject.ApplyModifiedProperties();
        }
    }
}
