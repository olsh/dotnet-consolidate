# dotnet consolidate

[![Build](https://github.com/olsh/dotnet-consolidate/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/olsh/dotnet-consolidate/actions/workflows/build.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=olsh_dotnet-consolidate&metric=alert_status)](https://sonarcloud.io/dashboard?id=olsh_dotnet-consolidate)
[![NuGet](https://img.shields.io/nuget/v/dotnet-consolidate.svg)](https://www.nuget.org/packages/dotnet-consolidate/)

.NET core tool that verifies that all NuGet packages in a solution are consolidated.

> Developers typically consider it bad practice to use different versions of the same NuGet package across different projects in the same solution. 
> 
> https://docs.microsoft.com/en-us/nuget/consume-packages/install-use-packages-visual-studio#consolidate-tab

The tool finds such discrepancies.

## Installation

`dotnet tool install dotnet-consolidate --global`

The tool targets `net8.0` and rolls forward across major versions, so a single installed runtime of **.NET 8 or newer** (.NET 8, 9, 10, …) is enough.

The version of .NET your solution's projects target does not matter — the tool reads project files directly and never builds them.

> **Upgrading from 4.2.0 or earlier?** Those versions have no roll-forward policy and need the .NET 8 runtime specifically. If you see `You must install .NET to run this application` / `framework 'Microsoft.NETCore.App', version '8.0.0' was not found`, run `dotnet tool update dotnet-consolidate --global` to get 5.0.0 or later.

## Usage

Pass a solution file as a parameter

`dotnet consolidate -s YourSolution.sln`

Both the classic `.sln` format and the XML `.slnx` format are supported

`dotnet consolidate -s YourSolution.slnx`

or multiple solutions

`dotnet consolidate -s YourSolution.sln AnotherSolution.sln`

If no solution is specified, every `.sln` and `.slnx` file in the working directory is analyzed.

You can also optionally specify the a package ID if you want only a single package to be consolidated

`dotnet consolidate -s YourSolution.sln -p PackageId`

or a list of package IDs if you want to consolidate multiple, but not all which are referenced in the solution projects

`dotnet consolidate -s YourSolution.sln -p PackageID1 PackageID2`

Alternatively, you can configure the opposite, package IDs that should be skipped during consolidation:

`dotnet consolidate -s YourSolution.sln -e ExcludedPackageID1 ExcludedPackageID2`

Both options match package IDs case-insensitively, the way NuGet itself treats them, so `-p serilog` matches a `Serilog` reference.

It's also possible to skip a pattern of versions during consolidation with a regular expression:

`dotnet consolidate -s YourSolution.sln --excludedVersionsRegex .*-alpha$`

With this, if e.g one of the projects in the solution uses `MyPackage` v1.0.0, and another project `MyPackage` v1.1.0-alpha, then no discrepancy will be indicated.

`Directory.Build.props` files are taken into account by default — the packages one declares count as references of every project underneath it. To compare the project files alone, turn that off with `-d false` (or `--directoryBuildProps false`):

`dotnet consolidate -s YourSolution.sln -d false`

Give the option a value rather than writing a bare `-d`; on its own it reads whatever follows it as its value.

If the tool finds discrepancies between projects (only the specified ones if -p is given), it exits with non-success status code and prints these discrepancies.

A package ID passed to `-p` that no project in the solution references is also reported and also exits with a non-success status code — that is almost always a typo, and exiting successfully would let it pass a build unnoticed.

A command line the tool can't parse — an unknown option, or one repeated where it takes a space-separated list — exits with a non-success status code too, for the same reason: nothing was checked, so a green build would be a lie. The complaint is written to stderr. `--help` and `--version` are requests that were satisfied, not failures, and still exit successfully.

To get machine-readable output instead of the report, ask for the JSON format:

`dotnet consolidate -s YourSolution.sln -f json`

stdout then carries a single JSON document and nothing else — progress messages are suppressed rather than moved to stderr, so `dotnet consolidate -f json | ConvertFrom-Json` works and CI systems that treat any stderr output as a failure stay happy. Anything the tool would have reported along the way (a project that couldn't be parsed, a condition it couldn't evaluate) is carried in `warnings`. Exit codes are the same as for the text format.

```json
{
  "warnings": [],
  "solutions": [
    {
      "solutionFile": "YourSolution.sln",
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
      ]
    }
  ]
}
```

A command line the tool rejects produces that same document rather than nothing at all — the parser's complaint in `warnings`, an empty `solutions`, and a non-success status code:

```json
{
  "warnings": [
    "Option 'p, packageIds' is defined multiple times."
  ],
  "solutions": []
}
```

## MSBuild conditions

`Condition` attributes on `PropertyGroup`, `ItemGroup` and `PackageReference` are evaluated, so a package reference that isn't actually active is left out of the check:

```xml
<ItemGroup Condition="'$(NuGetBuild)' == 'true'">
  <!-- Not checked for consolidation unless NuGetBuild is passed in. -->
  <PackageReference Include="MyPackage" Version="1.0.0" />
</ItemGroup>
```

Property values are supplied with `--property`, and they take precedence over anything the project file sets:

`dotnet consolidate -s YourSolution.sln --property NuGetBuild=true Configuration=Release`

`$(...)` references in `Include` and `Version` are expanded too, so `Version="$(SerilogVersion)"` is compared as the version it resolves to. When a property can't be resolved, the literal text is kept.

A project that multi-targets is evaluated once per entry in `<TargetFrameworks>` and the results are combined, so references guarded by `'$(TargetFramework)' == '...'` still take part in the check.

The tool implements the commonly used part of the condition language — `==`, `!=`, numeric comparisons, `And`, `Or`, `!`, parentheses, `Exists()` and `HasTrailingSlash()`. Anything beyond that, such as a property function like `$([MSBuild]::VersionGreaterThan(...))`, can't be evaluated; the tool says so and **keeps** the package references that condition guards, rather than dropping them. `Import` directives are not followed, so properties defined in an imported file are unknown (and therefore empty).

## Examples

`dotnet consolidate -s umbraco.sln`

:white_check_mark: Output:

```
All packages are consolidated.
```

`dotnet consolidate -s Sentry.sln`

:x: Output:

```
Found 5 non-consolidated packages

----------------------------
Newtonsoft.Json
----------------------------
Sentry - 11.0.2
Sentry - 6.0.8

----------------------------
Microsoft.Extensions.Logging.Configuration
----------------------------
Sentry.Extensions.Logging - 2.1.0
Sentry.Extensions.Logging - 3.0.0

----------------------------
Microsoft.Extensions.DependencyInjection
----------------------------
Sentry.AspNetCore - 2.1.0
Sentry.Extensions.Logging.Tests - 2.1.1
Sentry.Extensions.Logging.Tests - 3.0.0

----------------------------
Microsoft.Extensions.Configuration.Json
----------------------------
Sentry.Extensions.Logging.Tests - 2.1.1
Sentry.Samples.GenericHost - 2.1.1
Sentry.Extensions.Logging.Tests - 3.0.0

----------------------------
Microsoft.AspNetCore.TestHost
----------------------------
Sentry.Testing - 2.1.1
Sentry.Testing - 3.1.0
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
