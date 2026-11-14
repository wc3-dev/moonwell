namespace Moonwell.Procgen
{
    /// <summary>The scope Procgen makes ground in, one child of the scope given: it holds a whole forest, so a development build leaves it out of the leak report (decision 326).</summary>
    internal sealed class GroundScope : Scope
    {
        internal GroundScope(Scope scope)
        {
            IgnoreLeaks();
            scope.Adopt(this);
        }
    }
}
