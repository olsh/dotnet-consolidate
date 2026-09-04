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

Several solutions are checked one at a time, each against itself. To check them as one set instead — so a package referenced at 1.0.0 by a project in one solution and at 2.0.0 by a project in another is reported, even though neither solution disagrees with itself — add `-c` (or `--crossSolution`):

`dotnet consolidate -s cms.sln TheSite.sln --crossSolution`

```
Found 1 non-consolidated packages

----------------------------
Serilog
----------------------------
Web - 1.0.0
Api - 2.0.0
```

There is one report for the whole set rather than one per solution, and a project belonging to more than one of them is counted **once** — the reading from the first solution on the command line is the one that is kept, including which `Directory.Build.props` it inherited from and therefore what the `-o` report says about it. That matters because every solution is searched for props files from its own directory, so the same shared project can otherwise be read twice with different inherited versions and appear to disagree with itself.

One thing to know before putting `-c` in a build: `-p` asks whether a package is referenced *anywhere in the set*, so an ID that only one of the solutions references is no longer reported as missing. That is the one way this flag can turn a failing run into a passing one.

You can also optionally specify the a package ID if you want only a single package to be consolidated

`dotnet consolidate -s YourSolution.sln -p PackageId`

or a list of package IDs if you want to consolidate multiple, but not all which are referenced in the solution projects

`dotnet consolidate -s YourSolution.sln -p PackageID1 PackageID2`

Alternatively, you can configure the opposite, package IDs that should be skipped during consolidation:

`dotnet consolidate -s YourSolution.sln -e ExcludedPackageID1 ExcludedPackageID2`

Both options match package IDs case-insensitively, the way NuGet itself treats them, so `-p serilog` matches a `Serilog` reference.

Either option also takes wildcards, so a whole family of packages can be named at once — `*` stands for any run of characters and `?` for exactly one:

`dotnet consolidate -s YourSolution.sln -p "MyCompany.*"`

That checks `MyCompany.Dal`, `MyCompany.Logging` and every other package whose ID starts with `MyCompany.`, without having to list them or come back and edit the command line when a new one is added. `-e "MyCompany.*"` is the opposite, skipping all of them.

Quote the pattern. In POSIX shells an unquoted `*` is left alone only while nothing in the working directory happens to match it, so `-p MyCompany.*` can silently turn into a list of file names. PowerShell doesn't expand arguments and needs no quotes, but they do no harm.

An entry without a wildcard is still matched in full: `-p Serilog` does not check `Serilog.Sinks.Console`.

The two options can be given together, and `-e` applies to what `-p` selected — which is how you check a family of packages apart from a branch of it:

`dotnet consolidate -s YourSolution.sln -p "MyCompany.*" -e "MyCompany.Internal.*"`

Excluding a package doesn't hide it from the "not referenced by any project" check below, which is only ever about `-p`: excluding everything `-p` matched checks nothing and still exits successfully, while a `-p` entry that matches no package in the solution keeps failing the run.

It's also possible to skip a pattern of versions during consolidation with a regular expression:

`dotnet consolidate -s YourSolution.sln --excludedVersionsRegex .*-alpha$`

With this, if e.g one of the projects in the solution uses `MyPackage` v1.0.0, and another project `MyPackage` v1.1.0-alpha, then no discrepancy will be indicated.

`Directory.Build.props` files are taken into account by default — the packages one declares count as references of every project underneath it. To compare the project files alone, turn that off with `-d false` (or `--directoryBuildProps false`):

`dotnet consolidate -s YourSolution.sln -d false`

Give the option a value rather than writing a bare `-d`; on its own it reads whatever follows it as its value.

When a project declares a package that its `Directory.Build.props` already declares, the project file wins and the central version stops applying to it. That is reported too, with both versions and the props file to go and change:

```
Found 1 Directory.Build.props overrides

----------------------------
Serilog
----------------------------
ProjectB - 4.0.0 overrides 3.0.1 from C:\src\MySolution\Directory.Build.props
```

