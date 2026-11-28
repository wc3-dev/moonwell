using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// The object data's target lists (an attack's Targets Allowed, an ability's, a unit's Targeted As) as the game reads them:
    /// bit flags in the game's own numbers, which BlzGetUnitWeaponIntegerField and BlzGetUnitIntegerField give, and the
    /// rules by which a list takes or refuses a unit.
    /// </summary>
    internal static class TargetLists
    {
        internal const int None = 1;
        internal const int Ground = 2;
        internal const int Air = 4;
        internal const int Structure = 8;
        internal const int Ward = 16;
        internal const int Item = 32;
        internal const int Tree = 64;
        internal const int Wall = 128;
        internal const int Debris = 256;
        internal const int Decoration = 512;
        internal const int Bridge = 1024;
        internal const int Self = 4096;
        internal const int Player = 8192;
        internal const int Allies = 16384;
        internal const int Neutral = 32768;
        internal const int Enemies = 65536;
        internal const int Vulnerable = 1 << 20;
        internal const int Invulnerable = 1 << 21;
        internal const int Hero = 1 << 22;
        internal const int NonHero = 1 << 23;
        internal const int Alive = 1 << 24;
        internal const int Dead = 1 << 25;
        internal const int Organic = 1 << 26;
        internal const int Mechanical = 1 << 27;
        internal const int NonSapper = 1 << 28;
        internal const int Sapper = 1 << 29;
        internal const int NonAncient = 1 << 30;
        internal const int Ancient = 1 << 31;

        /// <summary>Friend: the player's own and its allies' (24576, seen 11-28).</summary>
        internal const int Friend = Player | Allies;

        /// <summary>Not self: every owner but the unit itself, no flag of its own (122880, seen 11-28).</summary>
        internal const int NotSelf = Player | Allies | Neutral | Enemies;

        /// <summary>The kinds of thing a list names; a unit's Targeted As holds the ones it is.</summary>
        internal const int Kinds = Ground | Air | Structure | Ward;

        /// <summary>Whose a target is.</summary>
        internal const int Owners = Self | Player | Allies | Neutral | Enemies;

        /// <summary>The words of a list in the object data, each with its flags; a word may be written in full or as its first four letters, as the game's data has both. Made on first use.</summary>
        private static Dictionary<string, int>? words;

        /// <summary>The flags of a list written as the object data writes it: words joined by commas ("air,ground,enemies"); a word the game has no flag for is left out, and an empty text has none.</summary>
        internal static int Parse(string? text)
        {
            if (text == null || text.Length == 0)
            {
                return 0;
            }

            words ??= Words();
            var flags = 0;
            var start = 0;
            while (start <= text.Length)
            {
                var comma = text.IndexOf(',', start);
                var end = comma < 0 ? text.Length : comma;
                if (words.TryGetValue(text.Substring(start, end - start).Trim().ToLower(), out var bits))
                {
                    flags |= bits;
                }

                start = end + 1;
            }

            return flags;
        }

        private static void Add(Dictionary<string, int> found, int bits, string name, string? shortName = null, string? other = null)
        {
            found[name] = bits;
            if (shortName != null)
            {
                found[shortName] = bits;
            }

            if (other != null)
            {
                found[other] = bits;
            }
        }

        private static Dictionary<string, int> Words()
        {
            var found = new Dictionary<string, int>();
            Add(found, None, "none");
            Add(found, Ground, "ground", "grou");
            Add(found, Air, "air");
            Add(found, Structure, "structure", "stru");
            Add(found, Ward, "ward");
            Add(found, Item, "item");
            Add(found, Tree, "tree");
            Add(found, Wall, "wall");
            Add(found, Debris, "debris", "debr");
            Add(found, Decoration, "decoration", "deco");
            Add(found, Bridge, "bridge");
            Add(found, Self, "self");
            Add(found, Player, "player", "play");
            Add(found, Allies, "allies", "alli", "ally");
            Add(found, Friend, "friend", "frie");
            Add(found, Neutral, "neutral", "neut");
            Add(found, Enemies, "enemies", "enem", "enemy");
            Add(found, NotSelf, "notself", "nots");
            Add(found, Vulnerable, "vulnerable", "vuln");
            Add(found, Invulnerable, "invulnerable", "invu");
            Add(found, Hero, "hero");
            Add(found, NonHero, "nonhero", "nonh");
            Add(found, Alive, "alive", "aliv");
            Add(found, Dead, "dead");
            Add(found, Organic, "organic", "orga");
            Add(found, Mechanical, "mechanical", "mech");
            Add(found, NonSapper, "nonsapper");
            Add(found, Sapper, "sapper");
            Add(found, NonAncient, "nonancient");
            Add(found, Ancient, "ancient");
            return found;
        }

        /// <summary>What an empty list takes: ground, air and structures, not wards (an attack's, seen 11-28; a spell's took the first three, its ward was immune to spells).</summary>
        internal const int EmptyKinds = Ground | Air | Structure;

        /// <summary>
        /// Whether a list takes a unit as a target of a unit's, as the game took orders in probe-targets (11-28): the kind it is
        /// targeted as, whose it is, alive or dead, vulnerable or not, and a hero, a machine, a sapper or an ancient, each read live
        /// from the game. A pair the list names neither side of takes both, but for life, which takes the living, and vulnerability,
        /// which takes the vulnerable. An attack (attack true) takes any owner whatever the list names, and never a corpse.
        /// </summary>
        internal static bool Allows(int list, Unit source, Unit target, bool attack)
        {
            var world = Runtime.World;
            var handle = target.Handle;
            if (((list == 0 ? EmptyKinds : list) & world.TargetedAsOf(handle) & Kinds) == 0)
            {
                return false;
            }

            if (!attack && (list & Owners) != 0 && (list & OwnerOf(source, target)) == 0)
            {
                return false;
            }

            // A list that names neither alive nor dead takes the living; neither vulnerable nor invulnerable, the vulnerable. An
            // attack whose list names dead alone takes nothing: never a corpse, and not the living it does not name.
            var alive = world.IsAlive(handle);
            if (!((list & (Alive | Dead)) == 0 ? alive : Either(list, Dead, Alive, !alive)) || (attack && !alive))
            {
                return false;
            }

            var invulnerable = world.IsInvulnerable(handle);
            if (!((list & (Vulnerable | Invulnerable)) == 0 ? !invulnerable : Either(list, Invulnerable, Vulnerable, invulnerable)))
            {
                return false;
            }

            return Either(list, Hero, NonHero, world.IsHero(handle))
                && Either(list, Mechanical, Organic, world.IsKind(handle, UnitKind.Mechanical))
                && Either(list, Sapper, NonSapper, world.IsKind(handle, UnitKind.Suicidal))
                && Either(list, Ancient, NonAncient, world.IsKind(handle, UnitKind.Ancient));
        }

        /// <summary>
        /// The owner flag a target has to a unit: itself, its player's, Neutral Passive's (which the game counts as neutral though
        /// IsPlayerAlly reads it an ally), an ally's, or an enemy's, Neutral Hostile's among them (seen 11-28).
        /// </summary>
        private static int OwnerOf(Unit source, Unit target)
        {
            if (target == source)
            {
                return Self;
            }

            var world = Runtime.World;
            var mine = world.OwnerOf(source.Handle);
            var theirs = world.OwnerOf(target.Handle);
            if (mine == theirs)
            {
                return Player;
            }

            if (world.SlotOf(theirs) == world.NeutralPassiveSlot())
            {
                return Neutral;
            }

            return world.IsAllyOf(mine, theirs) ? Allies : Enemies;
        }

        /// <summary>Whether a pair of flags takes a target that is the first (is) or the second: the side the list names, or either when it names neither.</summary>
        private static bool Either(int list, int yes, int no, bool @is)
        {
            return (list & (yes | no)) == 0 || (list & (@is ? yes : no)) != 0;
        }
    }
}
