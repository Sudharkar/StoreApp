using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Infrastructure.Auth
{
    public class JwtSettings
    {
        public const string SectionName = "Jwt";

        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = "StoreApi";
        public string Audience { get; set; } = "StoreClient";
        public int AccessTokenMinutes { get; set; } = 15;
        public int RefreshTokenDays { get; set; } = 7;
    }
}
