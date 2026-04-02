namespace NuGetUsageCoverageAnalyzer;

static class CoberturaParser
{
    public static Dictionary<string, FileCoverage> Load(string xmlPath)
    {
        var result = new Dictionary<string, FileCoverage>(StringComparer.OrdinalIgnoreCase);
        var doc = System.Xml.Linq.XDocument.Load(xmlPath);

        var sources = doc.Descendants("sources")
            .SelectMany(s => s.Elements("source"))
            .Select(s => s.Value.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        foreach (var cls in doc.Descendants("class"))
        {
            var filename = cls.Attribute("filename")?.Value;
            if (string.IsNullOrEmpty(filename))
                continue;
            filename = filename.Replace('/', Path.DirectorySeparatorChar);
            if (!Path.IsPathRooted(filename))
            {
                filename = sources
                    .Select(s => Path.Combine(s, filename))
                    .FirstOrDefault(File.Exists)
                    ?? (sources.Count > 0 ? Path.Combine(sources[0], filename) : filename);
            }
            if (!Path.IsPathRooted(filename))
                continue;

            if (!result.TryGetValue(filename, out var fc))
            {
                fc = new FileCoverage(new HashSet<int>(), new HashSet<int>());
                result[filename] = fc;
            }

            foreach (var line in cls.Descendants("line"))
            {
                if (!int.TryParse(line.Attribute("number")?.Value, out int lineNo))
                    continue;
                fc.AllInstrumented.Add(lineNo);
                if (int.TryParse(line.Attribute("hits")?.Value, out int hits) && hits > 0)
                    fc.Covered.Add(lineNo);
            }
        }
        return result;
    }
}
