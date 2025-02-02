using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    [CustomPropertyDrawer(typeof(NoiseProperties))]
    public class NoisePropertiesPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement container = new VisualElement();

            VisualElement imageContainer = new VisualElement();
            imageContainer.style.alignItems = Align.Center;
            imageContainer.style.marginBottom = imageContainer.style.marginTop =
                imageContainer.style.marginLeft = imageContainer.style.marginRight = 4;

            Image image = new Image();
            image.style.maxWidth = 200;
            image.style.maxHeight = 200;
            imageContainer.Add(image);


            var childProperty = property.Copy();
            var endProperty = childProperty.GetEndProperty();
            childProperty.NextVisible(true);
            while (!SerializedProperty.EqualContents(childProperty, endProperty))
            {
                var propertyField = new PropertyField(childProperty);
                propertyField.Bind(childProperty.serializedObject);
                propertyField.TrackPropertyValue(childProperty, OnPropertyValueChanged);

                container.Add(propertyField);
                childProperty.NextVisible(false);
            }

            NoiseProperties properties = property.boxedValue as NoiseProperties;
            image.image = properties.NoiseTexture;
            container.Add(imageContainer);
            imageContainer.RegisterCallback<GeometryChangedEvent>(OnImageGeometryChanged);

            void OnImageGeometryChanged(GeometryChangedEvent evt)
            {
                if (Mathf.Approximately(evt.newRect.size.x, evt.newRect.size.y))
                {
                    return;
                }

                image.style.height = image.style.width = evt.newRect.size.x;
            }

            return container;

            void OnPropertyValueChanged(SerializedProperty serializedProperty)
            {
                var dirtyProperty = property?.FindPropertyRelative("m_IsDirty");
                if (dirtyProperty != null)
                {
                    dirtyProperty.boolValue = true;
                }

                NoiseProperties noiseProperties = property.boxedValue as NoiseProperties;
                image.image = noiseProperties.NoiseTexture;
                property?.serializedObject?.ApplyModifiedProperties();
            }
        }
    }
}