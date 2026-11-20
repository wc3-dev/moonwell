namespace Moonwell
{
    /// <summary>Reactions to the heals a unit gives, on the unit's class or its components; write only the ones needed, as public methods.</summary>
    public interface IHealSource
    {
        /// <summary>Before the life is set: the heal may change its amount.</summary>
        /// @CSharpLua.Ignore
        void OnHealing(Heal heal)
        {
        }

        /// <summary>After the life is set: <see cref="Heal.Done"/> and <see cref="Heal.Over"/> tell what went in.</summary>
        /// @CSharpLua.Ignore
        void OnHealed(Heal heal)
        {
        }
    }
}
