#tool nuget:?package=MSBuild.SonarQube.Runner.Tool&version=4.8.0

#addin nuget:?package=Cake.Sonar&version=1.1.32

var target = Argument("target", "Default");

var buildConfiguration = "Release";
var projectName = "DotNet.Consolidate";
var testProjectName = "DotNet.Consolidate.Tests";
var projectFolder = string.Format("./src/{0}/", projectName);
var solutionFile = string.Format("./src/{0}.sln", projectName);
var projectFile = string.Format("./src/{0}/{0}.csproj", projectName);
var testProjectFile = string.Format("./src/{0}/{0}.csproj", testProjectName);
var nuGetPackageId = "dotnet-consolidate";

var extensionsVersion = XmlPeek(projectFile, "Project/PropertyGroup[1]/VersionPrefix/text()");

Task("UpdateBuildVersion")
  .WithCriteria(BuildSystem.AppVeyor.IsRunningOnAppVeyor)
  .Does(() =>
{
    var buildNumber = BuildSystem.AppVeyor.Environment.Build.Number;

    BuildSystem.AppVeyor.UpdateBuildVersion(string.Format("{0}.{1}", extensionsVersion, buildNumber));
});

Task("Build")
  .Does(() =>
{
    var settings = new DotNetBuildSettings
    {
        Configuration = buildConfiguration
    };

    DotNetBuild(solutionFile, settings);
});

Task("Test")
  .IsDependentOn("Build")
  .Does(() =>
{
     var settings = new DotNetTestSettings
     {
         Configuration = buildConfiguration
     };

     DotNetTest(testProjectFile, settings);
});

Task("SonarBegin")
  .Does(() => {
     SonarBegin(new SonarBeginSettings {
        Url = "https://sonarcloud.io",
        Login = EnvironmentVariable("sonar:apikey"),
        Key = "dotnet-consolidate",
        Name = "dotnet consolidate",
        ArgumentCustomization = args => args
            .Append("/o:olsh-github"),
        Version = extensionsVersion
     });
  });

Task("SonarEnd")
  .Does(() => {
     SonarEnd(new SonarEndSettings {
        Login = EnvironmentVariable("sonar:apikey")
     });
  });

Task("NugetPack")
  .Does(() =>
{
     var settings = new DotNetPackSettings
     {
         Configuration = buildConfiguration,
         OutputDirectory = "."
     };

     DotNetPack(projectFolder, settings);
});

Task("CreateArtifact")
  .IsDependentOn("NugetPack")
  .WithCriteria(BuildSystem.AppVeyor.IsRunningOnAppVeyor)
  .Does(() =>
{
    BuildSystem.AppVeyor.UploadArtifact(string.Format("{0}.{1}.nupkg", nuGetPackageId, extensionsVersion));
});

Task("Default")
    .IsDependentOn("NugetPack");

Task("Sonar")
  .IsDependentOn("SonarBegin")
  .IsDependentOn("Build")
  .IsDependentOn("Test")
  .IsDependentOn("SonarEnd");

Task("CI")
    .IsDependentOn("UpdateBuildVersion")
    .IsDependentOn("Sonar")
    .IsDependentOn("CreateArtifact");

RunTarget(target);
