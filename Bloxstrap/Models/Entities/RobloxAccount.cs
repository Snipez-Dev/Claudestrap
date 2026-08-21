namespace Claudestrap.Models.Entities
{
    public class RobloxAccount
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Username { get; set; } = "";

        public string UserId { get; set; } = "";

        /// <summary>DPAPI-protected (current user scope), base64-encoded .ROBLOSECURITY cookie.</summary>
        public string EncryptedCookie { get; set; } = "";

        public DateTime AddedUtc { get; set; } = DateTime.UtcNow;

        public DateTime? LastUsedUtc { get; set; }
    }
}
