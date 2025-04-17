using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using UnityEditor.UIElements;
using System.Linq;

public class DependenceTracker : EditorWindow
{
    private Object selectedPrefab;
    private DependencyLogic dependencyLogic;
    private TreeView dependenciesTreeView;
    private TreeView dependenciesTreeView_Packages;

    [MenuItem("Tools/DependenceTracker")]
    public static void ShowExample()
    {
        DependenceTracker wnd = GetWindow<DependenceTracker>();
        wnd.titleContent = new GUIContent("DependenceTracker");
    }

    public void CreateGUI()
    {
        dependencyLogic = new DependencyLogic();

        // Load UXML
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UniGears/DependenceTracker/Res/UXML/DependenceTracker.uxml");
        VisualElement rootFromUXML = visualTree.Instantiate();
        rootVisualElement.Add(rootFromUXML);

        // Bind UI elements
        var objectField = rootVisualElement.Q<ObjectField>("objectField");
        dependenciesTreeView = rootFromUXML.Q<TreeView>("dependenciesTreeView_Assets");
        dependenciesTreeView_Packages = rootFromUXML.Q<TreeView>("dependenciesTreeView_Packages");

        // Ensure TreeView is properly initialized
        dependenciesTreeView.reorderable = false; // Disable drag-and-drop
        dependenciesTreeView.selectionType = SelectionType.Single; // Enable single selection
        dependenciesTreeView.autoExpand = true; // Automatically expand items
        dependenciesTreeView.SetRootItems(new List<TreeViewItemData<string>>()); // Initialize with empty data

        dependenciesTreeView_Packages.reorderable = false; // Disable drag-and-drop
        dependenciesTreeView_Packages.selectionType = SelectionType.Single; // Enable single selection
        dependenciesTreeView_Packages.autoExpand = true; // Automatically expand items
        dependenciesTreeView_Packages.SetRootItems(new List<TreeViewItemData<string>>()); // Initialize with empty data

        objectField.RegisterValueChangedCallback(evt =>
        {
            selectedPrefab = evt.newValue;
            UpdateDependencies();
        });

        // Use selectionChanged event to handle item selection
        dependenciesTreeView.selectionChanged += OnTreeViewSelectionChanged;
        dependenciesTreeView_Packages.selectionChanged += OnTreeViewSelectionChanged;
    }

    private void OnTreeViewSelectionChanged(IEnumerable<object> selectedItems)
    {
        foreach (var item in selectedItems)
        {
            string assetPath = item.ToString(); // Assuming item is a string path
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset != null)
            {
                // Select and ping the asset in the Project window
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            else
            {
                Debug.LogWarning($"Could not load asset at path: {assetPath}");
            }
        }
    }

    private void UpdateDependencies()
    {
        if (selectedPrefab != null)
        {
            var dependencies = dependencyLogic.GetDependencies(selectedPrefab);

            if (dependencies == null || dependencies.Count == 0)
            {
                Debug.LogWarning("No dependencies found for the selected prefab.");
                dependenciesTreeView.Clear(); // Clear the TreeView if no dependencies are found.
                dependenciesTreeView_Packages.Clear(); // Clear the TreeView if no dependencies are found.
                return;
            }

            // Separate dependencies into Assets and Packages
            var assetsDependencies = dependencies.Where(d => d.StartsWith("Assets")).ToList();
            var packagesDependencies = dependencies.Where(d => d.StartsWith("Packages")).ToList();

            // Create TreeView items for Assets
            var assetsTreeViewItems = new List<TreeViewItemData<string>>();
            for (int i = 0; i < assetsDependencies.Count; i++)
            {
                assetsTreeViewItems.Add(new TreeViewItemData<string>(i, assetsDependencies[i]));
            }

            // Create TreeView items for Packages
            var packagesTreeViewItems = new List<TreeViewItemData<string>>();
            for (int i = 0; i < packagesDependencies.Count; i++)
            {
                packagesTreeViewItems.Add(new TreeViewItemData<string>(i, packagesDependencies[i]));
            }

            // Set items to respective TreeViews
            dependenciesTreeView.SetRootItems(assetsTreeViewItems);
            dependenciesTreeView_Packages.SetRootItems(packagesTreeViewItems);

            // Force refresh TreeView UI
            dependenciesTreeView.Rebuild();
            dependenciesTreeView_Packages.Rebuild();

            // Enable selection functionality
            dependenciesTreeView.selectionType = SelectionType.Single;
            dependenciesTreeView_Packages.selectionType = SelectionType.Single;
        }
        else
        {
            Debug.LogWarning("No prefab selected.");
            dependenciesTreeView.Clear(); // Clear the TreeView if no prefab is selected.
            dependenciesTreeView_Packages.Clear(); // Clear the TreeView if no prefab is selected.
        }
    }

