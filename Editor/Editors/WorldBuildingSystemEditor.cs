using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(WorldBuildingSystem))]
public class WorldBuildingSystemEditor : Editor
{
    //public List<
    WorldBuildingSystem Target => target as WorldBuildingSystem;
    public override VisualElement CreateInspectorGUI()
    {
        // Create a new VisualElement to be the root of the Inspector UI.
        VisualElement inspector = new VisualElement();
    
        // Attach a default Inspector to the Foldout.
        InspectorElement.FillDefaultInspector(inspector, serializedObject, this);
        
        Button backupButton = new Button() { text = "Backup Terrain Data" };
        backupButton.clicked += () => Target.BackupTerrainData();
        inspector.Add(backupButton);
        
        Button restoreTerrainData = new Button() { text = "Restore Terrain Data" };
        restoreTerrainData.clicked += () => Target.RestoreTerrainData();
        inspector.Add(restoreTerrainData);
        
        Button generateButton = new Button() { text = "Generate" };
        generateButton.clicked += () => Target.Generate();
        inspector.Add(generateButton);
    
        // Return the finished Inspector UI.
        return inspector;
    }
}