using System;

namespace DotNet.Consolidate.Services
{
    public class Logger : ILogger
    {
        public bool SupressMessages { get; set; }

        public void Message(string message)
        {
            if (SupressMessages)
            {
                return;
            }

            Console.WriteLine(message);
        }
    }
}
