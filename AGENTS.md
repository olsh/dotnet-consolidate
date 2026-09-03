# Repository Guidelines

This file provides guidance to coding agents when working with code in this repository.

## What this is

`dotnet-consolidate` is a .NET global tool (`dotnet consolidate`) that parses a solution's projects and reports NuGet packages referenced at more than one version. Non-consolidated packages, a `-p` package ID that no project references, a solution that failed to parse, or a command line the parser rejected set `Environment.ExitCode = 1`.

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

- `TreatWarningsAsErrors` is on for `src/DotNet.Consolidate`, so any compiler warning there fails the build. There is no analyzer package: StyleCop.Analyzers was removed, and `.editorconfig` now only carries formatting preferences (`dotnet_sort_system_directives_first`, `dotnet_separate_import_directive_groups` — a blank line between `System.*`, third-party and project using groups — plus indentation and charset). Those are IDE conventions, not build failures; follow them to match the surrounding code.
- `Nullable` is enabled in the main project (but **not** in the test project, so `?` annotations in tests warn). Nullability is expressed deliberately (e.g. `SolutionInfo.Solution` is nullable to signal a parse failure).
- The tool targets `net8.0` (with `<RollForward>Major</RollForward>` so it also runs on newer runtimes) because `Microsoft.VisualStudio.SolutionPersistence` ships only `net472` and `net8.0` assemblies; the test project is `net8.0` too.
- Bumping the released version means editing `<VersionPrefix>` in `src/DotNet.Consolidate/DotNet.Consolidate.csproj`; it is what the packed `.nupkg` is named after.

## Architecture

Everything is instantiated by hand in `Program.cs` — no DI container. The pipeline is:

1. **`Options`** (`Models/Options.cs`) — CommandLineParser attributes on `init` properties, filled one at a time by reflection, so adding a CLI flag is a local change and callers use an object initializer for just the options they care about. Give a new option a C# initializer matching its `Default`, so an `Options` built in code behaves like one built from a command line; `OptionsTests` asserts the two agree, and is also the only thing covering the binding itself. The parser is built by `Services/CommandLineParserFactory` rather than `Parser.Default` — see the remarks there before changing it. `--property` (`GlobalProperties`) carries `Name=Value` pairs that `Program.cs` parses into the dictionary handed to `ProjectParser`.
   - `-d` / `-o` are declared `bool?`, not `bool`, and that is load-bearing: CommandLineParser makes a `bool` property a switch that can never be given a value, so `-d false` would leave the default standing. Nullable makes them scalars, which is what lets them be turned off; `Default = true` still applies when the option is omitted, so the value is never actually `null` (`Program.cs` still reads it as `?? true`). The cost is that a bare `-d` now takes the next token as its value, so it only survives at the end of the command line or in front of a sequence option — `-d -f json` is a parse error. `OptionsTests` pins all of this.
   - A rejected command line is `Environment.ExitCode = 1`; without it a mistyped option passes a build having analyzed nothing. `HandleParseError` only sets the code — what gets reported is `Services/CommandLineErrorReporter`, which lives outside `Program` so `CommandLineErrorReporterTests` can reach it (`Program` is `internal`, its members `private`, and there is no `InternalsVisibleTo`).
     - `HelpRequestedError` / `VersionRequestedError` are dropped first, and if nothing is left it isn't a failure: `--help` and `--version` arrive through `WithNotParsed` too and must still exit 0. Dropping rather than testing them with `All` is also what keeps `SentenceBuilder` — which has no sentence for either and *throws* — away from them.
     - In the text format nothing is written at all: the parser's `HelpWriter` has already put a readable block on stderr. `-f json` is the exception, and the point of the class — stdout owes the caller one parseable document however the run ended, so the errors are rendered with `SentenceBuilder.FormatError` (not `error.Tag`, which doesn't name the option) into the `warnings` of a document with an empty `solutions`, the same shape the argument errors in `Consolidate` emit. The stderr block stays; a parse failure is exiting 1 anyway, so the "never write to stderr in JSON mode" rule has nothing left to protect.
     - The format can't be read from `Options` — the parse it would have come from is the one that failed. `DetectFormat` re-parses the raw args against a private type declaring nothing but `--format`, with `IgnoreUnknownArguments`, so the very failures being reported (an unknown option, `-p` given twice) are invisible to it; anything it can't parse falls back to `Text`.
