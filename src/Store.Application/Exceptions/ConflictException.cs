using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.Exceptions
{
    // Maps to HTTP 409 (concurrency conflict, duplicate email)
    public class ConflictException : AppException
    {
        public ConflictException(string message) : base(message) { }
    }
}
