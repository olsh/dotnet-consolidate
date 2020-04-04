using System.Collections.Generic;
using System.Linq;

namespace DotNet.Consolidate.Models
{
    public class AnalysisResult
    {
        public AnalysisResult(string nuGetPackageId)
        {
            NuGetPackageId = nuGetPackageId;
            PackageVersions = new List<ProjectNuGetPackageVersion>();
        }

        public bool ContainsDifferentPackagesVersions =>
            PackageVersions.GroupBy(p => p.NuGetPackageVersion)
                .Count() > 1;

        public string NuGetPackageId { get; }

        public ICollection<ProjectNuGetPackageVersion> PackageVersions { get; }
    }
}
