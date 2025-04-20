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
        private NetworkPrefabHandling prefabHandler;
        public List<string> ScriptFolders { get; set; } = new List<string>();
        
        public List<string> PrefabFolders { get; set; } = new List<string>();
        
        public GenericNetworkConverter(NetworkSystemMappings mappings, NetworkPrefabHandling prefabHandler)
        {
            this.mappings = mappings;
            this.prefabHandler = prefabHandler;
        }

        public string SystemName => mappings.SystemName;
        public ConversionResult ConvertFullProject()
        {
            var codeResult = ConvertCode();
            if (!codeResult.Success)
                return codeResult;
            var prefabResult = ConvertPrefabs();
            // Merge results
            codeResult.ConversionStats.ToList().ForEach(x =>
            {
                if (prefabResult.ConversionStats.ContainsKey(x.Key))
                    prefabResult.ConversionStats[x.Key] += x.Value;
                else
                    prefabResult.ConversionStats[x.Key] = x.Value;
            }

            );
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
            result.ConversionStats["attributes converted"] = 0;
            result.ConversionStats["attribute parameters converted"] = 0;
            result.ConversionStats["parameter default values converted"] = 0;
            result.ConversionStats["type-specific member accesses converted"] = 0;
        }

        protected virtual void ConvertFile(string filePath, ConversionResult result)
        {
            string code = File.ReadAllText(filePath);
            if (!ContainsSystemIdentifiers(code) && !ContainsAnyTargetPattern(code))
            {
                return;
            }

            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            HashSet<string> requiredNamespaces = DetectRequiredNamespaces(root);
            root = (CompilationUnitSyntax)ConvertNamespaces(root, result);
            root = (CompilationUnitSyntax)ConvertTypes(root, result);
            root = (CompilationUnitSyntax)ConvertProperties(root, result);
            root = (CompilationUnitSyntax)ConvertMembers(root, result);
            root = (CompilationUnitSyntax)ConvertAttributes(root, result);
            root = (CompilationUnitSyntax)ConvertAttributeParameters(root, result);
            root = (CompilationUnitSyntax)ConvertSpecialCases(root, result);
            root = (CompilationUnitSyntax)ConvertMethodCalls(root, result);
            root = (CompilationUnitSyntax)mappings.SpecialCaseHandler(root, result);
            root = (CompilationUnitSyntax)ConvertUsingDirectives(root, result, requiredNamespaces);
            File.WriteAllText(filePath, root.NormalizeWhitespace().ToFullString());
            
            if (!code.Equals(root.NormalizeWhitespace().ToFullString()))
            {
                List<string> changes = result.ConversionStats
                    .Where(kvp => kvp.Value > 0)
                    .Select(kvp => $"{kvp.Value} {kvp.Key}")
                    .ToList();

                string summary = string.Join(", ", changes);
                ConversionLogger.LogChange($"Modified: {Path.GetFileName(filePath)} ({summary})");
            }

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

            foreach (var mapping in mappings.AttributeMappings)
            {
                if (code.Contains(mapping.Key))
                    return true;
            }

            foreach (var mapping in mappings.AttributeParameterMappings)
            {
                if (code.Contains(mapping.Key))
                    return true;
            }

            foreach (var typeMapping in mappings.TypeSpecificMemberMappings)
            {
                foreach (var memberMapping in typeMapping.Value)
                {
                    if (code.Contains(memberMapping.Key))
                        return true;
                }
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
            return node.ReplaceNodes(node.DescendantNodes().OfType<NamespaceDeclarationSyntax>(), (original, rewritten) =>
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
            }

            );
        }

        protected SyntaxNode ConvertTypes(SyntaxNode node, ConversionResult result)
        {
            node = node.ReplaceNodes(
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
                
            node = node.ReplaceNodes(
                node.DescendantNodes().OfType<PredefinedTypeSyntax>(),
                (original, rewritten) =>
                {
                    var keyword = original.Keyword.ValueText;
                    
                    if (mappings.TypeMappings.TryGetValue(keyword, out string newType))
                    {
                        result.ConversionStats["types converted"]++;
                        return SyntaxFactory.IdentifierName(newType);
                    }
                    
                    return rewritten;
                });
                
            return node;
        }

        protected SyntaxNode ConvertProperties(SyntaxNode node, ConversionResult result)
        {
            return node.ReplaceNodes(node.DescendantNodes().OfType<IdentifierNameSyntax>(), (original, rewritten) =>
            {
                var propertyName = original.Identifier.Text;
                if (mappings.PropertyMappings.TryGetValue(propertyName, out string newProperty))
                {
                    result.ConversionStats["properties converted"]++;
                    return SyntaxFactory.IdentifierName(newProperty);
                }

                return rewritten;
            }

            );
        }

        protected SyntaxNode ConvertMembers(SyntaxNode node, ConversionResult result)
        {
            return node.ReplaceNodes(node.DescendantNodes().OfType<IdentifierNameSyntax>(), (original, rewritten) =>
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
            }

            );
        }

        protected SyntaxNode ConvertAttributes(SyntaxNode node, ConversionResult result)
        {
            return node.ReplaceNodes(node.DescendantNodes().OfType<AttributeSyntax>(), (original, rewritten) =>
            {
                if (original.Name is IdentifierNameSyntax attributeName)
                {
                    string originalName = attributeName.Identifier.Text;
                    if (mappings.AttributeMappings.TryGetValue(originalName, out string newAttributeName))
                    {
                        result.ConversionStats["attributes converted"]++;
                        return rewritten.WithName(SyntaxFactory.IdentifierName(newAttributeName));
                    }

                    if (originalName.EndsWith("Attribute"))
                    {
                        string trimmedName = originalName.Substring(0, originalName.Length - 9);
                        if (mappings.AttributeMappings.TryGetValue(trimmedName, out string trimmedNewName))
                        {
                            result.ConversionStats["attributes converted"]++;
                            return rewritten.WithName(SyntaxFactory.IdentifierName(trimmedNewName + "Attribute"));
                        }
                    }
                }

                return rewritten;
            }

            );
        }

        protected SyntaxNode ConvertAttributeParameters(SyntaxNode node, ConversionResult result)
        {
            return node.ReplaceNodes(node.DescendantNodes().OfType<AttributeArgumentSyntax>(), (original, rewritten) =>
            {
                if (original.NameEquals != null && original.NameEquals.Name is IdentifierNameSyntax nameIdentifier)
                {
                    string parameterName = nameIdentifier.Identifier.Text;
                    if (mappings.AttributeParameterMappings.TryGetValue(parameterName, out string newParameterName))
                    {
                        result.ConversionStats["attribute parameters converted"]++;
                        var newNameEquals = SyntaxFactory.NameColon(SyntaxFactory.IdentifierName(newParameterName));
                        return rewritten.WithNameColon(newNameEquals).WithNameEquals(null);
                    }
                }

                return rewritten;
            }

            );
        }

        protected SyntaxNode ConvertSpecialCases(SyntaxNode node, ConversionResult conversionResult)
        {
            // First pass: Handle parameter type to name mappings
            node = node.ReplaceNodes(node.DescendantNodes().OfType<ParameterSyntax>(), (original, rewritten) =>
            {
                string typeName = original.Type?.ToString();
                if (typeName != null && mappings.ParameterMappings.TryGetValue(typeName, out string newName))
                {
                    conversionResult.ConversionStats["parameters renamed"]++;
                    return rewritten.WithIdentifier(SyntaxFactory.Identifier(newName));
                }

                return rewritten;
            }

            );
            // Second pass: Handle target type default values
            node = node.ReplaceNodes(node.DescendantNodes().OfType<ParameterSyntax>(), (original, rewritten) =>
            {
                if (original.Type == null || original.Default == null || original.Default.Value == null)
                    return rewritten;
                string typeName = original.Type.ToString();
                string defaultValue = original.Default.Value.ToString();
                string key = $"{typeName}:{defaultValue}";
                if (mappings.TargetTypeDefaultMappings.TryGetValue(key, out string newDefaultValue))
                {
                    var newEqualsValue = SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression(newDefaultValue));
                    conversionResult.ConversionStats["parameter default values converted"]++;
                    return rewritten.WithDefault(newEqualsValue);
                }

                return rewritten;
            }

            );
            // Handle member access mappings like conn.ClientId -> player.id  
            node = node.ReplaceNodes(node.DescendantNodes().OfType<MemberAccessExpressionSyntax>(), (original, rewritten) =>
            {
                if (original.Expression is IdentifierNameSyntax identifier)
                {
                    string accessPattern = $"{identifier.Identifier.Text}.{original.Name.Identifier.Text}";
                    if (mappings.MemberAccessMappings.TryGetValue(accessPattern, out var replacement))
                    {
                        conversionResult.ConversionStats["member access patterns converted"]++;
                        return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName(replacement.Item1), SyntaxFactory.IdentifierName(replacement.Item2));
                    }
                }

                return rewritten;
            }

            );
            // Handle type-specific member access patterns - we need to match by partial type name
            if (!conversionResult.ConversionStats.ContainsKey("type-specific member accesses converted"))
            {
                conversionResult.ConversionStats["type-specific member accesses converted"] = 0;
            }

            node = node.ReplaceNodes(node.DescendantNodes().OfType<MemberAccessExpressionSyntax>(), (original, rewritten) =>
            {
                if (original.Name is IdentifierNameSyntax memberName)
                {
                    string memberNameText = memberName.Identifier.Text;
                    bool memberIsTargeted = false;
                    foreach (var mapping in mappings.TypeSpecificMemberMappings)
                    {
                        if (mapping.Value.ContainsKey(memberNameText))
                        {
                            memberIsTargeted = true;
                            break;
                        }
                    }

                    if (!memberIsTargeted)
                        return rewritten;
                    if (original.Expression is IdentifierNameSyntax variableIdentifier)
                    {
                        string variableName = variableIdentifier.Identifier.Text;
                        var fieldDeclarations = node.DescendantNodes().OfType<FieldDeclarationSyntax>();
                        foreach (var field in fieldDeclarations)
                        {
                            foreach (var variable in field.Declaration.Variables)
                            {
                                if (variable.Identifier.Text == variableName)
                                {
                                    string fullTypeName = field.Declaration.Type.ToString();
                                    foreach (var typeMapping in mappings.TypeSpecificMemberMappings)
                                    {
                                        string typeKey = typeMapping.Key;
                                        if (fullTypeName.EndsWith(typeKey) || fullTypeName.Contains(typeKey + "<") || fullTypeName == typeKey)
                                        {
                                            if (typeMapping.Value.TryGetValue(memberNameText, out string newMemberName))
                                            {
                                                conversionResult.ConversionStats["type-specific member accesses converted"]++;
                                                return rewritten.WithName(SyntaxFactory.IdentifierName(newMemberName));
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        var localDeclarations = node.DescendantNodes().OfType<LocalDeclarationStatementSyntax>();
                        foreach (var local in localDeclarations)
                        {
                            foreach (var variable in local.Declaration.Variables)
                            {
                                if (variable.Identifier.Text == variableName)
                                {
                                    string fullTypeName = local.Declaration.Type.ToString();
                                    foreach (var typeMapping in mappings.TypeSpecificMemberMappings)
                                    {
                                        string typeKey = typeMapping.Key;
                                        if (fullTypeName.EndsWith(typeKey) || fullTypeName.Contains(typeKey + "<") || fullTypeName == typeKey)
                                        {
                                            if (typeMapping.Value.TryGetValue(memberNameText, out string newMemberName))
                                            {
                                                conversionResult.ConversionStats["type-specific member accesses converted"]++;
                                                return rewritten.WithName(SyntaxFactory.IdentifierName(newMemberName));
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // Check parameters as well
                        var parameters = node.DescendantNodes().OfType<ParameterSyntax>().Where(p => p.Identifier.Text == variableName);
                        foreach (var parameter in parameters)
                        {
                            if (parameter.Type != null)
                            {
                                string parameterType = parameter.Type.ToString();
                                foreach (var typeMapping in mappings.TypeSpecificMemberMappings)
                                {
                                    string typeKey = typeMapping.Key;
                                    if (parameterType.EndsWith(typeKey) || parameterType.Contains(typeKey + "<") || parameterType == typeKey)
                                    {
                                        if (typeMapping.Value.TryGetValue(memberNameText, out string newMemberName))
                                        {
                                            conversionResult.ConversionStats["type-specific member accesses converted"]++;
                                            return rewritten.WithName(SyntaxFactory.IdentifierName(newMemberName));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return rewritten;
            }

            );
            return node;
        }

        protected SyntaxNode ConvertMethodCalls(SyntaxNode node, ConversionResult result)
        {
            return node.ReplaceNodes(node.DescendantNodes().OfType<InvocationExpressionSyntax>(), (original, rewritten) =>
            {
                if (original.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    var methodName = memberAccess.Name.Identifier.Text;
                    if (methodName == "StartConnection" && original.ArgumentList.Arguments.Count == 1)
                    {
                        var argument = original.ArgumentList.Arguments[0].Expression;
                        if (argument is LiteralExpressionSyntax literal)
                        {
                            string patternKey = $"{methodName}({literal.Token.ValueText})";
                            if (mappings.MethodCallMappings.TryGetValue(patternKey, out string newMethodNameWithParens))
                            {
                                string newMethodName = newMethodNameWithParens.Substring(0, newMethodNameWithParens.IndexOf('('));
                                var newMethodAccess = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, memberAccess.Expression, SyntaxFactory.IdentifierName(newMethodName));
                                var newInvocation = SyntaxFactory.InvocationExpression(newMethodAccess, SyntaxFactory.ArgumentList());
                                result.ConversionStats["method calls converted"]++;
                                return newInvocation;
                            }
                        }
                    }
                }

                return rewritten;
            }

            );
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
                    if (namespaceName.Contains("Transport"))
                    {
                        hasTransportNamespace = true;
                    }
                }
                else
                {
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

                foreach (var requiredNamespace in requiredNamespaces)
                {
                    newUsings.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(requiredNamespace)));
                }
            }

            return compilationUnit.WithUsings(SyntaxFactory.List(newUsings));
        }

        public ConversionResult ConvertPrefabs()
        {
            var result = new ConversionResult();
            result.ConversionStats["prefabs processed"] = 0;
            result.ConversionStats["prefabs converted"] = 0;
            try
            {
                foreach (var folder in PrefabFolders)
                {
                    if (!Directory.Exists(folder))
                    {
                        EditorUtility.DisplayProgressBar($"Converting {SystemName} Prefabs", $"Folder not found: {folder}", 0);
                        continue;
                    }

                    var prefabFiles = Directory.GetFiles(folder, "*.prefab", SearchOption.AllDirectories);
                    int fileCount = 0;
                    foreach (var file in prefabFiles)
                    {
                        float progress = (float)fileCount / prefabFiles.Length;
                        EditorUtility.DisplayProgressBar($"Converting {SystemName} Prefabs", $"Processing {Path.GetFileName(file)}", progress);
                        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file);
                        if (prefab != null)
                        {
                            bool converted = prefabHandler.ConvertPrefab(prefab);
                            if (converted)
                            {
                                EditorUtility.SetDirty(prefab);
                                result.ConversionStats["prefabs converted"]++;
                            }

                            result.ConversionStats["prefabs processed"]++;
                        }

                        fileCount++;
                    }
                }

                AssetDatabase.SaveAssets();
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
    }
}