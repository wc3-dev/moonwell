using System.ComponentModel;
#if DEBUG
using Moonwell.AI;
#endif

namespace Moonwell
{
    /// <summary>Starts the AI package; a map's generated Main calls Install when the map uses the package.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AISystem
    {
        /// <summary>Installs the package on the game, before anything registers: in a development build, its tools join the debug mode, so their commands answer and -help lists them before the first brain. A release build does nothing here; the package's state is made at its first use.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Install()
        {
#if DEBUG
            Minds.Current();
#endif
        }
    }
}
