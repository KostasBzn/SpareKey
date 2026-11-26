namespace SpareKey.Core
{
    /// <summary>
    /// Entry object
    /// </summary>
    public class VaultEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Title { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Url { get; set; } = "";
        public string Notes { get; set; } = "";

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    }
}