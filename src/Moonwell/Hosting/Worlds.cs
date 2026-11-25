#if !MOONWELL_LUA
using System;

namespace Moonwell.Hosting
{
    /// <summary>
    /// Where each package's Install passes the world it is given, in .NET only: the twin run sets Around to wrap every
    /// package world in one that writes down the calls that change the game, as FakeWorld.Around does for IWorld.
    /// </summary>
    internal static class Worlds
    {
        /// <summary>What a package keeps in place of the world it is given, when set: called with the world and the package's world interface.</summary>
        internal static Func<object, Type, object>? Around;

        /// <summary>The world a package keeps: the one given, or what Around makes of it.</summary>
        internal static T Wrap<T>(T world)
            where T : class
        {
            return Around == null ? world : (T)Around(world, typeof(T));
        }
    }
}
#endif
