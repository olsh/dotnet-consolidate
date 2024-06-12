namespace DotNet.Consolidate.Services
{
    public interface ILogger
    {
        bool SupressMessages { get; set; }

        void Message(string message);
    }
}
