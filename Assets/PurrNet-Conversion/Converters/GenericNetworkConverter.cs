using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PurrNet.ConversionTool
{
    public interface IFolderAwareConverter
    {
        List<string> ScriptFolders { get; set; }
        List<string> PrefabFolders { get; set; }
    }
    
    public abstract class NetworkSystemConverter
    {
        public abstract string SystemName { get; }
        
        public virtual ConversionResult ConvertFullProject()
        {
            var codeResult = ConvertCode();
            
            if (!codeResult.Success)
                return codeResult;
            
            var prefabResult = ConvertPrefabs();
            
            // Merge results
            foreach (var stat in codeResult.ConversionStats)
            {
                if (prefabResult.ConversionStats.ContainsKey(stat.Key))
                    prefabResult.ConversionStats[stat.Key] += stat.Value;
                else
                    prefabResult.ConversionStats[stat.Key] = stat.Value;
            }
            
            prefabResult.Success &= codeResult.Success;
            
            return prefabResult;
        }
        
        public virtual ConversionResult ConvertPrefabs()
        {
            return new ConversionResult();
        }
        
        public virtual ConversionResult ConvertCode()
        {
            return new ConversionResult();
        }
    }
    
    public class ConversionResult
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; } = string.Empty;
        public Dictionary<string, int> ConversionStats { get; set; } = new Dictionary<string, int>();
    
        public override string ToString()
        {
            if (!Success)
                return $"Conversion failed: {ErrorMessage}";
        
            string result = "Conversion completed successfully.\n";
        
            foreach (var stat in ConversionStats)
            {
                result += $"* {stat.Value} {stat.Key}\n";
            }
        
            return result;
        }
    }

    namespace PurrNet.ConversionTool
    {
        public static class ConversionHelper
        {
            private static string conversionToolPath;
        
            static ConversionHelper()
            {
                conversionToolPath = GetToolPath();
            }
        
            private static string GetToolPath()
            {
                var assembly = Assembly.GetExecutingAssembly();
                var types = assembly.GetTypes();
            
                foreach (var type in types)
                {
                    if (type.Namespace == "PurrNet.ConversionTool")
                    {
                        var script = MonoScript.FromScriptableObject(ScriptableObject.CreateInstance(type.Name));
                        if (script != null)
                        {
                            string path = AssetDatabase.GetAssetPath(script);
                            if (!string.IsNullOrEmpty(path))
                            {
                                return Path.GetDirectoryName(path);
                            }
                        }
                    }
                }
            
                return "Assets/PurrNet-Conversion";
            }
        
            public static string GetConversionToolPath()
            {
                return conversionToolPath;
            }
        
            public static bool ShouldSkipPath(string path)
            {
                path = Path.GetFullPath(path);
                string toolPath = Path.GetFullPath(conversionToolPath);
            
                return path.StartsWith(toolPath) || path.Contains("PurrNet-Conversion") || path.Contains("ConversionTool");
            }
        }
    }
}