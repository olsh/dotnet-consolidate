using System.Collections.Generic;

namespace DotNet.Consolidate.Models;

public class ProjectInfo
{
    public ProjectInfo(string projectName, ICollection<NuGetPackageInfo> packages)
    {
        ProjectName = projectName;
        Packages = packages;
    }

    public ICollection<NuGetPackageInfo> Packages { get; }

    public string ProjectName { get; }
}