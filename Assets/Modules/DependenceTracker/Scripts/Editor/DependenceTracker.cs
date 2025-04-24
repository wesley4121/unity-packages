using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class DependenceTracker : EditorWindow
{
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;
    private Object selectedObject;
    private TreeView dependenciesTreeView;
    private TreeView dependenciesTreeView_Packages;
    private bool autoSelectFromProject = false; // Toggle state
    [MenuItem("Tools/DependenceTracker")]
    public static void ShowExample()
    {
        DependenceTracker wnd = GetWindow<DependenceTracker>();
        wnd.titleContent = new GUIContent("DependenceTracker");
    }


    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // Instantiate UXML
        VisualElement UXML = m_VisualTreeAsset.Instantiate();
        root.Add(UXML);

        // Bind UI elements
        var objectField = UXML.Q<ObjectField>("objectField");
        dependenciesTreeView = UXML.Q<TreeView>("dependenciesTreeView_Assets");
        dependenciesTreeView_Packages = UXML.Q<TreeView>("dependenciesTreeView_Packages");

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
            selectedObject = evt.newValue;
            UpdateDependencies();
        });

        // Bind refreshButton and add click event
        var refreshButton = rootVisualElement.Q<Button>("refreshButton");
        refreshButton.clicked += () =>
        {
            if (selectedObject != null)
            {
                UpdateDependencies();
            }
            else
            {
                Debug.LogWarning("No Object selected to refresh.");
                dependenciesTreeView.SetRootItems(new List<TreeViewItemData<string>>()); // Clear the TreeView
                dependenciesTreeView_Packages.SetRootItems(new List<TreeViewItemData<string>>()); // Clear the TreeView
                dependenciesTreeView.Rebuild(); // Refresh the TreeView UI
                dependenciesTreeView_Packages.Rebuild(); // Refresh the TreeView UI
            }
        };

        // Bind auto-select toggle from UXML
        var autoSelectToggle = rootVisualElement.Q<Toggle>("autoSelectToggle");
        autoSelectToggle.value = autoSelectFromProject;
        autoSelectToggle.RegisterValueChangedCallback(evt =>
        {
            autoSelectFromProject = evt.newValue;
        });

        // Monitor selection changes in the Project window
        Selection.selectionChanged += OnProjectSelectionChanged;

        // Use selectionChanged event to handle item selection
        dependenciesTreeView.selectionChanged += OnTreeViewSelectionChanged;
        dependenciesTreeView_Packages.selectionChanged += OnTreeViewSelectionChanged;

        // Customize TreeView item rendering
        dependenciesTreeView.makeItem = () => new Label();
        dependenciesTreeView.bindItem = (element, i) =>
        {
            var label = (Label)element;
            var item = dependenciesTreeView.GetItemDataForIndex<string>(i);
            label.text = item;
            SetLabelColor(label, item); // Set color based on file type
        };

        dependenciesTreeView_Packages.makeItem = () => new Label();
        dependenciesTreeView_Packages.bindItem = (element, i) =>
        {
            var label = (Label)element;
            var item = dependenciesTreeView_Packages.GetItemDataForIndex<string>(i);
            label.text = item;
            SetLabelColor(label, item); // Set color based on file type
        };

    }
        private void SetLabelColor(Label label, string item)
    {
        if (item.EndsWith(".cs"))
        {
            label.style.color = HexColor("#BFD641");
        }
        else if (item.EndsWith(".png"))
        {

            label.style.color = HexColor("#FFECA1");
        }
        else if (item.EndsWith(".mat"))
        {
            label.style.color = HexColor("#99BBFA");
        }
        else if (item.EndsWith(".prefab"))
        {
            label.style.color = HexColor("#98F5F9");
        }
        else if (item.EndsWith(".shader"))
        {
            label.style.color = HexColor("#CC6CE7");
        }
        else
        {
            label.style.color = Color.white;
        }
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
    private void OnProjectSelectionChanged()
    {
        if (autoSelectFromProject && Selection.activeObject != null)
        {
            selectedObject = Selection.activeObject;
            var objectField = rootVisualElement.Q<ObjectField>("objectField");
            objectField.value = selectedObject; // Update ObjectField
            UpdateDependencies(); // Update dependencies automatically
        }
    }
    private void OnDestroy()
    {
        // Unsubscribe from selection changes to avoid memory leaks
        Selection.selectionChanged -= OnProjectSelectionChanged;
    }
    private Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        return color;
    }

    private void UpdateDependencies()
    {
        if (selectedObject != null)
        {
            List<string> dependencies = new();

            string objPath = AssetDatabase.GetAssetPath(selectedObject);
            if (AssetDatabase.IsValidFolder(objPath))
            {
                // 如果是資料夾，取得所有資源
                string[] guids = AssetDatabase.FindAssets("", new[] { objPath });
                HashSet<string> allDeps = new HashSet<string>();
                int total = guids.Length;
                for (int i = 0; i < total; i++)
                {
                    string guid = guids[i];
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    EditorUtility.DisplayProgressBar("Dependency Analysis", $"Analyzing resource: {assetPath}", (float)i / total);
                    if (!AssetDatabase.IsValidFolder(assetPath))
                    {
                        var deps = AssetDatabase.GetDependencies(assetPath);
                        foreach (var dep in deps)
                            allDeps.Add(dep);
                    }
                }
                dependencies = allDeps.ToList();
                EditorUtility.ClearProgressBar();
            }
            else
            {
                EditorUtility.DisplayProgressBar("Dependency Analysis", "Analyzing resource...", 0.5f);
                dependencies = GetDependencies(selectedObject);
                EditorUtility.ClearProgressBar();
            }

            if (dependencies == null || dependencies.Count == 0)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogWarning("No dependencies found for the selected Object.");
                dependenciesTreeView.Clear();
                dependenciesTreeView_Packages.Clear();
                return;
            }

            // Separate dependencies into Assets and Packages
            var assetsDependencies = dependencies.Where(d => d.StartsWith("Assets")).ToList();
            var packagesDependencies = dependencies.Where(d => d.StartsWith("Packages")).ToList();

            // Group and sort dependencies for Assets
            var groupedAssetsDependencies = assetsDependencies
                .GroupBy(d =>
                {
                    string extension = System.IO.Path.GetExtension(d).ToLower();
                    switch (extension)
                    {
                        case ".cs": return "Scripts";
                        case ".png":
                        case ".jpg":
                        case ".jpeg": return "Images";
                        case ".mat": return "Materials";
                        case ".prefab": return "Prefabs";
                        case ".shader": return "Shaders";
                        default: return "Others";
                    }
                })
                .OrderBy(g => g.Key) // Sort groups alphabetically
                .Select(g => new
                {
                    Key = g.Key,
                    Items = g.OrderBy(item => item).ToList() // Sort items within each group
                });

            // Group and sort dependencies for Packages
            var groupedPackagesDependencies = packagesDependencies
                .GroupBy(d =>
                {
                    if (d.Contains("Unity")) return "Unity Packages";
                    return "Other Packages";
                })
                .OrderBy(g => g.Key) // Sort groups alphabetically
                .Select(g => new
                {
                    Key = g.Key,
                    Items = g.OrderBy(item => item).ToList() // Sort items within each group
                });

            // Create TreeView items for Assets
            var assetsTreeViewItems = new List<TreeViewItemData<string>>();
            int id = 0;
            foreach (var group in groupedAssetsDependencies)
            {
                var groupItem = new TreeViewItemData<string>(id++, group.Key, group.Items.Select(item => new TreeViewItemData<string>(id++, item)).ToList());
                assetsTreeViewItems.Add(groupItem);
            }

            // Create TreeView items for Packages
            var packagesTreeViewItems = new List<TreeViewItemData<string>>();
            foreach (var group in groupedPackagesDependencies)
            {
                var groupItem = new TreeViewItemData<string>(id++, group.Key, group.Items.Select(item => new TreeViewItemData<string>(id++, item)).ToList());
                packagesTreeViewItems.Add(groupItem);
            }

            // Set items to respective TreeViews
            dependenciesTreeView.SetRootItems(assetsTreeViewItems);
            dependenciesTreeView_Packages.SetRootItems(packagesTreeViewItems);

            // Expand all items in TreeViews
            dependenciesTreeView.ExpandAll();
            dependenciesTreeView_Packages.ExpandAll();

            // Force refresh TreeView UI
            dependenciesTreeView.Rebuild();
            dependenciesTreeView_Packages.Rebuild();

            // Force layout update to fix display order issue
            rootVisualElement.MarkDirtyRepaint();
        }
        else
        {
            Debug.LogWarning("No Object selected.");
            dependenciesTreeView.Clear();
            dependenciesTreeView_Packages.Clear();
        }
    }
    public List<string> GetDependencies(Object obj)
    {
        List<string> dependencies = new();

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
            Debug.LogWarning("Object is null.");
        }

        return dependencies;
    }
}
