using System.IO;

namespace DotNet.Consolidate.Services;

public static class PathUtils
{
    /// <summary>
    /// Converts the path to use the correct system path separator.
    /// </summary>
    /// <param name="path">A path using any path separator.</param>
    /// <returns>The equivalent path using the <see cref="Path.DirectorySeparatorChar"/>.</returns>
    public static string EnsureSystemSeparator(string path)
    {
        if (Path.DirectorySeparatorChar != '\\')
        {
            return path.Replace('\\', Path.DirectorySeparatorChar);
        }
        else if (Path.DirectorySeparatorChar != '/')
        {
            return path.Replace('/', Path.DirectorySeparatorChar);
        }

        return path;
    }
}
