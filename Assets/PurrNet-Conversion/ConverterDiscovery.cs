using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PurrNet.ConversionTool
{
    public static class ConverterDiscovery
    {
        public class ConverterInfo
        {
            public string Name { get; set; }

            public GenericNetworkConverter Converter { get; set; }
        }

        public static List<ConverterInfo> DiscoverConverters()
        {
            List<ConverterInfo> discoveredConverters = new List<ConverterInfo>();
            string[] guids = AssetDatabase.FindAssets("t:MonoScript");
            HashSet<string> processedFolders = new HashSet<string>();
            foreach (string guid in guids)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                string folderPath = System.IO.Path.GetDirectoryName(scriptPath);
                if (string.IsNullOrEmpty(folderPath) || !processedFolders.Add(folderPath))
                    continue;
                GenericNetworkConverter converter = CreateConverterFromFolder(folderPath);
                if (converter != null)
                {
                    discoveredConverters.Add(new ConverterInfo{Name = converter.SystemName, Converter = converter});
                }
            }

            return discoveredConverters;
        }

        private static GenericNetworkConverter CreateConverterFromFolder(string folderPath)
        {
            NetworkSystemMappings mappings = null;
            NetworkPrefabHandling prefabHandling = null;
            NetworkSceneHandling sceneHandler = null;
            string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[]{folderPath});
            foreach (string guid in scriptGuids)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                if (script == null)
                    continue;
                Type scriptType = script.GetClass();
                if (scriptType == null)
                    continue;
                if (scriptType.IsAbstract)
                    continue;
                if (typeof(NetworkSystemMappings).IsAssignableFrom(scriptType) && scriptType != typeof(NetworkSystemMappings))
                {
                    try
                    {
                        mappings = (NetworkSystemMappings)Activator.CreateInstance(scriptType);
                    }
                    catch (Exception)
                    {
                    }
                }

                if (typeof(NetworkPrefabHandling).IsAssignableFrom(scriptType) && scriptType != typeof(NetworkPrefabHandling))
                {
                    try
                    {
                        prefabHandling = (NetworkPrefabHandling)Activator.CreateInstance(scriptType);
                    }
                    catch (Exception)
                    {
                    }
                }
                
                if (typeof(NetworkSceneHandling).IsAssignableFrom(scriptType) && scriptType != typeof(NetworkSceneHandling))
                {
                    try
                    {
                        sceneHandler = (NetworkSceneHandling)Activator.CreateInstance(scriptType);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            if (mappings != null && prefabHandling != null && mappings.SystemName != "Generic")
            {
                return new GenericNetworkConverter(mappings, prefabHandling, sceneHandler);
            }

            return null;
        }
    }
}