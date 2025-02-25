using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PurrNet.ConversionTool
{
    public class ConverterWindow : EditorWindow
    {
        private string[] networkingSystemOptions = { "FishNet" };
        private int selectedNetworkingSystem = 0;
        private string conversionLog = "Waiting for action...";
        private Vector2 scrollPosition;
        private Vector2 foldersScrollPosition;

        private Dictionary<string, NetworkSystemConverter> converters = new Dictionary<string, NetworkSystemConverter>();
        
        // Folder paths
        private List<DefaultAsset> scriptFolderAssets = new List<DefaultAsset>();
        private List<DefaultAsset> prefabFolderAssets = new List<DefaultAsset>();
        
        // Serialized properties for reorderable lists
        private SerializedObject serializedObject;
        private UnityEditorInternal.ReorderableList scriptFoldersList;
        private UnityEditorInternal.ReorderableList prefabFoldersList;
        
        // Foldout states
        private bool showScriptFolders = true;
        private bool showPrefabFolders = true;

        [MenuItem("PurrNet/Conversion Tool")]
        public static void ShowWindow()
        {
            GetWindow<ConverterWindow>("PurrNet Converter");
        }

        private void OnEnable()
        {
            // Set up serialized object and reorderable lists
            serializedObject = new SerializedObject(this);
            
            // Set up script folders list
            scriptFoldersList = new UnityEditorInternal.ReorderableList(
                scriptFolderAssets, 
                typeof(DefaultAsset),
                true, // draggable
                true, // display header
                true, // add button
                true  // remove button
            );
            
            scriptFoldersList.drawHeaderCallback = (Rect rect) => {
                EditorGUI.LabelField(rect, "Script Folders");
            };
            
            scriptFoldersList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;
                
                scriptFolderAssets[index] = (DefaultAsset)EditorGUI.ObjectField(
                    rect, scriptFolderAssets[index], typeof(DefaultAsset), false);
            };
            
            scriptFoldersList.onAddCallback = (UnityEditorInternal.ReorderableList list) => {
                scriptFolderAssets.Add(null);
            };
            
            // Set up prefab folders list
            prefabFoldersList = new UnityEditorInternal.ReorderableList(
                prefabFolderAssets, 
                typeof(DefaultAsset),
                true, // draggable
                true, // display header
                true, // add button
                true  // remove button
            );
            
            prefabFoldersList.drawHeaderCallback = (Rect rect) => {
                EditorGUI.LabelField(rect, "Prefab Folders");
            };
            
            prefabFoldersList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;
                
                prefabFolderAssets[index] = (DefaultAsset)EditorGUI.ObjectField(
                    rect, prefabFolderAssets[index], typeof(DefaultAsset), false);
            };
            
            prefabFoldersList.onAddCallback = (UnityEditorInternal.ReorderableList list) => {
                prefabFolderAssets.Add(null);
            };
            
            // Initialize with default folder if empty
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

            // Register converters
            converters.Add("FishNet", new FishNetConverter());
            // converters.Add("Photon PUN", new PhotonPunConverter());
            // converters.Add("Mirror", new MirrorConverter());
            // converters.Add("Unity Netcode", new NetcodeConverter());
        }

        private void OnGUI()
        {
            serializedObject.Update();
            
            GUILayout.Label("Convert from:", EditorStyles.boldLabel);
            selectedNetworkingSystem = EditorGUILayout.Popup(selectedNetworkingSystem, networkingSystemOptions);

            EditorGUILayout.Space(10);
            
            // Folder selection section
            GUILayout.Label("Conversion Scope:", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foldersScrollPosition = EditorGUILayout.BeginScrollView(foldersScrollPosition, GUILayout.Height(200));
            
            // Script folders section
            showScriptFolders = EditorGUILayout.Foldout(showScriptFolders, "Script Folders", true);
            if (showScriptFolders)
            {
                scriptFoldersList.DoLayoutList();
            }
            
            EditorGUILayout.Space(5);
            
            // Prefab folders section
            showPrefabFolders = EditorGUILayout.Foldout(showPrefabFolders, "Prefab Folders", true);
            if (showPrefabFolders)
            {
                prefabFoldersList.DoLayoutList();
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);

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

            EditorGUILayout.Space(15);

            GUILayout.Label("Conversion Log:", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));

            EditorGUILayout.SelectableLabel(conversionLog, EditorStyles.textArea, GUILayout.ExpandHeight(true));

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            
            serializedObject.ApplyModifiedProperties();
        }

        private void ConvertFullProject()
        {
            string selectedSystem = networkingSystemOptions[selectedNetworkingSystem];

            if (!converters.ContainsKey(selectedSystem))
            {
                conversionLog = $"Converter for {selectedSystem} not implemented yet.";
                return;
            }

            try
            {
                var converter = converters[selectedSystem];
                
                // Set folders for the converter
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
            string selectedSystem = networkingSystemOptions[selectedNetworkingSystem];

            if (!converters.ContainsKey(selectedSystem))
            {
                conversionLog = $"Converter for {selectedSystem} not implemented yet.";
                return;
            }

            try
            {
                var converter = converters[selectedSystem];
                
                // Set folders for the converter
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
            string selectedSystem = networkingSystemOptions[selectedNetworkingSystem];

            if (!converters.ContainsKey(selectedSystem))
            {
                conversionLog = $"Converter for {selectedSystem} not implemented yet.";
                return;
            }

            try
            {
                var converter = converters[selectedSystem];
                
                // Set folders for the converter
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
        
        // Helper method to convert DefaultAsset list to string paths list
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
            
            // If no valid folders, use Assets as default
            if (paths.Count == 0)
            {
                paths.Add("Assets");
            }
            
            return paths;
        }
    }
}