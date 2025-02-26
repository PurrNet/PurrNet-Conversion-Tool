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
    public class FishNetConverter : NetworkSystemConverter, IFolderAwareConverter
    {
        public override string SystemName => "FishNet";
        
        public List<string> ScriptFolders { get; set; } = new List<string>();
        public List<string> PrefabFolders { get; set; } = new List<string>();
        
        // Conversion mappings
        private Dictionary<string, string> namespaceMappings = new Dictionary<string, string>
        {
            { "FishNet", "PurrNet" }
        };
        
        private Dictionary<string, string> typeMappings = new Dictionary<string, string>
        {
            { "NetworkConnection", "PlayerID" },
            { "NetworkManager", "NetworkManager" },
            { "Multipass", "CompositeTransport"},
            { "Tugboat", "UDPTransport"},
            { "Yak", "LocalTransport"},
            { "FishySteamworks", "SteamTransport"},
            { "NetworkObject", "NetworkIdentity"},
        };
        
        private Dictionary<string, string> propertyMappings = new Dictionary<string, string>
        {
            { "IsOwner", "isOwner" },
            { "IsServer", "isServer" },
            { "IsServerInitialized", "isServer" },
            { "IsClient", "isClient" },
            { "IsClientInitialized", "isClient" },
            { "OnStartClientCalled", "isSpawned"},
            { "OnStartServerCalled", "isSpawned"},
            { "OwnerId", "owner" },
            { "Owner", "owner" },
        };
        
        private Dictionary<string, string> methodMappings = new Dictionary<string, string>
        {
            { "OnStartClient", "OnSpawned" },
            { "OnStartServer", "OnSpawned" },
            { "OnStopClient", "OnDespawned" },
            { "OnStopServer", "OnDespawned" }
        };
        
        private Dictionary<string, string> memberMappings = new Dictionary<string, string>
        {
            { "InstanceFinder", "InstanceHandler" },
            { "ClientId", "id" }
        };
        
        private Dictionary<string, string> methodCallMappings = new Dictionary<string, string>
        {
            { "StartConnection(true)", "StartServer()" },
            { "StartConnection(false)", "StartClient()" }
        };
        
        private Dictionary<string, string> typeNamespaceRequirements = new Dictionary<string, string>
        {
            { "SteamTransport", "PurrNet.Steam" }
        };

        public override ConversionResult ConvertFullProject()
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
        
        public override ConversionResult ConvertCode()
        {
            var result = new ConversionResult();
            result.ConversionStats["files processed"] = 0;
            result.ConversionStats["namespaces converted"] = 0;
            result.ConversionStats["types converted"] = 0;
            result.ConversionStats["properties converted"] = 0;
            result.ConversionStats["methods converted"] = 0;
            result.ConversionStats["members converted"] = 0;
            result.ConversionStats["method calls converted"] = 0;
            
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
        
        private void ConvertFile(string filePath, ConversionResult result)
        {
            string code = File.ReadAllText(filePath);
            
            if (!code.Contains("FishNet") && !ContainsAnyTargetPattern(code))
            {
                return;
            }
            
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            
            root = (CompilationUnitSyntax)ConvertNamespaces(root, result);
            root = (CompilationUnitSyntax)ConvertTypes(root, result);
            root = (CompilationUnitSyntax)ConvertProperties(root, result);
            root = (CompilationUnitSyntax)ConvertMethods(root, result);
            root = (CompilationUnitSyntax)MethodsSecondPass(root, result);
            root = (CompilationUnitSyntax)ConvertMembers(root, result);
            root = (CompilationUnitSyntax)ConvertSpecialCases(root, result);
            root = (CompilationUnitSyntax)ConvertUsingDirectives(root, result);
            root = (CompilationUnitSyntax)ConvertMethodCalls(root, result);
            root = (CompilationUnitSyntax)ConvertNamespaces(root, result);
            
            File.WriteAllText(filePath, root.NormalizeWhitespace().ToFullString());
        }
        
        private bool ContainsAnyTargetPattern(string code)
        {
            foreach (var mapping in propertyMappings)
            {
                if (code.Contains(mapping.Key))
                    return true;
            }
            
            foreach (var mapping in methodMappings)
            {
                if (code.Contains(mapping.Key))
                    return true;
            }
            
            foreach (var mapping in memberMappings)
            {
                if (code.Contains(mapping.Key))
                    return true;
            }
            
            foreach (var mapping in typeMappings)
            {
                if (code.Contains(mapping.Key))
                    return true;
            }
            
            return false;
        }
        
        private SyntaxNode ConvertNamespaces(SyntaxNode node, ConversionResult result)
        {
            return node.ReplaceNodes(
                node.DescendantNodes().OfType<NamespaceDeclarationSyntax>(),
                (original, rewritten) =>
                {
                    var namespaceName = original.Name.ToString();
                    
                    foreach (var mapping in namespaceMappings)
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
        
        private SyntaxNode ConvertTypes(SyntaxNode node, ConversionResult result)
        {
            return node.ReplaceNodes(
                node.DescendantNodes().OfType<IdentifierNameSyntax>(),
                (original, rewritten) =>
                {
                    var typeName = original.Identifier.Text;
                    
                    if (typeMappings.TryGetValue(typeName, out string newType))
                    {
                        result.ConversionStats["types converted"]++;
                        return SyntaxFactory.IdentifierName(newType);
                    }
                    
                    return rewritten;
                });
        }
        
        private SyntaxNode ConvertProperties(SyntaxNode node, ConversionResult result)
        {
            return node.ReplaceNodes(
                node.DescendantNodes().OfType<IdentifierNameSyntax>(),
                (original, rewritten) =>
                {
                    var propertyName = original.Identifier.Text;
            
                    if (propertyMappings.TryGetValue(propertyName, out string newProperty))
                    {
                        result.ConversionStats["properties converted"]++;
                        return SyntaxFactory.IdentifierName(newProperty);
                    }
            
                    return rewritten;
                });
        }
        
        private SyntaxNode ConvertMethods(SyntaxNode node, ConversionResult result)
        {
            Dictionary<string, List<MethodDeclarationSyntax>> convertedMethodsMap = new Dictionary<string, List<MethodDeclarationSyntax>>();
            
            foreach (var method in node.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var methodName = method.Identifier.Text;
                if (methodMappings.TryGetValue(methodName, out string newMethod))
                {
                    if (!convertedMethodsMap.ContainsKey(newMethod))
                    {
                        convertedMethodsMap[newMethod] = new List<MethodDeclarationSyntax>();
                    }
                    convertedMethodsMap[newMethod].Add(method);
                }
            }
            
            foreach (var methodGroup in convertedMethodsMap)
            {
                string newMethodName = methodGroup.Key;
                var methods = methodGroup.Value;
                
                if (methods.Count == 0)
                    continue;
                
                var firstMethod = methods[0];
                
                var modifiers = SyntaxFactory.TokenList(
                    SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(),
                        SyntaxKind.ProtectedKeyword,
                        SyntaxFactory.TriviaList(SyntaxFactory.Space)),
                    SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(),
                        SyntaxKind.OverrideKeyword,
                        SyntaxFactory.TriviaList(SyntaxFactory.Space)));
                
                var parameter = SyntaxFactory.Parameter(
                        SyntaxFactory.Identifier("asServer"))
                    .WithType(SyntaxFactory.PredefinedType(
                        SyntaxFactory.Token(SyntaxKind.BoolKeyword)));
                
                var parameters = SyntaxFactory.ParameterList(
                    SyntaxFactory.SingletonSeparatedList(parameter));
                
                List<StatementSyntax> statements = new List<StatementSyntax>();
                
                foreach (var method in methods)
                {
                    var originalMethodName = method.Identifier.Text;
                    bool checkForServer = originalMethodName == "OnStartServer" || originalMethodName == "OnStopServer";
                    
                    BlockSyntax body;
                    if (method.Body != null)
                    {
                        body = method.Body;
                    }
                    else if (method.ExpressionBody != null)
                    {
                        body = SyntaxFactory.Block(
                            SyntaxFactory.ExpressionStatement(method.ExpressionBody.Expression));
                    }
                    else
                    {
                        body = SyntaxFactory.Block();
                    }
                    
                    ExpressionSyntax condition = checkForServer
                        ? (ExpressionSyntax)SyntaxFactory.IdentifierName("asServer")
                        : (ExpressionSyntax)SyntaxFactory.PrefixUnaryExpression(
                            SyntaxKind.LogicalNotExpression,
                            SyntaxFactory.IdentifierName("asServer"));
                    
                    var ifStatement = SyntaxFactory.IfStatement(condition, body)
                        .WithLeadingTrivia(SyntaxFactory.Whitespace("        "))
                        .NormalizeWhitespace();
                    
                    statements.Add(ifStatement);
                    result.ConversionStats["methods converted"]++;
                }
                
                var newBody = SyntaxFactory.Block(statements)
                    .WithLeadingTrivia(SyntaxFactory.Whitespace(" "))
                    .WithTrailingTrivia(SyntaxFactory.Whitespace("\n    "));
                
                MethodDeclarationSyntax newMethodDeclaration = SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.PredefinedType(
                            SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                        SyntaxFactory.Identifier(newMethodName))
                    .WithModifiers(modifiers)
                    .WithParameterList(parameters)
                    .WithBody(newBody)
                    .NormalizeWhitespace();

                node = node.ReplaceNode(firstMethod, newMethodDeclaration);
            }
            
            return node;
        }
        
        private SyntaxNode ConvertMethodCalls(SyntaxNode node, ConversionResult result)
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
                        
                                if (methodCallMappings.TryGetValue(patternKey, out string newMethodName))
                                {
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

        private SyntaxNode MethodsSecondPass(SyntaxNode node, ConversionResult result)
        {
            var methodsToRemove = node.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => 
                    m.Identifier.Text == "OnStartServer" || 
                    m.Identifier.Text == "OnStartClient" || 
                    m.Identifier.Text == "OnStopServer" || 
                    m.Identifier.Text == "OnStopClient")
                .ToList();
        
            if (methodsToRemove.Any())
            {
                result.ConversionStats["leftover methods removed"] = methodsToRemove.Count;
                return node.RemoveNodes(methodsToRemove, SyntaxRemoveOptions.KeepNoTrivia);
            }
    
            return node;
        }
        
        private SyntaxNode ConvertMembers(SyntaxNode node, ConversionResult result)
        {
            return node.ReplaceNodes(
                node.DescendantNodes().OfType<IdentifierNameSyntax>(),
                (original, rewritten) =>
                {
                    var memberName = original.Identifier.Text;
                    
                    if (memberMappings.TryGetValue(memberName, out string newMember))
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
        
        private SyntaxNode ConvertSpecialCases(SyntaxNode node, ConversionResult result)
        {
            node = node.ReplaceNodes(
                node.DescendantNodes().OfType<ParameterSyntax>(),
                (original, rewritten) =>
                {
                    if (original.Type?.ToString() == "NetworkManager")
                    {
                        return rewritten.WithIdentifier(SyntaxFactory.Identifier("networkManager"));
                    }
                    
                    return rewritten;
                });
            
            node = node.ReplaceNodes(
                node.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
                (original, rewritten) =>
                {
                    if (original.Name.Identifier.Text == "ClientId" && 
                        original.Expression is IdentifierNameSyntax identifier && 
                        identifier.Identifier.Text == "conn")
                    {
                        return SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("player"),
                            SyntaxFactory.IdentifierName("id"));
                    }
                    
                    return rewritten;
                });
                
            return node;
        }
        
        private SyntaxNode ConvertUsingDirectives(SyntaxNode node, ConversionResult result)
        {
            if (!(node is CompilationUnitSyntax compilationUnit))
                return node;

            var usingDirectives = compilationUnit.Usings.ToList();
            bool hasFishNetUsing = false;
            bool hasFishNetTransportingUsing = false;
            
            HashSet<string> requiredNamespaces = new HashSet<string>();
            
            foreach (var type in node.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var typeName = type.Identifier.Text;
                if (typeNamespaceRequirements.TryGetValue(typeName, out string requiredNamespace))
                {
                    requiredNamespaces.Add(requiredNamespace);
                }
            }

            var newUsings = new List<UsingDirectiveSyntax>();

            foreach (var usingDirective in usingDirectives)
            {
                var namespaceName = usingDirective.Name.ToString();
            
                if (namespaceName.StartsWith("FishNet"))
                {
                    hasFishNetUsing = true;
                    result.ConversionStats["namespaces converted"]++;
                
                    if (namespaceName == "FishNet.Transporting")
                    {
                        hasFishNetTransportingUsing = true;
                    }
                }
                else
                {
                    newUsings.Add(usingDirective);
                }
            }

            if (hasFishNetUsing)
            {
                newUsings.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("PurrNet")));
            
                if (hasFishNetTransportingUsing)
                {
                    newUsings.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("PurrNet.Transports")));
                }
                
                foreach (var requiredNamespace in requiredNamespaces)
                {
                    newUsings.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(requiredNamespace)));
                }
            }

            return compilationUnit.WithUsings(SyntaxFactory.List(newUsings));
        }
        
        public override ConversionResult ConvertPrefabs()
        {
            var result = new ConversionResult();
            result.ConversionStats["prefabs processed"] = 0;
            
            return result;
        }
    }
}