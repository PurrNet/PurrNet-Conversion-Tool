using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace PurrNet.ConversionTool
{
    public class GenericNetworkConverter : IFolderAwareConverter
    {
        private NetworkSystemMappings mappings;
        
        public List<string> ScriptFolders { get; set; } = new List<string>();
        public List<string> PrefabFolders { get; set; } = new List<string>();
        
        public GenericNetworkConverter(NetworkSystemMappings mappings)
        {
            this.mappings = mappings;
        }
        
        public string SystemName => mappings.SystemName;
        
        public ConversionResult ConvertFullProject()
        {
            var codeResult = ConvertCode();
            
            if (!codeResult.Success)
                return codeResult;
            
            var prefabResult = ConvertPrefabs();
            
            // Merge results
            codeResult.ConversionStats.ToList().ForEach(x => {
                if (prefabResult.ConversionStats.ContainsKey(x.Key))
                    prefabResult.ConversionStats[x.Key] += x.Value;
                else
                    prefabResult.ConversionStats[x.Key] = x.Value;
            });
            
            prefabResult.Success &= codeResult.Success;
            
            return prefabResult;
        }
        
        public ConversionResult ConvertCode()
        {
            var result = new ConversionResult();
            InitializeConversionStats(result);
            
            try
            {
                foreach (var folder in ScriptFolders)
                {
                    if (!Directory.Exists(folder))
                    {
                        EditorUtility.DisplayProgressBar($"Converting {SystemName} Code", $"Folder not found: {folder}", 0);
                        continue;
                    }
                    
                    var scriptFiles = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
                    int fileCount = 0;
                    
                    foreach (var file in scriptFiles)
                    {
                        float progress = (float)fileCount / scriptFiles.Length;
                        EditorUtility.DisplayProgressBar($"Converting {SystemName} Code", $"Processing {Path.GetFileName(file)}", progress);
                        
                        ConvertFile(file, result);
                        fileCount++;
                        result.ConversionStats["files processed"]++;
                    }
                }
                
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            
            return result;
        }
        
        protected virtual void InitializeConversionStats(ConversionResult result)
        {
            result.ConversionStats["files processed"] = 0;
            result.ConversionStats["namespaces converted"] = 0;
            result.ConversionStats["types converted"] = 0;
            result.ConversionStats["properties converted"] = 0;
            result.ConversionStats["methods converted"] = 0;
            result.ConversionStats["members converted"] = 0;
            result.ConversionStats["method calls converted"] = 0;
            result.ConversionStats["parameters renamed"] = 0;
            result.ConversionStats["member access patterns converted"] = 0;
            result.ConversionStats["leftover methods removed"] = 0;
        }
        
        protected virtual void ConvertFile(string filePath, ConversionResult result)
        {
            string code = File.ReadAllText(filePath);
            
            // Skip files that don't contain system identifiers or target patterns
            if (!ContainsSystemIdentifiers(code) && !ContainsAnyTargetPattern(code))
            {
                return;
            }
            
            // Parse the code
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            
            // Track required namespaces
            HashSet<string> requiredNamespaces = DetectRequiredNamespaces(root);
            
            // Apply transformations
            root = (CompilationUnitSyntax)ConvertNamespaces(root, result);
            root = (CompilationUnitSyntax)ConvertTypes(root, result);
            root = (CompilationUnitSyntax)ConvertProperties(root, result);
            root = (CompilationUnitSyntax)ConvertMembers(root, result);
            root = (CompilationUnitSyntax)ConvertSpecialCases(root, result);
            root = (CompilationUnitSyntax)ConvertMethodCalls(root, result);
            
            // Apply mapping-specific special case handler
            root = (CompilationUnitSyntax)mappings.SpecialCaseHandler(root, result);
            
            // Update using directives last
            root = (CompilationUnitSyntax)ConvertUsingDirectives(root, result, requiredNamespaces);
            
            // Save the converted code
            File.WriteAllText(filePath, root.NormalizeWhitespace().ToFullString());
        }
        
        protected virtual bool ContainsSystemIdentifiers(string code)
        {
            foreach (var identifier in mappings.SystemIdentifiers)
            {
                if (code.Contains(identifier))
                    return true;
            }
            
            return false;
        }
        
        protected virtual bool ContainsAnyTargetPattern(string code)
        {
            foreach (var mapping in mappings.PropertyMappings)
            {
                if (code.Contains(mapping.Key))
                    return true;
            }
            
            foreach (var mapping in mappings.MethodMappings)
            {
                if (code.Contains(mapping.Key))
                    return true;
            }
            
            foreach (var mapping in mappings.MemberMappings)
            {
                if (code.Contains(mapping.Key))
                    return true;
            }
            
            foreach (var mapping in mappings.TypeMappings)
            {
                if (code.Contains(mapping.Key))
                    return true;
            }
            
            foreach (var methodCall in mappings.MethodCallMappings.Keys)
            {
                string methodName = methodCall.Split('(')[0];
                if (code.Contains(methodName))
                    return true;
            }
            
            return false;
        }
        
        protected HashSet<string> DetectRequiredNamespaces(SyntaxNode node)
        {
            HashSet<string> requiredNamespaces = new HashSet<string>();
            
            foreach (var type in node.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var typeName = type.Identifier.Text;
                if (mappings.TypeNamespaceRequirements.TryGetValue(typeName, out string requiredNamespace))
                {
                    requiredNamespaces.Add(requiredNamespace);
                }
            }
            
            return requiredNamespaces;
        }
        
        protected SyntaxNode ConvertNamespaces(SyntaxNode node, ConversionResult result)
        {
            return node.ReplaceNodes(
                node.DescendantNodes().OfType<NamespaceDeclarationSyntax>(),
                (original, rewritten) =>
                {
                    var namespaceName = original.Name.ToString();
                    
                    foreach (var mapping in mappings.NamespaceMappings)
                    {
                        if (namespaceName.StartsWith(mapping.Key))
                        {
                            var newName = namespaceName.Replace(mapping.Key, mapping.Value);
                            result.ConversionStats["namespaces converted"]++;
                            return rewritten.WithName(SyntaxFactory.ParseName(newName));
                        }
                    }
                    
                    return rewritten;
                });
        }
        
        protected SyntaxNode ConvertTypes(SyntaxNode node, ConversionResult result)
        {
            return node.ReplaceNodes(
                node.DescendantNodes().OfType<IdentifierNameSyntax>(),
                (original, rewritten) =>
                {
                    var typeName = original.Identifier.Text;
                    
                    if (mappings.TypeMappings.TryGetValue(typeName, out string newType))
                    {
                        result.ConversionStats["types converted"]++;
                        return SyntaxFactory.IdentifierName(newType);
                    }
                    
                    return rewritten;
                });
        }
        
        protected SyntaxNode ConvertProperties(SyntaxNode node, ConversionResult result)
        {
            return node.ReplaceNodes(
                node.DescendantNodes().OfType<IdentifierNameSyntax>(),
                (original, rewritten) =>
                {
                    var propertyName = original.Identifier.Text;
            
                    if (mappings.PropertyMappings.TryGetValue(propertyName, out string newProperty))
                    {
                        result.ConversionStats["properties converted"]++;
                        return SyntaxFactory.IdentifierName(newProperty);
                    }
            
                    return rewritten;
                });
        }
        
        protected SyntaxNode ConvertMembers(SyntaxNode node, ConversionResult result)
        {
            return node.ReplaceNodes(
                node.DescendantNodes().OfType<IdentifierNameSyntax>(),
                (original, rewritten) =>
                {
                    var memberName = original.Identifier.Text;
                    
                    if (mappings.MemberMappings.TryGetValue(memberName, out string newMember))
                    {
                        if (original.Parent is MemberAccessExpressionSyntax)
                        {
                            result.ConversionStats["members converted"]++;
                            return SyntaxFactory.IdentifierName(newMember);
                        }
                    }
                    
                    return rewritten;
                });
        }
        
        protected SyntaxNode ConvertSpecialCases(SyntaxNode node, ConversionResult result)
        {
            // Handle parameter mappings like NetworkManager -> networkManager
            node = node.ReplaceNodes(
                node.DescendantNodes().OfType<ParameterSyntax>(),
                (original, rewritten) =>
                {
                    string typeName = original.Type?.ToString();
                    if (typeName != null && mappings.ParameterMappings.TryGetValue(typeName, out string newName))
                    {
                        result.ConversionStats["parameters renamed"]++;
                        return rewritten.WithIdentifier(SyntaxFactory.Identifier(newName));
                    }
                    
                    return rewritten;
                });
            
            // Handle member access mappings like conn.ClientId -> player.id  
            node = node.ReplaceNodes(
                node.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
                (original, rewritten) =>
                {
                    if (original.Expression is IdentifierNameSyntax identifier)
                    {
                        string accessPattern = $"{identifier.Identifier.Text}.{original.Name.Identifier.Text}";
                        
                        if (mappings.MemberAccessMappings.TryGetValue(accessPattern, out var replacement))
                        {
                            result.ConversionStats["member access patterns converted"]++;
                            return SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName(replacement.Item1),
                                SyntaxFactory.IdentifierName(replacement.Item2));
                        }
                    }
                    
                    return rewritten;
                });
                
            return node;
        }
        
        protected SyntaxNode ConvertMethodCalls(SyntaxNode node, ConversionResult result)
        {
            return node.ReplaceNodes(
                node.DescendantNodes().OfType<InvocationExpressionSyntax>(),
                (original, rewritten) =>
                {
                    if (original.Expression is MemberAccessExpressionSyntax memberAccess)
                    {
                        var methodName = memberAccess.Name.Identifier.Text;
                
                        // Handle method calls with arguments
                        if (methodName == "StartConnection" && original.ArgumentList.Arguments.Count == 1)
                        {
                            var argument = original.ArgumentList.Arguments[0].Expression;
                    
                            if (argument is LiteralExpressionSyntax literal)
                            {
                                string patternKey = $"{methodName}({literal.Token.ValueText})";
                        
                                if (mappings.MethodCallMappings.TryGetValue(patternKey, out string newMethodNameWithParens))
                                {
                                    // Extract just the method name without parentheses
                                    string newMethodName = newMethodNameWithParens.Substring(0, newMethodNameWithParens.IndexOf('('));
                                
                                    var newMethodAccess = SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        memberAccess.Expression,
                                        SyntaxFactory.IdentifierName(newMethodName));
                                
                                    var newInvocation = SyntaxFactory.InvocationExpression(
                                        newMethodAccess,
                                        SyntaxFactory.ArgumentList());
                                
                                    result.ConversionStats["method calls converted"]++;
                                    return newInvocation;
                                }
                            }
                        }
                    }
            
                    return rewritten;
                });
        }
        
        protected SyntaxNode ConvertUsingDirectives(SyntaxNode node, ConversionResult result, HashSet<string> requiredNamespaces)
        {
            if (!(node is CompilationUnitSyntax compilationUnit))
                return node;

            var usingDirectives = compilationUnit.Usings.ToList();
            bool hasSystemIdentifierNamespace = false;
            bool hasTransportNamespace = false;
            
            var newUsings = new List<UsingDirectiveSyntax>();
            string sourceSystemPrefix = null;
            string targetSystemPrefix = null;
            
            // Find the root namespace prefixes (e.g., "FishNet" -> "PurrNet")
            foreach (var mapping in mappings.NamespaceMappings)
            {
                sourceSystemPrefix = mapping.Key;
                targetSystemPrefix = mapping.Value;
                break;
            }

            // Process existing using directives
            foreach (var usingDirective in usingDirectives)
            {
                var namespaceName = usingDirective.Name.ToString();
            
                if (sourceSystemPrefix != null && namespaceName.StartsWith(sourceSystemPrefix))
                {
                    hasSystemIdentifierNamespace = true;
                    result.ConversionStats["namespaces converted"]++;
                
                    // Check for transport namespace
                    if (namespaceName.Contains("Transport"))
                    {
                        hasTransportNamespace = true;
                    }
                    
                    // Don't add the original namespace, it will be replaced
                }
                else
                {
                    // Keep other using directives
                    newUsings.Add(usingDirective);
                }
            }

            // Add replacement namespaces
            if (hasSystemIdentifierNamespace && targetSystemPrefix != null)
            {
                newUsings.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(targetSystemPrefix)));
            
                if (hasTransportNamespace)
                {
                    newUsings.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName($"{targetSystemPrefix}.Transports")));
                }
                
                // Add any additional required namespaces
                foreach (var requiredNamespace in requiredNamespaces)
                {
                    newUsings.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(requiredNamespace)));
                }
            }

            return compilationUnit.WithUsings(SyntaxFactory.List(newUsings));
        }
        
        public ConversionResult ConvertPrefabs()
        {
            // Prefab conversion implementation would go here
            var result = new ConversionResult();
            result.ConversionStats["prefabs processed"] = 0;
            
            return result;
        }
    }
}