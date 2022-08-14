using System.Collections.Generic;

namespace DotNet.Consolidate.Services;

public interface ISolutionFileProvider
{
    ICollection<string> FindSolutionsInCurrentDirectory();
}
