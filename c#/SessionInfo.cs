using System;

namespace Safety
{
    public sealed class SessionInfo
    {
        public string DiscordId { get; private set; }
        public string LicenseKey { get; private set; }
        public string Token { get; private set; }      // keep
        public string UserName { get; private set; }   // keep
        public string DiscordUsername { get; private set; }
        public string DiscordGlobalName { get; private set; }
        public string DiscordAvatarUrl { get; private set; }
        public string ProductHash { get; private set; }
        public DateTime? ExpiresAt { get; private set; }

        private SessionInfo() { }

        public static SessionInfo Create(
            string discordId,
            string licenseKey,
            DateTime? expiresAt,
            string token,
            string userName,
            string productHash = null)
        {
            return new SessionInfo
            {
                DiscordId = discordId,
                LicenseKey = licenseKey,
                ExpiresAt = expiresAt,
                ProductHash = productHash,

                Token = token ?? string.Empty,
                UserName = userName ?? string.Empty
            };
        }
    }
}