2. **`SolutionInfoProvider`** — parses `.sln` and `.slnx` via `Microsoft.VisualStudio.SolutionPersistence` (`SolutionSerializers.GetSerializerByMoniker` picks the serializer by extension; `OpenAsync` is blocked on with `GetAwaiter().GetResult()` since the library offers no sync overload). `SolutionModel.SolutionProjects` already excludes solution folders — they live in `SolutionFolders` — so there is no type-GUID filtering. For each project it reads `packages.config` if present, otherwise the project file. Parse failures are logged and swallowed per-project so one bad project doesn't abort the run; the discrepancy surfaces as `SolutionInfo.IsParsedWithoutIssues == false` (project count mismatch).
3. **`ProjectParser`** — a thin wrapper over **`ProjectEvaluator`**, which does the XML walking. Still no MSBuild engine: `ProjectEvaluator` collects `<PropertyGroup>` values in document order, then walks the top-level `<ItemGroup>`s, evaluating the `Condition` on the group, the property and the `PackageReference` through **`ConditionEvaluator`** (a hand-written tokenizer + recursive-descent parser over `==`/`!=`/numeric comparisons/`And`/`Or`/`!`/parens/`Exists`/`HasTrailingSlash`). **`MSBuildProperties`** holds the values, expands `$(Name)`, and keeps global (`--property`) and reserved properties read-only so a `<PropertyGroup>` can't overwrite them. `PackageReference` is still matched by `LocalName` (namespace-agnostic), with the version as an attribute or a child element.
   - A condition outside the supported subset (a property function, `@(...)`, `%(...)`) is **unevaluatable**, which logs once per condition per file and **keeps** the guarded items. Never make an unparseable condition drop packages.
   - `<TargetFrameworks>` fans out: the project is evaluated once per TFM and the results are unioned (deduped on id + normalized version), so TFM-conditional references aren't lost. `ConditionEvaluator` deliberately does **not** short-circuit `And`/`Or` — that can only turn a condition unevaluatable, never flip a result.
   - `Include`/`Version` go through `TryExpand`, which falls back to the **literal text** when a property is unknown, rather than substituting an empty string and silently discarding the reference.
   - Still not supported: `Import`s, `Choose`/`When`, item groups inside a `Target`, and Central Package Management.
4. **`ApplyInheritedPackages`** (in `SolutionInfoProvider`) — `Directory.Build.props` files are discovered by recursive directory walk from the solution folder, then each project is matched to the *longest* directory that contains it (its nearest ancestor). Containment goes through `PathUtils.IsSameOrUnderDirectory`, never a bare `StartsWith`: the match has to end on a directory boundary, or a props file in `src/Project` also claims `src/ProjectB` — and since the candidates are ordered longest-first, that sibling would beat the correct ancestor and the project would silently inherit the wrong versions. Those packages are appended to the project as `NuGetPackageReferenceType.Inherited`. Chained `Directory.Build.props` (via `Import`) is explicitly unsupported.
5. **`PackagesAnalyzer`** — groups by package ID, keeps groups with more than one distinct version, then applies the `-p` / `-e` / `--excludedVersionsRegex` filters in that order.
   - NuGet package IDs are **case-insensitive**, so every comparison goes through the single `PackageIdComparer` (`StringComparer.OrdinalIgnoreCase`) — the grouping dictionary, both ID filters, and `FindPackageIdsNotInSolution`. The reported ID is the casing of the first project that references the package. `FindPackageIdsNotInSolution` (which backs the "not found in the solution projects" report and its exit code) lives here rather than in `Program` precisely so it can't drift from the filters: an ID `-p` matches must never also be reported as missing.
