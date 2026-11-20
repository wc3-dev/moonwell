namespace Moonwell
{
    /// <summary>Reactions to the heals a unit receives, on the unit's class or its components; write only the ones needed, as public methods.</summary>
    public interface IHealTarget
    {
        /// <summary>Before the life is set: the heal may change its amount.</summary>
        /// @CSharpLua.Ignore
        void OnReceiving(Heal heal)
        {
        }

        /// <summary>After the life is set: <see cref="Heal.Done"/> and <see cref="Heal.Over"/> tell what went in.</summary>
        /// @CSharpLua.Ignore
        void OnReceived(Heal heal)
        {
        }
    }
}