The overlap is reported even when the two versions match, since the copy in the project file silently stops following the props file the next time it is bumped. It is informational — an override never changes the exit code — and it is on by default; turn it off with `-o false` (or `--reportOverridenDirectoryBuildProps false`), and note that `-d false` switches it off along with everything else about `Directory.Build.props`.

Both ways of overriding are recognised. Re-declaring the package with `Include` is the one shown above; the idiomatic way is `Update`, which changes the version of the item the props file already added instead of adding a second one that NuGet would flag as NU1504:

```xml
<!-- Directory.Build.props -->
<PackageReference Include="Serilog" Version="3.0.1" />
```
```xml
<!-- ProjectB.csproj -->
<PackageReference Update="Serilog" Version="4.0.0" />
```

ProjectB restores Serilog 4.0.0, and that is the version it is checked against. It is listed **once**, at 4.0.0 — where a re-declared `Include` leaves the project holding two references and listed twice.

`<PackageReference Remove="Serilog" />` is honoured as well: the project stops counting as a reference of the package altogether. A removal is not an override and is not reported by `-o`; it simply disappears from the report.

Both only act on packages the project inherits, so `-d false` leaves them with nothing to change. Within a project file, MSBuild order applies — an `Update` or a `Remove` affects the `PackageReference` items declared above it and no others.

If the tool finds discrepancies between projects (only the specified ones if -p is given), it exits with non-success status code and prints these discrepancies. With `-c` the projects compared are those of every given solution, so a disagreement *between* two solutions fails the run just as one inside a single solution does.

A package ID passed to `-p` that no project in the solution references is also reported and also exits with a non-success status code — that is almost always a typo, and exiting successfully would let it pass a build unnoticed. A wildcard pattern that matches nothing is treated the same way and for the same reason: `-p "MyCompnay.*"` has checked exactly nothing, so it must not pass the build.

A command line the tool can't parse — an unknown option, or one repeated where it takes a space-separated list — exits with a non-success status code too, for the same reason: nothing was checked, so a green build would be a lie. The complaint is written to stderr. `--help` and `--version` are requests that were satisfied, not failures, and still exit successfully.

To get machine-readable output instead of the report, ask for the JSON format:

`dotnet consolidate -s YourSolution.sln -f json`

stdout then carries a single JSON document and nothing else — progress messages are suppressed rather than moved to stderr, so `dotnet consolidate -f json | ConvertFrom-Json` works and CI systems that treat any stderr output as a failure stay happy. The one thing ever written to stderr is the parser's complaint about a command line it couldn't parse, and that run fails on its exit code anyway. Anything the tool would have reported along the way (a project that couldn't be parsed, a condition it couldn't evaluate) is carried in `warnings`. Exit codes are the same as for the text format.

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

`$(...)` references in `Include` and `Version` are expanded too, so `Version="$(SerilogVersion)"` is compared as the version it resolves to. When a property can't be resolved, the literal text is kept — except on an `Update`, which is dropped instead, leaving the inherited version standing. Overwriting a real version with the text `$(SerilogVersion)` would invent a discrepancy, and a property declared in the `Directory.Build.props` is unresolvable in the project file, which is parsed separately.

A project that multi-targets is evaluated once per entry in `<TargetFrameworks>` and the results are combined, so references guarded by `'$(TargetFramework)' == '...'` still take part in the check.

The tool implements the commonly used part of the condition language — `==`, `!=`, numeric comparisons, `And`, `Or`, `!`, parentheses, `Exists()` and `HasTrailingSlash()`. Anything beyond that, such as a property function like `$([MSBuild]::VersionGreaterThan(...))`, can't be evaluated; the tool says so and **keeps** the package references that condition guards, rather than dropping them. A `Remove` behind such a condition is discarded for the same reason — nothing may drop a package because a project file wasn't understood — and an `Update` behind one is applied without displacing the inherited version, so both are reported. `Import` directives are not followed, so properties defined in an imported file are unknown (and therefore empty).

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
