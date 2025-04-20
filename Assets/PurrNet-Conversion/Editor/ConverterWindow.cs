using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace PurrNet.ConversionTool
{
    public class ConverterWindow : EditorWindow
    {
        private List<ConverterDiscovery.ConverterInfo> availableConverters = new List<ConverterDiscovery.ConverterInfo>();
        private string[] networkingSystemOptions = new string[0];
        private int selectedNetworkingSystem = 0;
        private string conversionLog = "Waiting for action...";
        private Vector2 scrollPosition;
        private Vector2 foldersScrollPosition;
        private List<DefaultAsset> scriptFolderAssets = new List<DefaultAsset>();
        private List<DefaultAsset> prefabFolderAssets = new List<DefaultAsset>();
        private SerializedObject serializedObject;
        private ReorderableList scriptFoldersList;
        private ReorderableList prefabFoldersList;
        private bool showScriptFolders = true;
        private bool showPrefabFolders = true;
        [MenuItem("PurrNet/Conversion Tool")]
        public static void ShowWindow()
        {
            GetWindow<ConverterWindow>("PurrNet Converter");
        }

        private void OnEnable()
        {
            serializedObject = new SerializedObject(this);
            scriptFoldersList = new ReorderableList(scriptFolderAssets, typeof(DefaultAsset), true, true, true, true);
            scriptFoldersList.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, "Script Folders");
            }

            ;
            scriptFoldersList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;
                scriptFolderAssets[index] = (DefaultAsset)EditorGUI.ObjectField(rect, scriptFolderAssets[index], typeof(DefaultAsset), false);
            }

            ;
            scriptFoldersList.onAddCallback = (list) =>
            {
                scriptFolderAssets.Add(null);
            }

            ;
            prefabFoldersList = new ReorderableList(prefabFolderAssets, typeof(DefaultAsset), true, true, true, true);
            prefabFoldersList.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, "Prefab Folders");
            }

            ;
            prefabFoldersList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;
                prefabFolderAssets[index] = (DefaultAsset)EditorGUI.ObjectField(rect, prefabFolderAssets[index], typeof(DefaultAsset), false);
            }

            ;
            prefabFoldersList.onAddCallback = (list) =>
            {
                prefabFolderAssets.Add(null);
            }

            ;
            if (scriptFolderAssets.Count == 0)
            {
                DefaultAsset assetsFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets");
                if (assetsFolder != null)
                {
                    scriptFolderAssets.Add(assetsFolder);
                }
            }

            if (prefabFolderAssets.Count == 0)
            {
                DefaultAsset assetsFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets");
                if (assetsFolder != null)
                {
                    prefabFolderAssets.Add(assetsFolder);
                }
            }

            RefreshConverters();
        }

        private void RefreshConverters()
        {
            availableConverters.Clear();
            availableConverters = ConverterDiscovery.DiscoverConverters();
            networkingSystemOptions = availableConverters.Select(c => c.Name).ToArray();
            if (networkingSystemOptions.Length == 0)
            {
                networkingSystemOptions = new[]{"No converters found"};
                selectedNetworkingSystem = 0;
            }
            else if (selectedNetworkingSystem >= networkingSystemOptions.Length)
            {
                selectedNetworkingSystem = 0;
            }
        }

        private void OnGUI()
        {
            serializedObject.Update();
            GUILayout.Label("Convert from:", EditorStyles.boldLabel);
            if (networkingSystemOptions.Length > 0)
            {
                selectedNetworkingSystem = EditorGUILayout.Popup(selectedNetworkingSystem, networkingSystemOptions);
            }
            else
            {
                EditorGUILayout.HelpBox("No converters found. Please add converter classes to your project.", MessageType.Warning);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Converters"))
            {
                RefreshConverters();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);
            GUILayout.Label("Conversion Scope:", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foldersScrollPosition = EditorGUILayout.BeginScrollView(foldersScrollPosition, GUILayout.Height(200));
            showScriptFolders = EditorGUILayout.Foldout(showScriptFolders, "Script Folders", true);
            if (showScriptFolders)
            {
                scriptFoldersList.DoLayoutList();
            }

            EditorGUILayout.Space(5);
            showPrefabFolders = EditorGUILayout.Foldout(showPrefabFolders, "Prefab Folders", true);
            if (showPrefabFolders)
            {
                prefabFoldersList.DoLayoutList();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
            bool hasConverters = availableConverters.Count > 0;
            GUI.enabled = hasConverters;
            GUILayout.Label("Conversion Options:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Convert Full Project", GUILayout.Height(30)))
            {
                ConvertFullProject();
            }

            if (GUILayout.Button("Convert Prefabs", GUILayout.Height(30)))
            {
                ConvertPrefabs();
            }

            if (GUILayout.Button("Convert Code", GUILayout.Height(30)))
            {
                ConvertCode();
            }

            EditorGUILayout.EndHorizontal();
            GUI.enabled = true;
            EditorGUILayout.Space(15);
            GUILayout.Label("Conversion Log:", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            EditorGUILayout.SelectableLabel(conversionLog, EditorStyles.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            serializedObject.ApplyModifiedProperties();
        }

        private GenericNetworkConverter GetSelectedConverter()
        {
            if (availableConverters.Count == 0 || selectedNetworkingSystem >= availableConverters.Count)
            {
                return null;
            }

            return availableConverters[selectedNetworkingSystem].Converter;
        }

        private void ConvertFullProject()
        {
            var converter = GetSelectedConverter();
            if (converter == null)
            {
                conversionLog = "No converter selected.";
                return;
            }

            try
            {
                if (converter is IFolderAwareConverter folderAwareConverter)
                {
                    folderAwareConverter.ScriptFolders = GetAssetPaths(scriptFolderAssets);
                    folderAwareConverter.PrefabFolders = GetAssetPaths(prefabFolderAssets);
                }

                var result = converter.ConvertFullProject();
                conversionLog = result.ToString();
            }
            catch (Exception ex)
            {
                conversionLog = $"Error during conversion: {ex.Message}";
            }
        }

        private void ConvertPrefabs()
        {
            var converter = GetSelectedConverter();
            if (converter == null)
            {
                conversionLog = "No converter selected.";
                return;
            }

            try
            {
                if (converter is IFolderAwareConverter folderAwareConverter)
                {
                    folderAwareConverter.PrefabFolders = GetAssetPaths(prefabFolderAssets);
                }

                var result = converter.ConvertPrefabs();
                conversionLog = result.ToString();
            }
            catch (Exception ex)
            {
                conversionLog = $"Error during prefab conversion: {ex.Message}";
            }
        }

        private void ConvertCode()
        {
            var converter = GetSelectedConverter();
            if (converter == null)
            {
                conversionLog = "No converter selected.";
                return;
            }

            try
            {
                if (converter is IFolderAwareConverter folderAwareConverter)
                {
                    folderAwareConverter.ScriptFolders = GetAssetPaths(scriptFolderAssets);
                }

                var result = converter.ConvertCode();
                conversionLog = result.ToString();
            }
            catch (Exception ex)
            {
                conversionLog = $"Error during code conversion: {ex.Message}";
            }
        }

        private List<string> GetAssetPaths(List<DefaultAsset> assets)
        {
            List<string> paths = new List<string>();
            foreach (var asset in assets)
            {
                if (asset != null)
                {
                    string path = AssetDatabase.GetAssetPath(asset);
                    if (!string.IsNullOrEmpty(path))
                    {
                        paths.Add(path);
                    }
                }
            }

            if (paths.Count == 0)
            {
                paths.Add("Assets");
            }

            return paths;
        }
    }
}