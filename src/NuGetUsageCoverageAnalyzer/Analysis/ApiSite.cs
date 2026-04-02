namespace NuGetUsageCoverageAnalyzer;

record ApiSite(
    string File,
    int LineNo,
    string MatchedName,
    string Kind,
    string PackageName,
    bool Covered,
    bool Coverable
);
