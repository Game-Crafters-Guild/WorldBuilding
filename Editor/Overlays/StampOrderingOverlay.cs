using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    [Overlay(typeof(SceneView), "Stamp Order Manager")]
    public class StampOrderingOverlay : Overlay
    {
        private ListView stampListView;
        private List<Stamp> stamps = new List<Stamp>();
        private Toggle autoRefreshToggle;
        private WorldBuildingSystem worldBuildingSystem;
        private VisualTreeAsset listItemTemplate;
        
        // Called when the overlay is created to construct the UI
        public override VisualElement CreatePanelContent()
        {
            worldBuildingSystem = WorldBuildingSystem.FindSystemInScene();
            
            // Load UI assets using Resources.Load
            var visualTree = Resources.Load<VisualTreeAsset>("StampOrderingWindow");
            listItemTemplate = Resources.Load<VisualTreeAsset>("StampOrderingListItem");
            
            if (visualTree == null)
            {
                var errorElement = new VisualElement();
                errorElement.Add(new Label("Failed to load UI assets. Please ensure the UXML files are in the Resources folder."));
                return errorElement;
            }
            
            // Clone the tree to create our panel content
            var root = visualTree.CloneTree();
            
            // Get UI elements
            stampListView = root.Q<ListView>("stamps-list-view");
            autoRefreshToggle = root.Q<Toggle>("auto-refresh-toggle");
            var refreshButton = root.Q<Button>("refresh-button");
            var selectAllButton = root.Q<Button>("select-all-button");
            var deselectAllButton = root.Q<Button>("deselect-all-button");
            
            // Setup event handlers
            refreshButton.clicked += RefreshStampsList;
            selectAllButton.clicked += SelectAllStamps;
            deselectAllButton.clicked += DeselectAllStamps;
            
            // Setup the ListView
            SetupListView();
            
            // Initial refresh
            RefreshStampsList();
            
            // Register for hierarchy changes to keep the list updated
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            
            // Handle cleanup when the panel is closed
            root.RegisterCallback<DetachFromPanelEvent>(evt => OnDisable());
            
            return root;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnHierarchyChanged()
        {
            if (autoRefreshToggle != null && autoRefreshToggle.value)
            {
                RefreshStampsList();
            }
        }

        private void SetupListView()
        {
            if (stampListView == null) return;
            
            stampListView.makeItem = () =>
            {
                var itemElement = listItemTemplate.CloneTree();
                var objectField = itemElement.Q<ObjectField>("stamp-object");
                objectField.SetEnabled(false); // Make it read-only
                
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
                
                // Set user data for the select button
                var selectButton = element.Q<Button>("select-button");
                selectButton.userData = index;
                
                // Set object field
                var objectField = element.Q<ObjectField>("stamp-object");
                objectField.objectType = typeof(Stamp);
                objectField.value = stamp;
                
                // Set path label to show hierarchy path
                var pathLabel = element.Q<Label>("stamp-path");
                pathLabel.text = GetHierarchyPath(stamp.transform);
                
                // Set priority label
                var priorityLabel = element.Q<Label>("stamp-priority");
                priorityLabel.text = $"Priority: {stamp.Priority}";
            };
            
            stampListView.itemsSource = stamps;
            
            // Handle reordering
            stampListView.itemIndexChanged += ReorderStampsInHierarchy;
        }

        private void RefreshStampsList()
        {
            stamps.Clear();
            
            // Find all stamps in the scene
            var allStamps = Object.FindObjectsByType<Stamp>(FindObjectsSortMode.None);
            
            if (worldBuildingSystem != null)
            {
                // Sort stamps according to the same logic as in WorldBuildingSystem
                Array.Sort(allStamps, (stamp1, stamp2) => 
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
                Array.Sort(allStamps, (a, b) => a.name.CompareTo(b.name));
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
            if (worldBuildingSystem != null)
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

        private string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            Transform parent = transform.parent;
            
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }

        // Set default position and dimensions
        public Vector2 GetPosition()
        {
            // Position in the right side of the scene view
            return new Vector2(500, 200);
        }

        public override void OnCreated()
        {
            // Set a nice icon for the overlay
            this.displayName = "Stamp Order";
            this.collapsed = false;
        }

        // Override this to provide an icon for the overlay
        public Texture2D icon => EditorGUIUtility.FindTexture("TerrainInspector.TerrainToolSplat");
    }
} 