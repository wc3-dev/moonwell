using System.Diagnostics;
using Moonwell.Tool;

Process? game = null;
using var cancel = new CancellationTokenSource();

// Ctrl+C ends a watch after its build; any other command stops at once, as it always has.
if (args.Length > 0 && args[0] == "watch")
{
    Console.CancelKeyPress += (_, press) =>
    {
        press.Cancel = true;
        cancel.Cancel();
    };
}

return Cli.Run(
    args,
    Console.Out,
    Console.Error,
    (exe, arguments) => game = Process.Start(new ProcessStartInfo(exe, arguments) { UseShellExecute = false }),
    () =>
    {
        if (game is { HasExited: false })
        {
            game.Kill();
            game.WaitForExit();
        }
    },
    cancel.Token);
