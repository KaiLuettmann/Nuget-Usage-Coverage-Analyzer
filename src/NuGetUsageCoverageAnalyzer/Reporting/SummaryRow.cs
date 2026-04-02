namespace NuGetUsageCoverageAnalyzer;

public record SummaryRow(string Pkg, int Usages, int Instr, int Cov, string Pct, string Status);
