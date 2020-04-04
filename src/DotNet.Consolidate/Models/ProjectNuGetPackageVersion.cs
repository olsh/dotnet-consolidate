namespace DotNet.Consolidate.Models
{
    public class ProjectNuGetPackageVersion
    {
        public ProjectNuGetPackageVersion(string projectName, string nuGetPackageVersion)
        {
            ProjectName = projectName;
            NuGetPackageVersion = nuGetPackageVersion;
        }

        public string NuGetPackageVersion { get; }

        public string ProjectName { get; }
    }
}
