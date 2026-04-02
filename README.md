# NuGet Usage Coverage Analyzer

Analysiert, welche NuGet-Pakete einer .NET-Solution tatsächlich in Test-Coverage-Daten
erfasst werden. Das Tool liest eine Cobertura-XML-Coverage-Datei und korreliert sie mit
den tatsächlichen API-Verwendungsstellen im Produktionscode via Roslyn-Semantic-Analyse.

## Voraussetzungen

- .NET 8 SDK
- Die zu analysierende Solution muss vollständig wiederhergestellt sein (`dotnet restore`)
- Eine Cobertura-XML-Coverage-Datei (z. B. erzeugt durch `dotnet test --collect:"XPlat Code Coverage"`)

## Workflow

Die Analyse läuft in vier Phasen:

### Phase 0 — Package Audit

Das Tool liest alle `.csproj`-Dateien aus der `.sln`, wertet deren `obj/project.assets.json`
aus und ermittelt alle **direkten** NuGet-Abhängigkeiten der Solution. Pakete, die per
Konfiguration als Test-, Build- oder interne Pakete markiert sind, werden herausgefiltert
und erscheinen im Report als `SKIP`.

> **Hinweis:** Ohne vorheriges `dotnet restore` sind keine `project.assets.json`-Dateien
> vorhanden und die Analyse liefert keine Ergebnisse.

### Phase 1 — Coverage laden

Die Cobertura-XML-Datei wird eingelesen. Für jede Quelldatei wird festgehalten, welche
Zeilen instrumentiert (coverable) und welche davon durch Tests abgedeckt (covered) sind.

### Phase 2 — Quelldateien scannen

1. Alle `.cs`-Dateien des Repos werden gesammelt (Testdateien werden per Regex aus
   `sourceExcludePattern` ausgeschlossen).
2. Eine Roslyn-Compilation wird aufgebaut, wobei die NuGet-DLLs als Metadatenreferenzen
   eingebunden werden.
3. Jeder Identifier im Syntaxbaum wird semantisch aufgelöst. Das enthaltende Assembly wird
   über zwei Strategien einem Katalogeintrag (NuGet-Paket) zugeordnet:
   - **Strategie 1 – Direkte Zuordnung:** Assembly ist in der `assemblyToPackage`-Map
     aus `project.assets.json` enthalten.
   - **Strategie 2 – Präfix-Fallback:** Assembly-Name beginnt mit einem Paketnamen aus dem
     Katalog (z. B. `Microsoft.AspNetCore.Mvc.Core` → `Microsoft.AspNetCore.Mvc`). Der
     längste Treffer gewinnt.
4. Jede Verwendungsstelle (Datei + Zeile) wird mit den Coverage-Daten aus Phase 1 abgeglichen.

Erkannte Verwendungsarten (`Kind`):

| Kind            | Beispiel                        |
|-----------------|---------------------------------|
| `ObjectCreation`| `new MyService()`               |
| `Attribute`     | `[Authorize]`                   |
| `MethodCall`    | `logger.LogInformation(...)`    |
| `TypeRef`       | Typ als Parameter, Rückgabetyp  |

### Phase 3 — Report ausgeben

Der Report wird als UTF-8-Textdatei geschrieben und enthält:

- **Package Audit:** Übersicht über alle Pakete (analysiert / übersprungen)
- **Summary-Tabelle:** Pro Paket Anzahl Usages, Coverable, Covered, Coverage% und Status
- **Per-Package-Detail:** Alle Verwendungsstellen mit Datei, Zeile, Art und Coverage-Symbol

Coverage-Status-Abstufungen:

| Status              | Bedeutung                                        |
|---------------------|--------------------------------------------------|
| `FULL_COVERED`      | 100 % der coverable Usages sind abgedeckt        |
| `HIGH_COVERED`      | 80–99 % abgedeckt                                |
| `MEDIUM_COVERED`    | 50–79 % abgedeckt                                |
| `LOW_COVERED`       | 1–49 % abgedeckt                                 |
| `NOT_COVERED`       | Coverable Usages vorhanden, aber keine abgedeckt |
| `NOT_INSTRUMENTED`  | Usages gefunden, aber keine coverable            |
| `NO_USAGES`         | Keine Identifier-Treffer im Produktionscode      |

Symbole im Detail-Report: `C` = covered · `.` = coverable, nicht abgedeckt · `-` = nicht coverable

## Verwendung

```
dotnet run -- --solution <Pfad\zur.sln> --config <Pfad\zur\config.jsonc> [Optionen]
```

