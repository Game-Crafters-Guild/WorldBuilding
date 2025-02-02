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
            
            float currentMin = property.FindPropertyRelative("Min").floatValue;
            float currentMax = property.FindPropertyRelative("Max").floatValue;
            MinMaxSlider slider = new MinMaxSlider();
            slider.AddToClassList("unity-base-field__aligned");
            slider.label = "Falloff";
            slider.SetValueWithoutNotify(new Vector2(currentMin, currentMax));
            slider.lowLimit = 0.0f;
            slider.highLimit = 1.0f;
            slider.style.paddingRight = 4;
            slider.RegisterValueChangedCallback(OnValueChanged); 
            container.Add(slider);
            
            return container;

            void OnValueChanged(ChangeEvent<Vector2> evt)
            {
                property.FindPropertyRelative("Min").floatValue = evt.newValue.x;
                property.FindPropertyRelative("Max").floatValue = evt.newValue.y;
                property.serializedObject.ApplyModifiedProperties();
            }
        }
    }
}