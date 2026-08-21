using System.Security.Cryptography;

namespace Claudestrap.Utility
{
    /// <summary>
    /// Encrypts saved .ROBLOSECURITY cookies at rest with DPAPI, scoped to the
    /// current Windows user -- the same protection level Chrome/Edge use for
    /// their own saved cookies. Decrypting the accounts JSON file off this
    /// machine, or as a different Windows user, yields nothing usable.
    /// </summary>
    public static class AccountCookieProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Claudestrap.RobloxAccount.v1");

        public static string Protect(string plainCookie)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainCookie);
            byte[] encrypted = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        public static string? Unprotect(string encryptedBase64)
        {
            try
            {
                byte[] encrypted = Convert.FromBase64String(encryptedBase64);
                byte[] plainBytes = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return null;
            }
        }
    }
}
