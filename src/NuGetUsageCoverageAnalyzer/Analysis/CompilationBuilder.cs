#nullable enable
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuGet.Common;
using NuGet.ProjectModel;

namespace NuGetUsageCoverageAnalyzer;

static class CompilationBuilder
{
    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.Latest,
        DocumentationMode.None
    );

    private static readonly string[] TfmPreference =
    [
        "net10.0",
        "net10.0-windows",
        "net9.0",
        "net9.0-windows",
        "net8.0",
        "net8.0-windows",
        "net7.0",
        "net6.0",
        "netstandard2.1",
        "netstandard2.0",
        "netstandard1.6",
    ];

    /// <summary>
    /// Builds a CSharpCompilation from all source files in the solution.
    /// Returns:
    ///   AssemblyToPackage — assembly filename → NuGet package name (for direct DLL matches)
    ///   CatalogueNamesSorted — all discovered non-skipped package names, sorted by descending
    ///     length so FindBestCatalogueMatch returns the most-specific prefix match first.
    ///     Includes packages with _._  compile assets (framework packages).
    /// </summary>
    public static (
        CSharpCompilation Compilation,
        Dictionary<string, string> AssemblyToPackage,
        string[] CatalogueNamesSorted,
        IReadOnlyList<string> DllWarnings
    ) Build(
        string slnFile,
        string[] allCsFiles,
        SkipRules skipRules,
        HashSet<string>? directDepsOnly = null,
        bool verbose = false
    )
    {
        var assemblyToPackage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var allPackageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dllWarnings = new List<string>();
        // key = assembly filename without extension (deduplicates across package versions and
        // framework vs. NuGet copies of the same assembly), value = MetadataReference
        var metadataRefs = new Dictionary<string, MetadataReference>(
            StringComparer.OrdinalIgnoreCase
        );

        AddBclReferences(metadataRefs);
        AddFrameworkReferences(metadataRefs);
        AddNuGetReferences(
            slnFile,
            assemblyToPackage,
            metadataRefs,
            allPackageNames,
            dllWarnings,
            skipRules,
            verbose
        );

        var catalogueSource =
            directDepsOnly != null
                ? allPackageNames.Where(n => directDepsOnly.Contains(n))
                : (IEnumerable<string>)allPackageNames;
        var catalogueNamesSorted = catalogueSource.OrderByDescending(n => n.Length).ToArray();

        var syntaxTrees = ParseSourceFiles(allCsFiles);
        var compilation = CSharpCompilation.Create(
            "NuGetUsageCoverageAnalysis",
            syntaxTrees,
            metadataRefs.Values,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        return (compilation, assemblyToPackage, catalogueNamesSorted, dllWarnings);
    }

    private static void AddBclReferences(Dictionary<string, MetadataReference> refs)
    {
        var bclDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        AddDllsFromDir(bclDir, refs);
    }

    /// <summary>
    /// Adds ASP.NET Core (and Windows Desktop App) shared framework assemblies.
    /// Without these, types like ControllerBase, IServiceCollection, DbContext (ASP.NET Core
    /// flavours) cannot be resolved by the semantic model.
    /// </summary>
    private static void AddFrameworkReferences(Dictionary<string, MetadataReference> refs)
    {
        var sharedDir = GetSharedFrameworkDir();
        if (sharedDir == null)
            return;

        var major = Environment.Version.Major.ToString();
        foreach (
            var frameworkName in new[]
            {
                "Microsoft.AspNetCore.App",
                "Microsoft.WindowsDesktop.App",
            }
        )
        {
            var baseDir = Path.Combine(sharedDir, frameworkName);
            var versionDir = FindLatestCompatibleVersionDir(baseDir, major);
            if (versionDir != null)
                AddDllsFromDir(versionDir, refs);
        }
    }

    private static string? GetSharedFrameworkDir()
    {
        // typeof(object) is in: .../shared/Microsoft.NETCore.App/{version}/
        // Two levels up gives: .../shared/
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (runtimeDir == null)
            return null;
        var netcoreDir = Path.GetDirectoryName(runtimeDir);
        if (netcoreDir == null)
            return null;
        return Path.GetDirectoryName(netcoreDir);
    }

    private static string? FindLatestCompatibleVersionDir(string baseDir, string majorVersion)
    {
        if (!Directory.Exists(baseDir))
            return null;

        return Directory
            .GetDirectories(baseDir)
            .Where(d => Path.GetFileName(d).StartsWith(majorVersion + "."))
            .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static void AddDllsFromDir(string dir, Dictionary<string, MetadataReference> refs)
    {
        foreach (var dll in Directory.GetFiles(dir, "*.dll"))
        {
            var asmName = Path.GetFileNameWithoutExtension(dll);
            try
            {
                refs.TryAdd(asmName, MetadataReference.CreateFromFile(dll));
            }
            catch { }
        }
    }

    private static void AddNuGetReferences(
        string slnFile,
        Dictionary<string, string> assemblyToPackage,
        Dictionary<string, MetadataReference> refs,
        HashSet<string> allPackageNames,
        List<string> dllWarnings,
        SkipRules skipRules,
        bool verbose
    )
    {
        foreach (var csproj in SolutionParser.GetProjectPaths(slnFile))
            AddRefsFromProject(
                csproj,
                assemblyToPackage,
                refs,
                allPackageNames,
                dllWarnings,
                skipRules,
                verbose
            );
    }

    private static void AddRefsFromProject(
        string csproj,
        Dictionary<string, string> assemblyToPackage,
        Dictionary<string, MetadataReference> refs,
        HashSet<string> allPackageNames,
        List<string> dllWarnings,
        SkipRules skipRules,
        bool verbose
    )
    {
        var assetsFile = Path.Combine(Path.GetDirectoryName(csproj)!, "obj", "project.assets.json");
        if (!File.Exists(assetsFile))
            return;

        LockFile lockFile;
        try
        {
            lockFile = LockFileUtilities.GetLockFile(assetsFile, NullLogger.Instance);
        }
        catch
        {
            return;
        }

        var packageFolders = lockFile.PackageFolders.Select(f => f.Path).ToList();
        if (packageFolders.Count == 0)
            return;

        var target = ChooseTarget(lockFile.Targets);
        if (target == null)
            return;

        foreach (var lib in target.Libraries)
        {
            if (lib.Type == null || !lib.Type.Equals("package", StringComparison.OrdinalIgnoreCase) || lib.Name == null)
                continue;

            AddRefsFromLibrary(
                lib,
                packageFolders,
                assemblyToPackage,
                refs,
                allPackageNames,
                dllWarnings,
                skipRules,
                verbose
            );
        }
    }

    private static void AddRefsFromLibrary(
        LockFileTargetLibrary lib,
        List<string> packageFolders,
        Dictionary<string, string> assemblyToPackage,
        Dictionary<string, MetadataReference> refs,
        HashSet<string> allPackageNames,
        List<string> dllWarnings,
        SkipRules skipRules,
        bool verbose
    )
    {
        // lib.Name is nullable in NuGet.ProjectModel; guard is redundant with caller but required for analysis
        if (lib.Name is not { } libName)
            return;

        // Record the package name for the catalogue regardless of whether it ships a DLL.
        // This covers framework-provided packages (those with _._  compile assets) like
        // Microsoft.Extensions.DependencyInjection, Microsoft.AspNetCore.Mvc, etc.
        if (!skipRules.ShouldSkip(libName))
            allPackageNames.Add(libName);

        var version = lib.Version?.ToNormalizedString() ?? string.Empty;
        foreach (var asset in lib.CompileTimeAssemblies)
        {
            if (asset.Path.EndsWith("_._", StringComparison.Ordinal))
                continue; // types come from the shared framework — MetadataReference already added

            var dllPath = ResolveDllPath(packageFolders, libName, version, asset.Path);
            if (dllPath == null)
            {
                if (verbose)
                    dllWarnings.Add($"DLL not found on disk: {libName}/{version} {asset.Path}");
                continue;
            }

            var asmName = Path.GetFileNameWithoutExtension(dllPath);
            assemblyToPackage.TryAdd(asmName, libName);

            // Use assembly name as key — prevents two versions of the same DLL (e.g.
            // AutoMapper 10.x from one project and 12.x from another) from both entering
            // the compilation, which would cause GetSymbolInfo to return null for those types.
            if (!refs.ContainsKey(asmName))
            {
                try
                {
                    refs[asmName] = MetadataReference.CreateFromFile(dllPath);
                }
                catch (Exception ex)
                {
                    if (verbose)
                        dllWarnings.Add($"DLL load failed: {dllPath}: {ex.Message}");
                }
            }
        }
    }

    private static string? ResolveDllPath(
        List<string> packageFolders,
        string libName,
        string version,
        string assetPath
    )
    {
        // NuGet stores packages in lowercase-named directories
        var nameLower = libName.ToLowerInvariant();
        var relPath = assetPath.Replace('/', Path.DirectorySeparatorChar);

        foreach (var folder in packageFolders)
        {
            var path = Path.Combine(folder, nameLower, version, relPath);
            if (File.Exists(path))
                return path;

            // Some environments use original casing
            path = Path.Combine(folder, libName, version, relPath);
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static LockFileTarget? ChooseTarget(IList<LockFileTarget> targets)
    {
        // Prefer targets without RID first, then by TFM preference
        foreach (var tfm in TfmPreference)
        {
            var found = targets.FirstOrDefault(t =>
                t.TargetFramework.GetShortFolderName()
                    .Equals(tfm, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrEmpty(t.RuntimeIdentifier)
            );
            if (found != null)
                return found;
        }

        // Fall back to any target without RID, then any target
        return targets.FirstOrDefault(t => string.IsNullOrEmpty(t.RuntimeIdentifier))
            ?? targets.FirstOrDefault();
    }

    private static List<SyntaxTree> ParseSourceFiles(string[] files)
    {
        var trees = new List<SyntaxTree>(files.Length);
        foreach (var file in files)
        {
            try
            {
                var source = File.ReadAllText(file);
                trees.Add(CSharpSyntaxTree.ParseText(source, ParseOptions, file));
            }
            catch { }
        }
        return trees;
    }

}
