namespace Claudestrap.Models.Persistable
{
    public class RobloxAccountsData
    {
        public List<RobloxAccount> Accounts { get; set; } = new();

        /// <summary>
        /// <see cref="RobloxAccount.Id"/> of the account picked last, so the launch
        /// menu dropdown comes back up on it next start instead of blank.
        /// </summary>
        public string? LastSelectedAccountId { get; set; }
    }
}
