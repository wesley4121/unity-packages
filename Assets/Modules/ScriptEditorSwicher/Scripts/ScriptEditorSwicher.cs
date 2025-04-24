using System.Collections.Generic;
using System.Linq;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Modules.ScriptEditorSwicher
{
    public class ScriptEditorSwicher : EditorWindow
    {
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        [MenuItem("Tools/ScriptEditorSwicher")]
        public static void ShowExample()
        {
            ScriptEditorSwicher wnd = GetWindow<ScriptEditorSwicher>();
            wnd.titleContent = new GUIContent("ScriptEditorSwicher");
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // Instantiate UXML
            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            // Get available script editors
            var foundScriptEditors = CodeEditor.Editor.GetFoundScriptEditorPaths();
            var availableEditorsPath = foundScriptEditors.Select(pair => pair.Key).ToList();
            var availableEditors = foundScriptEditors.Select(pair => pair.Value).ToList();

            // Setup current script editor label
            var currentScriptEditorPath = CodeEditor.CurrentEditorInstallation;
            var currentScriptEditorContainer = root.Q<VisualElement>("CurrentScriptEditorContainer");
            var currentScriptEditorLabel = currentScriptEditorContainer.Q<Label>("CurrentScriptEditorLabel");
            var currentScriptEditorName = currentScriptEditorContainer.Q<Label>("CurrentScriptEditorName");

            // Determine the editor name based on the current script editor path
            string editorName = foundScriptEditors.FirstOrDefault(pair => currentScriptEditorPath.Contains(pair.Key)).Value ?? "Unknown Editor";
            currentScriptEditorLabel.text = "Current Script Editor: ";
            currentScriptEditorName.text = editorName;
            currentScriptEditorName.style.color = new StyleColor(Color.green);

            // Setup dropdown menu
            var dropdown = root.Q<DropdownField>("ScriptEditorDropdown");
            dropdown.choices = availableEditors;
            dropdown.index = availableEditorsPath.IndexOf(currentScriptEditorPath);

            dropdown.RegisterValueChangedCallback(evt =>
            {
                // Get event index
                int index = dropdown.index;
                // Set new script editor
                CodeEditor.SetExternalScriptEditor(availableEditorsPath[index]);
                // Refresh current script editor label
                currentScriptEditorPath = availableEditorsPath[index];
                editorName = foundScriptEditors.FirstOrDefault(pair => currentScriptEditorPath.Contains(pair.Key)).Value ?? "Unknown Editor";
                currentScriptEditorLabel.text = "Current Script Editor: ";
                currentScriptEditorName.text = editorName;
                currentScriptEditorName.style.color = new StyleColor(Color.green);
            });

            var targetDropdown = root.Q<DropdownField>("TargetScriptEditorDropdown");
            var switchButton = root.Q<Button>("SwitchScriptEditorButton");

            // 設定第二個下拉選單的選項
            if (availableEditors.Count > 1)
            {
                targetDropdown.choices = availableEditors;
                targetDropdown.index = availableEditorsPath.IndexOf(currentScriptEditorPath) == 0 ? 1 : 0;
                targetDropdown.SetEnabled(true);
            }
            else
            {
                targetDropdown.choices = new List<string> { "No other editors available" };
                targetDropdown.index = 0;
                targetDropdown.SetEnabled(false);
            }

            var progressBarContainer = root.Q<VisualElement>("ProgressBarContainer");
            var progressBar = root.Q<ProgressBar>("ProgressBar");

            // 設定按鈕行為
            switchButton.clicked += () =>
            {
                progressBarContainer.style.display = DisplayStyle.Flex;
                progressBar.value = 0;

                EditorApplication.update += UpdateProgressBar;

                void UpdateProgressBar()
                {
                    progressBar.value += 0.1f;
                    if (progressBar.value >= 1.0f)
                    {
                        EditorApplication.update -= UpdateProgressBar;
                        progressBarContainer.style.display = DisplayStyle.None;

                        // 切換腳本編輯器
                        int sourceIndex = dropdown.index;
                        int targetIndex = targetDropdown.index;

                        if (sourceIndex != targetIndex)
                        {
                            CodeEditor.SetExternalScriptEditor(availableEditorsPath[targetIndex]);
                            currentScriptEditorPath = availableEditorsPath[targetIndex];
                            editorName = foundScriptEditors.FirstOrDefault(pair => currentScriptEditorPath.Contains(pair.Key)).Value ?? "Unknown Editor";
                            currentScriptEditorLabel.text = "Current Script Editor: ";
                            currentScriptEditorName.text = editorName;
                            currentScriptEditorName.style.color = new StyleColor(Color.green);

                            // 更新下拉選單
                            dropdown.index = targetIndex;
                            targetDropdown.index = sourceIndex;
                        }
                    }
                }
            };

            var switchIcon = root.Q<Image>("SwitchIcon");
            switchIcon.image = EditorGUIUtility.IconContent("RotateTool On").image;
        }
    }

}
