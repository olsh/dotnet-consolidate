# Repository Guidelines

This file provides guidance to coding agents when working with code in this repository.

## What this is

`dotnet-consolidate` is a .NET global tool (`dotnet consolidate`) that parses a solution's projects and reports NuGet packages referenced at more than one version. Non-consolidated packages (or a solution that failed to parse) set `Environment.ExitCode = 1`.

## Build & test

The build is [NUKE](https://nuke.build/). The build project is `build/_build.csproj` (targets defined in `build/Build.cs`); the bootstrap scripts are `build.cmd` / `build.ps1` / `build.sh` at the root.

The solution is `DotNet.Consolidate.slnx` **at the repo root** (not under `src/`). NUKE reads its path from `.nuke/parameters.json`; `[Solution(GenerateProjects = true)]` generates `Solution.DotNet_Consolidate` / `Solution.DotNet_Consolidate_Tests` from it. Only Nuke.Common 10+ parses `.slnx`, which is why `build/_build.csproj` targets `net10.0` and the build needs a .NET 10 SDK (`dotnet build` on the solution itself needs 9.0.200+).

```powershell
.\build.cmd                     # default: Compile, Test, NugetPack
.\build.cmd test                # targets are kebab-cased on the CLI
.\build.cmd nuget-pack --configuration Release
```

Targets: `compile`, `test`, `nuget-pack`. `Configuration` defaults to `Debug` locally, `Release` on CI. `nuget-pack` outputs to `artifacts/` and runs with `--no-build --no-restore`, so it needs `compile` to have run in the same configuration.

Plain SDK commands also work and are faster for inner-loop work:

```powershell
dotnet build DotNet.Consolidate.slnx
dotnet test src/DotNet.Consolidate.Tests/DotNet.Consolidate.Tests.csproj
dotnet test src/DotNet.Consolidate.Tests/DotNet.Consolidate.Tests.csproj --filter "FullyQualifiedName~PackagesAnalyzerTests"
dotnet test src/DotNet.Consolidate.Tests/DotNet.Consolidate.Tests.csproj --filter "DisplayName~Versions_with_trailing_zeroes_are_the_same"
```

CI is GitHub Actions (`.github/workflows/build.yml`), on `ubuntu-latest` for pushes and pull requests to `master`: `./build.sh test --configuration Release` then `./build.sh nuget-pack --configuration Release`, uploading `artifacts/*.nupkg`. The job installs both the .NET 8 (tests are `net8.0`) and .NET 10 (NUKE and `.slnx`) SDKs.

SonarCloud runs as server-side Automatic Analysis via the GitHub app — there is no scanner, token, or Java in the build. Publishing to NuGet.org is manual.

`.github/workflows/dependabot-auto-merge.yml` approves and squash-auto-merges patch and minor Dependabot PRs.

## Constraints that break the build if ignored

- `TreatWarningsAsErrors` is on for `src/DotNet.Consolidate`, and StyleCop.Analyzers runs on both projects. `.editorconfig` disables a specific set of SA rules (SA1101, SA1200, SA1309, SA1413, SA1600, SA1602, SA1633, SA0001) — everything else is enforced, including `dotnet_separate_import_directive_groups` (blank line between `System.*`, third-party, and project using groups).
- `Nullable` is enabled in the main project. Nullability is expressed deliberately (e.g. `SolutionInfo.Solution` is nullable to signal a parse failure).
- The tool multi-targets `net6.0;net7.0;net8.0`; the test project is `net8.0` only.
- Bumping the released version means editing `<VersionPrefix>` in `src/DotNet.Consolidate/DotNet.Consolidate.csproj`; it is what the packed `.nupkg` is named after.

## Architecture

Everything is instantiated by hand in `Program.cs` — no DI container. The pipeline is:

1. **`Options`** (`Models/Options.cs`) — CommandLineParser attributes. Adding a CLI flag means adding a constructor parameter here; the constructor is called positionally by every test, so new options ripple through the test files.
2. **`SolutionInfoProvider`** — parses the `.sln` via `Onion.SolutionParser`, skips solution-folder entries (`ProjectTypeGuids.SolutionFolder`), and for each project reads `packages.config` if present, otherwise the project file. Parse failures are logged and swallowed per-project so one bad project doesn't abort the run; the discrepancy surfaces as `SolutionInfo.IsParsedWithoutIssues == false` (project count mismatch).
3. **`ProjectParser`** — XML parsing only, no MSBuild evaluation. It matches `PackageReference` by `LocalName` (namespace-agnostic) and accepts the version as either an attribute or a child element. It does **not** resolve MSBuild properties, `Import`s, or `Central Package Management`.
4. **`ApplyInheritedPackages`** (in `SolutionInfoProvider`) — `Directory.Build.props` files are discovered by recursive directory walk from the solution folder, then each project is matched to the *longest matching directory prefix* (nearest ancestor). Those packages are appended to the project as `NuGetPackageReferenceType.Inherited`. Chained `Directory.Build.props` (via `Import`) is explicitly unsupported.
5. **`PackagesAnalyzer`** — groups by package ID, keeps groups with more than one distinct version, then applies the `-p` / `-e` / `--excludedVersionsRegex` filters in that order.
6. **`Logger`** — the only output path; `Console.WriteLine` throughout.

### Version comparison

`Models/Version` is a custom type, not `System.Version` (note the `using Version = DotNet.Consolidate.Models.Version;` aliases). Equality compares `NormalizedValue`, which is `OriginalValue.Trim('0', '.')` — so `1.0.1` and `1.0.1.0` are equal (mixed `packages.config` / SDK-style solutions write the same version differently), while `OriginalValue` is what's printed and what `--excludedVersionsRegex` matches against. `CompareTo` is an ordinal string compare on `OriginalValue`, used only for output ordering.

### Path handling

Solution files store project paths with `\`. `PathUtils.EnsureSystemSeparator` normalizes them; use it for any path read out of a `.sln` so the tool keeps working on Linux/macOS.

## Tests

xUnit, in `src/DotNet.Consolidate.Tests`. Test data comes in two flavors and the distinction matters when adding fixtures:

- **Embedded resources** (`TestData/*.csproj`, `packages.config`, `Directory.build.props`) — read via `FileHelper.ReadResource(name)`, which prefixes `DotNet.Consolidate.Tests.TestData.`. Used for content-level parser tests.
- **Copied to output** (`TestData/TestSolution/**`) — a real on-disk solution tree used by `SolutionParserTests` for end-to-end solution/`Directory.Build.props` resolution. New files here need an explicit `<None ... CopyToOutputDirectory="PreserveNewest">` entry in the test csproj; nothing is globbed.

Test method names are snake_case sentences (`Versions_with_trailing_zeroes_are_the_same`).

## Manual verification of the packaged tool

```powershell
cd src/DotNet.Consolidate
dotnet pack
cd <some other solution folder>
dotnet tool install dotnet-consolidate --local --add-source <full path to bin/Release>
dotnet consolidate -s YourSolution.sln
```
