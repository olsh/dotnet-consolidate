using System;
using System.IO;

namespace DotNet.Consolidate.Services
{
    public static class PathUtils
    {
        // Directory paths are compared ordinally — the StringComparison-less StartsWith overload uses the
        // current culture, which has no business deciding whether one directory contains another — and
        // case-insensitively, so a casing difference between the path recorded in the solution file and the
        // on-disk directory name doesn't silently drop a project's inherited packages. Case-insensitive on
        // every platform, matching the MatchCasing.CaseInsensitive Directory.Build.props lookup in
        // SolutionInfoProvider and PackagesAnalyzer.PackageIdComparer; the cost is that two sibling
        // directories differing only in case can't be told apart on a case-sensitive file system.
        private const StringComparison DirectoryComparison = StringComparison.OrdinalIgnoreCase;

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

        /// <summary>
        /// Resolves a directory to an absolute path, so that two spellings of the same directory compare equal.
        /// </summary>
        /// <remarks>
        /// A project sitting next to a solution passed as a bare file name has no directory part at all, and
        /// <see cref="Path.GetFullPath(string)"/> rejects an empty string.
        /// </remarks>
        /// <param name="directory">The directory to resolve, absolute or relative to the working directory.</param>
        /// <returns>The absolute path of the directory.</returns>
        public static string ResolveDirectory(string directory)
        {
            return Path.GetFullPath(string.IsNullOrEmpty(directory) ? "." : directory);
        }

        /// <summary>
        /// Determines whether <paramref name="directory"/> is <paramref name="ancestorDirectory"/> itself,
        /// or sits underneath it.
        /// </summary>
        /// <remarks>
        /// A plain <c>StartsWith</c> is not enough: it also matches a sibling whose name merely starts with
        /// the same text, so a <c>Directory.Build.props</c> in <c>src/Project</c> would claim
        /// <c>src/ProjectB</c>. The match therefore has to end on a directory boundary.
        /// </remarks>
        /// <param name="directory">The directory to test, using the system path separator.</param>
        /// <param name="ancestorDirectory">The candidate ancestor, using the system path separator.</param>
        /// <returns><c>true</c> if the two are the same directory, or the first is below the second.</returns>
        public static bool IsSameOrUnderDirectory(string directory, string ancestorDirectory)
        {
            directory = Path.TrimEndingDirectorySeparator(directory);
            ancestorDirectory = Path.TrimEndingDirectorySeparator(ancestorDirectory);

            if (!directory.StartsWith(ancestorDirectory, DirectoryComparison))
            {
                return false;
            }

            if (directory.Length == ancestorDirectory.Length)
            {
                return true;
            }

            // A root ("C:\", "/") keeps its separator through the trim, so the boundary is already
            // part of the prefix that was just matched.
            return Path.EndsInDirectorySeparator(ancestorDirectory)
                   || IsDirectorySeparator(directory[ancestorDirectory.Length]);
        }

        private static bool IsDirectorySeparator(char character)
        {
            return character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar;
        }
    }
}
