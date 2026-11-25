using System.Collections;
using System.Globalization;
using System.Reflection;
using Moonwell.Hosting;
using Moonwell.Testing;

namespace Moonwell.Tests;

/// <summary>
/// One run's record: the calls that change the game (<see cref="GameChanging"/>), in order, with what they were given
/// and what they answered, from <see cref="IWorld"/> and every package world alike.
/// </summary>
internal sealed class Recording
{
    private readonly Dictionary<object, string> handles = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(object, Type), object> wrapped = new();
    private FakeWorld fake = null!;

    public Recording(List<string> calls)
    {
        Calls = calls;
    }

    public List<string> Calls { get; }

    /// <summary>The runtime's world, recorded: FakeWorld.Around is set to this.</summary>
    public IWorld Around(FakeWorld inner)
    {
        fake = inner;
        return (IWorld)Wrap(inner, typeof(IWorld));
    }

    /// <summary>
    /// A package's world, recorded: Worlds.Around is set to this. A world already recorded is kept as it is, and one
    /// wrapped before gets the same wrapper, as a package that is installed again on the same world keeps what it made.
    /// </summary>
    public object Wrap(object inner, Type face)
    {
        if (inner is RecordingWorld)
        {
            return inner;
        }

        if (!wrapped.TryGetValue((inner, face), out var world))
        {
            world = DispatchProxy.Create(face, typeof(RecordingWorld));
            ((RecordingWorld)world).Start(inner, this, face == typeof(IWorld) ? "" : face.Name + ".");
            wrapped[(inner, face)] = world;
        }

        return world;
    }

    // A handle is named by its type and the order it was first seen in, which is the same on both machines until they part.
    public string Show(object? value)
    {
        switch (value)
        {
            case null:
                return "null";
            case player player:
                return "slot " + fake.SlotOf(player);
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

/// <summary>
/// A world, <see cref="IWorld"/> or a package's, that passes every call on to the fake one it wraps and writes down the
/// ones that change the game into its <see cref="Recording"/>; a package world's calls carry its interface's name.
/// </summary>
internal class RecordingWorld : DispatchProxy
{
    private object inner = null!;
    private Recording recording = null!;
    private string prefix = "";

    internal void Start(object wrapped, Recording into, string name)
    {
        inner = wrapped;
        recording = into;
        prefix = name;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (!GameChanging.Members.ContainsKey(method!))
        {
            return method!.Invoke(inner, BindingFlags.DoNotWrapExceptions, null, args, null);
        }

        // Written down before the call, as the fake game runs reactions inside it, whose own calls come after.
        var calls = recording.Calls;
        var at = calls.Count;
        calls.Add(prefix + method!.Name + "(" + string.Join(", ", args!.Select(recording.Show)) + ")");
        var answer = method.Invoke(inner, BindingFlags.DoNotWrapExceptions, null, args, null);
        if (method.ReturnType != typeof(void))
        {
            calls[at] += " -> " + recording.Show(answer);
        }

        return answer;
    }
}
