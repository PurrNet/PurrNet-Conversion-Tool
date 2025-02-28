using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PurrNet.ConversionTool
{
    public class FishNetMappings : NetworkSystemMappings
    {
        public FishNetMappings()
        {
            SystemName = "FishNet";
            SystemIdentifiers = new List<string> { "FishNet" };
            
            NamespaceMappings = new Dictionary<string, string> 
            {
                { "FishNet", "PurrNet" }
            };
            
            TypeMappings = new Dictionary<string, string>
            {
                { "NetworkConnection", "PlayerID" },
                { "NetworkManager", "NetworkManager" },
                { "Multipass", "CompositeTransport"},
                { "Tugboat", "UDPTransport"},
                { "Yak", "LocalTransport"},
                { "FishySteamworks", "SteamTransport"},
                { "NetworkObject", "NetworkIdentity"}
            };
            
            PropertyMappings = new Dictionary<string, string>
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
                { "LocalConnection", "localPlayer" },
            };
            
            MethodMappings = new Dictionary<string, string>
            {
                { "OnStartClient", "OnSpawned" },
                { "OnStartServer", "OnSpawned" },
                { "OnStopClient", "OnDespawned" },
                { "OnStopServer", "OnDespawned" }
            };
            
            MemberMappings = new Dictionary<string, string>
            {
                { "InstanceFinder", "InstanceHandler" },
                { "ClientId", "id" }
            };
            
            MethodCallMappings = new Dictionary<string, string>
            {
                { "StartConnection(true)", "StartServer()" },
                { "StartConnection(false)", "StartClient()" },
                { "StopConnection(true)", "StopServer()" },
                { "StopConnection(false)", "StopClient()" },
            };
            
            TypeNamespaceRequirements = new Dictionary<string, string>
            {
                { "SteamTransport", "PurrNet.Steam" }
            };
            
            // Special case: NetworkManager parameter -> networkManager
            ParameterMappings = new Dictionary<string, string>
            {
                { "NetworkManager", "networkManager" }
            };
            
            // Special case: conn.ClientId -> player.id
            MemberAccessMappings = new Dictionary<string, (string, string)>
            {
                { "conn.ClientId", ("player", "id") }
            };
            
            TargetTypeDefaultMappings = new Dictionary<string, string>
            {
                { "PlayerID:null", "default" }
            };
            
            AttributeMappings = new Dictionary<string, string> { };
            AttributeParameterMappings = new Dictionary<string, string>
            {
                {"BufferLast", "bufferLast"},
                {"RequireOwnership", "requireOwnership"},
                {"ExcludeOwner", "excludeOwner"},
                {"ExcludeServer", "excludeSender"},
                {"RunLocally", "runLocally"},
            };
            
            TypeSpecificMemberMappings = new Dictionary<string, Dictionary<string, string>>
            {
                { "SyncVar", new Dictionary<string, string> 
                    {
                        { "Value", "value" },
                        {"OnChange", "onChanged"},
                        {"DirtyAll", "FlushImmediately"}
                    }
                },
            };
        }
        
        public override SyntaxNode SpecialCaseHandler(SyntaxNode node, ConversionResult result)
        {
            Dictionary<string, List<MethodDeclarationSyntax>> convertedMethodsMap = new Dictionary<string, List<MethodDeclarationSyntax>>();
            
            foreach (var method in node.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var methodName = method.Identifier.Text;
                if (MethodMappings.TryGetValue(methodName, out string newMethod))
                {
                    if (!convertedMethodsMap.ContainsKey(newMethod))
                    {
                        convertedMethodsMap[newMethod] = new List<MethodDeclarationSyntax>();
                    }
                    convertedMethodsMap[newMethod].Add(method);
                }
            }
            
            // Process each method group
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
            
            // Remove any leftover OnStart/OnStop methods that have been processed
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
                node = node.RemoveNodes(methodsToRemove, SyntaxRemoveOptions.KeepNoTrivia);
            }
            
            return node;
        }
    }
}