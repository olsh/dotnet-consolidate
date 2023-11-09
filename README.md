# dotnet consolidate

[![Build status](https://ci.appveyor.com/api/projects/status/k8hwnc4d6d897vc8?svg=true)](https://ci.appveyor.com/project/olsh/dotnet-consolidate)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=dotnet-consolidate&metric=alert_status)](https://sonarcloud.io/dashboard?id=dotnet-consolidate)
[![NuGet](https://img.shields.io/nuget/v/dotnet-consolidate.svg)](https://www.nuget.org/packages/dotnet-consolidate/)

.NET core tool that verifies that all NuGet packages in a solution are consolidated.

> Developers typically consider it bad practice to use different versions of the same NuGet package across different projects in the same solution. 
> 
> https://docs.microsoft.com/en-us/nuget/consume-packages/install-use-packages-visual-studio#consolidate-tab

The tool finds such discrepancies.

## Installation

`dotnet tool install dotnet-consolidate --global`

## Usage

Pass a solution file as a parameter

`dotnet consolidate -s YourSolution.sln`

or multiple solutions

`dotnet consolidate -s YourSolution.sln AnotherSolution.sln`

You can also optionally specify the a package ID if you want only a single package to be consolidated

`dotnet consolidate -s YourSolution.sln -p PackageId`

or a list of package IDs if you want to consolidate multiple, but not all which are referenced in the solution projects

`dotnet consolidate -s YourSolution.sln -p PackageID1 PackageID2`

Alternatively, you can configure the opposite, package IDs that should be skipped during consolidation:

`dotnet consolidate -s YourSolution.sln -e ExcludedPackageID1 ExcludedPackageID2`

If the tool finds discrepancies between projects (only the specified ones if -p is given), it exits with non-success status code and prints these discrepancies.

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
