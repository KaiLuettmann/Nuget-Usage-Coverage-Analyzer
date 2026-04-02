namespace NuGetUsageCoverageAnalyzer;

class SkipRules
{
    private readonly HashSet<string> skipExact;
    private readonly string[] skipPrefixes;
    private readonly string[] skipInternalPrefixes;

    public SkipRules(AnalysisConfig config)
    {
        skipExact = new HashSet<string>(config.SkipExact, StringComparer.OrdinalIgnoreCase);
        skipPrefixes = config.SkipPrefixes;
        skipInternalPrefixes = config.SkipInternalPrefixes;
    }

    public bool ShouldSkip(string id)
    {
        if (skipExact.Contains(id))
            return true;
        foreach (var p in skipPrefixes)
            if (id.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                return true;
        foreach (var p in skipInternalPrefixes)
            if (id.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
