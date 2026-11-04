using System;

namespace Moonwell.Json
{
    /// <summary>Text that <see cref="JsonNode.Parse"/> cannot read: no JSON, or nested deeper than 64.</summary>
    public sealed class JsonException : Exception
    {
        /// <summary>Makes the exception for a problem at a place in the text.</summary>
        public JsonException(string message, int position)
            : base(message + ", at " + position)
        {
            Position = position;
        }

        /// <summary>Where in the text the reading stopped, from 0: a character on .NET, a byte in the game.</summary>
        public int Position { get; }
    }
}
