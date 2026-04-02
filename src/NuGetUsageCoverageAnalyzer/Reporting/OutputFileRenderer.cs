using System.Text;

namespace NuGetUsageCoverageAnalyzer;

static class OutputFileRenderer
{
    public static void Render(
        string repoRoot,
        string outputPath,
        PackageAudit audit,
        IReadOnlyCollection<string> displayPackages,
        Dictionary<string, List<ApiSite>> sitesByPkg,
        IReadOnlyList<SummaryRow> summaryRows
    )
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            $"NuGetUsageCoverageAnalyzer - NuGet Package Usage Coverage Analysis  |  {DateTime.Now:yyyy-MM-dd HH:mm}"
        );
        sb.AppendLine(new string('=', 110));
        sb.AppendLine();

        AppendAuditSection(sb, audit);

        sb.AppendLine(new string('=', 110));
        sb.AppendLine();

        AppendSummaryTable(sb, summaryRows);
        AppendLegend(sb);
        AppendPerPackageDetail(sb, repoRoot, displayPackages, sitesByPkg);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(
            outputPath,
            sb.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
        );
    }

    private static void AppendAuditSection(StringBuilder sb, PackageAudit audit)
    {
        sb.AppendLine("PACKAGE AUDIT");
        sb.AppendLine(new string('-', 60));
        sb.AppendLine($"  Total packages in solution    : {audit.TotalCount}");
        sb.AppendLine($"  Skipped (test/build/internal) : {audit.SkippedCount}");
        sb.AppendLine($"  To be analysed                : {audit.AnalysedCount}");
        sb.AppendLine();

        if (audit.SkippedIds.Count > 0)
        {
            sb.AppendLine(
                $"  Skipped packages ({audit.SkippedIds.Count}) — test/build/internal, no coverage analysis needed:"
            );
            foreach (var id in audit.SkippedIds.Order(StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"    SKIP  {id}");
            sb.AppendLine();
        }
    }

    private static void AppendSummaryTable(StringBuilder sb, IReadOnlyList<SummaryRow> summaryRows)
    {
        sb.AppendLine(
            $"{"Package", -38} {"Usages", 7} {"Coverable", 10} {"Covered", 10} {"Coverage%", 10}  Status"
        );
        sb.AppendLine(new string('-', 95));

        foreach (var row in summaryRows)
            sb.AppendLine(
                $"{row.Pkg, -38} {row.Usages, 7} {row.Instr, 13} {row.Cov, 10} {row.Pct, 10}  {row.Status}"
            );

        sb.AppendLine();
    }

    private static void AppendLegend(StringBuilder sb)
    {
        sb.AppendLine("Legend:");
        sb.AppendLine(
            "  Usages       = unique (file, line) pairs where a package API identifier was found"
        );
        sb.AppendLine(
            "  Coverable    = Usages whose line appears in the Cobertura XML (executable statement)"
        );
        sb.AppendLine("  Covered          = of Coverable, how many have hits > 0 in any test");
        sb.AppendLine("  Coverage%        = Covered / Coverable");
        sb.AppendLine("  FULL_COVERED     = 100% of coverable usages are covered");
        sb.AppendLine("  HIGH_COVERED     = 80–99% of coverable usages are covered");
        sb.AppendLine("  MEDIUM_COVERED   = 50–79% of coverable usages are covered");
        sb.AppendLine("  LOW_COVERED      =  1–49% of coverable usages are covered");
        sb.AppendLine("  NOT_COVERED      = coverable usages exist, but none are covered");
        sb.AppendLine("  NOT_INSTRUMENTED = usages found, but none are coverable");
        sb.AppendLine("  NO_USAGES        = no identifier matches found in production code");
        sb.AppendLine();
        sb.AppendLine(
            "Per-usage coverage: C=covered | .=coverable but not covered | -=not coverable"
        );
        sb.AppendLine();
    }

    private static void AppendPerPackageDetail(
        StringBuilder sb,
        string repoRoot,
        IReadOnlyCollection<string> displayPackages,
        Dictionary<string, List<ApiSite>> sitesByPkg
    )
    {
        foreach (var pkgName in displayPackages)
        {
            sb.AppendLine(new string('=', 110));
            sb.AppendLine($"PACKAGE: {pkgName}");
            sb.AppendLine(new string('-', 110));

            if (!sitesByPkg.TryGetValue(pkgName, out var pkgSites) || pkgSites.Count == 0)
            {
                sb.AppendLine("  NO_USAGES — no identifier matches found in production code.");
                sb.AppendLine();
                continue;
            }

            sb.AppendLine($"{"Cov", -4} {"Line", 6}  {"Kind", -14}  {"Name", -30}  File");
            sb.AppendLine(new string('-', 100));

            foreach (
                var fileGroup in pkgSites
                    .GroupBy(s => s.File)
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            )
            {
                string relFile = fileGroup.Key.StartsWith(
                    repoRoot,
                    StringComparison.OrdinalIgnoreCase
                )
                    ? fileGroup.Key[(repoRoot.Length + 1)..]
                    : fileGroup.Key;

                foreach (var site in fileGroup.OrderBy(s => s.LineNo))
                {
                    char c =
                        site.Covered ? 'C'
                        : site.Coverable ? '.'
                        : '-';
                    sb.AppendLine(
                        $"{c}    {site.LineNo, 6}  {site.Kind, -14}  {site.MatchedName, -30}  {relFile}"
                    );
                }
            }

            sb.AppendLine();
        }
    }
}
