namespace DotNet.Consolidate.Models
{
    public class ProjectNuGetPackageVersion
    {
        public ProjectNuGetPackageVersion(string projectName, Version nuGetPackageVersion)
        {
            ProjectName = projectName;
            NuGetPackageVersion = nuGetPackageVersion;
        }

        public Version NuGetPackageVersion { get; }

        public string ProjectName { get; }
    }
}
