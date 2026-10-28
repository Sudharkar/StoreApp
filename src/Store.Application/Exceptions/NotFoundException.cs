using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.Exceptions
{
    // Maps to HTTP 404
    public class NotFoundException : AppException
    {
        public NotFoundException(string message) : base(message) { }
    }
}