6. **Output** — two separate paths, deliberately. **`ILogger`** reports what happened along the way: `Message` for things the user needs to know (a file that wouldn't parse, an unusable argument), `Progress` for the purely cosmetic `Analyzing packages in …` line. **`IOutputWriter`** renders the results, and `Program` hands it a `SolutionAnalysisResult` per solution — the writers depend on neither `SolutionInfo` nor `Options`, which is what lets them be tested without parsing anything. `OutputWriterFactory.Create` picks the implementation from `--format`.
   - `TextOutputWriter` streams each solution as it is analyzed; its `Flush` does nothing. `JsonOutputWriter` buffers and emits one document in `Flush`, because several solutions have to end up in the same document. `Flush` is also called on the early argument-error returns, and by `CommandLineErrorReporter` when the command line didn't parse at all, so `-f json` always produces a parseable document — an empty stdout is as unusable to a consumer as a plain-text one.
   - **Once the command line has parsed, `-f json` writes nothing to stderr**, and it isn't allowed to interleave anything with the document either: `Program` swaps in `CollectingLogger`, which drops `Progress` and collects `Message` into the document's `warnings`. Redirecting messages to stderr was tried and rejected — some CI systems fail a build on any stderr output. The exception, and the only one, is the parser's own report of a command line it rejected, which `HelpWriter` puts there before any of this runs; there is nothing left to protect by then, since that run is exiting 1 whatever the CI system makes of the stream.
   - The JSON shape lives in `Models/JsonReport.cs` as its own DTOs rather than being serialized off the domain models, so refactoring internals can't silently break whoever scripts against the output. The text report is covered by `TextOutputWriterTests`; changing what it prints should be deliberate and reflected there.

### Version comparison

`Models/Version` is a custom type, not `System.Version` (note the `using Version = DotNet.Consolidate.Models.Version;` aliases). Equality compares `NormalizedValue`, which is `OriginalValue.Trim('0', '.')` — so `1.0.1` and `1.0.1.0` are equal (mixed `packages.config` / SDK-style solutions write the same version differently), while `OriginalValue` is what's printed and what `--excludedVersionsRegex` matches against. `CompareTo` is an ordinal string compare on `OriginalValue`, used only for output ordering.

### Path handling

Solution files store project paths with `\` (`.sln`) or `/` (`.slnx`). `PathUtils.EnsureSystemSeparator` normalizes either one; use it for any path read out of a solution file so the tool keeps working on Linux/macOS.

`PathUtils.IsSameOrUnderDirectory` is the only place that asks whether one directory contains another, and it takes both paths already system-separated. It compares **ordinally** — `StartsWith(string)` defaults to the *current culture*, which has no business deciding this — and **case-insensitively on every platform**, so a casing difference between the path recorded in the solution file and the on-disk directory name doesn't silently drop a project's inherited packages. That matches the case-insensitive `Directory.Build.props` file lookup in `TryGetDirectoryBuildPropsInfo` and `PackagesAnalyzer.PackageIdComparer`; the accepted cost is that two sibling directories differing only in case can't be told apart on a case-sensitive file system.

## Tests

xUnit, in `src/DotNet.Consolidate.Tests`. Test data comes in two flavors and the distinction matters when adding fixtures:

- **Embedded resources** (`TestData/*.csproj`, `packages.config`, `Directory.build.props`) — read via `FileHelper.ReadResource(name)`, which prefixes `DotNet.Consolidate.Tests.TestData.`. Used for content-level parser tests.
- **Copied to output** (`TestData/TestSolution/**`) — a real on-disk solution tree used by `SolutionParserTests` for end-to-end solution/`Directory.Build.props` resolution. New files here need an explicit `<None ... CopyToOutputDirectory="PreserveNewest">` entry in the test csproj; nothing is globbed.

`TestData/**` is excluded from `Compile` in the test csproj. If a tool (Rider, an IDE restore) ever builds those sample projects, their generated `obj/**/*.cs` would otherwise be swept into the test assembly by the default glob and fail the build with `CS0579: Duplicate ... attribute`. If you hit that, delete the stray `obj` folders under `TestData/TestSolution`.

Test method names are snake_case sentences (`Versions_with_trailing_zeroes_are_the_same`).

## Manual verification of the packaged tool

```powershell
cd src/DotNet.Consolidate
dotnet pack
cd <some other solution folder>
dotnet tool install dotnet-consolidate --local --add-source <full path to bin/Release>
dotnet consolidate -s YourSolution.sln
```
