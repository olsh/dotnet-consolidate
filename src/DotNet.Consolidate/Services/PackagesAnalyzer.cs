using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
                nonConsolidatedPackages = nonConsolidatedPackages.Where(p => options.PackageIds.Contains(p.NuGetPackageId)).ToList();
            }

            if (options.ExcludedPackageIds?.Any() == true)
            {
                nonConsolidatedPackages = nonConsolidatedPackages.Where(p => !options.ExcludedPackageIds.Contains(p.NuGetPackageId)).ToList();
            }

            if (!string.IsNullOrEmpty(options.ExcludedPackageVersionsRegex))
            {
                nonConsolidatedPackages = nonConsolidatedPackages
                    .Where(p => !p.PackageVersions.Any(version =>
                        Regex.IsMatch(version.NuGetPackageVersion.OriginalValue, options.ExcludedPackageVersionsRegex)));
            }

            return nonConsolidatedPackages.ToList();
        }
    }
}
