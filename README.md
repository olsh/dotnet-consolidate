# dotnet consolidate

[![Build status](https://ci.appveyor.com/api/projects/status/k8hwnc4d6d897vc8?svg=true)](https://ci.appveyor.com/project/olsh/dotnet-consolidate)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=dotnet-consolidate&metric=alert_status)](https://sonarcloud.io/dashboard?id=dotnet-consolidate)
[![NuGet](https://img.shields.io/nuget/v/dotnet-consolidate.svg)](https://www.nuget.org/packages/dotnet-consolidate/)

.NET core tool that verifies that all NuGet packages in a solution are consolidated.

> Developers typically consider it bad practice to use different versions of the same NuGet package across different projects in the same solution.
>
> <https://docs.microsoft.com/en-us/nuget/consume-packages/install-use-packages-visual-studio#consolidate-tab>

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

It's also possible to skip a pattern of versions during consolidation with a regular expression:

`dotnet consolidate -s YourSolution.sln --excludedVersionsRegex .*-alpha$`

With this, if e.g one of the projects in the solution uses `MyPackage` v1.0.0, and another project `MyPackage` v1.1.0-alpha, then no discrepancy will be indicated.

If the tool finds discrepancies between projects (only the specified ones if -p is given), it exits with non-success status code and prints these discrepancies.

`dotnet consolidate -s YourSolution.sln -f Json`

If you want to output the discrepancies in JSON format, you can use the `-f Json` option.

## Examples

### With no changes:
`dotnet consolidate -s .\src\DotNet.Consolidate.Tests\TestData\TestSolution\TestSolution.sln`

:white_check_mark: Output:

```text
Analyzing packages in .\src\DotNet.Consolidate.Tests\TestData\TestSolution\TestSolution.sln
****************************
.\src\DotNet.Consolidate.Tests\TestData\TestSolution\TestSolution.sln
****************************
All packages in .\src\DotNet.Consolidate.Tests\TestData\TestSolution\TestSolution.sln are consolidated.

```

`dotnet consolidate -s .\src\DotNet.Consolidate.Tests\TestData\TestSolution\TestSolution.sln -f Json`
```json
{
  "Solutions": [
    {
      "Solution": {
        "ProjectInfos": [
          {
            "Packages": [],
            "ProjectName": "ProjectA",
            "ProjectDirectory": ".\\src\\DotNet.Consolidate.Tests\\TestData\\TestSolution\\src\\ProjectA"
          },
          {
            "Packages": [
              {
                "Id": "System.Text.Json",
                "Version": {
                  "OriginalValue": "7.0.3",
                  "NormalizedValue": "7.0.3"
                },
                "PackageReferenceType": "Direct"
              }
            ],
            "ProjectName": "ProjectB",
            "ProjectDirectory": ".\\src\\DotNet.Consolidate.Tests\\TestData\\TestSolution\\src\\ProjectB"
          },
          {
            "Packages": [],
            "ProjectName": "ProjectA.Tests",
            "ProjectDirectory": ".\\src\\DotNet.Consolidate.Tests\\TestData\\TestSolution\\tests\\ProjectA.Tests"
          },
          {
            "Packages": [],
            "ProjectName": "ProjectB.Tests",
            "ProjectDirectory": ".\\src\\DotNet.Consolidate.Tests\\TestData\\TestSolution\\tests\\ProjectB.Tests"
          }
        ],
        "DirectoryBuildPropsInfos": [
          {
            "Packages": [
              {
                "Id": "CommandLineParser",
                "Version": {
                  "OriginalValue": "2.7.82",
                  "NormalizedValue": "2.7.82"
                },
                "PackageReferenceType": "Direct"
              },
              {
                "Id": "Serilog",
                "Version": {
                  "OriginalValue": "3.0.1",
                  "NormalizedValue": "3.0.1"
                },
                "PackageReferenceType": "Direct"
              }
            ],
            "FileName": "Directory.build.props",
            "DirectoryName": "C:\\git\\dotnet-consolidate\\src\\DotNet.Consolidate.Tests\\TestData\\TestSolution"
          },
          {
            "Packages": [
              {
                "Id": "AutoFixture",
                "Version": {
                  "OriginalValue": "4.17.0",
                  "NormalizedValue": "4.17"
                },
                "PackageReferenceType": "Direct"
              },
              {
                "Id": "FluentAssertions",
                "Version": {
                  "OriginalValue": "6.7.0",
                  "NormalizedValue": "6.7"
                },
                "PackageReferenceType": "Direct"
              },
              {
                "Id": "Microsoft.NET.Test.Sdk",
                "Version": {
                  "OriginalValue": "17.2.0",
                  "NormalizedValue": "17.2"
                },
                "PackageReferenceType": "Direct"
              },
              {
                "Id": "Moq",
                "Version": {
                  "OriginalValue": "4.18.1",
                  "NormalizedValue": "4.18.1"
                },
                "PackageReferenceType": "Direct"
              },
              {
                "Id": "NUnit",
                "Version": {
                  "OriginalValue": "3.13.3",
                  "NormalizedValue": "3.13.3"
                },
                "PackageReferenceType": "Direct"
              },
              {
                "Id": "NUnit3TestAdapter",
                "Version": {
                  "OriginalValue": "4.2.1",
                  "NormalizedValue": "4.2.1"
                },
                "PackageReferenceType": "Direct"
              },
              {
                "Id": "coverlet.collector",
                "Version": {
                  "OriginalValue": "3.1.2",
                  "NormalizedValue": "3.1.2"
                },
                "PackageReferenceType": "Direct"
              }
            ],
            "FileName": "Directory.build.props",
            "DirectoryName": "C:\\git\\dotnet-consolidate\\src\\DotNet.Consolidate.Tests\\TestData\\TestSolution\\tests"
          }
        ],
        "SolutionFile": ".\\src\\DotNet.Consolidate.Tests\\TestData\\TestSolution\\TestSolution.sln",
        "Solution": {
          "Header": [
            "",
            "Microsoft Visual Studio Solution File, Format Version 12.00",
            "# Visual Studio Version 17",
            "VisualStudioVersion = 17.6.33723.286",
            "MinimumVisualStudioVersion = 10.0.40219.1"
          ],
          "Global": [
            {
              "Name": "SolutionConfigurationPlatforms",
              "Type": "PreSolution",
              "Entries": {
                "Debug|x64": "Debug|x64",
                "Release|x64": "Release|x64"
              }
            },
            {
              "Name": "ProjectConfigurationPlatforms",
              "Type": "PostSolution",
              "Entries": {
                "{B2641D3B-1DE4-462D-BBBF-6874EFC3B7D0}.Debug|x64.ActiveCfg": "Debug|Any CPU",
                "{B2641D3B-1DE4-462D-BBBF-6874EFC3B7D0}.Debug|x64.Build.0": "Debug|Any CPU",
                "{B2641D3B-1DE4-462D-BBBF-6874EFC3B7D0}.Release|x64.ActiveCfg": "Release|Any CPU",
                "{B2641D3B-1DE4-462D-BBBF-6874EFC3B7D0}.Release|x64.Build.0": "Release|Any CPU",
                "{76DB371A-4871-417B-8CD5-38F4250050BF}.Debug|x64.ActiveCfg": "Debug|Any CPU",
                "{76DB371A-4871-417B-8CD5-38F4250050BF}.Debug|x64.Build.0": "Debug|Any CPU",
                "{76DB371A-4871-417B-8CD5-38F4250050BF}.Release|x64.ActiveCfg": "Release|Any CPU",
                "{76DB371A-4871-417B-8CD5-38F4250050BF}.Release|x64.Build.0": "Release|Any CPU",
                "{01777C80-CDE9-49C7-8FAD-590CAF9E8E23}.Debug|x64.ActiveCfg": "Debug|Any CPU",
                "{01777C80-CDE9-49C7-8FAD-590CAF9E8E23}.Debug|x64.Build.0": "Debug|Any CPU",
                "{01777C80-CDE9-49C7-8FAD-590CAF9E8E23}.Release|x64.ActiveCfg": "Release|Any CPU",
                "{01777C80-CDE9-49C7-8FAD-590CAF9E8E23}.Release|x64.Build.0": "Release|Any CPU",
                "{FA79411B-61D5-40E5-8E5C-B832E5E57BBD}.Debug|x64.ActiveCfg": "Debug|Any CPU",
                "{FA79411B-61D5-40E5-8E5C-B832E5E57BBD}.Debug|x64.Build.0": "Debug|Any CPU",
                "{FA79411B-61D5-40E5-8E5C-B832E5E57BBD}.Release|x64.ActiveCfg": "Release|Any CPU",
                "{FA79411B-61D5-40E5-8E5C-B832E5E57BBD}.Release|x64.Build.0": "Release|Any CPU"
              }
            },
            {
              "Name": "SolutionProperties",
              "Type": "PreSolution",
              "Entries": {
                "HideSolutionNode": "FALSE"
              }
            },
            {
              "Name": "NestedProjects",
              "Type": "PreSolution",
              "Entries": {
                "{B2641D3B-1DE4-462D-BBBF-6874EFC3B7D0}": "{91927A48-FE66-4083-8F03-EEAFA974C86C}",
                "{76DB371A-4871-417B-8CD5-38F4250050BF}": "{91927A48-FE66-4083-8F03-EEAFA974C86C}",
                "{01777C80-CDE9-49C7-8FAD-590CAF9E8E23}": "{5CD86CD0-0B02-47B4-9633-5C65875733FC}",
                "{FA79411B-61D5-40E5-8E5C-B832E5E57BBD}": "{5CD86CD0-0B02-47B4-9633-5C65875733FC}"
              }
            },
            {
              "Name": "ExtensibilityGlobals",
              "Type": "PostSolution",
              "Entries": {
                "SolutionGuid": "{40DE902E-DB50-4D7C-A519-E6DC61D0059A}"
              }
            }
          ],
          "Projects": [
            {
              "TypeGuid": "2150e333-8fdc-42a3-9474-1a3956d46de8",
              "Name": "src",
              "Path": "src",
              "Guid": "91927a48-fe66-4083-8f03-eeafa974c86c",
              "ProjectSection": null
            },
            {
              "TypeGuid": "2150e333-8fdc-42a3-9474-1a3956d46de8",
              "Name": "tests",
              "Path": "tests",
              "Guid": "5cd86cd0-0b02-47b4-9633-5c65875733fc",
              "ProjectSection": {
                "Name": "SolutionItems",
                "Type": "PreProject",
                "Entries": {
                  "tests\\Directory.build.props": "tests\\Directory.build.props"
                }
              }
            },
            {
              "TypeGuid": "9a19103f-16f7-4668-be54-9a1e7a4f7556",
              "Name": "ProjectA",
              "Path": "src\\ProjectA\\ProjectA.csproj",
              "Guid": "b2641d3b-1de4-462d-bbbf-6874efc3b7d0",
              "ProjectSection": null
            },
            {
              "TypeGuid": "9a19103f-16f7-4668-be54-9a1e7a4f7556",
              "Name": "ProjectB",
              "Path": "src\\ProjectB\\ProjectB.csproj",
              "Guid": "76db371a-4871-417b-8cd5-38f4250050bf",
              "ProjectSection": null
            },
            {
              "TypeGuid": "9a19103f-16f7-4668-be54-9a1e7a4f7556",
              "Name": "ProjectA.Tests",
              "Path": "tests\\ProjectA.Tests\\ProjectA.Tests.csproj",
              "Guid": "01777c80-cde9-49c7-8fad-590caf9e8e23",
              "ProjectSection": null
            },
            {
              "TypeGuid": "9a19103f-16f7-4668-be54-9a1e7a4f7556",
              "Name": "ProjectB.Tests",
              "Path": "tests\\ProjectB.Tests\\ProjectB.Tests.csproj",
              "Guid": "fa79411b-61d5-40e5-8e5c-b832e5e57bbd",
              "ProjectSection": null
            },
            {
              "TypeGuid": "2150e333-8fdc-42a3-9474-1a3956d46de8",
              "Name": "Solution Items",
              "Path": "Solution Items",
              "Guid": "cf9fa02d-aa0b-4a3a-8cf0-f1124fa43b43",
              "ProjectSection": {
                "Name": "SolutionItems",
                "Type": "PreProject",
                "Entries": {
                  "Directory.build.props": "Directory.build.props"
                }
              }
            }
          ]
        },
        "IsParsedWithoutIssues": true
      },
      "NonConsolidatedPackages": []
    }
  ]
}

```

With changes:

```patch
diff --git a/src/DotNet.Consolidate.Tests/TestData/TestSolution/src/ProjectA/ProjectA.csproj b/src/DotNet.Consolidate.Tests/TestData/TestSolution/src/ProjectA/ProjectA.csproj
index 8faa44b..f0aca33 100644
--- a/src/DotNet.Consolidate.Tests/TestData/TestSolution/src/ProjectA/ProjectA.csproj
+++ b/src/DotNet.Consolidate.Tests/TestData/TestSolution/src/ProjectA/ProjectA.csproj
@@ -5,5 +5,9 @@
     <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
     <Platforms>x64;x64</Platforms>
   </PropertyGroup>
+  <ItemGroup>
+    <PackageReference Include="System.Text.Json" Version="7.0.1" />
+    <PackageReference Include="System.Numerics" Version="4.3.0" />
+  </ItemGroup>
 
 </Project>
diff --git a/src/DotNet.Consolidate.Tests/TestData/TestSolution/src/ProjectB/ProjectB.csproj b/src/DotNet.Consolidate.Tests/TestData/TestSolution/src/ProjectB/ProjectB.csproj
index d5fdeaa..1c1d7b2 100644
--- a/src/DotNet.Consolidate.Tests/TestData/TestSolution/src/ProjectB/ProjectB.csproj
+++ b/src/DotNet.Consolidate.Tests/TestData/TestSolution/src/ProjectB/ProjectB.csproj
@@ -6,7 +6,8 @@
   </PropertyGroup>
 
   <ItemGroup>
-    <PackageReference Include="System.Text.Json" Version="7.0.3" />
+    <PackageReference Include="System.Text.Json" Version="7.0.2" />
+    <PackageReference Include="System.Numerics" Version="4.0.1" />
   </ItemGroup>
 
 </Project>
diff --git a/src/DotNet.Consolidate.Tests/TestData/TestSolution/tests/ProjectA.Tests/ProjectA.Tests.csproj b/src/DotNet.Consolidate.Tests/TestData/TestSolution/tests/ProjectA.Tests/ProjectA.Tests.csproj
index 52e6553..1748bf3 100644
--- a/src/DotNet.Consolidate.Tests/TestData/TestSolution/tests/ProjectA.Tests/ProjectA.Tests.csproj
+++ b/src/DotNet.Consolidate.Tests/TestData/TestSolution/tests/ProjectA.Tests/ProjectA.Tests.csproj
@@ -3,5 +3,8 @@
   <PropertyGroup>
     <OutputType>Exe</OutputType>
   </PropertyGroup>
-
+  
+  <ItemGroup>
+    <PackageReference Include="System.Text.Json" Version="7.0.3" />
+  </ItemGroup>
 </Project>
diff --git a/src/DotNet.Consolidate.Tests/TestData/TestSolution/tests/ProjectB.Tests/ProjectB.Tests.csproj b/src/DotNet.Consolidate.Tests/TestData/TestSolution/tests/ProjectB.Tests/ProjectB.Tests.csproj
index 52e6553..1bb5727 100644
--- a/src/DotNet.Consolidate.Tests/TestData/TestSolution/tests/ProjectB.Tests/ProjectB.Tests.csproj
+++ b/src/DotNet.Consolidate.Tests/TestData/TestSolution/tests/ProjectB.Tests/ProjectB.Tests.csproj
@@ -3,5 +3,8 @@
   <PropertyGroup>
     <OutputType>Exe</OutputType>
   </PropertyGroup>
-
+  
+  <ItemGroup>
+    <PackageReference Include="System.Text.Json" Version="7.0.4" />
+  </ItemGroup>
 </Project>

```

`dotnet consolidate -s .\src\DotNet.Consolidate.Tests\TestData\TestSolution\TestSolution.sln`

:x: Output:

```text
Analyzing packages in .\src\DotNet.Consolidate.Tests\TestData\TestSolution\TestSolution.sln
Found 2 non-consolidated packages in all solutions

****************************
.\src\DotNet.Consolidate.Tests\TestData\TestSolution\TestSolution.sln
****************************
----------------------------
System.Text.Json
----------------------------
ProjectA - 7.0.1
ProjectB - 7.0.2
ProjectA.Tests - 7.0.3
ProjectB.Tests - 7.0.4

----------------------------
System.Numerics
----------------------------
ProjectB - 4.0.1
ProjectA - 4.3.0


```

`dotnet consolidate -s .\src\DotNet.Consolidate.Tests\TestData\TestSolution\TestSolution.sln -f Json`

```json
{
  "Solutions": [
    {
      "Solution": {
        "ProjectInfos": [
          {
            "Packages": [
              {
                "Id": "System.Text.Json",
                "Version": {
                  "OriginalValue": "7.0.1",
                  "NormalizedValue": "7.0.1"
                },
                "PackageReferenceType": "Direct"
              },
              {
                "Id": "System.Numerics",
                "Version": {
                  "OriginalValue": "4.3.0",
                  "NormalizedValue": "4.3"
                },
                "PackageReferenceType": "Direct"
              }
            ],
            "ProjectName": "ProjectA",
            "ProjectDirectory": ".\\src\\DotNet.Consolidate.Tests\\TestData\\TestSolution\\src\\ProjectA"
          },
          {
            "Packages": [
              {
                "Id": "System.Text.Json",
                "Version": {
                  "OriginalValue": "7.0.2",
                  "NormalizedValue": "7.0.2"
                },
                "PackageReferenceType": "Direct"
              },
              {
                "Id": "System.Numerics",
                "Version": {
                  "OriginalValue": "4.0.1",
                  "NormalizedValue": "4.0.1"
                },
                "PackageReferenceType": "Direct"
              }
            ],
            "ProjectName": "ProjectB",
            "ProjectDirectory": ".\\src\\DotNet.Consolidate.Tests\\TestData\\TestSolution\\src\\ProjectB"
          },
          {
            "Packages": [
              {
                "Id": "System.Text.Json",
                "Version": {
                  "OriginalValue": "7.0.3",
                  "NormalizedValue": "7.0.3"
                },
                "PackageReferenceType": "Direct"
              }
            ],
            "ProjectName": "ProjectA.Tests",
            "ProjectDirectory": ".\\src\\DotNet.Consolidate.Tests\\TestData\\TestSolution\\tests\\ProjectA.Tests"
          },
          {
            "Packages": [
              {
                "Id": "System.Text.Json",
                "Version": {
                  "OriginalValue": "7.0.4",
                  "NormalizedValue": "7.0.4"
                },
                "PackageReferenceType": "Direct"
              }
            ],
            "ProjectName": "ProjectB.Tests",
            "ProjectDirectory": ".\\src\\DotNet.Consolidate.Tests\\TestData\\TestSolution\\tests\\ProjectB.Tests"
          }
        ],
        "DirectoryBuildPropsInfos": [
          {
            "Packages": [
              {
                "Id": "CommandLineParser",
                "Version": {
                  "OriginalValue": "2.7.82",
                  "NormalizedValue": "2.7.82"
                },
                "PackageReferenceType": "Direct"
              },
              {
                "Id": "Serilog",
                "Version": {
                  "OriginalValue": "3.0.1",
                  "NormalizedValue": "3.0.1"
                },
                "PackageReferenceType": "Direct"
              }
            ],
            "FileName": "Directory.build.props",
            "DirectoryName": "C:\\git\\dotnet-consolidate\\src\\DotNet.Consolidate.Tests\\TestData\\TestSolution"
          },
          {
            "Packages": [
              {
                "Id": "AutoFixture",
                "Version": {
                  "OriginalValue": "4.17.0",
                  "NormalizedValue": "4.17"
                },
                "PackageReferenceType": "Direct"
              },
              {
                "Id": "FluentAssertions",
                "Version": {
                  "OriginalValue": "6.7.0",
                  "NormalizedValue": "6.7"
                },
                "PackageReferenceType": "Direct"
              },
              {
                "Id": "Microsoft.NET.Test.Sdk",
                "Version": {
                  "OriginalValue": "17.2.0",
                  "NormalizedValue": "17.2"
                },
                "PackageReferenceType": "Direct"
              },
              {
                "Id": "Moq",
                "Version": {
                  "OriginalValue": "4.18.1",
                  "NormalizedValue": "4.18.1"
                },
                "PackageReferenceType": "Direct"
              },
              {
                "Id": "NUnit",
                "Version": {
                  "OriginalValue": "3.13.3",
                  "NormalizedValue": "3.13.3"
                },
                "PackageReferenceType": "Direct"
              },
              {
                "Id": "NUnit3TestAdapter",
                "Version": {
                  "OriginalValue": "4.2.1",
                  "NormalizedValue": "4.2.1"
                },
                "PackageReferenceType": "Direct"
              },
              {
                "Id": "coverlet.collector",
                "Version": {
                  "OriginalValue": "3.1.2",
                  "NormalizedValue": "3.1.2"
                },
                "PackageReferenceType": "Direct"
              }
            ],
            "FileName": "Directory.build.props",
            "DirectoryName": "C:\\git\\dotnet-consolidate\\src\\DotNet.Consolidate.Tests\\TestData\\TestSolution\\tests"
          }
        ],
        "SolutionFile": ".\\src\\DotNet.Consolidate.Tests\\TestData\\TestSolution\\TestSolution.sln",
        "Solution": {
          "Header": [
            "",
            "Microsoft Visual Studio Solution File, Format Version 12.00",
            "# Visual Studio Version 17",
            "VisualStudioVersion = 17.6.33723.286",
            "MinimumVisualStudioVersion = 10.0.40219.1"
          ],
          "Global": [
            {
              "Name": "SolutionConfigurationPlatforms",
              "Type": "PreSolution",
              "Entries": {
                "Debug|x64": "Debug|x64",
                "Release|x64": "Release|x64"
              }
            },
            {
              "Name": "ProjectConfigurationPlatforms",
              "Type": "PostSolution",
              "Entries": {
                "{B2641D3B-1DE4-462D-BBBF-6874EFC3B7D0}.Debug|x64.ActiveCfg": "Debug|Any CPU",
                "{B2641D3B-1DE4-462D-BBBF-6874EFC3B7D0}.Debug|x64.Build.0": "Debug|Any CPU",
                "{B2641D3B-1DE4-462D-BBBF-6874EFC3B7D0}.Release|x64.ActiveCfg": "Release|Any CPU",
                "{B2641D3B-1DE4-462D-BBBF-6874EFC3B7D0}.Release|x64.Build.0": "Release|Any CPU",
                "{76DB371A-4871-417B-8CD5-38F4250050BF}.Debug|x64.ActiveCfg": "Debug|Any CPU",
                "{76DB371A-4871-417B-8CD5-38F4250050BF}.Debug|x64.Build.0": "Debug|Any CPU",
                "{76DB371A-4871-417B-8CD5-38F4250050BF}.Release|x64.ActiveCfg": "Release|Any CPU",
                "{76DB371A-4871-417B-8CD5-38F4250050BF}.Release|x64.Build.0": "Release|Any CPU",
                "{01777C80-CDE9-49C7-8FAD-590CAF9E8E23}.Debug|x64.ActiveCfg": "Debug|Any CPU",
                "{01777C80-CDE9-49C7-8FAD-590CAF9E8E23}.Debug|x64.Build.0": "Debug|Any CPU",
                "{01777C80-CDE9-49C7-8FAD-590CAF9E8E23}.Release|x64.ActiveCfg": "Release|Any CPU",
                "{01777C80-CDE9-49C7-8FAD-590CAF9E8E23}.Release|x64.Build.0": "Release|Any CPU",
                "{FA79411B-61D5-40E5-8E5C-B832E5E57BBD}.Debug|x64.ActiveCfg": "Debug|Any CPU",
                "{FA79411B-61D5-40E5-8E5C-B832E5E57BBD}.Debug|x64.Build.0": "Debug|Any CPU",
                "{FA79411B-61D5-40E5-8E5C-B832E5E57BBD}.Release|x64.ActiveCfg": "Release|Any CPU",
                "{FA79411B-61D5-40E5-8E5C-B832E5E57BBD}.Release|x64.Build.0": "Release|Any CPU"
              }
            },
            {
              "Name": "SolutionProperties",
              "Type": "PreSolution",
              "Entries": {
                "HideSolutionNode": "FALSE"
              }
            },
            {
              "Name": "NestedProjects",
              "Type": "PreSolution",
              "Entries": {
                "{B2641D3B-1DE4-462D-BBBF-6874EFC3B7D0}": "{91927A48-FE66-4083-8F03-EEAFA974C86C}",
                "{76DB371A-4871-417B-8CD5-38F4250050BF}": "{91927A48-FE66-4083-8F03-EEAFA974C86C}",
                "{01777C80-CDE9-49C7-8FAD-590CAF9E8E23}": "{5CD86CD0-0B02-47B4-9633-5C65875733FC}",
                "{FA79411B-61D5-40E5-8E5C-B832E5E57BBD}": "{5CD86CD0-0B02-47B4-9633-5C65875733FC}"
              }
            },
            {
              "Name": "ExtensibilityGlobals",
              "Type": "PostSolution",
              "Entries": {
                "SolutionGuid": "{40DE902E-DB50-4D7C-A519-E6DC61D0059A}"
              }
            }
          ],
          "Projects": [
            {
              "TypeGuid": "2150e333-8fdc-42a3-9474-1a3956d46de8",
              "Name": "src",
              "Path": "src",
              "Guid": "91927a48-fe66-4083-8f03-eeafa974c86c",
              "ProjectSection": null
            },
            {
              "TypeGuid": "2150e333-8fdc-42a3-9474-1a3956d46de8",
              "Name": "tests",
              "Path": "tests",
              "Guid": "5cd86cd0-0b02-47b4-9633-5c65875733fc",
              "ProjectSection": {
                "Name": "SolutionItems",
                "Type": "PreProject",
                "Entries": {
                  "tests\\Directory.build.props": "tests\\Directory.build.props"
                }
              }
            },
            {
              "TypeGuid": "9a19103f-16f7-4668-be54-9a1e7a4f7556",
              "Name": "ProjectA",
              "Path": "src\\ProjectA\\ProjectA.csproj",
              "Guid": "b2641d3b-1de4-462d-bbbf-6874efc3b7d0",
              "ProjectSection": null
            },
            {
              "TypeGuid": "9a19103f-16f7-4668-be54-9a1e7a4f7556",
              "Name": "ProjectB",
              "Path": "src\\ProjectB\\ProjectB.csproj",
              "Guid": "76db371a-4871-417b-8cd5-38f4250050bf",
              "ProjectSection": null
            },
            {
              "TypeGuid": "9a19103f-16f7-4668-be54-9a1e7a4f7556",
              "Name": "ProjectA.Tests",
              "Path": "tests\\ProjectA.Tests\\ProjectA.Tests.csproj",
              "Guid": "01777c80-cde9-49c7-8fad-590caf9e8e23",
              "ProjectSection": null
            },
            {
              "TypeGuid": "9a19103f-16f7-4668-be54-9a1e7a4f7556",
              "Name": "ProjectB.Tests",
              "Path": "tests\\ProjectB.Tests\\ProjectB.Tests.csproj",
              "Guid": "fa79411b-61d5-40e5-8e5c-b832e5e57bbd",
              "ProjectSection": null
            },
            {
              "TypeGuid": "2150e333-8fdc-42a3-9474-1a3956d46de8",
              "Name": "Solution Items",
              "Path": "Solution Items",
              "Guid": "cf9fa02d-aa0b-4a3a-8cf0-f1124fa43b43",
              "ProjectSection": {
                "Name": "SolutionItems",
                "Type": "PreProject",
                "Entries": {
                  "Directory.build.props": "Directory.build.props"
                }
              }
            }
          ]
        },
        "IsParsedWithoutIssues": true
      },
      "NonConsolidatedPackages": [
        {
          "ContainsDifferentPackagesVersions": true,
          "NuGetPackageId": "System.Text.Json",
          "PackageVersions": [
            {
              "NuGetPackageVersion": {
                "OriginalValue": "7.0.1",
                "NormalizedValue": "7.0.1"
              },
              "ProjectName": "ProjectA"
            },
            {
              "NuGetPackageVersion": {
                "OriginalValue": "7.0.2",
                "NormalizedValue": "7.0.2"
              },
              "ProjectName": "ProjectB"
            },
            {
              "NuGetPackageVersion": {
                "OriginalValue": "7.0.3",
                "NormalizedValue": "7.0.3"
              },
              "ProjectName": "ProjectA.Tests"
            },
            {
              "NuGetPackageVersion": {
                "OriginalValue": "7.0.4",
                "NormalizedValue": "7.0.4"
              },
              "ProjectName": "ProjectB.Tests"
            }
          ]
        },
        {
          "ContainsDifferentPackagesVersions": true,
          "NuGetPackageId": "System.Numerics",
          "PackageVersions": [
            {
              "NuGetPackageVersion": {
                "OriginalValue": "4.3.0",
                "NormalizedValue": "4.3"
              },
              "ProjectName": "ProjectA"
            },
            {
              "NuGetPackageVersion": {
                "OriginalValue": "4.0.1",
                "NormalizedValue": "4.0.1"
              },
              "ProjectName": "ProjectB"
            }
          ]
        }
      ]
    }
  ]
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
