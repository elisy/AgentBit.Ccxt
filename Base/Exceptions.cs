using System;
using System.Collections.Generic;
using System.Text;

namespace AgentBit.Ccxt.Base
{
    public class ExchangeError : Exception
    {
        public ExchangeError() : base()
        {
        }

        public ExchangeError(string? message) : base(message)
        {
        }

        public ExchangeError(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }

    public class AuthenticationError : Exception
    {
    }
    public class ArgumentsRequired : Exception
    {
    }
    public class BadRequest : Exception
    {
    }
    public class InsufficientFunds : Exception
    {
    }
    public class InvalidOrder : Exception
    {
    }
    public class OrderNotFound : Exception
    {
    }
    public class DDoSProtection : Exception
    {
    }
    public class RateLimitExceeded : Exception
    {
    }
    public class ExchangeNotAvailable : Exception
    {
    }
}
