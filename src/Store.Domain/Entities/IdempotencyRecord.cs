using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Domain.Entities
{
    // Rule 3: same key + same user => return the original order
    public class IdempotencyRecord
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public int UserId { get; set; }
        public int OrderId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
