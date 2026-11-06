namespace Moonwell
{
    /// <summary>
    /// The game's lightning, as World Editor names it, by the code the game takes. Drain Life, Drain Mana and Aerial
    /// Shackles stay drawn; Chain Lightning, Healing Wave, Spirit Link, Mana Burn, Mana Flare, Forked Lightning and Finger
    /// of Death fade by themselves at once (seen), so they are for Lightning.PlayOnce; the rest are not seen yet. Any other
    /// code of the map's own LightningData.slk works too.
    /// </summary>
    public static class LightningKind
    {
        /// <summary>Chain Lightning - Primary (CLPB): fades.</summary>
        public const string ChainLightningPrimary = "CLPB";

        /// <summary>Chain Lightning - Secondary (CLSB): fades.</summary>
        public const string ChainLightningSecondary = "CLSB";

        /// <summary>Mana Burn (MBUR): fades.</summary>
        public const string ManaBurn = "MBUR";

        /// <summary>Lightning Attack (CHIM), the Chimaera's.</summary>
        public const string LightningAttack = "CHIM";

        /// <summary>Finger of Death (AFOD): fades.</summary>
        public const string FingerOfDeath = "AFOD";

        /// <summary>Healing Wave - Primary (HWPB).</summary>
        public const string HealingWavePrimary = "HWPB";

        /// <summary>Healing Wave - Secondary (HWSB): fades.</summary>
        public const string HealingWaveSecondary = "HWSB";

        /// <summary>Mana Flare (MFPB): fades.</summary>
        public const string ManaFlare = "MFPB";

        /// <summary>Drain Life and Mana (DRAB).</summary>
        public const string DrainLifeAndMana = "DRAB";

        /// <summary>Drain Life (DRAL): stays drawn.</summary>
        public const string DrainLife = "DRAL";

        /// <summary>Drain Mana (DRAM): stays drawn.</summary>
        public const string DrainMana = "DRAM";

        /// <summary>Forked Lightning (FORK): fades.</summary>
        public const string ForkedLightning = "FORK";

        /// <summary>Spirit Link (SPLK): fades.</summary>
        public const string SpiritLink = "SPLK";

        /// <summary>Aerial Shackles (LEAS): stays drawn.</summary>
        public const string AerialShackles = "LEAS";

        /// <summary>Possession (POSS).</summary>
        public const string Possession = "POSS";
    }
}
