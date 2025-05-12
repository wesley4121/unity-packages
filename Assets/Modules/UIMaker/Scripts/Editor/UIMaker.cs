using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Modules.UIMaker
{
    public class UIMaker : EditorWindow
    {
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        private readonly List<PrefabItem> prefabItems = new();
        private UIMakerConfig prefabListConfig;
        private bool doNotUnpackPrefab = false; // 新增變數來追蹤 TOGGLE 狀態

        private class PrefabItem
        {
            public string ButtonText { get; set; }
            public GameObject Prefab { get; set; }
            public event System.Action<GameObject> PrefabChanged;

            public void OnPrefabChanged(GameObject newPrefab)
            {
                PrefabChanged?.Invoke(newPrefab);
            }
        }

        [MenuItem("Tools/UIMaker")]
        public static void DisplayWindow()
        {
            UIMaker wnd = GetWindow<UIMaker>();
            wnd.titleContent = new GUIContent("UIMaker");
        }

        private void MonitorConfigChanges()
        {
            EditorApplication.update += () =>
            {
                if (prefabListConfig == null) return;

                // 檢查 Config 是否有變更
                var currentPrefabs = new List<GameObject>(prefabListConfig.prefabList);
                if (!currentPrefabs.SequenceEqual(prefabItems.Select(item => item.Prefab)))
                {
                    // 同步 Config 資料到工具
                    prefabItems.Clear();
                    foreach (var prefab in prefabListConfig.prefabList)
                    {
                        prefabItems.Add(new PrefabItem { Prefab = prefab, ButtonText = prefab != null ? prefab.name : "Unnamed Prefab" });
                    }
                    rootVisualElement.Q<ListView>("PrefabListView")?.Rebuild();
                    GenerateDynamicButtons(rootVisualElement);
                }
            };
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            // 初始化 PrefabListConfig
            InitializePrefabListConfig();

            // 監控 Config 的變更
            MonitorConfigChanges();

            ConfigurePrefabListView(root);
            GenerateDynamicButtons(root);

            // 監控 PrefabSettingFoldout 的摺疊事件
            var prefabSettingFoldout = root.Q<Foldout>("PrefabSettingFoldout");
            if (prefabSettingFoldout != null)
            {
                prefabSettingFoldout.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue)
                    {
                        // 展開時刷新 PrefabListView
                        root.Q<ListView>("PrefabListView")?.Rebuild();
                    }
                });
            }

            // 監控 DoNotUnpackPrefabToggle 的變更
            var doNotUnpackToggle = root.Q<Toggle>("DoNotUnpackPrefabToggle");
            if (doNotUnpackToggle != null)
            {
                doNotUnpackToggle.RegisterValueChangedCallback(evt =>
                {
                    doNotUnpackPrefab = evt.newValue;
                });
            }
        }

        private void InitializePrefabListConfig()
        {
            string configPath = "Assets/Editor/UIMaker/UIMakerConfig.asset";
            string directoryPath = Path.GetDirectoryName(configPath);

            // 確保目標目錄存在
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            prefabListConfig = AssetDatabase.LoadAssetAtPath<UIMakerConfig>(configPath);

            if (prefabListConfig == null)
            {
                prefabListConfig = ScriptableObject.CreateInstance<UIMakerConfig>();
                AssetDatabase.CreateAsset(prefabListConfig, configPath);
                AssetDatabase.SaveAssets();
            }

            // 同步 Config 資料到工具
            prefabItems.Clear();
            foreach (var prefab in prefabListConfig.prefabList)
            {
                prefabItems.Add(new PrefabItem { Prefab = prefab, ButtonText = prefab != null ? prefab.name : "Unnamed Prefab" });
            }
        }

        private void RefreshPrefabListViewAndButtons(VisualElement root)
        {
            var prefabListView = root.Q<ListView>("PrefabListView");
            prefabListView.Rebuild();
            GenerateDynamicButtons(root);
        }

        private void AddPrefabToList(GameObject prefab, VisualElement root)
        {
            prefabItems.Add(new PrefabItem { ButtonText = prefab != null ? prefab.name : "Unnamed Prefab", Prefab = prefab });
            SavePrefabListToConfig(); // 確保新增的資料同步到 Config
            RefreshPrefabListViewAndButtons(root);
        }

        private void ConfigurePrefabListView(VisualElement root)
        {
            var prefabListView = root.Q<ListView>("PrefabListView");
            var addPrefabButton = root.Q<UnityEngine.UIElements.Button>("AddPrefabButton");
            var removePrefabButton = root.Q<UnityEngine.UIElements.Button>("RemovePrefabButton");
            var setDefaultPrefabsButton = root.Q<UnityEngine.UIElements.Button>("SetDefaultPrefabsButton");
            var clearPrefabListButton = root.Q<UnityEngine.UIElements.Button>("ClearPrefabListButton");

            prefabListView.itemsSource = prefabItems;
            prefabListView.makeItem = CreatePrefabListViewItem;
            prefabListView.bindItem = BindPrefabListViewItem;
            prefabListView.fixedItemHeight = 30;

            addPrefabButton.clicked += () =>
            {
                AddPrefabToList(null, root);
            };

            removePrefabButton.clicked += () =>
            {
                if (prefabItems.Count > 0)
                {
                    prefabItems.RemoveAt(prefabItems.Count - 1);
                    SavePrefabListToConfig();
                    RefreshPrefabListViewAndButtons(root);
                }
            };

            setDefaultPrefabsButton.clicked += () =>
            {
                SetDefaultPrefabs();
                SavePrefabListToConfig();
                RefreshPrefabListViewAndButtons(root);
            };

            clearPrefabListButton.clicked += () =>
            {
                prefabItems.Clear();
                SavePrefabListToConfig();
                RefreshPrefabListViewAndButtons(root);
            };


        }

        private VisualElement CreatePrefabListViewItem()
        {
            var container = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var objectField = new ObjectField
            {
                objectType = typeof(GameObject),
                style =
            {
                flexGrow = 1,
                alignItems = Align.Center,
                justifyContent = Justify.Center
            }
            };
            container.Add(objectField);
            return container;
        }

        private void BindPrefabListViewItem(VisualElement element, int index)
        {
            var container = (VisualElement)element;
            var objectField = (ObjectField)container.ElementAt(0);

            var item = prefabItems[index];

            // 確保只在需要時更新數據綁定
            objectField.value = item.Prefab;
            objectField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is GameObject gameObject)
                {
                    // 檢查是否是 Hierarchy 中的 GameObject
                    if (!PrefabUtility.IsPartOfAnyPrefab(gameObject))
                    {
                        Debug.LogWarning("Only Prefabs are supported. Converting GameObject to Prefab.");
                        string directoryPath = "Assets/Editor/UIMaker/Prefab/";

                        // 確保目標目錄存在
                        if (!Directory.Exists(directoryPath))
                        {
                            Directory.CreateDirectory(directoryPath);
                        }

                        string localPath = Path.Combine(directoryPath, $"{gameObject.name}.prefab");
                        var prefab = PrefabUtility.SaveAsPrefabAsset(gameObject, localPath);
                        item.Prefab = prefab;

                        // 更新 ObjectField 的值為新生成的 Prefab
                        objectField.value = prefab;
                    }
                    else
                    {
                        item.Prefab = gameObject;
                    }

                    item.OnPrefabChanged(item.Prefab);

                    // 更新 Config
                    SavePrefabListToConfig();
                }
            });
        }

        private void GenerateDynamicButtons(VisualElement root)
        {
            var buttonContainer = root.Q<VisualElement>("DynamicButtonContainer");
            if (buttonContainer == null)
            {
                Debug.LogError("DynamicButtonContainer not found in the UI hierarchy.");
                return;
            }

            buttonContainer.Clear();

            if (prefabItems.Count == 0)
            {
                // 如果 PrefabList 為空，顯示提示文字
                var noPrefabsLabel = new Label("No prefabs set in the list. Please add prefabs to continue.")
                {
                    style =
                {
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    color = Color.gray,
                    marginTop = 10,
                    marginBottom = 10, // 添加底部間距
                    alignSelf = Align.Center, // 垂直居中
                    whiteSpace = WhiteSpace.Normal, // 啟用文字換行
                    width = Length.Percent(100) // 設置寬度為容器的 100%
                }
                };
                buttonContainer.Add(noPrefabsLabel);
                return;
            }

            foreach (var prefabItem in prefabItems)
            {
                var button = new UnityEngine.UIElements.Button(() => SpawnPrefab(prefabItem.Prefab))
                {
                    text = prefabItem.Prefab != null ? prefabItem.Prefab.name : "Unnamed Prefab"
                };
                buttonContainer.Add(button);

                prefabItem.PrefabChanged += (newPrefab) =>
                {
                    button.text = newPrefab != null ? newPrefab.name : "Unnamed Prefab";
                };
            }
        }

        private void SpawnPrefab(GameObject prefab)
        {
            if (prefab != null)
            {
                string objectName = GenerateObjectName(prefab.name);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = objectName;

                if (!doNotUnpackPrefab)
                {
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.UserAction);
                }

                if (Selection.activeGameObject != null)
                {
                    instance.transform.SetParent(Selection.activeGameObject.transform);
                    instance.transform.localScale = Vector3.one;
                    instance.transform.localPosition = Vector3.zero;
                }

                Undo.RegisterCreatedObjectUndo(instance, "Spawn Prefab");
                Selection.activeGameObject = instance;
            }
        }

        private string GenerateObjectName(string baseName)
        {
            return baseName; // 直接返回基礎名稱
        }

        private void SetDefaultPrefabs()
        {
            prefabItems.Clear();
            string projectPrefabFolder = "Assets/Modules/UIMaker/Resources/Prefab";
            string packagePrefabFolder = Path.Combine(Path.GetFullPath("Packages/com.unity.tools.uimaker"), "Resources/Prefab");
            string localPrefabFolder = "Assets/Editor/UIMaker/Prefab/";
            string[] prefabFiles;

            if (Directory.Exists(projectPrefabFolder))
            {
                // 專案內 prefab
                prefabFiles = Directory.GetFiles(projectPrefabFolder, "*.prefab");
                foreach (var prefabFile in prefabFiles)
                {
                    string relativePath = prefabFile.Replace("\\", "/");
                    if (!relativePath.StartsWith("Assets/"))
                    {
                        string assetsPath = Path.GetFullPath("Assets").Replace("\\", "/");
                        relativePath = relativePath.Replace(assetsPath, "Assets");
                    }
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(relativePath);
                    if (prefab != null)
                    {
                        prefabItems.Add(new PrefabItem
                        {
                            ButtonText = prefab.name,
                            Prefab = prefab
                        });
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to load prefab from path: {relativePath}");
                    }
                }
            }
            else if (Directory.Exists(packagePrefabFolder))
            {
                // 複製 package 內 prefab 到本地 Assets/Editor/UIMaker/Prefab
                if (!Directory.Exists(localPrefabFolder))
                {
                    Directory.CreateDirectory(localPrefabFolder);
                }
                prefabFiles = Directory.GetFiles(packagePrefabFolder, "*.prefab");
                foreach (var prefabFile in prefabFiles)
                {
                    string fileName = Path.GetFileName(prefabFile);
                    string destinationPath = Path.Combine(localPrefabFolder, fileName);
                    File.Copy(prefabFile, destinationPath, true);
                }
                AssetDatabase.Refresh();
                prefabFiles = Directory.GetFiles(localPrefabFolder, "*.prefab");
                foreach (var prefabFile in prefabFiles)
                {
                    string relativePath = prefabFile.Replace("\\", "/");
                    if (!relativePath.StartsWith("Assets/"))
                    {
                        string assetsPath = Path.GetFullPath("Assets").Replace("\\", "/");
                        relativePath = relativePath.Replace(assetsPath, "Assets");
                    }
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(relativePath);
                    if (prefab != null)
                    {
                        prefabItems.Add(new PrefabItem
                        {
                            ButtonText = prefab.name,
                            Prefab = prefab
                        });
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to load prefab from path: {relativePath}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("Prefab folder not found in Assets/Modules/UIMaker/Resources/Prefab or Packages/com.unity.tools.uimaker/Resources/Prefab.");
            }

            rootVisualElement.Q<ListView>("PrefabListView")?.Rebuild();
            GenerateDynamicButtons(rootVisualElement);
        }

        private void SavePrefabListToConfig()
        {
            prefabListConfig.prefabList.Clear();
            foreach (var item in prefabItems)
            {
                prefabListConfig.prefabList.Add(item.Prefab);
            }
            EditorUtility.SetDirty(prefabListConfig);
            AssetDatabase.SaveAssets();
        }


    }

}
