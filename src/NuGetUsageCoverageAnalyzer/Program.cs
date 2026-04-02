using System.CommandLine;
using NuGetUsageCoverageAnalyzer;
using Spectre.Console;

// --- CLI ---
var solutionOption = new Option<FileInfo>("--solution")
{
    Description = "Path to the .sln file (repo root is derived from its directory)",
    Required = true,
};
solutionOption.Aliases.Add("-s");

var configOption = new Option<FileInfo>("--config")
{
    Description = "Path to the package-analysis-config.jsonc",
    Required = true,
};
configOption.Aliases.Add("-c");

var outputOption = new Option<FileInfo?>("--output")
{
    Description =
        "Output file path (default: <repo>/NuGetUsageCoverageAnalyzer/nuget-coverage-output.txt)",
};
outputOption.Aliases.Add("-o");

var coverageOption = new Option<FileInfo?>("--coverage")
{
    Description =
        "Path to the Cobertura XML coverage file (default: <repo>/test-results/coverage-report/all/Cobertura.xml)",
};
coverageOption.Aliases.Add("-x");

var verboseOption = new Option<bool>("--verbose")
{
    Description =
        "Print detailed diagnostics: DLL resolution failures, assembly→package map, per-package usage counts",
};
verboseOption.Aliases.Add("-v");

var includeTransitiveOption = new Option<bool>("--include-transitive")
{
    Description =
        "Include transitive (indirect) dependencies in the analysis (default: direct dependencies only)",
};

var rootCommand = new RootCommand("NuGet package API coverage analysis");
rootCommand.Options.Add(solutionOption);
rootCommand.Options.Add(configOption);
rootCommand.Options.Add(outputOption);
rootCommand.Options.Add(coverageOption);
rootCommand.Options.Add(verboseOption);
rootCommand.Options.Add(includeTransitiveOption);

rootCommand.SetAction(parseResult =>
{
    var solution = parseResult.GetValue(solutionOption)!;
    var config = parseResult.GetValue(configOption)!;
    var output = parseResult.GetValue(outputOption);
    var coverage = parseResult.GetValue(coverageOption)!;

    new Analyzer()
        .WithConsole(AnsiConsole.Console)
        .Run(new AnalyzerOptions(
            SlnFile: solution.FullName,
            ConfigPath: config.FullName,
            OutputPath: output?.FullName,
            CoverageXml: coverage.FullName,
            Verbose: parseResult.GetValue(verboseOption),
            IncludeTransitive: parseResult.GetValue(includeTransitiveOption)
        ));
    return 0;
});

return rootCommand.Parse(args).Invoke();
