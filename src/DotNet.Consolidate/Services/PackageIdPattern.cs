using System.Collections.Generic;
using System.Linq;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services
{
    /// <summary>
    /// Decides whether a package ID is named by one of the entries given to <c>-p</c> or <c>-e</c>, each of
    /// which is either a package ID or a pattern using <c>*</c> for any run of characters and <c>?</c> for
    /// exactly one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Static and allocation-free, like the other stateless services here: a pattern is scanned against an ID
    /// directly, so there is nothing to compile up front and nothing to hold on to between calls.
    /// </para>
    /// <para>
    /// An entry containing neither wildcard never reaches the scan at all — it is answered by
    /// <see cref="NuGetPackageInfo.IdComparer"/>, so every command line that worked before this class existed
    /// is still matched by exactly the comparer the rest of the tool uses, and the invariant recorded on that
    /// comparer still holds.
    /// </para>
    /// <para>
    /// Neither wildcard needs an escape syntax, because neither can appear in a NuGet package ID — those are
    /// word characters separated by <c>.</c>, <c>_</c> and <c>-</c>. So nothing that matched before can stop
    /// matching, and an entry that reads as a pattern could never have named a real package.
    /// </para>
    /// <para>
    /// Translating the pattern to a <see cref="System.Text.RegularExpressions.Regex"/> was the obvious
    /// alternative, and was rejected on two counts. Regular expression case folding is close to, but not,
    /// <see cref="System.StringComparer.OrdinalIgnoreCase"/> — invariant folding maps a few non-ASCII
    /// characters that ordinal folding leaves alone — so the tool would end up with two notions of "the same
    /// package ID", which is the drift <see cref="NuGetPackageInfo.IdComparer"/> exists to prevent. And a
    /// generated pattern such as <c>-p "*a*a*a*a*"</c> becomes chained <c>.*</c>, the one shape a backtracking
    /// engine is worst at, which would then have to be bounded by a timeout or held off with
    /// <c>RegexOptions.NonBacktracking</c>. The scan below has neither problem by construction.
    /// </para>
    /// <para>
    /// There is deliberately no notion of an empty set of entries: "nothing given" means <i>everything</i> to
    /// <c>-p</c> and <i>nothing</i> to <c>-e</c>, so that decision stays with the caller that knows which
    /// option it is holding.
    /// </para>
    /// </remarks>
    public static class PackageIdPattern
    {
        /// <summary>
        /// The character that stands for any run of characters, including none.
        /// </summary>
        private const char AnyCharacters = '*';

        /// <summary>
        /// The character that stands for exactly one character.
        /// </summary>
        private const char SingleCharacter = '?';

        private static readonly char[] Wildcards = { AnyCharacters, SingleCharacter };

        /// <summary>
        /// Determines whether a <c>-p</c> or <c>-e</c> entry names the given package ID.
        /// </summary>
        /// <param name="pattern">The entry as the user typed it, with or without wildcards.</param>
        /// <param name="packageId">The package ID to test it against.</param>
        /// <returns><c>true</c> if the entry names the package.</returns>
        public static bool IsMatch(string pattern, string packageId)
        {
            return pattern.IndexOfAny(Wildcards) < 0
                ? NuGetPackageInfo.IdComparer.Equals(pattern, packageId)
                : IsWildcardMatch(pattern, packageId);
        }

        /// <summary>
        /// Determines whether any of the entries names the given package ID.
        /// </summary>
        /// <param name="patterns">The entries as the user typed them, with or without wildcards.</param>
        /// <param name="packageId">The package ID to test them against.</param>
        /// <returns><c>true</c> if at least one entry names the package.</returns>
        public static bool IsMatchAny(IReadOnlyCollection<string> patterns, string packageId)
        {
            return patterns.Any(pattern => IsMatch(pattern, packageId));
        }

        /// <summary>
        /// Matches a pattern that is known to contain at least one wildcard.
        /// </summary>
        /// <remarks>
        /// The greedy scan: characters are consumed in step until they disagree, and the only place the match
        /// can be resumed from is the most recent <c>*</c>, which is why one saved position is enough — an
        /// earlier <c>*</c> can always absorb whatever a later one gives back. Anchored at both ends by
        /// construction, since the loop has to consume the whole ID and the pattern has to be spent when it
        /// does.
        /// </remarks>
        // ReSharper disable once CognitiveComplexity
        private static bool IsWildcardMatch(string pattern, string packageId)
        {
            var patternIndex = 0;
            var packageIdIndex = 0;

            // The most recent `*` and the position it was allowed to start absorbing from, or -1 while no
            // `*` has been seen and there is therefore nothing to fall back to.
            var lastAnyCharactersIndex = -1;
            var resumeIndex = 0;

            while (packageIdIndex < packageId.Length)
            {
                if (patternIndex < pattern.Length
                    && (pattern[patternIndex] == SingleCharacter
                        || AreEqual(pattern[patternIndex], packageId[packageIdIndex])))
                {
                    patternIndex++;
                    packageIdIndex++;
                }
                else if (patternIndex < pattern.Length && pattern[patternIndex] == AnyCharacters)
                {
                    lastAnyCharactersIndex = patternIndex;
                    resumeIndex = packageIdIndex;
                    patternIndex++;
                }
                else if (lastAnyCharactersIndex >= 0)
                {
                    // Let the `*` absorb one more character and try the rest of the pattern again.
                    patternIndex = lastAnyCharactersIndex + 1;
                    resumeIndex++;
                    packageIdIndex = resumeIndex;
                }
                else
                {
                    return false;
                }
            }

            // Trailing `*`s have nothing left to match and are satisfied by matching nothing.
            while (patternIndex < pattern.Length && pattern[patternIndex] == AnyCharacters)
            {
                patternIndex++;
            }

            return patternIndex == pattern.Length;
        }

        /// <remarks>
        /// <see cref="char.ToUpperInvariant"/> is the simple case folding
        /// <see cref="System.StringComparer.OrdinalIgnoreCase"/> applies, so a wildcard match treats casing
        /// exactly as the literal path above — and as every other package ID comparison in the tool — does.
        /// </remarks>
        private static bool AreEqual(char patternCharacter, char packageIdCharacter)
        {
            return char.ToUpperInvariant(patternCharacter) == char.ToUpperInvariant(packageIdCharacter);
        }
    }
}