### Pflichtargumente

| Option               | Kurz | Beschreibung                             |
|----------------------|------|------------------------------------------|
| `--solution <Datei>` | `-s` | Pfad zur `.sln`-Datei                    |
| `--config <Datei>`   | `-c` | Pfad zur `package-analysis-config.jsonc` |

### Optionale Argumente

| Option                 | Kurz | Standard                                                      | Beschreibung                                          |
|------------------------|------|---------------------------------------------------------------|-------------------------------------------------------|
| `--coverage <Datei>`   | `-x` | `<Repo>\test-results\coverage-report\all\Cobertura.xml`       | Pfad zur Cobertura-XML-Coverage-Datei                 |
| `--output <Datei>`     | `-o` | `<Repo>\NuGetUsageCoverageAnalyzer\nuget-coverage-output.txt` | Ausgabedatei                                          |
| `--verbose`            | `-v` | `false`                                                       | Detaildiagnostik: DLL-Auflösung, Assembly-Map, Counts |
| `--include-transitive` | —    | `false`                                                       | Transitive (indirekte) Abhängigkeiten einbeziehen     |

### Beispiel

```powershell
dotnet run `
    -- --solution D:\dev\MyProject\MyProject.sln `
       --config D:\dev\MyProject\NuGetUsageCoverageAnalyzer\package-analysis-config.jsonc `
       --coverage D:\dev\MyProject\test-results\coverage-report\all\Cobertura.xml
```

Ausgabe auf der Konsole:

```
Solution : D:\dev\MyProject\MyProject.sln
Config   : D:\dev\MyProject\NuGetUsageCoverageAnalyzer\package-analysis-config.jsonc
Output   : D:\dev\MyProject\NuGetUsageCoverageAnalyzer\nuget-coverage-output.txt
```

## Konfigurationsdatei

Die `package-analysis-config.jsonc` steuert, welche Pakete ausgeschlossen werden und
welche Quelldateien als Produktionscode gelten.

```jsonc
{
  // Pakete, die exakt übereinstimmen, werden übersprungen
  "skipExact": [
    "xunit",
    "FluentAssertions",
    "NSubstitute",
    "Microsoft.NET.Test.Sdk"
    // ...
  ],

  // Pakete, deren Name mit einem dieser Präfixe beginnt, werden übersprungen
  "skipPrefixes": [
    "runtime."   // RID-spezifische Runtime-Packs
  ],

  // Interne Pakete, die ebenfalls übersprungen werden (eigene Bibliotheken o. Ä.)
  "skipInternalPrefixes": [
    "MyCompany.",
    "InternalLib."
  ],

  // Regex, der auf jeden .cs-Dateipfad angewendet wird.
  // Dateien, die matchen, werden als Nicht-Produktionscode ausgeschlossen.
  "sourceExcludePattern": "\\\\(Test|AcceptanceTests)\\\\|\\.Test[\\\\.]"
}
```

### Skip-Kategorien

- **`skipExact`** — Testframeworks, Analyzer, Build-Tools, BCL-Meta-Pakete
- **`skipPrefixes`** — RID-spezifische Runtime-Packs (`runtime.*`)
- **`skipInternalPrefixes`** — Eigene/interne Bibliotheken
- **`sourceExcludePattern`** — Regex zum Ausschließen von Testdateien aus dem Quelldateiscan

## Tipps zur Fehlersuche

**`NO_USAGES` für ein Paket, das definitiv verwendet wird**

1. `--verbose` aktivieren — zeigt die Assembly-Map und pro Paket die Verwendungsanzahl.
2. Prüfen, ob das Paket in `skipExact` / `skipPrefixes` / `skipInternalPrefixes` steht.
3. Sicherstellen, dass `dotnet restore` ausgeführt wurde und `project.assets.json` aktuell ist.
4. Bei Umbrella-Paketen (z. B. `Swashbuckle.AspNetCore`): Der Präfix-Fallback greift nur,
   wenn der Paketname ein Präfix des Assembly-Namens ist.

**Coverage-Datei nicht gefunden**

Das Tool läuft auch ohne Coverage-Datei weiter — alle Usages werden dann als
`NOT_INSTRUMENTED` gewertet. Coverage-Erzeugung mit ReportGenerator:

```powershell
dotnet test --collect:"XPlat Code Coverage" --results-directory test-results

dotnet tool run reportgenerator `
    -reports:"test-results\**\coverage.cobertura.xml" `
    -targetdir:"test-results\coverage-report\all" `
    -reporttypes:Cobertura
```
