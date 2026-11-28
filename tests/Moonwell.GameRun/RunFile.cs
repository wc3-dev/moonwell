namespace Moonwell.GameRun;

/// <summary>
/// A run's file: the Preload file a map writes in <c>Documents\Warcraft III\CustomMapData</c>. Each line the map keeps is
/// one <c>Preload("\")\n" + line + "\n//")</c> call, which the game writes as a <c>call Preload( "")</c> line, the map's
/// line, and a line that starts <c>//" )</c>.
/// </summary>
public static class RunFile
{
    /// <summary>The lines the map wrote: the text between each <c>call Preload( "")</c> line and the <c>//" )</c> line that closes it.</summary>
    public static IReadOnlyList<string> Parse(string text)
    {
        var lines = new List<string>();
        var inside = false;
        foreach (var raw in text.ReplaceLineEndings("\n").Split('\n'))
        {
            if (raw.Trim() == "call Preload( \"\")")
            {
                inside = true;
                continue;
            }

            if (raw.StartsWith("//\" )", StringComparison.Ordinal))
            {
                inside = false;
                continue;
            }

            if (inside)
            {
                lines.Add(raw);
            }
        }

        return lines;
    }

    /// <summary>
    /// The lines of the file when it was written at or after the moment, else null: no file, an older one (an earlier run's),
    /// or one the game holds while it writes it (read again on the next turn).
    /// </summary>
    public static IReadOnlyList<string>? ReadIfWrittenSince(string path, DateTime sinceUtc)
    {
        try
        {
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < sinceUtc)
            {
                return null;
            }

            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(file, System.Text.Encoding.UTF8);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
