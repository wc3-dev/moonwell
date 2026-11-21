namespace Moonwell
{
    /// <summary>One hit, as the damage package hands it to every stage: the damage with its types, where it came from, and what it did.</summary>
    public sealed class Hit : Damage
    {
        /// <summary>The damage kind, kept behind the property that checks the stage.</summary>
        private DamageKind damageKind;

        /// <summary>The weapon sound, kept behind the property that checks the stage.</summary>
        private WeaponSound weaponSound;

        /// <summary>The target's armor for this hit, kept behind the property that checks the stage.</summary>
        private float armor = 0.0f;

        /// <summary>Makes a hit that starts with the amount.</summary>
        internal Hit(Unit target, Unit? source, float amount, bool isAttack)
            : base(target, source, amount, isAttack)
        {
            Original = amount;
        }

        /// <summary>
        /// How far right of a unit's x the package's words (miss, parry, block) and a crit's number are put, and how high above the
        /// ground, so they start where the game starts its own miss and crit text. First look, from screenshots at 1920x1080 on the
        /// default camera, where 100 world units across are about 70 px and a height unit about 0.39 px, a unit's health bar about
        /// 56 px wide: the game's miss has its left edge 28 px right of the attacker's centre and its middle 8 px below the top of
        /// the bar, its crit number 18 px right and 20 px below. A text put at a unit's x shows its left edge 3 to 11 px right of the
        /// centre, and at height 100 about 15 px above the bar, so about 23 right (miss 25, crit 21) and a height of 26.
        /// Second look, a game check on 3.0.1.24371 (11-21): the miss was very close and the game's lower, nearer the ground, and
        /// the crit number too far right, the game's starting from about the middle of the unit. So the height went from 26 to 14
        /// for all, and the crit number's offset from 23 to 0. The numbers are what the game check left them at.
        /// </summary>
        internal const float WordsRight = 23.0f, NumberRight = 0.0f, TextHeight = 14.0f;

        /// <summary>Shows a red text over a unit where the game starts its own: the unit's place moved right by <paramref name="right"/> (<see cref="WordsRight"/> for words, <see cref="NumberRight"/> for a crit's number) and up by <see cref="TextHeight"/>.</summary>
        internal static void ShowText(string text, Unit over, float right = WordsRight)
        {
            FloatingText.Pop(text, over.X + right, over.Y, Color.Red, null, TextHeight);
        }

        /// <summary>The unit the game shows a miss or a crit over: the attacker, or the target for a hit with no source.</summary>
        internal Unit TextUnit => Source != null && !Source.IsGone ? Source : Target;

        /// <summary>The amount of the hit; a reaction may change it, and a missed hit keeps 0.</summary>
        [LocalSafe]
        public override float Amount
        {
            get => base.Amount;
            set
            {
                if (!Missed)
                {
                    base.Amount = value;
                }
            }
        }

        /// <summary>The amount the hit started with, before any reaction changed it.</summary>
        [LocalSafe]
        public float Original { get; } = 0.0f;

        /// <summary>Whether the hit was dealt by code, through Unit.Damage.</summary>
        [LocalSafe]
        public bool IsCode { get; internal set; }

        /// <summary>Whether the hit is a spell's: not an attack, with the attack type the game gives spells.</summary>
        [LocalSafe]
        public bool IsSpell => !IsAttack && AttackTypeCode == 0;

        /// <summary>Whether the hit is a melee attack.</summary>
        [LocalSafe]
        public bool IsMelee { get; internal set; }

        /// <summary>Whether the hit is a ranged attack.</summary>
        [LocalSafe]
        public bool IsRanged { get; internal set; }

        /// <summary>The damage kind, which the game calls its damage type; a reaction may change it before armor only.</summary>
        [LocalSafe]
        public DamageKind DamageKind
        {
            get => damageKind;
            set
            {
                CheckBeforeArmor("DamageKind");
                if (Known("DamageKind", (int)value, (int)DamageKind.Universal))
                {
                    damageKind = value;
                }
            }
        }

        /// <summary>The sound of the hit, which the game calls its weapon type; a reaction may change it before armor only.</summary>
        [LocalSafe]
        public WeaponSound WeaponSound
        {
            get => weaponSound;
            set
            {
                CheckBeforeArmor("WeaponSound");
                if (Known("WeaponSound", (int)value, (int)WeaponSound.RockHeavyBash))
                {
                    weaponSound = value;
                }
            }
        }

        /// <summary>The target's armor for this hit; a reaction may change it before armor only, and the target has its own armor back after the hit.</summary>
        [LocalSafe]
        public float Armor
        {
            get => armor;
            set
            {
                CheckBeforeArmor("Armor");
                armor = value;
            }
        }

        /// <summary>The attack type; a reaction may change it before armor only. A spell's is <see cref="AttackType.Spells"/>.</summary>
        [LocalSafe]
        public AttackType AttackType
        {
            get => TypeCodes.AttackOf(AttackTypeCode);
            set
            {
                CheckBeforeArmor("AttackType");
                AttackTypeCode = TypeCodes.AttackCode(value);
            }
        }

        /// <summary>The target's defense type for this hit; a reaction may change it before armor only, and the target has its own defense type back after the hit.</summary>
        [LocalSafe]
        public DefenseType DefenseType
        {
            get => TypeCodes.DefenseOf(DefenseTypeCode);
            set
            {
                CheckBeforeArmor("DefenseType");
                DefenseTypeCode = TypeCodes.DefenseCode(value);
            }
        }

        /// <summary>Whether a reaction made the hit miss (<see cref="Miss"/>).</summary>
        [LocalSafe]
        public bool Missed { get; private set; }

        /// <summary>Whether the hit is a critical one: a plain mark a before-armor reaction sets and later stages read, for texts and for a map's own reactions.</summary>
        [LocalSafe]
        public bool IsCrit { get; set; }

        /// <summary>How much of the hit a block took, 0 unless a reaction writes it: a plain amount for texts and bars.</summary>
        [LocalSafe]
        public float Blocked { get; set; } = 0.0f;

        /// <summary>How much of the hit a barrier took, 0 unless a reaction writes it: a plain amount for texts and bars.</summary>
        [LocalSafe]
        public float Absorbed { get; set; } = 0.0f;

        /// <summary>Whether the hit killed its target; known once the hit has landed.</summary>
        [LocalSafe]
        public bool Killed { get; internal set; }

        /// <summary>The game's number of the attack type.</summary>
        internal int AttackTypeCode;

        /// <summary>The game's number of the target's defense type for this hit.</summary>
        internal int DefenseTypeCode;

        /// <summary>How many hits led to this one, each made while the reactions of the one before ran: 0 for a hit made while no reaction ran.</summary>
        internal int Chain;

        /// <summary>The stage the hit is in.</summary>
        internal Stage Stage;

        /// <summary>The target's life as last read in the lethal check: after the reactions so far, so after any hit they dealt, and before this hit lowers it.</summary>
        internal float TargetLife = 0.0f;

        /// <summary>Whether the hit, at its amount now, leaves the target at death's life or below.</summary>
        internal bool Kills => TargetLife - Amount <= Pipeline.DeathLife;

        /// <summary>Whether the hit left its after-armor stage killing a living target; Killed is this and the target dead 0 s later.</summary>
        internal bool WouldKill;

        /// <summary>Whether no package reaction runs for the hit and it does not land: the game started it at 0, or it was cut.</summary>
        internal bool Quiet;

        /// <summary>Whether the hit was cut as the ninth of a chain: set to 0, and core's reactions do not run for it either.</summary>
        internal bool Cut;

        /// <summary>Whether the package put armor on the target for this hit, to be put back.</summary>
        internal bool ArmorChanged;

        /// <summary>The armor to set on the target to put its own back.</summary>
        internal float ArmorRestore = 0.0f;

        /// <summary>Whether the package put a defense type on the target for this hit, to be put back.</summary>
        internal bool DefenseChanged;

        /// <summary>The target's own defense type, to be put back.</summary>
        internal int DefenseRestore;

        /// <summary>
        /// Before armor only: makes the hit miss, as the game's own miss does. The amount becomes 0, the weapon sound none,
        /// and <see cref="Missed"/> true; the text is shown once in red over the source, as the game shows its own miss (over the target
        /// for a hit with no source), and an empty text shows nothing.
        /// The rest of the stage's reactions still run and read Missed, no later stage's reactions run for the hit (nor
        /// core's OnDamaged family), it takes no life, and a burst still lists it. The game's own on-hit effects still land.
        /// </summary>
        public void Miss(string text = "miss")
        {
            CheckBeforeArmor("Miss");
            if (Stage != Stage.BeforeArmor || Missed)
            {
                return;
            }

            Amount = 0.0f;
            Missed = true;
            weaponSound = WeaponSound.None;
            if (!string.IsNullOrEmpty(text) && !Target.IsGone)
            {
                ShowText(text, TextUnit);
            }
        }

        /// <summary>In the lethal stage: lowers the amount so that the target keeps that much life.</summary>
        public void Spare(float life = 1.0f)
        {
            Dev.Check(Stage == Stage.Lethal, "Hit.Spare can be called only in the lethal stage (OnKilling, OnDying, OnUnitDying)");
            if (Stage != Stage.Lethal)
            {
                // A release build has no check: outside the lethal stage the life it would keep is not known.
                return;
            }

            // Read now: a hit a lethal reaction dealt, this one included, has already lowered the life.
            TargetLife = Pipeline.World.Life(Target.Handle);
            var amount = TargetLife - life;
            Amount = amount < 0.0f ? 0.0f : amount;
        }

        /// <summary>Sets a type or the armor without the stage check; the package fills a new hit with it.</summary>
        internal void Fill(DamageKind type, WeaponSound sound, float targetArmor)
        {
            damageKind = type;
            weaponSound = sound;
            armor = targetArmor;
        }

        /// <summary>Whether a type's number is one the game has. A development build reports one it has not; a release build has no check, and the hit keeps the type it had, as the package could hand no other to the game.</summary>
        private static bool Known(string member, int number, int most)
        {
            var known = number >= 0 && number <= most;
            Dev.Check(known, "Hit." + member + " cannot be " + number + ": the game's numbers are 0 to " + most);
            return known;
        }

        /// <summary>Reports a change made after armor, which the game would ignore.</summary>
        private void CheckBeforeArmor(string member)
        {
            Dev.Check(Stage == Stage.BeforeArmor, "Hit." + member + " can be changed only before armor (OnDealing, OnTaking, OnUnitTaking)");
        }
    }
}
