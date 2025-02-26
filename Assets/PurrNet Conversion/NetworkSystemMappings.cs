using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace PurrNet.ConversionTool
{
    public class NetworkSystemMappings
    {
        // System identification
        public string SystemName { get; set; } = "Generic";
        public List<string> SystemIdentifiers { get; set; } = new List<string>();
        
        // Basic mappings
        public Dictionary<string, string> NamespaceMappings { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> TypeMappings { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> PropertyMappings { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> MethodMappings { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> MemberMappings { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> MethodCallMappings { get; set; } = new Dictionary<string, string>();
        
        // Special cases mappings
        public Dictionary<string, string> TypeNamespaceRequirements { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> ParameterMappings { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, (string, string)> MemberAccessMappings { get; set; } = new Dictionary<string, (string, string)>();

        // Override to handle unique cases
        public virtual SyntaxNode SpecialCaseHandler(SyntaxNode node, ConversionResult result)
        {
            return node;
        }
    }
}