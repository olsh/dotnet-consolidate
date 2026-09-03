using System.Collections.Generic;

using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace DotNet.Consolidate.Models
{
    public class SolutionInfo
    {
        public SolutionInfo(string solutionFile, SolutionModel? solution, ICollection<ProjectInfo> projectInfos, ICollection<DirectoryBuildPropsInfo> directoryBuildPropsInfos)
        {
            SolutionFile = solutionFile;
            Solution = solution;
            ProjectInfos = projectInfos;
            DirectoryBuildPropsInfos = directoryBuildPropsInfos;
        }

        public ICollection<ProjectInfo> ProjectInfos { get; }

        public ICollection<DirectoryBuildPropsInfo> DirectoryBuildPropsInfos { get; }

        public string SolutionFile { get; }

        /// <summary>
        /// Gets the solution information.
        /// If the value is null, the we unable to find the solution file or parse it correctly.
        /// </summary>
        public SolutionModel? Solution { get; }

        /// <summary>
        /// Gets a value indicating whether a solution file and its project were parsed correctly.
        /// <c>true</c> if the solution file and all its projects were parsed correctly, <c>false</c> otherwise.
        /// </summary>
        public bool IsParsedWithoutIssues => Solution != null && Solution.SolutionProjects.Count == ProjectInfos.Count;
    }
}
