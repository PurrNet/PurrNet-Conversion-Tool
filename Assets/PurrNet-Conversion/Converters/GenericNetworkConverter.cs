using System.Collections.Generic;
using UnityEngine;

namespace PurrNet.ConversionTool
{
    public interface IFolderAwareConverter
    {
        List<string> ScriptFolders
        {
            get;
            set;
        }

        List<string> PrefabFolders
        {
            get;
            set;
        }
    }

    public abstract class NetworkSystemConverter
    {
        public abstract string SystemName
        {
            get;
        }

        public virtual ConversionResult ConvertFullProject()
        {
            return new ConversionResult();
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
        public bool Success
        {
            get;
            set;
        }

        = true;
        public string ErrorMessage
        {
            get;
            set;
        }

        = string.Empty;
        public Dictionary<string, int> ConversionStats
        {
            get;
            set;
        }

        = new Dictionary<string, int>();
        private static Vector2 scrollPosition;
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