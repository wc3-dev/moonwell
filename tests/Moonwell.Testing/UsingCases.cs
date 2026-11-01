using System;
using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases for several values in one using, which the core disposed in the order they were made until CoreSystem 2.0.3-moonwell.6 (C44).</summary>
    public static class UsingCases
    {
        /// <summary>
        /// Two variables in one using are disposed last first, in a declaration and in a using statement, with a null one left
        /// out, and when a Dispose throws the others still run and the last failure is the one thrown.
        /// </summary>
        public static void InOneStatement(FakeWorld world)
        {
            var log = new List<string>();
            OneDeclaration(log);
            Expect.Equal("body,second,first", string.Join(",", log), "a declaration");

            log.Clear();
            OneUsingStatement(log);
            Expect.Equal("body,second,first", string.Join(",", log), "a using statement");

            log.Clear();
            WithANull(log);
            Expect.Equal("body,first", string.Join(",", log), "a null one");

            log.Clear();
            try
            {
                BothFail(log);
            }
            catch (Exception e)
            {
                log.Add(e.Message);
            }

            Expect.Equal("body,second,first,first failed", string.Join(",", log), "both fail");
        }

        private static void OneDeclaration(List<string> log)
        {
            using Labelled first = new Labelled(log, "first"), second = new Labelled(log, "second");
            log.Add("body");
        }

        private static void OneUsingStatement(List<string> log)
        {
            using (Labelled first = new Labelled(log, "first"), second = new Labelled(log, "second"))
            {
                log.Add("body");
            }
        }

        private static void WithANull(List<string> log)
        {
            using Labelled? first = new Labelled(log, "first"), second = null;
            log.Add("body");
        }

        private static void BothFail(List<string> log)
        {
            using Failing first = new Failing(log, "first"), second = new Failing(log, "second");
            log.Add("body");
        }

        private sealed class Labelled(List<string> log, string label) : IDisposable
        {
            public void Dispose()
            {
                log.Add(label);
            }
        }

        private sealed class Failing(List<string> log, string label) : IDisposable
        {
            public void Dispose()
            {
                log.Add(label);
                throw new InvalidOperationException(label + " failed");
            }
        }
    }
}
