using System.Collections.Generic;

namespace DotNet.Consolidate.Models
{
    public class ProjectInfo
    {
        public ProjectInfo(string projectName, string projectDirectory, ICollection<NuGetPackageInfo> packages)
        {
            ProjectName = projectName;
            ProjectDirectory = projectDirectory;
            Packages = packages;
        }

        public ICollection<NuGetPackageInfo> Packages { get; }

        public string ProjectName { get; }

        public string ProjectDirectory { get; }
    }
}
