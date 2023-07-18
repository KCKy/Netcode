using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Generator;

[Generator(LanguageNames.CSharp)]
public class Generator : IIncrementalGenerator
{
    const string GamestateInterfaceFullName = "MemoryPackTest.GameStateAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var types = context.SyntaxProvider.ForAttributeWithMetadataName(
            GamestateInterfaceFullName,
            (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax or InterfaceDeclarationSyntax,
            (ctx, _) => (TypeDeclarationSyntax) ctx.TargetNode);


        var source = types.Combine(context.CompilationProvider).WithComparer(Comparer.Instance);

        context.RegisterSourceOutput(source, (ctx, src) =>
        {
            var (syntax, compilation) = src;

            var model = compilation.GetSemanticModel(syntax.SyntaxTree);

            var symbol = model.GetDeclaredSymbol(syntax, ctx.CancellationToken);

            if (symbol is null)
                return;

            var fullname = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", "")
                .Replace("<", "_")
                .Replace(">", "_");

            var text = $@"[MemoryPackable]
public partial class {fullname} {{ }}";

            ctx.AddSource($"{fullname}", "");

        });
    }

    class Comparer : IEqualityComparer<(TypeDeclarationSyntax, Compilation)>
    {
        public static readonly Comparer Instance = new();

        public bool Equals((TypeDeclarationSyntax, Compilation) x, (TypeDeclarationSyntax, Compilation) y)
        {
            return x.Item1.Equals(y.Item1);
        }

        public int GetHashCode((TypeDeclarationSyntax, Compilation) obj)
        {
            return obj.Item1.GetHashCode();
        }
    }
}
