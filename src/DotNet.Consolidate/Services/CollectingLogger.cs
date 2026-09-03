using System.Collections.Generic;

namespace DotNet.Consolidate.Services
{
    /// <summary>
    /// Collects messages instead of printing them, so a machine readable format can carry them inside its own
    /// output rather than mixing them into stdout or pushing them to stderr, which some CI systems treat as
    /// a failure.
    /// </summary>
    public class CollectingLogger : ILogger
    {
        private readonly List<string> _messages = new List<string>();

        public IReadOnlyCollection<string> Messages => _messages;

        public void Message(string message)
        {
            _messages.Add(message);
        }

        public void Progress(string message)
        {
            // Progress is a console affordance; a machine readable report has no use for it.
        }
    }
}
