using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    [CustomEditor(typeof(WorldBuildingSystem))]
    public class WorldBuildingSystemEditor : UnityEditor.Editor
    {
        //public List<
        WorldBuildingSystem Target => target as WorldBuildingSystem;

        public override VisualElement CreateInspectorGUI()
        {
            // Create a new VisualElement to be the root of the Inspector UI.
            VisualElement inspector = new VisualElement();

            // Attach a default Inspector to the Foldout.
            InspectorElement.FillDefaultInspector(inspector, serializedObject, this);

            Button generateButton = new Button() { text = "Generate" };
            generateButton.clicked += () => Target.Generate();
            inspector.Add(generateButton);

            // Return the finished Inspector UI.
            return inspector;
        }
    }
}