using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Moonwell.GameRun;

/// <summary>A key the runner posts to the game's window.</summary>
public enum GameKey
{
    /// <summary>Gets the loading screen past, which waits for a key.</summary>
    Space = 0x20,

    /// <summary>Opens the game's menu.</summary>
    F10 = 0x79,

    /// <summary>Closes the menu and returns to the game.</summary>
    Escape = 0x1B,
}

/// <summary>
/// A game this runner started, and everything it started in turn, in one Windows job: the process is made suspended, put in
/// the job and only then let run, so nothing it starts is outside the job, and ending the job ends them all (a game that
/// ends by itself leaves its BlizzardBrowser processes running otherwise, game facts). The job also ends them when this
/// process ends, however it ends.
/// </summary>
public sealed class GameJob : IDisposable
{
    private const uint CreateSuspended = 0x4;
    private const int ExtendedLimitInformation = 9;
    private const int BasicProcessIdList = 3;
    private const uint KillOnJobClose = 0x2000;
    private const uint KeyDown = 0x100;
    private const uint KeyUp = 0x101;
    private const uint OwnerOf = 4;

    private readonly SafeFileHandle job;
    private readonly SafeProcessHandle process;

    private GameJob(SafeFileHandle job, SafeProcessHandle process, int processId)
    {
        this.job = job;
        this.process = process;
        ProcessId = processId;
    }

    /// <summary>The id of the game's own process.</summary>
    public int ProcessId { get; }

    /// <summary>Whether the game's own process has ended.</summary>
    public bool HasExited => process.IsClosed || WaitForSingleObject(process, 0) == 0;

    /// <summary>The ids of the job's processes that run now: the game and what it started.</summary>
    public IReadOnlyList<int> ProcessIds
    {
        get
        {
            if (job.IsClosed)
            {
                return Array.Empty<int>();
            }

            var list = new ProcessIdList { Ids = new nuint[ProcessIdList.Room] };
            return QueryInformationJobObject(job, BasicProcessIdList, ref list, Marshal.SizeOf<ProcessIdList>(), 0)
                ? list.Ids.Take((int)list.Listed).Select(id => (int)id).ToList()
                : Array.Empty<int>();
        }
    }

    /// <summary>Starts the program with the arguments, in its own folder, inside a new job.</summary>
    /// <exception cref="InvalidOperationException">Windows refused one of the calls; the message gives its reason.</exception>
    public static GameJob Start(string program, string arguments)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException("the game runs on Windows only");
        }

        var job = CreateJobObjectW(0, null);
        var limits = new ExtendedLimits { Basic = { LimitFlags = KillOnJobClose } };
        if (job.IsInvalid || !SetInformationJobObject(job, ExtendedLimitInformation, ref limits, Marshal.SizeOf<ExtendedLimits>()))
        {
            throw Refused(program, job);
        }

        var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
        var commandLine = new StringBuilder($"\"{program}\" {arguments}");
        var folder = Path.GetDirectoryName(program);
        if (!CreateProcessW(null, commandLine, 0, 0, false, CreateSuspended, 0, string.IsNullOrEmpty(folder) ? null : folder, ref startup, out var made))
        {
            throw Refused(program, job);
        }

        var process = new SafeProcessHandle(made.Process, ownsHandle: true);
        try
        {
            if (!AssignProcessToJobObject(job, made.Process))
            {
                var refused = Refused(program, job);
                TerminateProcess(made.Process, 1);
                process.Dispose();
                throw refused;
            }

            ResumeThread(made.Thread);
            return new GameJob(job, process, made.ProcessId);
        }
        finally
        {
            CloseHandle(made.Thread);
        }
    }

    /// <summary>Posts a press and a release of the key to the game's own shown top windows, without giving them the focus.</summary>
    /// <returns>False when the game has no such window (yet, or any more).</returns>
    public bool PostKey(GameKey key)
    {
        if (HasExited)
        {
            return false;
        }

        // What a key message carries besides the key: a count of one and the key's scan code, and for a release the two bits that say so.
        nint scan = key switch { GameKey.Space => 0x39, GameKey.F10 => 0x44, _ => 0x01 };
        nint down = (scan << 16) | 1;
        nint up = unchecked((nint)0xC0000001) | (scan << 16);
        var posted = false;
        EnumWindows(
            (window, _) =>
            {
                GetWindowThreadProcessId(window, out var owner);
                if (owner == ProcessId && IsWindowVisible(window) && GetWindow(window, OwnerOf) == 0)
                {
                    PostMessageW(window, KeyDown, (nuint)key, down);
                    PostMessageW(window, KeyUp, (nuint)key, up);
                    posted = true;
                }

                return true;
            },
            0);
        return posted;
    }

    /// <summary>Ends the game and everything it started, and waits up to 10 s for the game to be gone.</summary>
    public void Dispose()
    {
        if (job.IsClosed)
        {
            return;
        }

        TerminateJobObject(job, 1);
        WaitForSingleObject(process, 10000);
        job.Dispose();
        process.Dispose();
    }

    private static InvalidOperationException Refused(string program, SafeFileHandle job)
    {
        var reason = new Win32Exception(Marshal.GetLastPInvokeError()).Message;
        job.Dispose();
        return new InvalidOperationException($"cannot start {program}: {reason}");
    }

    private delegate bool WindowVisitor(nint window, nint state);

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Size;
        public nint Reserved, Desktop, Title;
        public int X, Y, Width, Height, ColumnCount, RowCount, FillAttribute, Flags;
        public short ShowWindow, Reserved2Size;
        public nint Reserved2, Input, Output, Error;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public nint Process, Thread;
        public int ProcessId, ThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass, SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimits
    {
        public BasicLimits Basic;
        public IoCounters Io;
        public nuint ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    // A job's processes: how many there are, how many ids follow, and the ids.
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessIdList
    {
        public const int Room = 256;

        public uint Assigned, Listed;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = Room)]
        public nuint[] Ids;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(
        string? application, StringBuilder commandLine, nint processSecurity, nint threadSecurity,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint flags, nint environment, string? directory,
        ref StartupInfo startup, out ProcessInformation information);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObjectW(nint security, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeFileHandle job, int kind, ref ExtendedLimits limits, int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(SafeFileHandle job, int kind, ref ProcessIdList list, int size, nint returned);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, nint process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(SafeFileHandle job, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(nint process, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(nint thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(WindowVisitor visitor, nint state);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out int processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint window, uint relation);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint window, uint message, nuint wide, nint value);
}
