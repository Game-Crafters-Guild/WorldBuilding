using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameCraftersGuild.WorldBuilding.Editor
{
    /// <summary>
    /// Custom VisualElement that provides stamp ordering functionality
    /// </summary>
    [UxmlElement]
    public partial class StampOrderingControl : VisualElement
    {
        private ListView m_StampListView;
        private List<Stamp> m_Stamps = new List<Stamp>();
        private Toggle m_AutoRefreshToggle;
        private WorldBuildingSystem m_WorldBuildingSystem;
        private VisualTreeAsset m_ListItemTemplate;
        private VisualElement m_HeaderContainer;
        private VisualElement m_ListContainer;
        private Button m_RefreshButton;
        private Button m_SelectAllButton;
        private Button m_DeselectAllButton;
        
        // Events that consumers can subscribe to
        public event Action OnRefreshRequested;
        
        // Customization options
        private bool m_ShowAutoRefreshToggle = true;
        public bool ShowAutoRefreshToggle 
        { 
            get => m_ShowAutoRefreshToggle;
            set 
            {
                m_ShowAutoRefreshToggle = value;
                if (m_AutoRefreshToggle != null)
                {
                    m_AutoRefreshToggle.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }
        
        private bool m_ShowHeader = true;
        public bool ShowHeader 
        { 
            get => m_ShowHeader;
            set 
            {
                m_ShowHeader = value;
                if (m_HeaderContainer != null)
                {
                    m_HeaderContainer.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }
        
        private float m_ListHeight = 300f;
        public float ListHeight 
        { 
            get => m_ListHeight;
            set 
            {
                m_ListHeight = value;
                if (m_StampListView != null)
                {
                    m_StampListView.style.height = value;
                }
            }
        }
        
        public bool AutoRefresh 
        {
            get => m_AutoRefreshToggle?.value ?? false;
            set 
            {
                if (m_AutoRefreshToggle != null)
                    m_AutoRefreshToggle.value = value;
            }
        }

        private bool m_AutoAdjustListHeight = false;
        public bool AutoAdjustListHeight
        {
            get => m_AutoAdjustListHeight;
            set
            {
                if (m_AutoAdjustListHeight == value) return;
                m_AutoAdjustListHeight = value;
                if (m_AutoAdjustListHeight)
                {
                    RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                }
                else
                {
                    UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                }
            }
        }
        
        public StampOrderingControl()
        {
            // Add class for styling
            AddToClassList("stamp-ordering-control");
            
            // Set up hierarchy changed event to auto-refresh when needed
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            
            // Ensure we unregister when this element is detached
            RegisterCallback<DetachFromPanelEvent>(evt => 
            {
                EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            });
            
            // Load and apply the UXML layout
            LoadLayout();
            
            // Set initial data
            InitializeData();
        }
        
        private void LoadLayout()
        {
            // Load UI assets using Resources.Load
            var visualTree = Resources.Load<VisualTreeAsset>("StampOrderingWindow");
            m_ListItemTemplate = Resources.Load<VisualTreeAsset>("StampOrderingListItem");
            
            if (visualTree == null || m_ListItemTemplate == null)
            {
                Add(new Label("Failed to load UI assets. Please ensure the UXML files are in the Resources folder."));
                return;
            }
            
            // Apply the stylesheet
            var styleSheet = Resources.Load<StyleSheet>("StampOrderingControlStylesheet");
            if (styleSheet != null && !styleSheets.Contains(styleSheet))
            {
                styleSheets.Add(styleSheet);
            }
            
            // Clone the UXML tree to create our UI
            visualTree.CloneTree(this);
            
            // Get references to UI elements
            m_HeaderContainer = this.Q<VisualElement>("header-container");
            m_StampListView = this.Q<ListView>("stamps-list-view");
            m_AutoRefreshToggle = this.Q<Toggle>("auto-refresh-toggle");
            m_RefreshButton = this.Q<Button>("refresh-button");
            m_SelectAllButton = this.Q<Button>("select-all-button");
            m_DeselectAllButton = this.Q<Button>("deselect-all-button");
            m_ListContainer = this.Q<VisualElement>("list-container");
            
            // Setup event handlers
            if (m_RefreshButton != null)
                m_RefreshButton.clicked += RefreshStampsList;
                
            if (m_SelectAllButton != null)
                m_SelectAllButton.clicked += SelectAllStamps;
                
            if (m_DeselectAllButton != null)
                m_DeselectAllButton.clicked += DeselectAllStamps;
            
            // Setup the ListView
            SetupListView();
            
            // Apply initial property values
            ShowHeader = m_ShowHeader;
            ShowAutoRefreshToggle = m_ShowAutoRefreshToggle;
            ListHeight = m_ListHeight;
        }
        
        private void SetupListView()
        {
            if (m_StampListView == null) return;
            
            // Configure the list view
            m_StampListView.fixedItemHeight = 30;
            m_StampListView.showBoundCollectionSize = false;
            m_StampListView.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
            m_StampListView.reorderable = true;
            m_StampListView.selectionType = SelectionType.Multiple;
            m_StampListView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            
            m_StampListView.makeItem = () =>
            {
                var itemElement = m_ListItemTemplate.CloneTree();
                
                // Register double-click handler for selection
                var listItem = itemElement.Q<VisualElement>("stamp-item");
                listItem.RegisterCallback<MouseDownEvent>(evt => {
                    int index = (int)listItem.userData;
                    if (index >= 0 && index < m_Stamps.Count) {
                        if (evt.clickCount == 2) {
                            // Double click selects the GameObject
                            Selection.activeGameObject = m_Stamps[index].gameObject;
                        }
                        // Single click always pings the item
                        EditorGUIUtility.PingObject(m_Stamps[index].gameObject);
                    }
                });
                
                var selectButton = itemElement.Q<Button>("select-button");
                selectButton.clicked += () => 
                {
                    int index = (int)selectButton.userData;
                    if (index >= 0 && index < m_Stamps.Count)
                    {
                        Selection.activeGameObject = m_Stamps[index].gameObject;
                        EditorGUIUtility.PingObject(m_Stamps[index].gameObject);
                    }
                };
                
                return itemElement;
            };
            
            m_StampListView.bindItem = (element, index) =>
            {
                var stamp = m_Stamps[index];
                
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
            
            // Set the items source
            m_StampListView.itemsSource = m_Stamps;
            
            // Handle reordering
            m_StampListView.itemIndexChanged += ReorderStampsInHierarchy;
            
            // Set the initial height
            m_StampListView.style.height = m_ListHeight;
        }
        
        private void InitializeData()
        {
            m_WorldBuildingSystem = WorldBuildingSystem.FindSystemInScene();
            RefreshStampsList();
        }
        
        public void RefreshStampsList()
        {
            m_Stamps.Clear();
            
            // Find all stamps in the scene
            var allStamps = UnityEngine.Object.FindObjectsByType<Stamp>(FindObjectsSortMode.None);
            
            if (m_WorldBuildingSystem != null)
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
            
            m_Stamps.AddRange(allStamps);
            
            if (m_StampListView != null)
            {
                m_StampListView.Rebuild();
            }
            
            // Notify subscribers that refresh occurred
            OnRefreshRequested?.Invoke();
        }
        
        private void OnHierarchyChanged()
        {
            if (m_AutoRefreshToggle != null && m_AutoRefreshToggle.value)
            {
                RefreshStampsList();
            }
        }
        
        private void SelectAllStamps()
        {
            if (m_Stamps.Count == 0) return;
            Selection.objects = m_Stamps.Select(s => s.gameObject).ToArray();
        }
        
        private void DeselectAllStamps()
        {
            Selection.objects = new UnityEngine.Object[0];
        }
        
        private void ReorderStampsInHierarchy(int oldIndex, int newIndex)
        {
            if (oldIndex == newIndex) return;
            
            Stamp stampToMove = m_Stamps[newIndex];
            Stamp referenceStamp = null;
            
            // Move stampToMove in the hierarchy
            if (newIndex > 0)
            {
                // Find a reference stamp that shares the same parent
                for (int i = newIndex - 1; i >= 0; i--)
                {
                    if (m_Stamps[i].transform.parent == stampToMove.transform.parent)
                    {
                        referenceStamp = m_Stamps[i];
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
            if (m_WorldBuildingSystem != null)
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
        
        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (m_StampListView != null && parent != null)
            {
                // Let the layout settle before adjusting the list height
                m_StampListView.schedule.Execute(() => {
                    AdjustListHeight();
                });
            }
        }
        
        private void AdjustListHeight()
        {
            if (m_StampListView == null)
                return;
                
            // Ensure list has flex grow
            m_StampListView.style.flexGrow = 1;
            
            if (m_ListContainer != null)
                m_ListContainer.style.flexGrow = 1;

            float headerHeight = m_HeaderContainer != null && m_ShowHeader ? m_HeaderContainer.worldBound.height : 0;
            float toolbarHeight = 30; // Approximate toolbar height
            float footerHeight = 30; // Approximate footer height
            float sectionLabelHeight = 20; // Approximate section label height
            float padding = 40; // Additional padding
            
            float availableHeight = this.worldBound.height - (headerHeight + toolbarHeight + footerHeight + sectionLabelHeight + padding);
            
            if (availableHeight > 100) // Make sure we don't make it too small
            {
                m_StampListView.style.height = availableHeight;
                m_ListHeight = availableHeight; // Update our stored height
            }
        }
    }
} 