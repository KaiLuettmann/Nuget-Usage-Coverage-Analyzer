namespace NuGetUsageCoverageAnalyzer;

class PackageAudit
{
    public int TotalCount { get; set; }
    public int SkippedCount => SkippedIds.Count;
    public int AnalysedCount => AnalysedIds.Count;
    public List<string> SkippedIds { get; } = [];
    public HashSet<string> AnalysedIds { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Warnings { get; } = [];
}
