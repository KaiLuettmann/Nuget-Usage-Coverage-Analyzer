using System.Diagnostics;
using System.Reflection;
using Spectre.Console;

namespace NuGetUsageCoverageAnalyzer.E2E.Tests;

public record AnalysisTestCase(
    string Name,
    string Solution,
    string Config,
    string Coverage,
    bool SkipTests = false,
    string? TestFilter = null)
{
    public override string ToString() => Name;
}

public class AnalyzerTests
{
    private static string FixtureRoot => Path.GetFullPath(
        Path.Combine(
            Path.GetDirectoryName(typeof(AnalyzerTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", "tests", "fixtures", "SampleSolution"));

    public static IEnumerable<object[]> TestCases =>
    [
        [new AnalysisTestCase(
            Name:     "FullCoverage",
            Solution: Path.Combine(FixtureRoot, "SampleSolution.sln"),
            Config:   Path.Combine(FixtureRoot, "package-analysis-config.jsonc"),
            Coverage: Path.Combine(FixtureRoot, "test-results", "coverage-report", "full", "Cobertura.xml")
        )],
        [new AnalysisTestCase(
            Name:      "NoTests",
            Solution:  Path.Combine(FixtureRoot, "SampleSolution.sln"),
            Config:    Path.Combine(FixtureRoot, "package-analysis-config.jsonc"),
            Coverage:  Path.Combine(FixtureRoot, "test-results", "coverage-report", "empty", "Cobertura.xml"),
            SkipTests: true
        )],
        [new AnalysisTestCase(
            Name:       "PartialCoverage",
            Solution:   Path.Combine(FixtureRoot, "SampleSolution.sln"),
            Config:     Path.Combine(FixtureRoot, "package-analysis-config.jsonc"),
            Coverage:   Path.Combine(FixtureRoot, "test-results", "coverage-report", "partial", "Cobertura.xml"),
            TestFilter: "FullyQualifiedName~SerializeOrder|FullyQualifiedName~DeserializeOrder|FullyQualifiedName~ParseRawOrder"
        )],
    ];

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task AnalyzesSolutionAndProducesExpectedOutput(AnalysisTestCase testCase)
    {
        var coverageDir = Path.GetDirectoryName(testCase.Coverage)!;
        Directory.CreateDirectory(coverageDir);

        if (testCase.SkipTests)
        {
            await File.WriteAllTextAsync(testCase.Coverage, """
                <?xml version="1.0" encoding="utf-8"?>
                <coverage line-rate="0" branch-rate="0" version="1.9" timestamp="0"
                          lines-covered="0" lines-valid="0" branches-covered="0" branches-valid="0">
                  <sources />
                  <packages />
                </coverage>
                """);
        }
        else
        {
            var filterArg = testCase.TestFilter is not null
                ? $"--filter \"{testCase.TestFilter}\" "
                : "";
            var testRun = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"test {filterArg}" +
                                "/p:CollectCoverage=true " +
                                "/p:CoverletOutputFormat=cobertura " +
                                $"\"/p:CoverletOutput={testCase.Coverage}\"",
                    WorkingDirectory = Path.GetDirectoryName(testCase.Solution)!,
                    UseShellExecute = false,
                }
            };
            testRun.Start();
            await testRun.WaitForExitAsync();
        }

        var outputFile = Path.Combine(Path.GetTempPath(), $"nuget-analysis-{testCase}.txt");
        try
        {
            var writer = new StringWriter();
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(writer),
                Interactive = InteractionSupport.No,
                Ansi = AnsiSupport.No,
            });

            new Analyzer()
                .WithConsole(console)
                .Run(new AnalyzerOptions(
                    SlnFile: testCase.Solution,
                    ConfigPath: testCase.Config,
                    OutputPath: outputFile,
                    CoverageXml: testCase.Coverage,
                    Verbose: false,
                    IncludeTransitive: false
                ));

            var stdout = writer.ToString();
            await Verify(stdout).UseParameters(testCase);
        }
        finally
        {
            if (File.Exists(outputFile))
                File.Delete(outputFile);
        }
    }
}
