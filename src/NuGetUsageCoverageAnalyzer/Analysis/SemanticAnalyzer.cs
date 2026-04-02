#nullable enable
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NuGetUsageCoverageAnalyzer;

/// <summary>
/// Uses Roslyn SemanticModel to resolve identifiers to their exact containing assembly,
/// then maps assembly → NuGet package via two strategies:
///   1. Direct lookup in assemblyToPackage (populated from project.assets.json)
///   2. Prefix-match fallback against the catalogue (handles framework assemblies like
///      "Microsoft.AspNetCore.Mvc.Core" → catalogue entry "Microsoft.AspNetCore.Mvc",
///      and sub-package assemblies like "Swashbuckle.AspNetCore.SwaggerGen" → umbrella
///      catalogue entry "Swashbuckle.AspNetCore").
/// </summary>
static class SemanticAnalyzer
{
    /// <param name="catalogueNamesSorted">
    /// All catalogue package names, sorted by descending length.
    /// Longest-first ordering ensures we return the most-specific match.
    /// </param>
    public static List<ApiSite> Analyze(
        SyntaxTree tree,
        SemanticModel semanticModel,
        Dictionary<string, string> assemblyToPackage,
        string[] catalogueNamesSorted,
        Dictionary<string, FileCoverage> coverage,
        string filePath
    )
    {
        var result = new List<ApiSite>();
        var seen = new HashSet<(string pkg, int line)>();
        var root = tree.GetRoot();

        coverage.TryGetValue(filePath, out var fc);

        foreach (var node in root.DescendantNodes())
        {
            ISymbol? symbol;
            string kind;

            switch (node)
            {
                case IdentifierNameSyntax idName:
                    symbol = semanticModel.GetSymbolInfo(idName).Symbol;
                    kind = DetermineKind(idName);
                    break;
                case GenericNameSyntax genName:
                    symbol = semanticModel.GetSymbolInfo(genName).Symbol;
                    kind = DetermineKind(genName);
                    break;
                default:
                    continue;
            }

            if (symbol == null)
                continue;

            var assemblyName = symbol.ContainingAssembly?.Name;
            if (assemblyName == null)
                continue;

            // Strategy 1: assembly is in assemblyToPackage (NuGet package DLL found on disk)
            // Strategy 2: assembly name prefix-matches a catalogue entry (framework assemblies,
            //             umbrella packages, sub-package assemblies not directly in catalogue)
            string? pkgName;
            if (assemblyToPackage.TryGetValue(assemblyName, out var mappedPkg))
                pkgName = FindBestCatalogueMatch(mappedPkg, catalogueNamesSorted);
            else
                pkgName = FindBestCatalogueMatch(assemblyName, catalogueNamesSorted);

            if (pkgName == null)
                continue;

            int lineNo = tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
            if (!seen.Add((pkgName, lineNo)))
                continue;

            bool covered = fc?.Covered.Contains(lineNo) ?? false;
            bool coverable = fc?.AllInstrumented.Contains(lineNo) ?? false;
            string matchedName = BuildMatchedName(symbol);

            result.Add(
                new ApiSite(filePath, lineNo, matchedName, kind, pkgName, covered, coverable)
            );
        }

        return result;
    }

    /// <summary>
    /// Returns the best (longest) catalogue name that exactly matches or is a prefix of
    /// <paramref name="name"/>. Returns null if no catalogue entry covers the name.
    /// </summary>
    private static string? FindBestCatalogueMatch(string name, string[] catalogueSorted)
    {
        // catalogueSorted is ordered by descending length — first match is the most specific
        foreach (var candidate in catalogueSorted)
        {
            if (name.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                return candidate;
            if (name.StartsWith(candidate + ".", StringComparison.OrdinalIgnoreCase))
                return candidate;
        }
        return null;
    }

    private static string BuildMatchedName(ISymbol symbol)
    {
        var typeName = symbol.ContainingType?.Name;
        return typeName != null ? $"{typeName}.{symbol.Name}" : symbol.Name;
    }

    private static string DetermineKind(SyntaxNode node)
    {
        var parent = node.Parent;
        return parent switch
        {
            ObjectCreationExpressionSyntax => "ObjectCreation",
            AttributeSyntax => "Attribute",
            NameSyntax { Parent: AttributeSyntax } => "Attribute",
            InvocationExpressionSyntax => "MethodCall",
            MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax } => "MethodCall",
            MemberBindingExpressionSyntax { Parent: InvocationExpressionSyntax } => "MethodCall",
            _ => "TypeRef",
        };
    }
}
