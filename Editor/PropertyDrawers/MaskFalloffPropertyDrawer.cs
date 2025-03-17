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
            
            container.Add(new PropertyField(property.FindPropertyRelative(nameof(MaskFalloff.FalloffFunction))));
            container.Add(CreateSliderForProperty("Falloff Intensity", property.FindPropertyRelative(nameof(MaskFalloff.MinIntensity)), property.FindPropertyRelative(nameof(MaskFalloff.MaxIntensity))));
            container.Add(CreateSliderForProperty("Falloff Area", property.FindPropertyRelative(nameof(MaskFalloff.MaskMin)), property.FindPropertyRelative(nameof(MaskFalloff.MaskMax)), "Only mask values between MaskMin and MaskMax will be affected"));
            container.Add(new PropertyField(property.FindPropertyRelative(nameof(MaskFalloff.InnerFalloff))));
            
            return container;

            
        }

        private MinMaxSlider CreateSliderForProperty(string label, SerializedProperty minProperty, SerializedProperty maxProperty, string tooltip = null)
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
            
            slider.tooltip = tooltip;
            
            return slider;
        }
    }
}