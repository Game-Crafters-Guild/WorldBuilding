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
        Image image = new Image();

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
        container.Add(image);
        image.RegisterCallback<GeometryChangedEvent>(OnImageGeometryChanged);
        
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

    private void OnImageGeometryChanged(GeometryChangedEvent evt)
    {
        if (!Mathf.Approximately(evt.newRect.size.x, evt.newRect.size.y))
        {
            Image image = evt.target as Image;
            image.style.height = evt.newRect.size.x;
        }
    }
}
