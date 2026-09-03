using System;

namespace DotNet.Consolidate.Services
{
    public class Logger : ILogger
    {
        public void Message(string message)
        {
            Console.WriteLine(message);
        }

        public void Progress(string message)
        {
            Console.WriteLine(message);
        }
    }
}
