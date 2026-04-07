# Contributing to NuGet Usage Coverage Analyzer

Thanks for your interest in contributing! This document explains how to get started.

## Prerequisites

- .NET 10 SDK
- Git

## Getting Started

1. **Fork** the repository on GitHub.
2. **Clone** your fork locally:
   ```bash
   git clone https://github.com/<your-username>/Nuget-Usage-Coverage-Analyzer.git
   cd Nuget-Usage-Coverage-Analyzer
   ```
3. **Create a branch** for your change:
   ```bash
   git checkout -b feature/my-change
   ```
4. **Restore** dependencies:
   ```bash
   dotnet restore
   ```
5. **Make your changes** and ensure the project builds:
   ```bash
   dotnet build
   ```
6. **Run the tests**:
   ```bash
   dotnet test
   ```
7. **Commit** your changes with a clear message:
   ```bash
   git commit -m "feat: add my change"
   ```
8. **Push** your branch and open a **Pull Request** against `main`.

## Branch Naming

Use a descriptive prefix:

| Prefix      | Purpose            |
|-------------|--------------------|
| `feature/`  | New functionality  |
| `fix/`      | Bug fix            |
| `docs/`     | Documentation only |
| `refactor/` | Code restructuring |

## Commit Messages

Follow [Conventional Commits](https://www.conventionalcommits.org/):

- `feat: add new analysis phase`
- `fix: handle missing coverage file`
- `docs: update README examples`

## Code Style

- Follow existing code conventions in the project.
- Keep changes focused — one concern per PR.

## Reporting Issues

Use the [issue templates](https://github.com/KaiLuettmann/Nuget-Usage-Coverage-Analyzer/issues/new/choose) to report bugs or request features.

## License

By contributing you agree that your contributions will be licensed under the [MIT License](LICENSE).
