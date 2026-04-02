using Spectre.Console;

namespace NuGetUsageCoverageAnalyzer;

static class ConsoleReporter
{
    public static void PrintSummary(
        IAnsiConsole console, IReadOnlyList<SummaryRow> summaryRows, string outputPath)
    {
        console.WriteLine();
        console.Write(new Rule("[bold cyan]SUMMARY[/]").RuleStyle("cyan"));
        console.WriteLine();

        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.AddColumn(new TableColumn("[bold]Package[/]"));
        table.AddColumn(new TableColumn("[bold]Usages[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Coverable[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Covered[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Coverage%[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Status[/]"));

        foreach (var row in summaryRows)
        {
            table.AddRow(
                Markup.Escape(row.Pkg),
                row.Usages == 0 ? "[dim]-[/]" : row.Usages.ToString(),
                row.Instr == 0 ? "[dim]-[/]" : row.Instr.ToString(),
                row.Cov == 0 ? "[dim]-[/]" : row.Cov.ToString(),
                ColorPct(row.Pct, row.Instr, row.Cov),
                ColorStatus(row.Status)
            );
        }

        console.Write(table);

        console.WriteLine();
        console.MarkupLine($"Output written to: [underline]{Markup.Escape(outputPath)}[/]");
    }

    private static string ColorPct(string pct, int instr, int cov)
    {
        if (pct == "-" || instr == 0)
            return "[dim]-[/]";
        int pctVal = cov * 100 / instr;
        return pctVal switch
        {
            0 => $"[red]{Markup.Escape(pct)}[/]",
            < 50 => $"[darkorange3]{Markup.Escape(pct)}[/]",
            < 80 => $"[yellow]{Markup.Escape(pct)}[/]",
            _ => $"[green]{Markup.Escape(pct)}[/]",
        };
    }

    private static string ColorStatus(string status) =>
        status switch
        {
            "FULL_COVERED" => $"[bold green]{Markup.Escape(status)}[/]",
            "HIGH_COVERED" => $"[green]{Markup.Escape(status)}[/]",
            "MEDIUM_COVERED" => $"[yellow]{Markup.Escape(status)}[/]",
            "LOW_COVERED" => $"[darkorange3]{Markup.Escape(status)}[/]",
            "NOT_COVERED" => $"[red]{Markup.Escape(status)}[/]",
            "NOT_INSTRUMENTED" => $"[yellow]{Markup.Escape(status)}[/]",
            _ => $"[dim]{Markup.Escape(status)}[/]",
        };
}
