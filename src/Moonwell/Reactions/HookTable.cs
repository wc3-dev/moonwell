using System;

namespace Moonwell
{
    /// <summary>The reactions a base class declares: each name and its bit, and the base that declares them.</summary>
    internal sealed class HookTable
    {
        /// <summary>The base class whose reactions are listed; a method declared here is not an override.</summary>
        internal readonly Type Base;

        /// <summary>The method name of each reaction.</summary>
        internal readonly string[] Names;

        /// <summary>The bit of each reaction, in the same order as Names.</summary>
        internal readonly int[] Bits;

        /// <summary>The method name of each reaction whose bit is in Hook2.</summary>
        internal readonly string[] Names2;

        /// <summary>The Hook2 bit of each reaction, in the same order as Names2.</summary>
        internal readonly int[] Bits2;

        /// <summary>The table of the base's own base class, whose reactions a class below this base also has (Unit's, below Hero); null when none.</summary>
        internal readonly HookTable? Parent;

        /// <summary>Makes a table for a base class's reactions.</summary>
        internal HookTable(Type declaringBase, string[] names, int[] bits, HookTable? parent = null, string[]? names2 = null, int[]? bits2 = null)
        {
            Base = declaringBase;
            Names = names;
            Bits = bits;
            Parent = parent;
            Names2 = names2 ?? new string[0];
            Bits2 = bits2 ?? new int[0];
        }
    }
}
