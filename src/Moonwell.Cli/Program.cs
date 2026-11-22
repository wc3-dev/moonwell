using System.Diagnostics;
using System.Text;
using Moonwell.Tool;

Process? game = null;
using var cancel = new CancellationTokenSource();

// The checklist's marks and the spinner are not in the console's old code page.
Console.OutputEncoding = Encoding.UTF8;

// Ctrl+C ends a watch after its build, and a second one at once; any other command stops at once, as it always has,
// showing the cursor the spinner hid.
Console.CancelKeyPress += (_, press) =>
{
    press.Cancel = Cli.WaitsOnCtrlC(args, cancel);
    if (!press.Cancel && !Console.IsOutputRedirected)
    {
        Console.Write("\u001b[?25h");
    }
};

return Cli.Run(
    args,
    Console.Out,
    Console.Error,
    (exe, arguments) => game = Process.Start(new ProcessStartInfo(exe, arguments) { UseShellExecute = false }),
    () => Cli.EndGame(game),
    cancel.Token);
