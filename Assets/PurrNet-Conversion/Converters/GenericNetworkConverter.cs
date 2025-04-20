using System.Collections.Generic;

namespace PurrNet.ConversionTool
{
    public interface IFolderAwareConverter
    {
        List<string> ScriptFolders { get; set; }
        List<string> PrefabFolders { get; set; }
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
}