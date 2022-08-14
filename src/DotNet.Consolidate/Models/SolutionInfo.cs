using System.Collections.Generic;

namespace DotNet.Consolidate.Models;

public class SolutionInfo
{
    public SolutionInfo(string solutionFile, ICollection<ProjectInfo> projectInfos)
    {
        SolutionFile = solutionFile;
        ProjectInfos = projectInfos;
    }

    public ICollection<ProjectInfo> ProjectInfos { get; }

    public string SolutionFile { get; }
}