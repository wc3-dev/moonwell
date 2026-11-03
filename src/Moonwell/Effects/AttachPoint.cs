namespace Moonwell
{
    /// <summary>The attachment points models share, as the game takes them. Any other text the game takes works too, such as "chest mount", "weapon first" or a point a model of the map's has; a point the model lacks is not checked, and the game draws nothing there.</summary>
    public static class AttachPoint
    {
        /// <summary>The model's origin, at its feet: "origin".</summary>
        public const string Origin = "origin";

        /// <summary>Above the model's head: "overhead".</summary>
        public const string Overhead = "overhead";

        /// <summary>The model's head: "head".</summary>
        public const string Head = "head";

        /// <summary>The model's chest: "chest".</summary>
        public const string Chest = "chest";

        /// <summary>A hand of the model: "hand".</summary>
        public const string Hand = "hand";

        /// <summary>The model's left hand: "hand left".</summary>
        public const string HandLeft = "hand left";

        /// <summary>The model's right hand: "hand right".</summary>
        public const string HandRight = "hand right";

        /// <summary>A foot of the model: "foot".</summary>
        public const string Foot = "foot";

        /// <summary>The model's left foot: "foot left".</summary>
        public const string FootLeft = "foot left";

        /// <summary>The model's right foot: "foot right".</summary>
        public const string FootRight = "foot right";

        /// <summary>The model's weapon: "weapon".</summary>
        public const string Weapon = "weapon";

        /// <summary>The model's weapon in its left hand: "weapon left".</summary>
        public const string WeaponLeft = "weapon left";

        /// <summary>The model's weapon in its right hand: "weapon right".</summary>
        public const string WeaponRight = "weapon right";
    }
}
