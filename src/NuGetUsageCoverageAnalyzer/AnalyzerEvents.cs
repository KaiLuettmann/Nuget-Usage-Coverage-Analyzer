namespace NuGetUsageCoverageAnalyzer;

public record RunStartingArgs(string SlnFile, string ConfigPath, string OutputPath);

public record AuditCompletedArgs(int TotalCount, int SkippedCount, int AnalysedCount,
    IReadOnlyList<string> Warnings);

public record CoverageLoadedArgs(int FileCount, string? Warning = null);

public record SourceFilesFoundArgs(int Count);

public record CompilationBuiltArgs(int AssemblyCount, int PackageCount, bool IncludeTransitive);

public record AssemblyMapReadyArgs(
    IReadOnlyDictionary<string, string> AssemblyToPackage,
    IReadOnlyList<string> CatalogueNames,
    IReadOnlyList<string> DllWarnings
);

public record ScanProgressArgs(int Scanned, int Total);

public record ScanCompletedArgs(int TotalUsages);

public record UsageCountsReadyArgs(
    IReadOnlyDictionary<string, int> CountByPackage,
    IReadOnlyList<string> CatalogueNames
);

public record OutputReadyArgs(IReadOnlyList<SummaryRow> SummaryRows, string OutputPath);

public record AnalyzerOptions(
    string SlnFile,
    string ConfigPath,
    string? OutputPath,
    string CoverageXml,
    bool Verbose,
    bool IncludeTransitive
);
