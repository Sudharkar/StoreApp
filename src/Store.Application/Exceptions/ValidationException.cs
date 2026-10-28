using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.Exceptions
{
    // Maps to HTTP 400 (invalid input, invalid status transition, insufficient stock)
    public class ValidationException : AppException
    {
        public ValidationException(string message) : base(message) { }
    }
}
