namespace NuGetUsageCoverageAnalyzer;

record FileCoverage(HashSet<int> Covered, HashSet<int> AllInstrumented);
