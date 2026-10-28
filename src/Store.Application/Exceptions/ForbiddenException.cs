using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.Exceptions
{
    // Maps to HTTP 403 (e.g. customer trying to view someone else's order)
    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message) : base(message) { }
    }
}
