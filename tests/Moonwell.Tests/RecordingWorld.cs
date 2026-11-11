using System.Collections;
using System.Globalization;
using System.Reflection;
using Moonwell.Hosting;
using Moonwell.Testing;

namespace Moonwell.Tests;

/// <summary>
/// An <see cref="IWorld"/> that passes every call on to a <see cref="FakeWorld"/> and writes down the ones that change
/// the game (<see cref="GameChanging"/>), in order, with what they were given and what they answered.
/// </summary>
internal class RecordingWorld : DispatchProxy
{
    private FakeWorld inner = null!;
    private List<string> calls = null!;
    private readonly Dictionary<object, string> handles = new(ReferenceEqualityComparer.Instance);

    /// <summary>A world that records the runtime's calls on a fake world into a list.</summary>
    public static IWorld Around(FakeWorld inner, List<string> calls)
    {
        var world = Create<IWorld, RecordingWorld>();
        var recording = (RecordingWorld)(object)world;
        recording.inner = inner;
        recording.calls = calls;
        return world;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (!GameChanging.Members.ContainsKey(method!))
        {
            return method!.Invoke(inner, BindingFlags.DoNotWrapExceptions, null, args, null);
        }

        // Written down before the call, as the fake game runs reactions inside it, whose own calls come after.
        var at = calls.Count;
        calls.Add(method!.Name + "(" + string.Join(", ", args!.Select(Show)) + ")");
        var answer = method.Invoke(inner, BindingFlags.DoNotWrapExceptions, null, args, null);
        if (method.ReturnType != typeof(void))
        {
            calls[at] += " -> " + Show(answer);
        }

        return answer;
    }

    // A handle is named by its type and the order it was first seen in, which is the same on both machines until they part.
    private string Show(object? value)
    {
        switch (value)
        {
            case null:
                return "null";
            case player player:
                return "slot " + inner.SlotOf(player);
            case handle handle:
                if (!handles.TryGetValue(handle, out var name))
                {
                    name = handle.GetType().Name + " " + (handles.Count + 1);
                    handles[handle] = name;
                }

                return name;
            case string text:
                return "\"" + text + "\"";
            case Color color:
                return "color " + color.R + " " + color.G + " " + color.B + " " + color.A;
            case Delegate:
                return "code";
            case IEnumerable list:
                return "[" + string.Join(", ", list.Cast<object?>().Select(Show)) + "]";
            default:
                return Convert.ToString(value, CultureInfo.InvariantCulture)!;
        }
    }
}
