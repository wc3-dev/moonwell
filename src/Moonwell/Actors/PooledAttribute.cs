using System;

namespace Moonwell
{
    /// <summary>
    /// Keeps the class's units when they end, hidden, and gives them out again from Unit.Create, which costs the game about a
    /// tenth of making one (decision 220). A unit kept is reset: its items removed, its buffs taken off, its life and mana as
    /// the game makes them, and a new object of the class with new components. A dead unit, a hero and an illusion are never
    /// kept. Anything else code changed on the unit itself (its scale, colour, abilities added with natives) stays.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PooledAttribute : Attribute
    {
    }
}
