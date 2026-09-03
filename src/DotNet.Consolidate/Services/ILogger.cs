namespace DotNet.Consolidate.Services
{
    public interface ILogger
    {
        /// <summary>
        /// Reports something the user needs to know about: a file that couldn't be parsed, an unusable argument,
        /// a result that may be incomplete.
        /// </summary>
        void Message(string message);

        /// <summary>
        /// Reports how far along the run is. Purely cosmetic, so a machine readable format may drop it.
        /// </summary>
        void Progress(string message);
    }
}
