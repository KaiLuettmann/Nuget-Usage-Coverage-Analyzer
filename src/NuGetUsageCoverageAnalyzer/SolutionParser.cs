using System.Text.RegularExpressions;

namespace NuGetUsageCoverageAnalyzer;

internal static class SolutionParser
{
    private static readonly Regex CsprojRegex = new(@"""([^""]+\.csproj)""", RegexOptions.IgnoreCase);

    public static List<string> GetProjectPaths(string slnFile)
    {
        var slnDir = Path.GetDirectoryName(slnFile)!;
        var paths = new List<string>();
        foreach (var line in File.ReadLines(slnFile))
        {
            if (!line.TrimStart().StartsWith("Project(", StringComparison.Ordinal))
                continue;
            var match = CsprojRegex.Match(line);
            if (match.Success)
                paths.Add(Path.GetFullPath(Path.Combine(slnDir, match.Groups[1].Value)));
        }
        return paths;
    }
}
