using System.Reflection;
using System.Reflection.Emit;
using Moonwell.Engine;
using Moonwell.Hosting;

namespace Moonwell.Tests;

/// <summary>
/// Which <see cref="IWorld"/> members change the game: those whose code in <see cref="NativeWorld"/> reaches a native
/// tagged <c>[ChangesState]</c> or <c>[CreatesHandle]</c>, the tags the analyzer refuses inside <c>Local.For</c>. Read
/// from the compiled code, so a new member is marked by the natives it calls.
/// </summary>
internal static class GameChanging
{
    private static readonly Dictionary<short, OpCode> opCodes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => code.Value);

    /// <summary>
    /// The reviewed list of members Moonwell calls on one machine on purpose, though a native of theirs carries a tag:
    /// the tags call every native they do not list a change of state. Each is safe on one machine by what the game is
    /// known to do, and none has been seen in a game with two players, so this is also the list to try first online.
    /// A member is added here only when a case's two machines differ by it.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> OneMachine = new Dictionary<string, string>
    {
        ["ShowWeather"] = "EnableWeatherEffect: weather shown to one player is made on every machine and turned on at theirs",
        ["ShowIcon"] = "SetMinimapIconVisible: a minimap icon shown to one player is made on every machine and hidden at the others",
        ["ShowQuest"] = "QuestSetEnabled: a quest shown to one player is made on every machine and enabled at theirs",
        ["Letterbox"] = "EnableOcclusion, beside ShowInterface: a cinematic changes only its watchers' screens (decision 208)",
        ["CinematicSounds"] = "SetCineModeVolumeGroupsBJ: a cinematic changes only its watchers' sound mix (decision 208)",
        ["MakeText"] = "CreateTextTag: one player's floating text is made on their machine only (decision 201); the game counts text tags apart from handle ids",
        ["EndText"] = "DestroyTextTag: ends a text made on this machine only (decision 201)",
        ["ReadPreload"] = "Preloader, through an ability's tooltips: a player's file is read on that player's machine, and what it holds is shared after",
    };

    /// <summary>Each member that changes the game on every machine, with the tagged natives it reaches.</summary>
    public static readonly IReadOnlyDictionary<MethodInfo, string[]> Members = Find(false);

    /// <summary>The members of OneMachine, with the tagged natives they reach.</summary>
    public static readonly IReadOnlyDictionary<MethodInfo, string[]> OneMachineMembers = Find(true);

    private static Dictionary<MethodInfo, string[]> Find(bool oneMachine)
    {
        var found = new Dictionary<MethodInfo, string[]>();
        var map = typeof(NativeWorld).GetInterfaceMap(typeof(IWorld));
        for (var i = 0; i < map.InterfaceMethods.Length; i++)
        {
            var natives = new SortedSet<string>(StringComparer.Ordinal);
            Collect(map.TargetMethods[i], natives, new HashSet<MethodBase>());
            if (natives.Count > 0 && OneMachine.ContainsKey(map.InterfaceMethods[i].Name) == oneMachine)
            {
                found[map.InterfaceMethods[i]] = natives.ToArray();
            }
        }

        return found;
    }

    // Follows calls and method references inside NativeWorld and the classes the compiler nests in it for lambdas.
    private static void Collect(MethodBase method, SortedSet<string> natives, HashSet<MethodBase> seen)
    {
        if (!seen.Add(method))
        {
            return;
        }

        foreach (var called in Called(method))
        {
            if (called.DeclaringType == typeof(Natives))
            {
                if (called.IsDefined(typeof(ChangesStateAttribute)) || called.IsDefined(typeof(CreatesHandleAttribute)))
                {
                    natives.Add(called.Name);
                }
            }
            else if (IsNativeWorlds(called.DeclaringType))
            {
                Collect(called, natives, seen);
            }
        }
    }

    private static bool IsNativeWorlds(Type? type)
    {
        for (; type != null; type = type.DeclaringType)
        {
            if (type == typeof(NativeWorld))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<MethodBase> Called(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray() ?? [];
        var typeArguments = method.DeclaringType!.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        for (var at = 0; at < il.Length;)
        {
            short value = il[at++];
            if (value == 0xFE)
            {
                value = unchecked((short)(0xFE00 | il[at++]));
            }

            var code = opCodes[value];
            switch (code.OperandType)
            {
                case OperandType.InlineMethod:
                    yield return method.Module.ResolveMethod(BitConverter.ToInt32(il, at), typeArguments, methodArguments)!;
                    at += 4;
                    break;
                case OperandType.InlineSwitch:
                    at += 4 + 4 * BitConverter.ToInt32(il, at);
                    break;
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    at += 1;
                    break;
                case OperandType.InlineVar:
                    at += 2;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    at += 8;
                    break;
                default:
                    at += 4;
                    break;
            }
        }
    }
}
