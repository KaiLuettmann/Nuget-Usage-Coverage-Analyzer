namespace NuGetUsageCoverageAnalyzer;

record AnalysisConfig
{
    public string[] SkipExact { get; init; } = [];
    public string[] SkipPrefixes { get; init; } = [];
    public string[] SkipInternalPrefixes { get; init; } = [];

    /// <summary>
    /// Regex applied to each .cs file path to exclude non-production files (tests, etc.).
    /// </summary>
    public string SourceExcludePattern { get; init; } = "";
}
