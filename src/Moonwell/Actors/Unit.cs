using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Moonwell
{
    /// <summary>A unit of the game: an object of its declared class, which holds the components the class starts with.</summary>
    public class Unit : Widget
    {
        /// <summary>The table behind UnitHooks, made on first use. A static initializer would not do: CSharp.lua runs it when the class is first indexed, and a map's static Dictionary keyed by Unit does that while the script loads, before this file has its imports.</summary>
        private static HookTable? unitHooks;

        /// <summary>The reactions of units; declared here, so that an override in a subclass is found.</summary>
        internal static HookTable UnitHooks
        {
            get
            {
                if (unitHooks == null)
                {
                    unitHooks = new HookTable(typeof(Unit), new string[] { "OnItemPickedUp", "OnItemDropped", "OnDamaged", "OnDealtDamage", "OnDeath", "OnKill", "OnCast", "OnOrdered", "OnSelected", "OnDeselected", "OnSummon", "OnLoaded", "OnUnloaded", "OnCargoLoaded", "OnCargoUnloaded", "OnResearchStarted", "OnResearchCancelled", "OnResearchFinished", "OnItemSold", "OnItemPawned", "OnUnitSold" }, new int[] { Hook.OnItemPickedUp, Hook.OnItemDropped, Hook.OnDamaged, Hook.OnDealtDamage, Hook.OnDeath, Hook.OnKill, Hook.OnCast, Hook.OnOrdered, Hook.OnSelection, Hook.OnSelection, Hook.OnSummon, Hook.OnTransport, Hook.OnTransport, Hook.OnTransport, Hook.OnTransport, Hook.OnResearchOrSale, Hook.OnResearchOrSale, Hook.OnResearchOrSale, Hook.OnResearchOrSale, Hook.OnResearchOrSale, Hook.OnResearchOrSale });
                }

                return unitHooks;
            }
        }

        /// <summary>The reactions of a unit's class: a hero's, which add the hero's own to Unit's, or Unit's.</summary>
        internal static HookTable HooksOf(Unit unit)
        {
            return unit is Hero ? Hero.HeroHooks : UnitHooks;
        }

        /// <summary>The bit mask of the reactions this class overrides, worked out when the unit is taken in.</summary>
        internal int Mask;

        /// <summary>The cast the unit is making now, which ends when its spell ends or its next cast begins; null when none.</summary>
        internal Cast? ActiveCast;

        /// <summary>The components the class starts with, added in order when the unit enters. Assigning a new list drops what a parent class listed.</summary>
        protected List<UnitComponent> Components { get; set; } = new List<UnitComponent>();

        // The instance fields are internal, not private, as a map's subclass may name a field of its own the same (see Scope).

        /// <summary>The effects, texts, sounds, lightning and minimap icons on the unit that have not ended, which end when it ends; null until the first.</summary>
        internal List<Scoped>? attached;

        /// <summary>The units this unit summoned, oldest first, the dead among them until next read; null when it summoned none.</summary>
        internal List<Unit>? summoned;

        /// <summary>Whether this summon dies when its summoner dies or leaves the game.</summary>
        internal bool diesWithSummoner;

        /// <summary>The ability objects of the unit, one per ability id, in the order they were first asked for.</summary>
        internal readonly OrderedMap<int, Ability> abilities = new OrderedMap<int, Ability>();

        /// <summary>The game's handle of the unit.</summary>
        [LocalSafe]
        public unit Handle { get; internal set; } = null!;

        /// <summary>The type id of the unit, as the game read it when the unit was taken in.</summary>
        [LocalSafe]
        public int TypeId { get; internal set; }

        /// <summary>Whether the unit is an illusion (Mirror Image, a Wand of Illusion), as the game read it when the unit was taken in. An illusion has its hero's type id but is taken in as a plain unit, not as the hero's class: it has none of the class's starting components and runs no OnCreated. Map-wide reactions (OnUnitCreated, OnUnitDied) still see it.</summary>
        [LocalSafe]
        public bool IsIllusion { get; internal set; }

        /// <summary>The default object of the unit's type, as Unreal's class default object: the values its class declares (a type no class of the map's declares, its values as World Editor made them, standard where World Editor left them), the same whatever this unit's are now and once it is gone. One per type, shared by its units, with no handle; it is gone, so its live actions do nothing.</summary>
        [LocalSafe]
        public Unit Default => ObjectTypes.DefaultOf(this, DeclaredTypeId);

        /// <summary>The id of the type whose declared values the unit reads: its type id, else, while its constructor runs, the id its class is registered with, else 0.</summary>
        internal int DeclaredTypeId => TypeId != 0 ? TypeId : ObjectTypes.IdOfClass(this);

        /// <summary>The value the unit's type declares for a field, which a shared member reads when the unit has no handle (in its constructor, and on a default object). Here, the value the build wrote for the map; the standard classes read their own declared values on .NET.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected virtual T DeclaredValue<T>(string field)
        {
            // The build has no world; the classes it reads override this.
            if (Runtime.World == null)
            {
                return default(T)!;
            }

            var value = Runtime.World.TypeValue(DeclaredTypeId, field);
            return value == null ? default(T)! : (T)value;
        }

        /// <summary>Declares the value of a field for the unit's class, which a shared member does when it is set with no handle. Here, nothing: in the game the values are the build's; the standard classes keep them on .NET.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected virtual void Declare(string field, object value)
        {
        }

        /// <summary>The game's player that owned the unit when it was last read; kept for when the game no longer gives it.</summary>
        internal player owner = null!;

        /// <summary>The player that owns the unit. Setting it takes effect at once and gives the unit the new player's colour (SetUnitOwner), and a dummy whose spell still acts for the unit goes with it. Once the game has removed the unit, the last owner read or set, and setting does nothing.</summary>
        [LocalSafe]
        public Player Owner
        {
            get
            {
                if (!IsGone)
                {
                    var now = Runtime.World.OwnerOf(Handle);
                    if (now != null)
                    {
                        if (Local.Depth != 0)
                        {
                            return Player.Of(now)!;
                        }

                        owner = now;
                    }
                }

                return Player.Of(owner)!;
            }

            set
            {
                if (InGame())
                {
                    owner = value.Handle;
                    Runtime.World.SetOwner(Handle, value.Handle);
                    Dummies.OwnerChanged(this);
                }
            }
        }

        /// <summary>The life last read, kept for when the game no longer gives it.</summary>
        internal float lastLife = 0.0f;

        /// <summary>The most life last read.</summary>
        internal float lastMaxLife = 0.0f;

        /// <summary>The mana last read.</summary>
        internal float lastMana = 0.0f;

        /// <summary>The most mana last read.</summary>
        internal float lastMaxMana = 0.0f;

        /// <summary>The x last read. Internal, as the interactions package reads a unit's place through the natives and keeps it here, as X does.</summary>
        internal float lastX = 0.0f;

        /// <summary>The y last read. Internal, as the interactions package reads a unit's place through the natives and keeps it here, as Y does.</summary>
        internal float lastY = 0.0f;

        /// <summary>The facing last read.</summary>
        internal float lastFacing = 0.0f;

        /// <summary>The level last read.</summary>
        internal int lastLevel;

        /// <summary>Whether the unit is paused, as last read or set.</summary>
        internal bool lastPaused;

        /// <summary>The scale last read or set.</summary>
        internal float lastScale = 1.0f;

        /// <summary>Whether the scale has been read or set, so that lastScale holds it.</summary>
        internal bool scaleKnown;

        /// <summary>The speed AnimationSpeed last set; the game keeps none to read.</summary>
        internal float animationSpeed = 1.0f;

        /// <summary>The fly height last read or set.</summary>
        internal float lastFlyHeight = 0.0f;

        /// <summary>The inventory size last read.</summary>
        internal int lastInventorySize;

        /// <summary>The name last set.</summary>
        internal string lastName = "";

        /// <summary>Whether the unit can show a fly height: it flies, or FlyHeight has done the Amrf trick on it.</summary>
        internal bool flyReady;

        /// <summary>Whether a getter that read 0 read it from a unit the game has removed: the game gives 0 for everything then, and the type id 0, before the sweep finds the unit. The getter then keeps the last value instead of the 0.</summary>
        internal bool RemovedByGame()
        {
            return Runtime.World.UnitTypeId(Handle) == 0;
        }

        /// <summary>Whether the unit is still in the game for a setter or an action: neither gone nor removed by the game, before the sweep finds it.</summary>
        internal bool InGame()
        {
            return !IsGone && !RemovedByGame();
        }

        /// <summary>The unit's life; a value above the most life is clamped to it, and setting it to 0.405 or less kills the unit (0.406 does not) (the death event runs inside the call, with life 0). Once the game has removed the unit, the last life read or set, and setting does nothing.</summary>
        [LocalSafe]
        public float Life
        {
            get
            {
                if (IsGone)
                {
                    return lastLife;
                }

                var now = Runtime.World.LifeOf(Handle);
                if (now == 0.0f && RemovedByGame())
                {
                    return lastLife;
                }

                if (Local.Depth == 0)
                {
                    lastLife = now;
                }

                return now;
            }

            set
            {
                if (InGame())
                {
                    // The game clamps to the most life, so that is what a removed unit gives back.
                    var most = MaxLife;
                    lastLife = value <= 0.405f ? 0.0f : (value < most ? value : most);
                    Runtime.World.SetLife(Handle, value);
                }
            }
        }

        /// <summary>The most life the unit can have, a whole number (BlzSetUnitMaxHP; SetUnitState does not change it). The life stays unless the new most is below it, and then falls to it. Once the game has removed the unit, the last value read or set, and setting does nothing. In a declared class's constructor, the most life every unit of the class starts with (uhpm); on a default object, its type's.</summary>
        [LocalSafe]
        public float MaxLife
        {
            get
            {
                if (Handle == null)
                {
                    return DeclaredValue<int>("uhpm");
                }

                if (IsGone)
                {
                    return lastMaxLife;
                }

                var now = Runtime.World.MaxLifeOf(Handle);
                if (now == 0.0f && RemovedByGame())
                {
                    return lastMaxLife;
                }

                if (Local.Depth == 0)
                {
                    lastMaxLife = now;
                }

                return now;
            }

            set
            {
                if (Handle == null)
                {
                    Declare("uhpm", (int)value);
                    return;
                }

                if (InGame())
                {
                    // The game keeps the whole number, and lowers the life to it.
                    lastMaxLife = (int)value;
                    if (lastLife > lastMaxLife)
                    {
                        lastLife = lastMaxLife;
                    }

                    Runtime.World.SetMaxLife(Handle, value);
                }
            }
        }

        /// <summary>The unit's mana; a value above the most mana is clamped to it, and a unit without mana reads 0 and ignores a set. Once the game has removed the unit, the last mana read or set, and setting does nothing. In a declared class's constructor, the mana every unit of the class starts with (umpi); on a default object, its type's.</summary>
        [LocalSafe]
        public float Mana
        {
            get
            {
                if (Handle == null)
                {
                    return DeclaredValue<int>("umpi");
                }

                if (IsGone)
                {
                    return lastMana;
                }

                var now = Runtime.World.ManaOf(Handle);
                if (now == 0.0f && RemovedByGame())
                {
                    return lastMana;
                }

                if (Local.Depth == 0)
                {
                    lastMana = now;
                }

                return now;
            }

            set
            {
                if (Handle == null)
                {
                    Declare("umpi", (int)value);
                    return;
                }

                if (InGame())
                {
                    // The game clamps to the most mana (0 for a unit without mana), so that is what a removed unit gives back.
                    var most = MaxMana;
                    lastMana = value < most ? value : most;
                    if (lastMana < 0.0f)
                    {
                        lastMana = 0.0f;
                    }

                    Runtime.World.SetMana(Handle, value);
                }
            }
        }

        /// <summary>The most mana the unit can have, a whole number (BlzSetUnitMaxMana; SetUnitState does not change it). The mana stays unless the new most is below it, and then falls to it. Once the game has removed the unit, the last value read or set, and setting does nothing. In a declared class's constructor, the most mana every unit of the class starts with (umpm); on a default object, its type's.</summary>
        [LocalSafe]
        public float MaxMana
        {
            get
            {
                if (Handle == null)
                {
                    return DeclaredValue<int>("umpm");
                }

                if (IsGone)
                {
                    return lastMaxMana;
                }

                var now = Runtime.World.MaxManaOf(Handle);
                if (now == 0.0f && RemovedByGame())
                {
                    return lastMaxMana;
                }

                if (Local.Depth == 0)
                {
                    lastMaxMana = now;
                }

                return now;
            }

            set
            {
                if (Handle == null)
                {
                    Declare("umpm", (int)value);
                    return;
                }

                if (InGame())
                {
                    // The game keeps the whole number, and lowers the mana to it.
                    lastMaxMana = (int)value;
                    if (lastMana > lastMaxMana)
                    {
                        lastMana = lastMaxMana;
                    }

                    Runtime.World.SetMaxMana(Handle, value);
                }
            }
        }

        /// <summary>The x of the unit; once the game has removed it, the last x read.</summary>
        [LocalSafe]
        public float X
        {
            get
            {
                if (IsGone)
                {
                    return lastX;
                }

                var now = Runtime.World.UnitX(Handle);
                if (now == 0.0f && RemovedByGame())
                {
                    return lastX;
                }

                if (Local.Depth == 0)
                {
                    lastX = now;
                }

                return now;
            }
        }

        /// <summary>The y of the unit; once the game has removed it, the last y read.</summary>
        [LocalSafe]
        public float Y
        {
            get
            {
                if (IsGone)
                {
                    return lastY;
                }

                var now = Runtime.World.UnitY(Handle);
                if (now == 0.0f && RemovedByGame())
                {
                    return lastY;
                }

                if (Local.Depth == 0)
                {
                    lastY = now;
                }

                return now;
            }
        }

        /// <summary>Moves the unit to a place through the game's pathing (SetUnitPosition): the game finds a free spot, and the unit's order ends. Does nothing once the unit is gone.</summary>
        public void MoveTo(float x, float y)
        {
            if (InGame())
            {
                Runtime.World.MoveUnit(Handle, x, y);
            }
        }

        /// <summary>Puts the unit at a place with no pathing and no bounds (SetUnitX and SetUnitY): it may stand where it could not walk, or past the edge of the world, and its order goes on. Keep it inside the map yourself. Does nothing once the unit is gone.</summary>
        public void PlaceAt(float x, float y)
        {
            if (InGame())
            {
                Runtime.World.PlaceUnit(Handle, x, y);
            }
        }

        /// <summary>The unit's facing in degrees, within 0 to 360 (400 reads 40, -90 reads 270). Setting it makes the unit turn at its turn rate (SetUnitFacing), so a read just after still gives the old facing; FaceNow turns at once. Once the game has removed the unit, the last facing read or set, and setting does nothing.</summary>
        [LocalSafe]
        public float Facing
        {
            get
            {
                if (IsGone)
                {
                    return lastFacing;
                }

                var now = Runtime.World.FacingOf(Handle);
                if (now == 0.0f && RemovedByGame())
                {
                    return lastFacing;
                }

                if (Local.Depth == 0)
                {
                    lastFacing = now;
                }

                return now;
            }

            set
            {
                if (InGame())
                {
                    lastFacing = value;
                    Runtime.World.TurnUnit(Handle, value);
                }
            }
        }

        /// <summary>Turns the unit to a facing in degrees at once (BlzSetUnitFacingEx), so a read just after gives it. A unit that is walking turns back toward where it walks, and its order goes on. Does nothing once the unit is gone.</summary>
        public void FaceNow(float degrees)
        {
            if (InGame())
            {
                Runtime.World.FaceUnitNow(Handle, degrees);
            }
        }

        /// <summary>The unit's level; a hero's hero level. Once the game has removed the unit, the last level read. The game has no setter: a set in a declared class's constructor is the level every unit of the class has (ulev), and one on a unit in the game does nothing (the build rejects it). A hero's level is not declared: it starts at 1, which a hero with no handle reads. On a default object, its type's.</summary>
        [LocalSafe]
        public int Level
        {
            get
            {
                if (Handle == null)
                {
                    return this is Hero ? 1 : DeclaredValue<int>("ulev");
                }

                if (IsGone)
                {
                    return lastLevel;
                }

                var now = Runtime.World.LevelOf(Handle);
                if (now == 0 && RemovedByGame())
                {
                    return lastLevel;
                }

                if (Local.Depth == 0)
                {
                    lastLevel = now;
                }

                return now;
            }

            set
            {
                if (Handle == null)
                {
                    Declare("ulev", value);
                }
            }
        }

        /// <summary>The unit's name (GetUnitName): a hero's is its type's name, not its proper name (seen 10-21). The game gives it in each player's language, so it is read only inside Local.For. Setting it (BlzSetUnitName) renames this unit only, the same on every machine. Once the game has removed the unit, the last name set (a read keeps nothing, as it differs by machine), and setting does nothing. In a declared class's constructor, the name of the class's type (unam), the same on every machine; on a default object, its type's, empty for a type the build's table does not hold.</summary>
        public string Name
        {
            [MachineLocal]
            get
            {
                if (Handle == null)
                {
                    return DeclaredValue<string>("unam") ?? string.Empty;
                }

                if (IsGone)
                {
                    return lastName;
                }

                var now = Runtime.World.UnitNameOf(Handle);
                if (string.IsNullOrEmpty(now) && RemovedByGame())
                {
                    return lastName;
                }

                return now;
            }

            set
            {
                if (Handle == null)
                {
                    Declare("unam", value);
                    return;
                }

                if (InGame())
                {
                    lastName = value;
                    Runtime.World.RenameUnit(Handle, value);
                }
            }
        }

        /// <summary>Whether the unit is alive; false once the game has removed the unit.</summary>
        [LocalSafe]
        public bool IsAlive => !IsGone && Runtime.World.IsAlive(Handle);

        /// <summary>Kills the unit through the game (KillUnit): the game delivers the death as for any other, with no killer, so OnDeath runs once, inside this call. Does nothing once the unit is gone.</summary>
        public void Kill()
        {
            if (InGame())
            {
                Runtime.World.SlayUnit(Handle);

                // The game leaves the life 0 (seen), so a unit that is killed and then removed reads 0, not the life it had;
                // read it, as a hero may have been revived inside the death event, unless OnDeath removed the unit.
                lastLife = IsGone ? 0.0f : Life;
            }
        }

        /// <summary>Whether the unit is paused (PauseUnit), a flag and not a count. A paused unit takes no orders (they answer false) and stays where it is, but its stand animation goes on. Once the game has removed the unit, the last value read or set, and setting does nothing.</summary>
        [LocalSafe]
        public bool IsPaused
        {
            get
            {
                if (IsGone)
                {
                    return lastPaused;
                }

#if DEBUG
                // A halt's hold is the debug mode's, not the map's: the map reads the pause it set itself.
                if (DebugSession.HoldsPause(Handle, out var wanted))
                {
                    return wanted;
                }
#endif
                var now = Runtime.World.IsPausedUnit(Handle);
                if (!now && RemovedByGame())
                {
                    return lastPaused;
                }

                if (Local.Depth == 0)
                {
                    lastPaused = now;
                }

                return now;
            }

            set
            {
                if (InGame())
                {
                    lastPaused = value;
#if DEBUG
                    if (DebugSession.WantPause(Handle, value))
                    {
                        return;
                    }
#endif
                    Runtime.World.SetPaused(Handle, value);
                }
            }
        }

        /// <summary>The size of the unit's model as a factor of its normal size, set on all three axes (SetUnitScale). Until it is set, it reads the scale of the unit's type (0.8 for a footman), which is not 1. Once the game has removed the unit, the last value read or set, and setting does nothing. In a declared class's constructor, the scale every unit of the class starts with (usca); on a default object, its type's.</summary>
        [LocalSafe]
        public float Scale
        {
            get
            {
                if (Handle == null)
                {
                    return DeclaredValue<float>("usca");
                }

                if (IsGone || scaleKnown)
                {
                    return lastScale;
                }

                var now = Runtime.World.ScaleOf(Handle);
                if (now == 0.0f && RemovedByGame())
                {
                    return lastScale;
                }

                if (Local.Depth == 0)
                {
                    lastScale = now;
                    scaleKnown = true;
                }

                return now;
            }

            set
            {
                if (Handle == null)
                {
                    Declare("usca", value);
                    return;
                }

                if (InGame())
                {
                    lastScale = value;
                    scaleKnown = true;
                    Runtime.World.ScaleUnit(Handle, value);
                }
            }
        }

        /// <summary>Puts an effect of a model on the unit, at an attachment point of its model (AttachPoint.Overhead, or any text the game takes), in a scope, and returns it. It follows the unit, stays over its corpse after a death, and ends when the scope ends or the unit ends, whichever is first; at a point the model lacks the game draws nothing. Given shownTo, only that player sees it. On a unit that is gone it ends at once and the game is not asked to make it.</summary>
        public SpecialEffect AddEffect(string model, string point, Scope scope, Player? shownTo = null)
        {
            return SpecialEffect.On(this, model, point, scope, shownTo);
        }

        /// <summary>Puts an icon of a model (MinimapIcon.Boss, or any model path) on the minimap at the unit, in a colour, in a scope, and returns it. It follows the unit and ends when the scope ends or the unit ends, whichever is first. Given shownTo, only that player sees it. On a unit that is gone it ends at once and the game is not asked to make it.</summary>
        public MinimapIcon AddMinimapIcon(string path, Color color, Scope scope, Player? shownTo = null)
        {
            return MinimapIcon.On(this, path, color, scope, shownTo);
        }

        /// <summary>Plays an effect of a model once on the unit, at an attachment point of its model: it is made and ended at once, so its death animation plays. Given shownTo, only that player sees it. Does nothing once the unit is gone.</summary>
        public void PlayEffectOnce(string model, string point, Player? shownTo = null)
        {
            SpecialEffect.PlayOnceOn(this, model, point, shownTo);
        }

        /// <summary>Puts a text over the unit, above its health bar, in a scope, and returns it. It follows the unit, stays over its corpse after a death, shows only to the players who see the unit, and ends when the scope ends or the unit ends, whichever is first. Given shownTo, only that player sees it. On a unit that is gone it ends at once and the game is not asked to make it.</summary>
        public FloatingText AddText(string text, Scope scope, Player? shownTo = null)
        {
            return FloatingText.On(this, text, scope, shownTo);
        }

        /// <summary>Plays a text once over the unit, as a damage number: it starts above the health bar, rises 64 a second, lasts 2 s and fades over the last 1, shown only to the players who see the unit when it is made. Given shownTo, only that player sees it. Does nothing once the unit is gone.</summary>
        public void PopText(string text, Color? color = null, Player? shownTo = null)
        {
            FloatingText.PopOn(this, text, color, shownTo);
        }

        /// <summary>Puts a looping 3D sound on the unit, in a scope, and returns it. It follows the unit, and ends, fading over its FadeOut, when the scope ends or the unit ends, whichever is first. Given heardBy, only that player hears it. On a unit that is gone it ends at once and the game is not asked to make it.</summary>
        public Sound AddSound(string path, Scope scope, Player? heardBy = null)
        {
            return Sound.On(this, path, scope, heardBy);
        }

        /// <summary>Plays a 3D sound once on the unit, which it follows; volume is 0 to 100. Given heardBy, only that player hears it. Does nothing once the unit is gone.</summary>
        public void PlaySound(string path, Player? heardBy = null, float volume = 100.0f)
        {
            Sound.PlayOn(this, path, heardBy, volume);
        }

        /// <summary>Lists an effect, text or sound on the unit, which ends it when the unit ends.</summary>
        internal void Attach(Scoped thing)
        {
            if (attached == null)
            {
                attached = new List<Scoped>();
            }

            attached.Add(thing);
        }

        /// <summary>Takes an ended effect, text or sound off the unit's list.</summary>
        internal void Detach(Scoped thing)
        {
            if (attached != null)
            {
                attached.Remove(thing);
            }
        }

        /// <summary>Tints the unit's model (SetUnitVertexColor); each value is clamped to 0-255, and an alpha of 0 makes the unit and its shadow invisible, though its selection circle and health bar still show. The game keeps no tint to read back. Does nothing once the unit is gone.</summary>
        public void Tint(int red, int green, int blue, int alpha = 255)
        {
            if (InGame())
            {
                Runtime.World.TintUnit(Handle, Clamp(red), Clamp(green), Clamp(blue), Clamp(alpha));
            }
        }

        private static int Clamp(int value)
        {
            return value < 0 ? 0 : value > 255 ? 255 : value;
        }

        /// <summary>Tints the unit's model in a colour, its alpha included; as Tint(red, green, blue, alpha).</summary>
        public void Tint(Color color)
        {
            Tint(color.R, color.G, color.B, color.A);
        }

        /// <summary>Plays an animation of the unit's model by name (SetUnitAnimation): Animation.Attack, or any name the model has with its tags ("attack slam"); a name the model lacks plays its stand. It plays once, then an idle unit stands; a walking unit keeps walking and only stutters. A name picks no variant ("attack 2" is any attack): PlayAnimation(index) does (seen 11-06). Does nothing once the unit is gone.</summary>
        public void PlayAnimation(string name)
        {
            if (InGame())
            {
                Runtime.World.AnimateUnit(Handle, name, false);
            }
        }

        /// <summary>Plays an animation of the unit's model by its index in the model, from 0 (SetUnitAnimationByIndex): the one way to pick a variant, such as the second attack. Indices differ between models. Does nothing once the unit is gone.</summary>
        public void PlayAnimation(int index)
        {
            if (InGame())
            {
                Runtime.World.AnimateUnitByIndex(Handle, index);
            }
        }

        /// <summary>Queues an animation of the unit's model by name, to play after the one playing, a stand's loop included (QueueUnitAnimation, seen 11-06). Does nothing once the unit is gone.</summary>
        public void QueueAnimation(string name)
        {
            if (InGame())
            {
                Runtime.World.AnimateUnit(Handle, name, true);
            }
        }

        /// <summary>The speed of the unit's animations as a factor of the model's own (SetUnitTimeScale): 0.25 plays them at a quarter, 0 holds the unit still mid-animation (seen 11-06). It reads what was set, 1 unless set, as the game keeps none to read. Once the unit is gone, setting does nothing.</summary>
        [LocalSafe]
        public float AnimationSpeed
        {
            get
            {
                return animationSpeed;
            }

            set
            {
                if (InGame())
                {
                    animationSpeed = value;
                    Runtime.World.SetUnitAnimationSpeed(Handle, value);
                }
            }
        }

        /// <summary>Adds a tag to the unit's animations (AddUnitAnimationProperties): AnimationTag.Defend, or any the game takes ("upgrade first"). The unit plays its animations with the tag, a standing footman with Defend its shield raised, until RemoveAnimationTag (seen 11-06). Does nothing once the unit is gone.</summary>
        public void AddAnimationTag(string tag)
        {
            if (InGame())
            {
                Runtime.World.TagUnitAnimation(Handle, tag, true);
            }
        }

        /// <summary>Removes a tag AddAnimationTag added. Does nothing once the unit is gone.</summary>
        public void RemoveAnimationTag(string tag)
        {
            if (InGame())
            {
                Runtime.World.TagUnitAnimation(Handle, tag, false);
            }
        }

        /// <summary>How high the unit flies above the ground, set at once (SetUnitFlyHeight with a rate of 0). A unit that does not fly only shows the height once Amrf has been added to it and removed (seen 10-21), so the first set on such a unit does that; reading gives the height set either way. Once the game has removed the unit, the last value read or set, and setting does nothing. In a declared class's constructor, the height every unit of the class flies at (umvh); on a default object, its type's.</summary>
        [LocalSafe]
        public float FlyHeight
        {
            get
            {
                if (Handle == null)
                {
                    return DeclaredValue<float>("umvh");
                }

                if (IsGone)
                {
                    return lastFlyHeight;
                }

                var now = Runtime.World.FlyHeightOf(Handle);
                if (now == 0.0f && RemovedByGame())
                {
                    return lastFlyHeight;
                }

                if (Local.Depth == 0)
                {
                    lastFlyHeight = now;
                }

                return now;
            }

            set
            {
                if (Handle == null)
                {
                    Declare("umvh", value);
                    return;
                }

                if (InGame())
                {
                    lastFlyHeight = value;
                    if (!flyReady)
                    {
                        flyReady = true;
                        // A unit that flies shows the height as it is; one that has Amrf must not lose it to the remove.
                        if (!Runtime.World.CanFly(Handle))
                        {
                            var crowForm = ObjectTypes.FourCC("Amrf");
                            if (Runtime.World.AbilityLevel(Handle, crowForm) <= 0)
                            {
                                Runtime.World.AddAbility(Handle, crowForm);
                                Runtime.World.RemoveAbility(Handle, crowForm);
                            }
                        }
                    }

                    Runtime.World.SetFlyHeight(Handle, value);
                }
            }
        }

        /// <summary>The transport carrying the unit (a zeppelin, a ship, a burrow), or null. Kept from the game's load event and the order the game gives a unit on every way out, so it is the same on every machine (decision 219).</summary>
        [LocalSafe]
        public Unit? Transport => transport;

        /// <summary>The units the transport carries, in the order they were loaded: a new list each time it is read. The game finds no loaded unit around a transport, so this is the way to them.</summary>
        [LocalSafe]
        public List<Unit> Cargo => cargo == null ? new List<Unit>() : new List<Unit>(cargo);

        /// <summary>How much the transport's cargo hold holds (its Cargo Capacity); 0 for a unit without one, or once the unit is gone. Setting it changes this unit's alone.</summary>
        [LocalSafe]
        public int CargoCapacity
        {
            get { return IsGone ? 0 : Runtime.World.CargoCapacity(Handle); }
            set
            {
                if (!IsGone)
                {
                    Runtime.World.SetCargoCapacity(Handle, value);
                }
            }
        }

        /// <summary>Orders the transport to load a unit: it goes to the unit, or the unit to it, as the game's load order does; OnCargoLoaded runs once it is aboard.</summary>
        public bool Load(Unit cargoUnit)
        {
            return OrderOn("load", cargoUnit);
        }

        /// <summary>Orders the transport to let one unit out where it is.</summary>
        public bool Unload(Unit cargoUnit)
        {
            return OrderOn("unload", cargoUnit);
        }

        /// <summary>Orders the transport to go to a place and let every unit out there.</summary>
        public bool UnloadAll(float x, float y)
        {
            return OrderAt("unloadall", x, y);
        }

        /// <summary>Gives the unit an order with no place and no target, by the game's order string ("stop", "holdposition"), and returns the game's answer. False, and no call to the game, once the unit is gone.</summary>
        public bool Order(string order)
        {
            return !IsGone && Runtime.World.OrderUnit(Handle, order);
        }

        /// <summary>Gives the unit an order to a place, by the game's order string ("move", "attack", "smart"), and returns the game's answer. False, and no call to the game, once the unit is gone.</summary>
        public bool OrderAt(string order, float x, float y)
        {
            return !IsGone && Runtime.World.OrderUnitAt(Handle, order, x, y);
        }

        /// <summary>Gives the unit an order on a unit, an item or a destructable, by the game's order string ("attack", "smart"), and returns the game's answer. False, and no call to the game, once the unit or the target is gone.</summary>
        public bool OrderOn(string order, Widget target)
        {
            if (IsGone || target.IsGone)
            {
                return false;
            }

            var unit = target as Unit;
            if (unit != null)
            {
                return Runtime.World.OrderUnitOn(Handle, order, unit.Handle);
            }

            var item = target as Item;
            if (item != null)
            {
                return Runtime.World.OrderUnitOn(Handle, order, item.Handle);
            }

            var destructable = target as Destructable;
            if (destructable != null)
            {
                return Runtime.World.OrderUnitOn(Handle, order, destructable.Handle);
            }

            return false;
        }

        /// <summary>
        /// Casts a spell of a registered class at a unit, item or destructable for this unit, through a hidden dummy that stands at
        /// this unit (decision 169): none of this unit's mana, cooldown or animation is used, and it needn't have the spell. The
        /// spell's buff, hits and kills are this unit's (decision 168), and the cast runs no cast reactions (decision 171). A
        /// spell with no target is cast with the dummy standing at the target. A target in the player's fog is seen for the
        /// instant; an invisible one is refused (decision 186). Ending the scope stops what the dummy still does, a channel.
        /// Returns the game's answer: false for a target the spell can't take, a spell the player hasn't researched (decision
        /// 185, reported in a development build), or a unit that is gone.
        /// </summary>
        public bool CastOn<T>(Widget target, Scope scope, int level = 1)
            where T : Ability => CastOn(ObjectTypes.IdOf<T>(), target, scope, level);

        /// <summary>Casts a spell by its ability id at a unit, item or destructable for this unit, as CastOn&lt;T&gt; does.</summary>
        public bool CastOn(int abilityId, Widget target, Scope scope, int level = 1)
        {
            return !IsGone && Dummies.Cast(this, Owner, abilityId, target, 0.0f, 0.0f, scope, level);
        }

        /// <summary>
        /// Casts a spell of a registered class at a place for this unit, through a hidden dummy that stands at this unit, as
        /// CastOn&lt;T&gt; does; a spell with no target is cast with the dummy standing at the place.
        /// </summary>
        public bool CastAt<T>(float x, float y, Scope scope, int level = 1)
            where T : Ability => CastAt(ObjectTypes.IdOf<T>(), x, y, scope, level);

        /// <summary>Casts a spell by its ability id at a place for this unit, as CastAt&lt;T&gt; does.</summary>
        public bool CastAt(int abilityId, float x, float y, Scope scope, int level = 1)
        {
            return !IsGone && Dummies.Cast(this, Owner, abilityId, null, x, y, scope, level);
        }

        /// <summary>
        /// The unit's last order worth giving again, by a player or by code: a move, an attack, a right-click, a
        /// patrol, holding position, attacking the ground or a spell, not a stop, a toggle, an item's slot or the
        /// game's own orders. The two before it are its <c>Previous</c>; an order that repeats the last one (a Reissue,
        /// the same click again) takes its place. Null until the unit has had one while Moonwell listens for orders,
        /// which it does in a map whose code reads LastOrder or Reissue, or writes an order reaction.
        /// </summary>
        [LocalSafe]
        public Order? LastOrder { get; internal set; }

        /// <summary>What the game reads for a patrol under way (seen 11-03), a number with no name.</summary>
        private const int PatrolUnderWay = 851991;

        /// <summary>The game's string of the order the unit carries out now, as the game says (GetUnitCurrentOrder); "" when it is idle or gone, and "patrol" for a patrol under way, which the game reads as a number with no name. Compare it with the constants: <c>unit.CurrentOrder == Orders.Move</c>.</summary>
        [LocalSafe]
        public string CurrentOrder
        {
            get
            {
                if (IsGone)
                {
                    return "";
                }

                var id = Runtime.World.CurrentOrderOf(Handle);
                if (id == PatrolUnderWay)
                {
                    return Orders.Patrol;
                }

                return id == 0 ? "" : Runtime.World.OrderName(id);
            }
        }

        /// <summary>Gives the last order again, as after a knockback or a stop of the map's own, and returns the game's answer. False, and no order, for a unit with none, a dead or removed unit, a target that is dead or gone, and a spell already cast.</summary>
        public bool Reissue()
        {
            var last = LastOrder;
            if (last == null || last.Done || last.Name == "" || !IsAlive)
            {
                return false;
            }

            switch (last.Kind)
            {
                case OrderKind.Place:
                    return OrderAt(last.Name, last.X, last.Y);
                case OrderKind.Target:
                    return last.Target != null && Stands(last.Target) && OrderOn(last.Name, last.Target);
                default:
                    return Order(last.Name);
            }
        }

        /// <summary>Whether an order's target is still there to be given the order again.</summary>
        internal static bool Stands(Widget target)
        {
            var unit = target as Unit;
            if (unit != null)
            {
                return unit.IsAlive;
            }

            var destructable = target as Destructable;
            if (destructable != null)
            {
                return destructable.IsAlive;
            }

            return !target.IsGone;
        }

        /// <summary>Whether the game's stunned order came while the unit carried out an order, so that the unit's next order may be the one a stun's end gives back.</summary>
        internal bool StunnedOrderCame;

        /// <summary>The transport carrying the unit, as the load and unload events left it.</summary>
        internal Unit? transport;

        /// <summary>The units the transport carries, in load order; null while it carries none yet.</summary>
        internal List<Unit>? cargo;

        /// <summary>How many of the unit's orders ran their reactions: a count that changes inside an order's reactions means a reaction gave the unit another.</summary>
        internal int OrdersHeard;

        /// <summary>The id of the unit's last order that ran its reactions.</summary>
        internal int LastHeardId;

        /// <summary>The order the unit carries out, to give back when one is refused: the last order kept, until a stop, a build or another order not worth giving again replaced it; instant orders leave it.</summary>
        internal Order? Carried;

        /// <summary>Orders the unit to stop. Does nothing once the unit is gone.</summary>
        public void Stop()
        {
            Order("stop");
        }

        /// <summary>The number of inventory slots of the unit. Once the game has removed the unit, the last size read.</summary>
        [LocalSafe]
        public int InventorySize
        {
            get
            {
                if (IsGone)
                {
                    return lastInventorySize;
                }

                var now = Runtime.World.InventorySizeOf(Handle);
                if (now == 0 && RemovedByGame())
                {
                    return lastInventorySize;
                }

                if (Local.Depth == 0)
                {
                    lastInventorySize = now;
                }

                return now;
            }
        }

        /// <summary>The item in a slot of the unit's inventory, counting from 0; null when the slot is empty, when there is no such slot, and once the unit is gone. Not for Local.For code: an item Moonwell has not seen is taken in (its components and OnCreated run), which must happen on every machine.</summary>
        public Item? ItemIn(int slot)
        {
            if (IsGone || slot < 0 || slot >= InventorySize)
            {
                return null;
            }

            return Item.Of(Runtime.World.ItemInSlot(Handle, slot));
        }

        /// <summary>Gives the unit an item to pick up into its first free slot (a slot a drop freed is filled first), and returns the game's answer. The game counts it as a pick-up, so OnPickedUp and OnItemPickedUp run, inside this call. The answer is false, with no event and the item left where it is, when the inventory is full or the unit has none. Giving an item the unit already holds keeps its slot and runs the drop reactions and then the pick-up reactions (seen; the answer it gives was not recorded). False, and no call to the game, once the unit or the item is gone.</summary>
        public bool Give(Item item)
        {
            return !IsGone && !item.IsGone && Runtime.World.GiveItem(Handle, item.Handle);
        }

        /// <summary>Gives the unit an item into a slot of its inventory, counting from 0, and answers whether it is there. An item the unit does not hold is picked up first, as Give does, so OnPickedUp and OnItemPickedUp run inside this call; then it moves into the slot, which the game does with no event. Onto a taken slot the two items swap, as the game's slot move does. False, and no call to the game, for a slot the inventory does not have, for a full inventory that does not hold the item, and once the unit or the item is gone.</summary>
        public bool Give(Item item, int slot)
        {
            if (IsGone || item.IsGone || slot < 0 || slot >= InventorySize)
            {
                return false;
            }

            if (!Runtime.World.CarriesItem(Handle, item.Handle))
            {
                var free = false;
                for (var i = 0; i < InventorySize && !free; i++)
                {
                    free = Runtime.World.ItemInSlot(Handle, i) == null;
                }

                if (!free || !Runtime.World.GiveItem(Handle, item.Handle) || IsGone || item.IsGone)
                {
                    return false;
                }
            }

            return Runtime.World.MoveItemToSlot(Handle, item.Handle, slot);
        }

        /// <summary>Makes the unit drop an item from its inventory where it stands (UnitRemoveItem). The game counts it as a drop, so OnDropped and OnItemDropped run, inside this call and while the unit still has the item: the item's Carrier is the unit there, and null after the call. Does nothing for an item that lies on the ground, and once the unit or the item is gone. A unit whose inventory drops its items at death (a footman's; a hero's keeps them) drops them as it dies, and the drop reactions run before its OnDeath, with Carrier already null. The items of a unit removed with RemoveUnit leave it inside that call, but the game fires their drop events after the call, while the unit's type id still reads. For a unit Moonwell removed (Remove(), or its scope ending) no drop reaction runs, neither the item's nor the unit's, and its handle makes no second object; for one a native removed, which Moonwell has not noticed yet, the drop reactions run for an object that is about to be swept, with Carrier already null. Seen for a hero, whose items are then removed with it; other units are assumed to do the same.</summary>
        public void Drop(Item item)
        {
            if (!IsGone && !item.IsGone)
            {
                var carry = Runtime.World.CarriesItem(Handle, item.Handle) ? item.carry : null;
                Runtime.World.DropItem(Handle, item.Handle);
                if (carry != null && !carry.HasEnded && !Runtime.World.CarriesItem(Handle, item.Handle))
                {
                    // The drop event came while the unit still had the item, so its carry ends here, inside the call.
                    item.EndCarry(carry);
                }
            }
        }

        /// <summary>The starting components, which the runtime adds in order when the unit enters.</summary>
        internal List<UnitComponent> StartComponents => Components;

        /// <summary>The object of a game unit: made when the unit is first seen; null for null, for a unit the game has removed, and for one Moonwell has removed, whose handle still reads its type in that frame.</summary>
        public static Unit? Of(unit? handle)
        {
            if (handle == null)
            {
                return null;
            }

            var known = KnownUnits.Find(handle);
            if (known != null)
            {
                return known.RemovedByGame() ? null : known;
            }

            return KnownUnits.TakeIn(handle, null, false);
        }

        /// <summary>The facing of a unit made without one, in degrees: the game's bj_UNIT_FACING.</summary>
        public const float DefaultFacing = 270.0f;

        /// <summary>Creates a unit of a type for its owning player at a place, in a scope, which ends it when it ends, and with a facing (270 degrees without one). Throws a MoonwellException naming the id when the game makes no unit of the type.</summary>
        public static Unit Create(int typeId, Player owner, float x, float y, Scope scope, float facing = DefaultFacing)
        {
            var handle = UnitPool.Take(typeId, owner, x, y, facing);
            if (handle == null)
            {
                handle = Runtime.World.SpawnUnit(owner.Handle, typeId, x, y, facing);
                if (handle == null)
                {
                    throw new MoonwellException("Unit.Create: the game made no unit of type " + Names.IdText(typeId));
                }

                UnitPool.Made(typeId, handle);
            }

            Unit made;
            try
            {
                made = KnownUnits.TakeIn(handle, scope, true)!;
            }
            catch
            {
                // A class whose constructor throws leaves no unit in the game that nothing holds.
                Runtime.World.DeleteUnit(handle);
                throw;
            }
            if (made.Parent == null)
            {
                scope.Adopt(made);
            }

            return made;
        }

        /// <summary>Creates a unit of a registered class for its owning player at a place, in a scope, which ends it when it ends, and with a facing (270 degrees without one).</summary>
        public static T Create<T>(Player owner, float x, float y, Scope scope, float facing = DefaultFacing)
            where T : Unit
        {
            return (T)Create(ObjectTypes.IdOf<T>(), owner, x, y, scope, facing);
        }

        /// <summary>Summons a unit of a registered class for this unit's player at a place (decision 227): it has the summoned type, so dispels hurt it, and with seconds above 0 the game's timed life and its bar, which kills it when it runs out; its Summoner is this unit. It dies when the scope ends, and with diesWithSummoner when this unit dies or leaves the game. A limit above 0 keeps at most that many of the class from this unit: the oldest die. Runs OnSummon and OnUnitSummoned. Throws a MoonwellException when this unit has left the game.</summary>
        public T Summon<T>(float x, float y, float seconds, Scope scope, int limit = 0, bool diesWithSummoner = false, float facing = DefaultFacing)
            where T : Unit
        {
            return (T)Summoning.Make(this, ObjectTypes.IdOf<T>(), x, y, seconds, scope, limit, diesWithSummoner, facing);
        }

        /// <summary>Summons a unit of a type by id, as Summon of a class does.</summary>
        public Unit Summon(int typeId, float x, float y, float seconds, Scope scope, int limit = 0, bool diesWithSummoner = false, float facing = DefaultFacing)
        {
            return Summoning.Make(this, typeId, x, y, seconds, scope, limit, diesWithSummoner, facing);
        }

        /// <summary>The unit that summoned this one, by Summon or by the game's spell (a Water Elemental's Archmage, an illusion's maker); null for a unit nobody summoned. Kept after the summoner dies or leaves.</summary>
        [LocalSafe]
        public Unit? Summoner { get; internal set; }

        /// <summary>The living units this unit summoned, oldest first.</summary>
        [LocalSafe]
        public IReadOnlyList<Unit> Summons => Summoning.LivingOf(this);

        /// <summary>The ability of the unit with an id: one object per id, made by the registered maker or as a plain Ability, in the unit's scope.</summary>
        public Ability AbilityOf(int abilityId)
        {
            if (abilities.TryGetValue(abilityId, out var known))
            {
                return known;
            }

            var maker = ObjectTypes.AbilityMaker(abilityId);
            var made = maker != null ? maker() : new Ability();
            made.Unit = this;
            made.Id = abilityId;
            Adopt(made);
            made.Mask = Hooks.MaskOf(made, Ability.AbilityHooks);
            Events.Need(made.Mask);
            abilities[abilityId] = made;
            return made;
        }

        /// <summary>The ability of the unit of a registered class: the object of that class.</summary>
        public T AbilityOf<T>()
            where T : Ability => (T)AbilityOf(ObjectTypes.IdOf<T>());

        /// <summary>Gives the unit an ability at level 1, made permanent so that a morph keeps it at its level, and returns the unit's object of it. A hero skill given to a hero runs the learn reactions, as learning it does: the ability's OnLearned, the hero's OnSkillLearned and its components', then the map's OnHeroSkillLearned. A unit that has the ability already keeps it at its level, and nothing runs. Once the unit is gone, the object, and no call to the game.</summary>
        public Ability AddAbility(int abilityId)
        {
            var ability = AbilityOf(abilityId);
            if (IsGone || !Runtime.World.AddAbility(Handle, abilityId))
            {
                return ability;
            }

            Runtime.World.MakeAbilityPermanent(Handle, abilityId);
            ability.lastLevel = 1;
            var hero = this as Hero;
            if (hero != null && Runtime.World.IsHeroAbility(Handle, abilityId))
            {
                Inbound.Learned(hero, ability);
            }

            return ability;
        }

        /// <summary>Gives the unit an ability of a registered class, as AddAbility(id) does, and returns the object of that class.</summary>
        public T AddAbility<T>()
            where T : Ability => (T)AddAbility(ObjectTypes.IdOf<T>());

        /// <summary>Takes an ability from the unit, one of its type's own too, and answers whether it had it. A cast of the ability that is running ends inside the call, as the game ends the spell. A hero skill taken from a hero runs the ability's OnLost after the removal, and gives no skill point back. False, and no call to the game, once the unit is gone.</summary>
        public bool RemoveAbility(int abilityId)
        {
            if (IsGone)
            {
                return false;
            }

            var skill = this is Hero && Runtime.World.IsHeroAbility(Handle, abilityId);
            if (!Runtime.World.RemoveAbility(Handle, abilityId))
            {
                return false;
            }

            if (abilities.TryGetValue(abilityId, out var known))
            {
                known.lastLevel = 0;
            }

            if (skill)
            {
                Inbound.Lost(AbilityOf(abilityId));
            }

            return true;
        }

        /// <summary>Takes an ability of a registered class from the unit, as RemoveAbility(id) does.</summary>
        public bool RemoveAbility<T>()
            where T : Ability => RemoveAbility(ObjectTypes.IdOf<T>());

        /// <summary>Whether the unit has an ability: its level is above 0. A carried item's ability counts, as the game gives it to the unit. False once the unit is gone.</summary>
        [LocalSafe]
        public bool HasAbility(int abilityId)
        {
            return !IsGone && Runtime.World.AbilityLevel(Handle, abilityId) > 0;
        }

        /// <summary>Whether the unit has an ability of a registered class, as HasAbility(id) says.</summary>
        [LocalSafe]
        public bool HasAbility<T>()
            where T : Ability => HasAbility(ObjectTypes.IdOf<T>());

        /// <summary>Adds a flat amount to a stat while a scope holds it (decision 221), and returns the modifier, whose End takes it off sooner: Modify(Stat.Armor, 5, heldBy: Item.Carry). A holder that has ended adds nothing, reported in development builds.</summary>
        public Modifier Modify(Stat stat, float amount, Scope heldBy) => Stats.Add(stat, amount, 0.0f, heldBy, "Unit.Modify");

        /// <summary>Adds a percent to a stat while a scope holds it (decision 221), and returns the modifier: Modify(Stat.Damage, percent: 20, heldBy: this). Percents add up, then multiply the base and the flat amounts.</summary>
        public Modifier Modify(Stat stat, Scope heldBy, float percent) => Stats.Add(stat, 0.0f, percent, heldBy, "Unit.Modify");

        /// <summary>A stat's value now: (base + flat) × (1 + percents / 100). A game stat reads the unit's number in the game, less what its items and the game's own bonuses add to damage and attack speed; 0 once the unit is gone.</summary>
        [LocalSafe]
        public float Get(Stat stat) => Stats.Get(stat);

        internal StatSheet? stats;

        internal StatSheet Stats => stats ??= new StatSheet(this);

        /// <summary>Adds a component to the unit, and returns it; held by a scope, it also ends when that scope ends (decision 164).</summary>
        public T Add<T>(T component, Scope? heldBy = null)
            where T : UnitComponent => AddComponent(component, heldBy);

        /// <summary>Applies a buff to the unit and returns the buff now on it: a buff of its class already there is refreshed, as its stacking says (it takes the given buff's duration and the source, and keeps its other values and what it has counted), and the given buff is dropped; otherwise the given buff is added and started. A buff that is on a unit already (one that applies itself again in OnExpire) or has ended is left as it is, and the error is reported, in every build. Held by a scope, the buff also ends when its last holder has ended, and an apply from another holder joins it as its stacking says (decision 164); an apply with no holder keeps it on after its holders let go.</summary>
        public T Apply<T>(T buff, Unit? source = null, Scope? heldBy = null)
            where T : Buff
        {
            Dev.Check(buff.Duration >= 0.0f && buff.Tick >= 0.0f, "Unit.Apply: Duration and Tick must be 0 or more");
            if (SpellBuffs.Watches(ObjectTypes.BuffIdOf(buff)))
            {
                Errors.Refuse("Unit.Apply", Names.NameOf(buff) + " is a buff a spell lists, which the game puts on and Moonwell watches for (decision 154), so it is not applied");
                return buff;
            }

            if (heldBy != null && (heldBy.HasEnded || heldBy.childrenEnded))
            {
                // Add reports it and ends the given buff, adding nothing.
                return Add(buff, heldBy);
            }

            // An ended buff is reported and left as it is, also where it would refresh one on the unit.
            if (buff.HasEnded && Refuses(buff, "Apply"))
            {
                return buff;
            }

            var existing = StackedWith(buff, source);
            if (existing != null && existing.Stacking == Stacking.Strongest)
            {
                existing.Offer(buff, source, heldBy);
                return (T)existing;
            }

            if (existing != null)
            {
                // The holder holds it before OnApply runs, so a holder that ends there leaves it held by this one.
                var timed = existing.expiry != null && !existing.expiry.HasEnded;
                if (heldBy == null)
                {
                    existing.unheld = true;
                }
                else
                {
                    existing.HoldBy(heldBy);
                }

                existing.Refresh(buff, source);

                // A held refresh that ends the duration of an apply with no holder leaves only its holders to keep it on.
                if (heldBy != null && timed && existing.Duration <= 0.0f)
                {
                    existing.unheld = false;
                }

                return (T)existing;
            }

            // A buff on a unit already (one that applies itself again) or one that has ended is reported and left as it is.
            if (Refuses(buff, "Apply"))
            {
                return buff;
            }

            buff.Source = source;
            if (buff.Stacking == Stacking.Strongest)
            {
                // A strongest buff keeps each apply's holder with its strength, which Begin takes.
                Add(buff);
                if (!buff.HasEnded)
                {
                    buff.Begin(heldBy);
                }

                return buff;
            }

            buff.unheld = heldBy == null;
            Add(buff, heldBy);
            if (!buff.HasEnded)
            {
                buff.Begin();
            }

            return buff;
        }

        /// <summary>The buff on the unit that a new apply stacks with, or null: the first of its class for Refresh and Strongest, the first of its class from the same source for PerSource, none for Separate.</summary>
        private Buff? StackedWith(Buff buff, Unit? source)
        {
            if (buff.Stacking == Stacking.Separate)
            {
                return null;
            }

            var found = GetAll<Buff>();
            var key = Names.ClassOf(buff);
            for (var i = 0; i < found.Count; i++)
            {
                var other = found[i];
                if (other.HasEnded || Names.ClassOf(other) != key)
                {
                    continue;
                }

                if (buff.Stacking == Stacking.Refresh || buff.Stacking == Stacking.Strongest || other.Source == source)
                {
                    return other;
                }
            }

            return null;
        }

        /// <summary>Removes the unit from the game: it ends, which runs its OnLeave. Does nothing on a default object, which is not in the game.</summary>
        public void Remove()
        {
            if (Handle != null)
            {
                End();
            }
        }

        /// <summary>Runs once, when the unit comes into the game, after its starting components are added.</summary>
        protected virtual void OnCreated()
        {
        }

        /// <summary>Runs when the unit leaves the game, before its components end; HasEnded is still false here. It runs once for each OnCreated: one made in a scope that had already ended, which ends at once, runs neither.</summary>
        protected virtual void OnLeave()
        {
        }

        /// <summary>Runs OnCreated; Units calls it, since a map overrides the hook as protected.</summary>
        internal void CallEnter()
        {
            OnCreated();
        }

        /// <summary>Runs when the unit picks up an item, before the unit's components see it.</summary>
        /// <param name="pickedItem">The item the unit picked up.</param>
        public virtual void OnItemPickedUp(Item pickedItem)
        {
        }

        /// <summary>Runs when the unit drops an item, before the unit's components see it; for the items a dying unit drops, before its OnDeath (see Drop).</summary>
        /// <param name="droppedItem">The item the unit dropped.</param>
        public virtual void OnItemDropped(Item droppedItem)
        {
        }

        /// <summary>Runs when the unit takes damage, before the unit's components see it.</summary>
        public virtual void OnDamaged(Damage damage)
        {
        }

        /// <summary>Runs when the unit deals damage, before the unit's components see it.</summary>
        public virtual void OnDealtDamage(Damage damage)
        {
        }

        /// <summary>Runs when the unit dies, before the unit's components see it; the killer is null when no unit killed it. Death is not an end: the unit lasts until its corpse goes, which can be at any time (it decays, explodes or is raised), so a timer made here with After is the unit's and ends with the corpse; a respawn timer is made on the game: Game.Instance.After(30f, Respawn).</summary>
        /// <param name="killerUnit">The unit whose damage killed it; null when no unit did: Kill() or Life set to 0 from code, timed life running out, or its own damage (seen 10-21).</param>
        public virtual void OnDeath(Unit? killerUnit)
        {
        }

        /// <summary>Runs when the unit kills another, before the unit's components see it.</summary>
        /// <param name="killedUnit">The unit it killed.</param>
        public virtual void OnKill(Unit killedUnit)
        {
        }

        /// <summary>Runs when the unit begins a cast, after the cast's ability has seen it and before the unit's components see it.</summary>
        public virtual void OnCast(Cast cast)
        {
        }

        /// <summary>
        /// Runs when the unit is given an order, by a player or by code (inside the call), before the order is carried out and
        /// before the unit's components hear it. <c>order.Cancel()</c> refuses it; an order given here replaces it. The game's own
        /// orders (the stunned order, undefend, the order a stun's end gives back) and Moonwell's own refusing orders run nothing.
        /// </summary>
        public virtual void OnOrdered(Order order)
        {
        }

        /// <summary>Runs when this unit summons another, by Summon or by the game's spell, before its components see it; the summon's Summoner is already this unit. A summon whose scope had ended, which dies at once, runs none.</summary>
        /// <param name="summonedUnit">The unit summoned.</param>
        public virtual void OnSummon(Unit summonedUnit)
        {
        }

        /// <summary>Runs when this building starts a research, inside the order that starts it, before its components see it (decision 213).</summary>
        /// <param name="upgrade">The upgrade researched: its class's object, or a plain Upgrade of its id.</param>
        public virtual void OnResearchStarted(Upgrade upgrade)
        {
        }

        /// <summary>Runs when this building's research is cancelled, before its components see it.</summary>
        /// <param name="upgrade">The upgrade whose research was cancelled.</param>
        public virtual void OnResearchCancelled(Upgrade upgrade)
        {
        }

        /// <summary>Runs when this building's research finishes, after the upgrade's OnResearched for the new level, before its components see it.</summary>
        /// <param name="upgrade">The upgrade researched.</param>
        public virtual void OnResearchFinished(Upgrade upgrade)
        {
        }

        /// <summary>Runs when this shop sells an item, to a player's click or to IssueNeutralImmediateOrder: inside the game's sale, with the gold and lumber already taken and before the buyer's pick-up, so the item has no owner yet (seen 11-08). Before its components see it (decision 214).</summary>
        /// <param name="item">The item sold.</param>
        /// <param name="buyer">The unit that receives it.</param>
        public virtual void OnItemSold(Item item, Unit buyer)
        {
        }

        /// <summary>Runs when a unit sells an item to this shop: before the seller's drop, and before its gold comes; the game removes the item soon after (seen 11-08). Before its components see it.</summary>
        /// <param name="item">The item sold to the shop.</param>
        /// <param name="seller">The unit that sold it.</param>
        public virtual void OnItemPawned(Item item, Unit seller)
        {
        }

        /// <summary>Runs when this shop sells a unit, as a mercenary camp does, on the game's sale event; how it falls among the new unit's other events is not measured. Before its components see it.</summary>
        /// <param name="soldUnit">The unit sold.</param>
        /// <param name="buyer">The unit that bought it.</param>
        public virtual void OnUnitSold(Unit soldUnit, Unit buyer)
        {
        }

        /// <summary>
        /// Puts an item class on sale in this shop, with a stock count and a most (AddItemToStock), or changes the count of one put
        /// on sale so. The game sells it only from a shop with Sell Items (Asid, as the Marketplace has; a development build reports
        /// one without), and never changes a good of the shop's own data (seen 11-08). A neutral passive shop takes a good sold
        /// out of its stock (Blizzard.j's RemovePurchasedItem, which every map runs), so it does not restock. Does nothing once
        /// the shop is gone.
        /// </summary>
        public void AddStock<T>(int count, int max) where T : Item
        {
            if (IsGone)
            {
                return;
            }

            if (Runtime.World.AbilityLevel(Handle, ObjectTypes.FourCC("Asid")) == 0)
            {
                Dev.Report(Names.NameOf(this) + ".AddStock", "the shop has no Sell Items (Asid), so the game sells nothing added to it");
            }

            Runtime.World.AddStock(Handle, ObjectTypes.IdOf<T>(), count, max);
        }

        /// <summary>Takes an item class put on sale with AddStock off sale in this shop (RemoveItemFromStock); a good of the shop's own data stays. Does nothing once the shop is gone.</summary>
        public void RemoveStock<T>() where T : Item
        {
            if (!IsGone)
            {
                Runtime.World.RemoveStock(Handle, ObjectTypes.IdOf<T>());
            }
        }

        /// <summary>Runs when a player selects the unit, before the unit's components see it: a click, a drag, a control group or code, as the game's event says, so a click on a unit already selected runs it again. It comes a moment after Select (0.1 to 0.2 s in single player, the network's delay online), and Selection already holds the unit. A unit the game drops from a selection with no event (it dies, is removed, hidden, gets Locust or changes hands) runs no OnDeselected (decision 203).</summary>
        /// <param name="selectingPlayer">The player who selected it.</param>
        public virtual void OnSelected(Player selectingPlayer)
        {
        }

        /// <summary>Runs when a player deselects the unit, before the unit's components see it; Selection no longer holds it. A click on another unit, a drag or a control group deselects every unit first, even one selected again in the same instant.</summary>
        /// <param name="deselectingPlayer">The player who deselected it.</param>
        public virtual void OnDeselected(Player deselectingPlayer)
        {
        }

        /// <summary>Runs when the unit is loaded into a transport, before the unit's components see it, then the transport's OnCargoLoaded. The unit's place reads where it was loaded until it comes out (seen 11-08).</summary>
        /// <param name="carryingTransport">The transport it is in.</param>
        public virtual void OnLoaded(Unit carryingTransport)
        {
        }

        /// <summary>Runs when the unit comes out of its transport, before the unit's components see it: unloaded, or let out because the transport died or was removed (the game puts it down alive where the transport was; seen 11-08). A unit that dies or leaves the game inside runs none.</summary>
        /// <param name="carryingTransport">The transport it was in.</param>
        public virtual void OnUnloaded(Unit carryingTransport)
        {
        }

        /// <summary>Runs when a unit is loaded into this transport, after the unit and its components saw it, before this unit's components see it.</summary>
        /// <param name="loadedUnit">The unit loaded.</param>
        public virtual void OnCargoLoaded(Unit loadedUnit)
        {
        }

        /// <summary>Runs when a unit comes out of this transport, after the unit and its components saw it, before this unit's components see it; Cargo no longer holds it.</summary>
        /// <param name="unloadedUnit">The unit that came out.</param>
        public virtual void OnCargoUnloaded(Unit unloadedUnit)
        {
        }

        /// <summary>Tells the interactions package, if there is one; runs OnLeave; the components then end, and the unit leaves the game. One that never came in, as one made in a scope that had ended, runs neither, as it ran no OnCreated.</summary>
        protected sealed override void OnEnd()
        {
            if (!entered)
            {
                return;
            }

            Hooks.CallEnding(this);
            try
            {
                OnLeave();
            }
            catch (Exception error)
            {
                Errors.Report(Names.NameOf(this) + ".OnLeave", error);
            }
        }

        /// <summary>Ends the unit's effects, texts, sounds, lightning and minimap icons, removes the unit from the game unless the game has already, and forgets it.</summary>
        internal override void Finish()
        {
            Summoning.SummonerEnds(this);
            if (attached != null)
            {
                var ending = new List<Scoped>(attached);
                for (var i = 0; i < ending.Count; i++)
                {
                    ending[i].End();
                }
            }

            var kept = false;
            if (!IsGone)
            {
                kept = UnitPool.Keep(this);
                if (!kept)
                {
                    Runtime.World.DeleteUnit(Handle);
                }

                IsGone = true;
            }

            if (transport != null || cargo != null)
            {
                Inbound.TransportLeft(this);
            }

            KnownUnits.Forget(this, kept);
        }
    }
}
