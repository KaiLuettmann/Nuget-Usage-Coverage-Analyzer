using NuGet.Common;
using NuGet.ProjectModel;

namespace NuGetUsageCoverageAnalyzer;

static class PackageAuditor
{
    public static PackageAudit Run(string slnFile, SkipRules skipRules)
    {
        var audit = new PackageAudit();
        try
        {
            var allIds = ReadPackageIds(slnFile, audit);
            audit.TotalCount = allIds.Count;
            ClassifyPackages(allIds, skipRules, audit);
        }
        catch (Exception ex)
        {
            audit.Warnings.Add($"Package audit failed: {ex.Message}");
        }
        return audit;
    }

    private static HashSet<string> ReadPackageIds(string slnFile, PackageAudit audit)
    {
        var allIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var csprojPaths = SolutionParser.GetProjectPaths(slnFile);

        int loaded = 0;
        foreach (var csproj in csprojPaths)
        {
            string assetsFile = Path.Combine(
                Path.GetDirectoryName(csproj)!,
                "obj",
                "project.assets.json"
            );
            if (!File.Exists(assetsFile))
                continue;

            var lockFile = LockFileUtilities.GetLockFile(assetsFile, NullLogger.Instance);
            foreach (var group in lockFile.ProjectFileDependencyGroups)
            foreach (var dep in group.Dependencies)
                allIds.Add(dep.Split(' ')[0]); // "PackageName >= 1.0" → "PackageName"
            loaded++;
        }

        if (loaded == 0)
            audit.Warnings.Add("No project.assets.json found — run dotnet restore first.");

        return allIds;
    }

    private static void ClassifyPackages(
        HashSet<string> allIds,
        SkipRules skipRules,
        PackageAudit audit
    )
    {
        foreach (var id in allIds)
        {
            if (skipRules.ShouldSkip(id))
            {
                audit.SkippedIds.Add(id);
                continue;
            }
            audit.AnalysedIds.Add(id);
        }
    }
}
