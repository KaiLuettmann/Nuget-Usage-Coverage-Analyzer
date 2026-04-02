using Spectre.Console;

namespace NuGetUsageCoverageAnalyzer;

public static class AnalyzerConsoleReporter
{
    /// <summary>
    /// Wires all <see cref="Analyzer"/> domain events to the given console.
    /// </summary>
    public static Analyzer WithConsole(this Analyzer analyzer, IAnsiConsole console)
    {
        analyzer.RunStarting += args =>
        {
            console.MarkupLine($"[dim]Solution :[/] {Markup.Escape(args.SlnFile)}");
            console.MarkupLine($"[dim]Config   :[/] {Markup.Escape(args.ConfigPath)}");
            console.MarkupLine($"[dim]Output   :[/] {Markup.Escape(args.OutputPath)}");
            console.WriteLine();
        };

        analyzer.AuditCompleted += args =>
        {
            console.MarkupLine("[bold cyan][[0/3]][/] Auditing NuGet packages ...");
            console.MarkupLine($"  Direct dependencies found   : [bold]{args.TotalCount}[/]");
            console.MarkupLine($"  Skipped (test/build/internal): [dim]{args.SkippedCount}[/]");
            console.MarkupLine($"  To be analysed              : [green]{args.AnalysedCount}[/]");
            foreach (var w in args.Warnings)
                console.MarkupLine($"  [yellow]WARNING:[/] {Markup.Escape(w)}");
        };

        analyzer.CoverageLoaded += args =>
        {
            console.MarkupLine("[bold cyan][[1/3]][/] Loading Cobertura coverage XML ...");
            if (args.Warning is not null)
                console.MarkupLine($"  [yellow]WARNING:[/] {Markup.Escape(args.Warning)}");
            console.MarkupLine($"  Coverage files : [bold]{args.FileCount:N0}[/]");
        };

        analyzer.SourceFilesFound += args =>
        {
            console.MarkupLine("[bold cyan][[2/3]][/] Scanning source files ...");
            console.MarkupLine($"  Production .cs files: [bold]{args.Count:N0}[/]");
        };

        analyzer.CompilationStarting += () =>
            console.Markup("  Building Roslyn compilation ... ");

        analyzer.CompilationBuilt += args =>
        {
            console.MarkupLine("[green]OK[/]");
            console.MarkupLine($"  NuGet assemblies mapped     : [bold]{args.AssemblyCount:N0}[/]");
            console.MarkupLine(
                $"  Packages in catalogue       : [bold]{args.PackageCount:N0}[/]" +
                (args.IncludeTransitive ? " [dim](direct + transitive)[/]" : " [dim](direct only)[/]")
            );
        };

        analyzer.AssemblyMapReady += args =>
        {
            foreach (var w in args.DllWarnings)
                console.MarkupLine($"  [dim yellow]VERBOSE[/] {Markup.Escape(w)}");
            console.MarkupLine(
                "  [dim]── verbose: assembly map coverage ──────────────────────────[/]"
            );
            foreach (var pkg in args.CatalogueNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                var directAsms = args.AssemblyToPackage
                    .Where(kv => kv.Value.Equals(pkg, StringComparison.OrdinalIgnoreCase))
                    .Select(kv => kv.Key)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (directAsms.Count > 0)
                    console.MarkupLine(
                        $"  [green]\u2713[/] [dim]{Markup.Escape(pkg),-40}[/] {Markup.Escape(string.Join(", ", directAsms))}"
                    );
                else
                    console.MarkupLine(
                        $"  [yellow]~[/] [dim]{Markup.Escape(pkg),-40}[/] [dim](no direct DLL mapping \u2014 prefix fallback only)[/]"
                    );
            }
            console.MarkupLine(
                "  [dim]\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500[/]"
            );
            console.WriteLine();
        };

        analyzer.ScanProgress += args =>
            console.MarkupLine($"  [dim]... {args.Scanned:N0} / {args.Total:N0}[/]");

        analyzer.ScanCompleted += args =>
            console.MarkupLine($"  Total API usages found: [bold]{args.TotalUsages:N0}[/]");

        analyzer.UsageCountsReady += args =>
        {
            console.MarkupLine(
                "  [dim]\u2500\u2500 verbose: per-package usage counts \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500[/]"
            );
            foreach (var pkg in args.CatalogueNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                var count = args.CountByPackage.GetValueOrDefault(pkg, 0);
                var color = count > 0 ? "green" : "yellow";
                console.MarkupLine($"  [{color}]{count,6:N0}[/]  [dim]{Markup.Escape(pkg)}[/]");
            }
            console.MarkupLine(
                "  [dim]\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500[/]"
            );
            console.WriteLine();
        };

        analyzer.OutputReady += args =>
        {
            console.MarkupLine("[bold cyan][[3/3]][/] Rendering output ...");
            ConsoleReporter.PrintSummary(console, args.SummaryRows, args.OutputPath);
        };

        return analyzer;
    }
}
