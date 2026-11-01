using System;

namespace Moonwell
{
    /// <summary>The error the runtime throws for a rule a modder broke, such as a failed development check.</summary>
    public sealed class MoonwellException : Exception
    {
        /// <summary>Makes the error with its message.</summary>
        internal MoonwellException(string message)
            : base(message)
        {
        }
    }
}
