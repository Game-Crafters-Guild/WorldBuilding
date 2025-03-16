using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    [CustomEditor(typeof(WorldBuildingSystem))]
    public class WorldBuildingSystemEditor : UnityEditor.Editor
    {
        WorldBuildingSystem Target => target as WorldBuildingSystem;
        private ListView stampListView;
        private List<Stamp> stamps = new List<Stamp>();
        private VisualTreeAsset listItemTemplate;

        public override VisualElement CreateInspectorGUI()
        {
            // Create a new VisualElement to be the root of the Inspector UI.
            VisualElement inspector = new VisualElement();

            // Attach a default Inspector to the Foldout.
            InspectorElement.FillDefaultInspector(inspector, serializedObject, this);

            Button generateButton = new Button() { text = "Generate" };
            generateButton.clicked += () => Target.Generate();
            inspector.Add(generateButton);

            // Add the stylesheet for the order management..
            StyleSheet stampOrderWindowStylesheet = Resources.Load<StyleSheet>("StampOrderingWindowStylesheet");
            inspector.styleSheets.Add(stampOrderWindowStylesheet);
            
            // Add stamp management section
            AddStampManagementSection(inspector);

            // Return the finished Inspector UI.
            return inspector;
        }

        private void AddStampManagementSection(VisualElement inspector)
        {
            // Create a foldout for the stamp management section
            Foldout stampManagementFoldout = new Foldout
            {
                text = "Stamp Order Management",
                value = true // Initially expanded
            };
            
            // Add class to apply styles from the shared stylesheet
            stampManagementFoldout.AddToClassList("stamp-order-manager-root");
            
            // Create buttons container
            VisualElement buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.marginTop = 5;
            buttonContainer.style.marginBottom = 10;

            // Create "Open Full Manager" button
            Button openManagerButton = new Button(() => StampOrderingWindow.ShowWindow())
            {
                text = "Open Full Manager Window",
                tooltip = "Open the Stamp Order Manager in a separate window"
            };
            openManagerButton.style.flexGrow = 1;
            buttonContainer.Add(openManagerButton);

            // Create "Refresh List" button
            Button refreshButton = new Button(RefreshStampsList)
            {
                text = "Refresh",
                tooltip = "Refresh the stamp list"
            };
            refreshButton.style.marginLeft = 5;
            buttonContainer.Add(refreshButton);

            stampManagementFoldout.Add(buttonContainer);

            // Create the inline list of stamps
            CreateStampList(stampManagementFoldout);

            // Add the entire foldout to the inspector
            inspector.Add(stampManagementFoldout);
        }

        private void CreateStampList(VisualElement container)
        {
            // Load the list item template
            listItemTemplate = Resources.Load<VisualTreeAsset>("StampOrderingListItem");
            if (listItemTemplate == null)
            {
                container.Add(new Label("Failed to load the stamp list item template."));
                return;
            }

            // Add stylesheet to container if not already added
            var styleSheet = Resources.Load<StyleSheet>("StampOrderingWindow");
            if (styleSheet != null && !container.styleSheets.Contains(styleSheet))
            {
                container.styleSheets.Add(styleSheet);
            }

            // Create a container for the list with appropriate styling
            VisualElement listContainer = new VisualElement();
            listContainer.AddToClassList("container");

            // Create label for the list
            Label listLabel = new Label("Stamps in Scene (Drag to Reorder)");
            listLabel.AddToClassList("section-label");
            listContainer.Add(listLabel);

            // Create the list view
            stampListView = new ListView
            {
                fixedItemHeight = 30,
                showBorder = true,
                selectionType = SelectionType.Multiple,
                reorderable = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.All,
                style = { 
                    height = 200, // Fixed height for the inline list
                    marginBottom = 5
                }
            };

            stampListView.makeItem = () =>
            {
                var itemElement = listItemTemplate.CloneTree();
                
                // Register double-click handler for selection
                var listItem = itemElement.Q<VisualElement>("stamp-item");
                listItem.RegisterCallback<MouseDownEvent>(evt => {
                    if (evt.clickCount == 2) {
                        int index = (int)listItem.userData;
                        if (index >= 0 && index < stamps.Count) {
                            Selection.activeGameObject = stamps[index].gameObject;
                            EditorGUIUtility.PingObject(stamps[index].gameObject);
                        }
                    }
                });
                
                var selectButton = itemElement.Q<Button>("select-button");
                selectButton.clicked += () => 
                {
                    int index = (int)selectButton.userData;
                    if (index >= 0 && index < stamps.Count)
                    {
                        Selection.activeGameObject = stamps[index].gameObject;
                        EditorGUIUtility.PingObject(stamps[index].gameObject);
                    }
                };
                
                return itemElement;
            };
            
            stampListView.bindItem = (element, index) =>
            {
                var stamp = stamps[index];
                
                // Store index on the list item for double-click
                var listItem = element.Q<VisualElement>("stamp-item");
                listItem.userData = index;
                
                // Set user data for the select button
                var selectButton = element.Q<Button>("select-button");
                selectButton.userData = index;
                
                // Set stamp icon
                var iconElement = element.Q<Image>("stamp-icon");
                iconElement.image = EditorGUIUtility.ObjectContent(stamp, typeof(Stamp)).image;
                
                // Set name label
                var nameLabel = element.Q<Label>("stamp-name");
                nameLabel.text = stamp.name;
                
                // Set type label (show shape type)
                var typeLabel = element.Q<Label>("stamp-type");
                typeLabel.text = stamp.Shape != null ? $"({stamp.Shape.GetType().Name.Replace("Shape", "")})" : "(Unknown)";
                
                // Set priority label
                var priorityLabel = element.Q<Label>("stamp-priority");
                priorityLabel.text = $"Priority: {stamp.Priority}";
            };

            stampListView.itemsSource = stamps;
            
            // Handle reordering
            stampListView.itemIndexChanged += ReorderStampsInHierarchy;
            
            listContainer.Add(stampListView);
            container.Add(listContainer);

            // Add utility buttons
            VisualElement utilityContainer = new VisualElement();
            utilityContainer.AddToClassList("toolbar");
            utilityContainer.style.marginTop = 5;
            
            Button selectAllButton = new Button(SelectAllStamps)
            {
                text = "Select All",
                tooltip = "Select all stamps in the scene"
            };
            selectAllButton.style.flexGrow = 1;
            utilityContainer.Add(selectAllButton);
            
            Button deselectAllButton = new Button(DeselectAllStamps)
            {
                text = "Deselect All",
                tooltip = "Deselect all stamps"
            };
            deselectAllButton.style.flexGrow = 1;
            deselectAllButton.style.marginLeft = 5;
            utilityContainer.Add(deselectAllButton);
            
            container.Add(utilityContainer);

            // Initial refresh of the list
            RefreshStampsList();
        }
        
        private void RefreshStampsList()
        {
            stamps.Clear();
            
            // Find all stamps in the scene
            var allStamps = Object.FindObjectsOfType<Stamp>();
            
            if (Target != null)
            {
                // Sort stamps according to the same logic as in WorldBuildingSystem
                System.Array.Sort(allStamps, (stamp1, stamp2) => 
                {
                    // Match the same sorting logic as in WorldBuildingSystem
                    if (stamp1.transform.parent == stamp2.transform.parent)
                    {
                        return stamp1.transform.GetSiblingIndex().CompareTo(stamp2.transform.GetSiblingIndex());
                    }
                    
                    int depth1 = GetHierarchyDepth(stamp1.transform);
                    int depth2 = GetHierarchyDepth(stamp2.transform);
                    if (depth1 != depth2)
                    {
                        return depth1.CompareTo(depth2);
                    }
                    
                    return stamp1.Priority.CompareTo(stamp2.Priority);
                });
            }
            else
            {
                // If no WorldBuildingSystem found, just sort by name
                System.Array.Sort(allStamps, (a, b) => a.name.CompareTo(b.name));
            }
            
            stamps.AddRange(allStamps);
            
            if (stampListView != null)
            {
                stampListView.Rebuild();
            }
        }
        
        private void SelectAllStamps()
        {
            if (stamps.Count == 0) return;
            Selection.objects = stamps.Select(s => s.gameObject).ToArray();
        }
        
        private void DeselectAllStamps()
        {
            Selection.objects = new UnityEngine.Object[0];
        }
        
        private void ReorderStampsInHierarchy(int oldIndex, int newIndex)
        {
            if (oldIndex == newIndex) return;
            
            Stamp stampToMove = stamps[newIndex];
            Stamp referenceStamp = null;
            
            // Move stampToMove in the hierarchy
            if (newIndex > 0)
            {
                // Find a reference stamp that shares the same parent
                for (int i = newIndex - 1; i >= 0; i--)
                {
                    if (stamps[i].transform.parent == stampToMove.transform.parent)
                    {
                        referenceStamp = stamps[i];
                        break;
                    }
                }
                
                if (referenceStamp != null)
                {
                    // Move after the reference stamp
                    stampToMove.transform.SetSiblingIndex(referenceStamp.transform.GetSiblingIndex() + 1);
                }
                else
                {
                    // If no reference with same parent, move to first position under its parent
                    stampToMove.transform.SetAsFirstSibling();
                }
            }
            else
            {
                // Move to first position if it's the first in the list
                stampToMove.transform.SetAsFirstSibling();
            }
            
            // Mark the world building system as dirty to process the changes
            if (Target != null)
            {
                stampToMove.IsDirty = true;
                EditorUtility.SetDirty(stampToMove);
            }
            
            // Refresh the list to reflect hierarchy changes
            EditorApplication.delayCall += () => RefreshStampsList();
        }
        
        private int GetHierarchyDepth(Transform transform)
        {
            int depth = 0;
            Transform current = transform;
            while (current.parent != null)
            {
                depth++;
                current = current.parent;
            }
            return depth;
        }
    }
}