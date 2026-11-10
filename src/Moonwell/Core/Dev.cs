using System.Diagnostics;

namespace Moonwell
{
    /// <summary>Checks that a development build makes while it runs; a release build leaves them out.</summary>
    internal static class Dev
    {
        /// <summary>Throws a MoonwellException with the message when the condition is false.</summary>
        [Conditional("DEBUG")]
        internal static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new MoonwellException(message);
            }
        }

        /// <summary>Reports a mistake as an error raised at the call, without throwing, so the call goes on as the game does.</summary>
        [Conditional("DEBUG")]
        internal static void Report(string label, string message)
        {
            try
            {
                throw new MoonwellException(message);
            }
            catch (MoonwellException error)
            {
                Errors.Warn(label, error);
            }
        }
    }
}
