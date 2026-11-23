namespace SpareKey.Core
{
    /// <summary>
    /// One saved login in the vault. This is what the UI shows in the list.
    /// The whole list of these is turned into JSON and encrypted on save.
    /// </summary>
    public class VaultEntry
    {
        // A unique id so we can find an entry again even if the user
        // renames it. Never shown to the user.
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Title { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Url { get; set; } = "";
        public string Notes { get; set; } = "";

        // Stored in UTC so the times stay correct if you move to another
        // time zone. Convert to local time only when showing them.
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    }
}