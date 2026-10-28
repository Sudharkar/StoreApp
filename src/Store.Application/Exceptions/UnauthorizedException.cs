using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.Exceptions
{
    // Maps to HTTP 401 (bad login, invalid refresh token)
    public class UnauthorizedException : AppException
    {
        public UnauthorizedException(string message) : base(message) { }
    }
}
