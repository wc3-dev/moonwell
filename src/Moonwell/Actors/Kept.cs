namespace Moonwell
{
    /// <summary>
    /// The bit of each member of a unit, item or destructable whose last value Moonwell keeps for when the game has
    /// removed it. A development build marks the ones it holds a value for (Widget.Keep), so that it can report a read
    /// that has none (Widget.CheckKept, decision 270).
    /// </summary>
    internal static class Kept
    {
        internal const int Life = 1;
        internal const int MaxLife = 2;
        internal const int Mana = 4;
        internal const int MaxMana = 8;
        internal const int X = 16;
        internal const int Y = 32;
        internal const int Facing = 64;
        internal const int Level = 128;
        internal const int Name = 256;
        internal const int IsPaused = 512;
        internal const int Scale = 1024;
        internal const int FlyHeight = 2048;
        internal const int InventorySize = 4096;
        internal const int ProperName = 8192;
        internal const int Experience = 16384;
        internal const int Strength = 32768;
        internal const int Agility = 65536;
        internal const int Intelligence = 131072;
        internal const int StrengthWithBonus = 262144;
        internal const int AgilityWithBonus = 524288;
        internal const int IntelligenceWithBonus = 1048576;
        internal const int SkillPoints = 2097152;
        internal const int Charges = 4194304;
    }
}
