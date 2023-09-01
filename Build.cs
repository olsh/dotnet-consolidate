using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.SignTool;
using Nuke.Common.Utilities.Collections;
using Nuke.Common;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.IO;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.SignTool;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.IO;
using Nuke.Common.IO;
using Nuke.Common;
using static Nuke.Common.ControlFlow;
using static Nuke.Common.Logger;
using static Nuke.Common.IO.CompressionTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;
using static Nuke.Common.Tools.SignTool.SignToolTasks;
using static Nuke.Common.Tools.NuGet.NuGetTasks;
using static Nuke.Common.IO.TextTasks;
using static Nuke.Common.IO.XmlTasks;
using static Nuke.Common.EnvironmentInfo;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Default);
    string BuildConfiguration = "Release";
    string ProjectName = "DotNet.Consolidate";
    string TestProjectName = "DotNet.Consolidate.Tests";
    AbsolutePath ProjectFolder => string.Format(RootDirectory / "src" / "{0}", projectName);
    AbsolutePath SolutionFile => string.Format(RootDirectory / "src" / "{0}.sln", projectName);
    AbsolutePath ProjectFile => string.Format(RootDirectory / "src" / "{0}" / "{0}.csproj", projectName);
    AbsolutePath TestProjectFile => string.Format(RootDirectory / "src" / "{0}" / "{0}.csproj", testProjectName);
    string NuGetPackageId = "dotnet-consolidate";
    AbsolutePath ExtensionsVersion => XmlPeek(projectFile, RootDirectory / "Project" / "PropertyGroup[1]" / "VersionPrefix" / "text()");

    Target UpdateBuildVersion => _ => _
        .OnlyWhenStatic(() => AppVeyor.IsRunningOnAppVeyor)
        .Executes(() =>
    {
        var buildNumber = AppVeyor.Environment.Build.Number;
        AppVeyor.UpdateBuildVersion(string.Format("{0}.{1}", ExtensionsVersion, buildNumber));
    });

    Target Build => _ => _
        .Executes(() =>
    {
        var settings = new DotNetBuildSettings{Configuration = BuildConfiguration};
        DotNetBuild(SolutionFile, settings);
    });

    Target Test => _ => _
        .DependsOn(Build)
        .Executes(() =>
    {
        var settings = new DotNetTestSettings{Configuration = BuildConfiguration};
        DotNetTest(TestProjectFile, settings);
    });

    Target SonarBegin => _ => _
        .Executes(() =>
    {
        SonarBegin(new SonarBeginSettings{Url = "https://sonarcloud.io", Login = GetVariable<string>("sonar:apikey"), Key = "dotnet-consolidate", Name = "dotnet consolidate", ArgumentCustomization = args => args.Add("/o:olsh"), Version = ExtensionsVersion});
    });

    Target SonarEnd => _ => _
        .Executes(() =>
    {
        SonarEnd(new SonarEndSettings{Login = GetVariable<string>("sonar:apikey")});
    });

    Target NugetPack => _ => _
        .Executes(() =>
    {
        var settings = new DotNetPackSettings{Configuration = BuildConfiguration, OutputDirectory = "."};
        DotNetPack(ProjectFolder, settings);
    });

    Target CreateArtifact => _ => _
        .DependsOn(NugetPack)
        .OnlyWhenStatic(() => AppVeyor.IsRunningOnAppVeyor)
        .Executes(() =>
    {
        AppVeyor.UploadArtifact(string.Format("{0}.{1}.nupkg", NuGetPackageId, ExtensionsVersion));
    });

    Target Default => _ => _
        .DependsOn(NugetPack);

    Target Sonar => _ => _
        .DependsOn(SonarBegin)
        .DependsOn(Build)
        .DependsOn(Test)
        .DependsOn(SonarEnd);

    Target CI => _ => _
        .DependsOn(UpdateBuildVersion)
        .DependsOn(Sonar)
        .DependsOn(CreateArtifact);
}
