using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(NoiseProperties))]
public class NoisePropertiesPropertyDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement container = new VisualElement();
        
        VisualElement imageContainer = new VisualElement();
        imageContainer.style.alignItems = Align.Center;
        imageContainer.style.marginBottom = imageContainer.style.marginTop = imageContainer.style.marginLeft = imageContainer.style.marginRight = 4;
        
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

        GenerateNoiseTexture(image, property);
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
            if (serializedProperty.name == "NoiseResolution")
            {
                return;
            }
            var dirtyProperty = property?.FindPropertyRelative("m_IsDirty");
            if (dirtyProperty != null)
            {
                dirtyProperty.boolValue = true;
            }
            property?.serializedObject?.ApplyModifiedProperties();
            GenerateNoiseTexture(image, property);
        }
    }

    private static void GenerateNoiseTexture(Image image, SerializedProperty property)
    {
        NoiseProperties properties = property.boxedValue as NoiseProperties;
        if (properties == null)
        {
            return;
        }

        var noiseMap = NoiseGenerator.FromNoiseProperties(properties, Allocator.Temp);
        image.image = NoiseGenerator.GenerateNoiseTexture(noiseMap, new int2(properties.NoiseResolution, properties.NoiseResolution));
        noiseMap.Dispose();
    }
}
