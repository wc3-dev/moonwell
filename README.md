<div align="center">
  <img src="mw.gif" alt="Moonwell" width="256" />

  <p>
    <i>All-in-one modding framework for <a href="https://warcraft3.blizzard.com/">Warcraft III: Reforged</a>.</i>
  </p>

  <p>
    <a href="https://www.hiveworkshop.com/">
      <img
        src="https://cdn.hiveworkshop.com/data/styles/7/styles/vindit-reforged/hive-logo.png"
        alt="Hive Workshop"
        height="36"
      />
    </a>
  </p>
</div>

Moonwell is a C# modding framework for Warcraft III: Reforged that's in active development. It is still far from production ready, expect breaking changes. Use with caution. If you don't know what you're doing without AI, use something else for now, there are lots of traps still need resolving.

## Install

Moonwell runs on Windows. You need:

- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Warcraft III: Reforged
- Moonwell is designed to work on the latest patch. 1.29 not yet supported.

Install the cli:

    dotnet tool install --global Moonwell.Cli

Make a map and play it:

    moonwell new MyMap
    cd MyMap
    moonwell run

`moonwell run` builds `build\MyMap.w3x` and starts the game with it. `moonwell build` only builds.

Game folder path defaults to `C:\Program Files (x86)\Warcraft III`, set it with `MOONWELL_WAR3` environment variable if necessary.

Moonwell is updated with, expect breaking changes:

    dotnet tool update --global Moonwell.Cli

Remember to set the version of the `Moonwell` packages in your map's `.csproj` to the same number.