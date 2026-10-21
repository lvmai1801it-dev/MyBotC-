using System;

namespace MyBot.Core.Config;

public class ConfigValidationException : Exception
{
    public ConfigValidationException(string message) : base(message) { }
    public ConfigValidationException(string message, Exception inner) : base(message, inner) { }
}
