using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Domain.Entities
{
    public class RefreshToken
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }

        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRevoked { get; set; }

        public bool IsActive => !IsRevoked && ExpiresAt > DateTime.UtcNow;
    }
}
