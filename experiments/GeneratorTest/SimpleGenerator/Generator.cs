using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class SyntaxTreeExtensions
{
    public static IEnumerable<SyntaxNode> Dn(this SyntaxTree tree)
    {
        return tree.GetRoot().DescendantNodes();
    }
}

public static class SyntaxExtensions
{
    public static IEnumerable<SyntaxNode> Dn(this SyntaxNode syntax)
    {
        return syntax.DescendantNodes();
    }

    public static IEnumerable<SyntaxToken> Dt(this SyntaxNode syntax)
    {
        return syntax.DescendantTokens();
    }
}

[Generator]
public class TestGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        /* for incremental
         
        context.RegisterPostInitializationOutput(i =>
        {
            const string attributeSource = @"public class MarkerAttribute : System.Attribute {}";

            i.AddSource("MarkerAttribute.generated.cs", attributeSource);
        });*/
    }

    public void Execute(GeneratorExecutionContext ctx)
    {
        // var main = context.Compilation.GetEntryPoint(context.CancellationToken);
        // main.ContainingNamespace.ToDisplayString()
        // main.ContainingType.Name

        foreach (var cl in GetClassesWithAttribute(ctx, "GameStateAttribute"))
        {

            var name = cl.Identifier;

            var code =
$@"
using System;
using MessagePack;

internal static partial class {name}GeneratedExtensions
{{
    public static void TestGen(this {name} self)
    {{
        Console.WriteLine(""{name} has a generated method."");
    }}
}}";

            ctx.AddSource($"{name}.g.cs", code);
        }
    }

    static IEnumerable<ClassDeclarationSyntax> GetClassesWithAttribute(GeneratorExecutionContext context, string attributeFullname)
    {
        var trees = context.Compilation.SyntaxTrees;

        var treesWithStuff = trees.Where(st => st.Dn().OfType<ClassDeclarationSyntax>().Any(p => p.Dn().OfType<AttributeSyntax>().Any()));

        foreach (var tree in treesWithStuff)
        {
            var semanticModel = context.Compilation.GetSemanticModel(tree);

            var classNodes = tree.Dn().OfType<ClassDeclarationSyntax>();
            
            foreach(var atrClass in classNodes.Where(cd => cd.Dn().OfType<AttributeSyntax>().Any()))
            {
                var atrNodes = atrClass.Dn().OfType<AttributeSyntax>();

                var desiredAtrNodes = atrNodes.Where(a => semanticModel.GetTypeInfo(a).Type?.Name == attributeFullname);

                if (desiredAtrNodes.Any())
                {
                    yield return atrClass;
                }
            }
        }
    }
}

