using System;
using System.Collections.Generic;
using System.Text;

namespace Store.Application.Exceptions
{
    // Base class for all expected business errors
    public abstract class AppException : Exception
    {
        protected AppException(string message) : base(message) { }
    }
}
