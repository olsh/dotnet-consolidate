using System;
using System.Collections.Generic;
using System.Linq;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services
{
    public class Logger : ILogger
    {
        public void Message(string message)
        {
            Console.WriteLine(message);
        }

        public void WriteAnalysisResults(List<AnalysisResult> packages)
        {
            if (packages.Any())
            {
                Console.WriteLine("Found {0} non-consolidated packages", packages.Count);
                Console.WriteLine();
            }

            foreach (var package in packages)
            {
                Console.WriteLine("----------------------------");
                Console.WriteLine(package.NuGetPackageId);
                Console.WriteLine("----------------------------");

                foreach (var packageVersion in package.PackageVersions.OrderBy(p => p.NuGetPackageVersion).ThenBy(p => p.ProjectName))
                {
                    Console.WriteLine("{0} - {1}", packageVersion.ProjectName, packageVersion.NuGetPackageVersion);
                }

                Console.WriteLine();
            }
        }
    }
}
