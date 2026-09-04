# dotnet consolidate

[![Build](https://github.com/olsh/dotnet-consolidate/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/olsh/dotnet-consolidate/actions/workflows/build.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=olsh_dotnet-consolidate&metric=alert_status)](https://sonarcloud.io/dashboard?id=olsh_dotnet-consolidate)
[![NuGet](https://img.shields.io/nuget/v/dotnet-consolidate.svg)](https://www.nuget.org/packages/dotnet-consolidate/)

.NET tool that verifies that all NuGet packages in a solution are consolidated.

> Developers typically consider it bad practice to use different versions of the same NuGet package across different projects in the same solution.
>
> https://docs.microsoft.com/en-us/nuget/consume-packages/install-use-packages-visual-studio#consolidate-tab

The tool finds such discrepancies.

## Installation

```shell
dotnet tool install dotnet-consolidate --global
```

Requires the .NET 8 runtime or newer. The .NET version your own projects target doesn't matter — the tool reads project files and never builds them.

## Usage

Check a solution:

```shell
dotnet consolidate -s YourSolution.sln
```

Both `.sln` and `.slnx` are supported. With no `-s`, every solution in the working directory is checked:

```shell
dotnet consolidate
```

Check only certain packages:

```shell
dotnet consolidate -s YourSolution.sln -p Serilog Newtonsoft.Json
```

Check a whole family of packages, using wildcards:

```shell
dotnet consolidate -s YourSolution.sln -p "MyCompany.*"
```

Skip packages:

```shell
dotnet consolidate -s YourSolution.sln -e "MyCompany.Internal.*"
```

Skip prerelease versions:

```shell
dotnet consolidate -s YourSolution.sln --excludedVersionsRegex ".*-alpha$"
```

Check several solutions as one set:

```shell
dotnet consolidate -s Cms.sln TheSite.sln -c
```

Output when everything agrees:

```
All packages in YourSolution.sln are consolidated.
```

and when it doesn't:

```
Found 2 non-consolidated packages

----------------------------
Newtonsoft.Json
----------------------------
Sentry - 11.0.2
Sentry.Tests - 6.0.8

----------------------------
Microsoft.Extensions.Logging.Configuration
----------------------------
Sentry.Extensions.Logging - 2.1.0
Sentry.Extensions.Logging.Tests - 3.0.0
```

## Options

| Option | Default | Description |
| --- | --- | --- |
| `-s`, `--solutions` | all solutions in the working directory | Solutions to check, space separated. |
| `-p`, `--packageIds` | all packages | Check only these package IDs, space separated. |
| `-e`, `--excluded` | none | Package IDs to skip, space separated. |
| `--excludedVersionsRegex` | none | Regular expression matching versions to skip. |
| `-c`, `--crossSolution` | off | Check all given solutions as one set instead of one at a time. |
| `-d`, `--directoryBuildProps` | `true` | Count packages declared in `Directory.Build.props` as references of the projects below it. |
| `-o`, `--reportOverridenDirectoryBuildProps` | `true` | Report projects that override a version coming from their `Directory.Build.props`. |
| `--property` | none | MSBuild properties as `Name=Value` pairs, used when evaluating conditions in project files. |
| `-f`, `--format` | `Text` | Output format, `Text` or `Json`. |
| `--help`, `--version` | | Print usage or the tool version and exit successfully. |

`-p` and `-e` match package IDs case-insensitively, the way NuGet does, and both accept wildcards: `*` for any run of characters, `?` for exactly one. An entry without a wildcard is matched in full, so `-p Serilog` does not check `Serilog.Sinks.Console`. Quote patterns in POSIX shells, otherwise `*` may expand to file names. The two options combine, `-e` narrowing what `-p` selected:

```shell
dotnet consolidate -s YourSolution.sln -p "MyCompany.*" -e "MyCompany.Internal.*"
```

`-d` and `-o` need an explicit value to be turned off — write `-d false`, not a bare `-d`, which reads whatever follows it as its value.

## Exit codes

The tool exits with a non-success code when

* packages are not consolidated,
* a `-p` entry matches no package in the solution (almost always a typo, and a pattern matching nothing means nothing was checked),
* the command line can't be parsed, for example an unknown option, or one repeated where it takes a space-separated list. The complaint goes to stderr.

`Directory.Build.props` overrides are informational and never affect the exit code.

## Multiple solutions

Several solutions are checked one at a time, each against itself. `-c` (`--crossSolution`) checks them as one set instead, so a package referenced at 1.0.0 in one solution and at 2.0.0 in another is reported even though neither solution disagrees with itself. There is a single report for the whole set, and a project belonging to more than one solution is counted once — the reading from the first solution on the command line wins, including the `Directory.Build.props` it inherited from.

One thing to know before putting `-c` in a build: `-p` then asks whether a package is referenced *anywhere in the set*, so an ID that only one of the solutions references is no longer reported as missing. That is the one way this flag can turn a failing run into a passing one.

## Directory.Build.props

Packages declared in a `Directory.Build.props` count as references of every project underneath it. To compare project files alone, use `-d false`.

When a project declares a package that its `Directory.Build.props` already declares, the project file wins and the central version stops applying to it. `-o` reports that, with both versions and the props file to go and change:

```
Found 1 Directory.Build.props overrides

----------------------------
Serilog
----------------------------
ProjectB - 4.0.0 overrides 3.0.1 from C:\src\MySolution\Directory.Build.props
```

The overlap is reported even when the two versions match, since the copy in the project file silently stops following the props file the next time it is bumped.

Both ways of overriding are recognised:

```xml
<!-- Directory.Build.props -->
<PackageReference Include="Serilog" Version="3.0.1" />
```

```xml
<!-- ProjectB.csproj -->

<!-- Re-declaring adds a second item, which NuGet flags as NU1504.
     The project is listed twice in the report. -->
<PackageReference Include="Serilog" Version="4.0.0" />

<!-- Updating changes the inherited item.
     The project is listed once, at 4.0.0. -->
<PackageReference Update="Serilog" Version="4.0.0" />
```

`<PackageReference Remove="Serilog" />` is honoured as well: the project stops counting as a reference of the package altogether. A removal is not an override and is not reported.

`Update` and `Remove` only act on packages the project inherits, so `-d false` leaves them with nothing to change. Within a project file, MSBuild order applies — an `Update` or a `Remove` affects the `PackageReference` items declared above it and no others.

## MSBuild conditions

`Condition` attributes on `PropertyGroup`, `ItemGroup` and `PackageReference` are evaluated, so a package reference that isn't actually active is left out of the check:

```xml
<ItemGroup Condition="'$(NuGetBuild)' == 'true'">
  <!-- Not checked for consolidation unless NuGetBuild is passed in. -->
  <PackageReference Include="MyPackage" Version="1.0.0" />
</ItemGroup>
```

Property values are supplied with `--property`, and they take precedence over anything the project file sets:

```shell
dotnet consolidate -s YourSolution.sln --property NuGetBuild=true Configuration=Release
```

`$(...)` references in `Include` and `Version` are expanded too, so `Version="$(SerilogVersion)"` is compared as the version it resolves to. When a property can't be resolved, the literal text is kept — except on an `Update`, which is dropped instead, leaving the inherited version standing. Overwriting a real version with the text `$(SerilogVersion)` would invent a discrepancy, and a property declared in the `Directory.Build.props` is unresolvable in the project file, which is parsed separately.

A project that multi-targets is evaluated once per entry in `<TargetFrameworks>` and the results are combined, so references guarded by `'$(TargetFramework)' == '...'` still take part in the check.

The supported part of the condition language is `==`, `!=`, numeric comparisons, `And`, `Or`, `!`, parentheses, `Exists()` and `HasTrailingSlash()`. Anything beyond that, such as a property function like `$([MSBuild]::VersionGreaterThan(...))`, can't be evaluated; the tool says so and **keeps** the package references that condition guards, rather than dropping them. A `Remove` behind such a condition is discarded for the same reason, and an `Update` behind one is applied without displacing the inherited version, so both are reported. `Import` directives are not followed, so properties defined in an imported file are unknown (and therefore empty).

## JSON output

`-f json` prints a single JSON document to stdout and nothing else. Progress messages are suppressed rather than moved to stderr, so `dotnet consolidate -f json | ConvertFrom-Json` works and CI systems that treat any stderr output as a failure stay happy. Anything the tool would have reported along the way (a project that couldn't be parsed, a condition it couldn't evaluate) is carried in `warnings`. Exit codes are the same as for the text format.

```json
{
  "warnings": [],
  "solutions": [
    {
      "solutionFile": "YourSolution.sln",
      "solutionFiles": ["YourSolution.sln"],
      "isParsedWithoutIssues": true,
      "packageIdsNotFound": [],
      "nonConsolidatedPackages": [
        {
          "packageId": "Newtonsoft.Json",
          "packageVersions": [
            { "projectName": "ProjectA", "version": "11.0.2" },
            { "projectName": "ProjectB", "version": "13.0.3" }
          ]
        }
      ],
      "directoryBuildPropsOverrides": [
        {
          "packageId": "Serilog",
          "projectName": "ProjectB",
          "version": "4.0.0",
          "directoryBuildPropsVersion": "3.0.1",
          "directoryBuildPropsFile": "C:\\src\\MySolution\\Directory.Build.props"
        }
      ]
    }
  ]
}
```

`solutionFiles` lists the solutions an entry covers and is always present. Ordinarily that is the one solution the entry is about, and `solutionFile` repeats it; with `-c` there is a single entry for the whole set, `solutionFiles` holds each path exactly and `solutionFile` is them joined for display.

A rejected command line produces the same document rather than nothing at all — the parser's complaint in `warnings`, an empty `solutions`, and a non-success exit code:

```json
{
  "warnings": [
    "Option 'p, packageIds' is defined multiple times."
  ],
  "solutions": []
}
```

## Testing a development version of the tool locally from source

Run the following commands in `src/DotNet.Consolidate`:

```powershell
dotnet build
dotnet pack
```

The package will be created under `bin/Release`.

Open the folder of the solution where you want to test the tool, then run:

```powershell
dotnet tool install dotnet-consolidate --local --add-source  <full path of bin/Release>
dotnet consolidate -s YourSolution.sln
```

When you're finished, you can also uninstall it to clean up:

```powershell
dotnet tool uninstall dotnet-consolidate
```
