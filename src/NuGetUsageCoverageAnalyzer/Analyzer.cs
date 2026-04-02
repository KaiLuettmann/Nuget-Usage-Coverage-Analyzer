using System.Text.Json;
using System.Text.RegularExpressions;

namespace NuGetUsageCoverageAnalyzer;

public class Analyzer
{
    public event Action<RunStartingArgs>? RunStarting;
    public event Action<AuditCompletedArgs>? AuditCompleted;
    public event Action<CoverageLoadedArgs>? CoverageLoaded;
    public event Action<SourceFilesFoundArgs>? SourceFilesFound;
    public event Action? CompilationStarting;
    public event Action<CompilationBuiltArgs>? CompilationBuilt;
    public event Action<AssemblyMapReadyArgs>? AssemblyMapReady;    // verbose only
    public event Action<ScanProgressArgs>? ScanProgress;
    public event Action<ScanCompletedArgs>? ScanCompleted;
    public event Action<UsageCountsReadyArgs>? UsageCountsReady;    // verbose only
    public event Action<OutputReadyArgs>? OutputReady;

    public void Run(AnalyzerOptions options)
    {
        string slnFile = options.SlnFile;
        string configPath = options.ConfigPath;
        string coverageXml = options.CoverageXml;
        bool verbose = options.Verbose;
        bool includeTransitive = options.IncludeTransitive;

        string repoRoot = Path.GetDirectoryName(slnFile)!;
        string outputPath = options.OutputPath
            ?? Path.Combine(repoRoot, "NuGetUsageCoverageAnalyzer", "nuget-coverage-output.txt");

        RunStarting?.Invoke(new RunStartingArgs(slnFile, configPath, outputPath));

        var analysisConfig = LoadConfig(configPath);
        var skipRules = new SkipRules(analysisConfig);

        // --- Phase 0: Package audit ---
        PackageAudit audit = PackageAuditor.Run(slnFile, skipRules);
        AuditCompleted?.Invoke(new AuditCompletedArgs(audit.TotalCount, audit.SkippedCount, audit.AnalysedCount, audit.Warnings));

        // --- Phase 1: Load coverage data ---
        var (coverage, coverageWarning) = LoadCoverageOrEmpty(coverageXml);
        CoverageLoaded?.Invoke(new CoverageLoadedArgs(coverage.Count, coverageWarning));

        // --- Phase 2: Scan source files ---
        var hasExcludePattern = !string.IsNullOrEmpty(analysisConfig.SourceExcludePattern);
        var excludePattern = hasExcludePattern
            ? new Regex(analysisConfig.SourceExcludePattern, RegexOptions.Compiled | RegexOptions.IgnoreCase)
            : null;
        var allCsFiles = Directory
            .GetFiles(repoRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutputPath(f))
            .Where(f => excludePattern is null || !excludePattern.IsMatch(f))
            .ToArray();

        SourceFilesFound?.Invoke(new SourceFilesFoundArgs(allCsFiles.Length));
        CompilationStarting?.Invoke();

        var (compilation, assemblyToPackage, catalogueNamesSorted, dllWarnings) = CompilationBuilder.Build(
            slnFile,
            allCsFiles,
            skipRules,
            includeTransitive ? null : audit.AnalysedIds,
            verbose
        );

        CompilationBuilt?.Invoke(new CompilationBuiltArgs(
            assemblyToPackage.Count,
            catalogueNamesSorted.Length,
            includeTransitive
        ));

        if (verbose)
            AssemblyMapReady?.Invoke(new AssemblyMapReadyArgs(assemblyToPackage, catalogueNamesSorted, dllWarnings));

        var treeByPath = compilation.SyntaxTrees.ToDictionary(
            t => t.FilePath,
            StringComparer.OrdinalIgnoreCase
        );

        var allSites = new List<ApiSite>();
        int scanned = 0;
        foreach (var file in allCsFiles)
        {
            scanned++;
            if (scanned % 500 == 0)
                ScanProgress?.Invoke(new ScanProgressArgs(scanned, allCsFiles.Length));

            if (!treeByPath.TryGetValue(file, out var tree))
                continue;

            var semanticModel = compilation.GetSemanticModel(tree);
            allSites.AddRange(
                SemanticAnalyzer.Analyze(
                    tree, semanticModel, assemblyToPackage, catalogueNamesSorted, coverage, file)
            );
        }

        ScanCompleted?.Invoke(new ScanCompletedArgs(allSites.Count));

        if (verbose)
        {
            var countByPkg = allSites
                .GroupBy(s => s.PackageName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
            UsageCountsReady?.Invoke(new UsageCountsReadyArgs(countByPkg, catalogueNamesSorted));
        }

        // --- Phase 3: Render output ---
        var sitesByPkg = allSites.GroupBy(s => s.PackageName).ToDictionary(g => g.Key, g => g.ToList());

        var displayPackages = audit
            .AnalysedIds.Union(sitesByPkg.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var summaryRows = BuildSummaryRows(displayPackages, sitesByPkg);
        OutputFileRenderer.Render(repoRoot, outputPath, audit, displayPackages, sitesByPkg, summaryRows);
        OutputReady?.Invoke(new OutputReadyArgs(summaryRows, outputPath));
    }

    private static (Dictionary<string, FileCoverage> coverage, string? warning) LoadCoverageOrEmpty(
        string xmlPath)
    {
        if (!File.Exists(xmlPath))
            return (
                new Dictionary<string, FileCoverage>(StringComparer.OrdinalIgnoreCase),
                $"Coverage XML not found: {xmlPath}"
            );
        try
        {
            return (CoberturaParser.Load(xmlPath), null);
        }
        catch (Exception ex)
        {
            return (
                new Dictionary<string, FileCoverage>(StringComparer.OrdinalIgnoreCase),
                $"Failed to load coverage XML: {ex.Message}"
            );
        }
    }

    internal static IReadOnlyList<SummaryRow> BuildSummaryRows(
        IReadOnlyCollection<string> packageNames,
        Dictionary<string, List<ApiSite>> sitesByPkg
    )
    {
        var rows = new List<SummaryRow>();
        foreach (var pkgName in packageNames)
        {
            if (!sitesByPkg.TryGetValue(pkgName, out var sites) || sites.Count == 0)
            {
                rows.Add(new SummaryRow(pkgName, 0, 0, 0, "-", "NO_USAGES"));
                continue;
            }

            int instr = sites.Count(s => s.Coverable);
            int cov = sites.Count(s => s.Covered);
            string pct = instr > 0 ? $"{cov * 100 / instr}%" : "-";
            string status =
                instr == 0 ? "NOT_INSTRUMENTED"
                : cov == 0 ? "NOT_COVERED"
                : cov == instr ? "FULL_COVERED"
                : cov * 100 / instr >= 80 ? "HIGH_COVERED"
                : cov * 100 / instr >= 50 ? "MEDIUM_COVERED"
                : "LOW_COVERED";

            rows.Add(new SummaryRow(pkgName, sites.Count, instr, cov, pct, status));
        }
        return rows;
    }

    private static bool IsBuildOutputPath(string path)
    {
        ReadOnlySpan<char> p = path.AsSpan();
        char sep = Path.DirectorySeparatorChar;
        char altSep = Path.AltDirectorySeparatorChar;
        int start = 0;
        while (start < p.Length)
        {
            int next = p[start..].IndexOfAny(sep, altSep);
            var segment = next < 0 ? p[start..] : p.Slice(start, next);
            if (segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("bin", StringComparison.OrdinalIgnoreCase))
                return true;
            if (next < 0) break;
            start += next + 1;
        }
        return false;
    }

    internal static AnalysisConfig LoadConfig(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config not found: {path}");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AnalysisConfig>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                }
            ) ?? new AnalysisConfig();
    }
}
