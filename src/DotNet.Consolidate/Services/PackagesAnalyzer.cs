using System.Collections.Generic;
using System.Linq;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services
{
    public class PackagesAnalyzer
    {
        public List<AnalysisResult> FindNonConsolidatedPackages(ICollection<ProjectInfo> projectInfos, Options options)
        {
            var analysisResults = new Dictionary<string, AnalysisResult>();
            foreach (var projectInfo in projectInfos)
            {
                foreach (var packageInfo in projectInfo.Packages)
                {
                    if (!analysisResults.TryGetValue(packageInfo.Id, out var analysisResult))
                    {
                        analysisResult = new AnalysisResult(packageInfo.Id);
                        analysisResults.Add(packageInfo.Id, analysisResult);
                    }

                    analysisResult.PackageVersions.Add(new ProjectNuGetPackageVersion(projectInfo.ProjectName, packageInfo.Version));
                }
            }

            var nonConsolidatedPackages = analysisResults.Values.Where(r => r.ContainsDifferentPackagesVersions);
            if (options.PackageIds?.Any() == true)
            {
                return nonConsolidatedPackages.Where(p => options.PackageIds.Contains(p.NuGetPackageId)).ToList();
            }

            return nonConsolidatedPackages.ToList();
        }
    }
}