    private TreeViewItemData<string> BuildTreeViewItem(DependencyNode node, ref int idCounter)
    {
        // Sort children nodes: Assets/Modules/Shared first, then Assets/Modules/Features, then others alphabetically
        var sortedChildren = node.Childrens
            .OrderBy(child => !child.Path.StartsWith("Assets/Modules/Shared")) // Shared first
            .ThenBy(child => !child.Path.StartsWith("Assets/Modules/Features")) // Features second
            .ToList();

        Debug.Log($"Building TreeViewItem for node: {node.Path}, with {sortedChildren.Count} children.");

        var children = new List<TreeViewItemData<string>>();

        foreach (var child in sortedChildren)
        {
            children.Add(BuildTreeViewItem(child, ref idCounter));
            Debug.Log($"Child node added: {child.Path}");
        }

        return new TreeViewItemData<string>(idCounter++, node.Path, children);
    }

}
public class DependencyLogic
{
    public List<string> GetDependencies(Object obj)
    {
        List<string> dependencies = new List<string>();

        if (obj != null)
        {
            string objPath = AssetDatabase.GetAssetPath(obj);

            string[] dependencyPaths = AssetDatabase.GetDependencies(objPath);

            foreach (var path in dependencyPaths)
            {
                dependencies.Add(path);
            }
        }
        else
        {
            Debug.LogWarning("Prefab is null.");
        }

        return dependencies;
    }

    public List<DependencyNode> GetDependenciesWithHierarchy(UnityEngine.Object obj)
    {
        List<DependencyNode> rootNodes = new List<DependencyNode>();

        if (obj != null)
        {
            string prefabPath = AssetDatabase.GetAssetPath(obj);
            string[] dependencyPaths = AssetDatabase.GetDependencies(prefabPath);

            Dictionary<string, DependencyNode> nodeMap = new Dictionary<string, DependencyNode>();

            foreach (var path in dependencyPaths)
            {
                string[] parts = path.Split('/');
                DependencyNode currentNode = null;

                for (int i = 0; i < parts.Length; i++)
                {
                    string subPath = string.Join("/", parts.Take(i + 1));

                    if (!nodeMap.ContainsKey(subPath))
                    {
                        nodeMap[subPath] = new DependencyNode(subPath);
                    }

                    if (currentNode != null)
                    {
                        if (!currentNode.Childrens.Contains(nodeMap[subPath]))
                        {
                            currentNode.Childrens.Add(nodeMap[subPath]);
                        }
                    }
                    else if (i == 0)
                    {
                        rootNodes.Add(nodeMap[subPath]);
                    }

                    currentNode = nodeMap[subPath];
                }
            }

            // Sort children nodes: Assets first, then others, then Packages, all alphabetically
            foreach (var node in nodeMap.Values)
            {
                node.Childrens = node.Childrens
                    .OrderBy(child => !child.Path.StartsWith("Assets")) // Assets first
                    .ThenBy(child => child.Path.StartsWith("Packages")) // Packages last
                    .ThenBy(child => child.Path) // Alphabetical order
                    .ToList();
            }

            // Sort root nodes as well
            rootNodes = rootNodes
                .OrderBy(node => !node.Path.StartsWith("Assets")) // Assets first
                .ThenBy(node => node.Path.StartsWith("Packages")) // Packages last
                .ThenBy(node => node.Path) // Alphabetical order
                .ToList();
        }

        return rootNodes;
    }
}

public class DependencyNode
{
    public string Path { get; set; }
    public List<DependencyNode> Childrens { get; set; }

    public DependencyNode(string path)
    {
        Path = path;
        Childrens = new List<DependencyNode>();
    }
}

