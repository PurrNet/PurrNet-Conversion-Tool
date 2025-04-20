using System;
using System.IO;
using UnityEngine;

namespace PurrNet.ConversionTool
{
    public static class ConversionLogger
    {
        private static string logFilePath;
        
        static ConversionLogger()
        {
            logFilePath = Path.Combine(Application.dataPath, "..", "ConversionChangelog.txt");
        }
        
        public static void LogChange(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string logEntry = $"[{timestamp}] {message}";
                
                bool isNewFile = !File.Exists(logFilePath);
                
                using (StreamWriter writer = File.AppendText(logFilePath))
                {
                    writer.WriteLine(logEntry);
                }
                
                if (isNewFile)
                {
                    Debug.Log($"Created conversion log at {logFilePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to write to conversion log: {ex.Message}");
            }
        }
    }
}