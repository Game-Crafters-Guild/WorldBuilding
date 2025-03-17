using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    [CustomPropertyDrawer(typeof(MaskFalloff))]
    public class MaskFalloffPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement container = new VisualElement();
            
            container.Add(CreateSliderForProperty("Falloff", property.FindPropertyRelative("Min"), property.FindPropertyRelative("Max")));
            container.Add(CreateSliderForProperty("Range", property.FindPropertyRelative("MaskMin"), property.FindPropertyRelative("MaskMax")));
            
            return container;

            
        }

        private MinMaxSlider CreateSliderForProperty(string label, SerializedProperty minProperty, SerializedProperty maxProperty)
        {
            float currentMin = minProperty.floatValue;
            float currentMax = maxProperty.floatValue;
            MinMaxSlider slider = new MinMaxSlider();
            slider.AddToClassList("unity-base-field__aligned");
            slider.label = label;
            slider.SetValueWithoutNotify(new Vector2(currentMin, currentMax));
            slider.lowLimit = 0.0f;
            slider.highLimit = 1.0f;
            slider.style.paddingRight = 4;
            slider.RegisterValueChangedCallback(OnValueChanged);

            void OnValueChanged(ChangeEvent<Vector2> evt)
            {
                minProperty.floatValue = evt.newValue.x;
                maxProperty.floatValue = evt.newValue.y;
                maxProperty.serializedObject.ApplyModifiedProperties();
            }
            
            return slider;
        }
    }
}