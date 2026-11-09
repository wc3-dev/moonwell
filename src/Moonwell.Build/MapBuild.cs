using System.Globalization;

namespace Moonwell.Build
{
    /// <summary>Builds a map project into a map archive.</summary>
    public static class MapBuild
    {
        /// <summary>The damage package's assembly: a map that references it gets it installed by its Main.</summary>
        private const string DamageAssemblyName = "Moonwell.Damage";

        /// <summary>The interactions package's assembly: a map that references it gets the table of its tree types in its object Lua.</summary>
        private const string InteractionsAssemblyName = "Moonwell.Interactions";

        /// <summary>The terrain package's assembly: a map that references it gets its grid written from <c>war3map.w3e</c> and installed by its Main.</summary>
        private const string TerrainAssemblyName = "Moonwell.Terrain";

        /// <summary>The areas package's assembly: a map that references it gets its World Editor regions written from <c>war3map.w3r</c>, their names in its code checked, and the package installed by its Main.</summary>
        private const string AreasAssemblyName = "Moonwell.Areas";

        /// <summary>The input package's assembly: a map that references it gets World Editor's cameras written from <c>war3map.w3c</c>, their names in its code checked, and the package installed by its Main, listening for the mouse only when the code names <c>Mouse</c> or <c>IMouseWatch</c>, and for its moves only when the code needs them.</summary>
        private const string InputAssemblyName = "Moonwell.Input";

        /// <summary>The frames package's assembly: a map that references it gets the package installed, which makes no frame until the map adds one.</summary>
        private const string FramesAssemblyName = "Moonwell.Frames";

        private const string KitAssemblyName = "Moonwell.Kit";

        /// <summary>The physics package's assembly: a map that references it gets its placed bridges written from <c>war3map.doo</c> and installed by its Main.</summary>
        private const string PhysicsAssemblyName = "Moonwell.Physics";

        /// <summary>Runs every step of the build, in order.</summary>
        /// <param name="path">A project folder, or the path of its <c>.csproj</c> file.</param>
        /// <param name="options">What kind of build to make.</param>
        /// <param name="log">Where the build's progress, notes and <c>dotnet build</c> output go.</param>
        /// <returns>The full path of the map archive.</returns>
        /// <exception cref="BuildException">A step failed in a way the modder can act on.</exception>
        public static string Run(string path, BuildOptions options, TextWriter log)
        {
            // The map's own C# that runs here (a tooltip's formula) and CSharp.lua (a negative constant) write numbers in the
            // current culture, and the game reads them as the invariant culture writes them: 37.5 and -1, not a Swedish 37,5
            // and its minus sign U+2212. Threads the build starts take it too; the caller gets its cultures back.
            var culture = CultureInfo.CurrentCulture;
            var uiCulture = CultureInfo.CurrentUICulture;
            var threadCulture = CultureInfo.DefaultThreadCurrentCulture;
            var threadUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            try
            {
                return Build(path, options, log);
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = uiCulture;
                CultureInfo.DefaultThreadCurrentCulture = threadCulture;
                CultureInfo.DefaultThreadCurrentUICulture = threadUiCulture;
            }
        }

        private static string Build(string path, BuildOptions options, TextWriter log)
        {
            var project = MapProject.Find(path);
            log.WriteLine($"moonwell: {project.Name} ({(options.Release ? "release" : "development")} build)");

            var targetPath = DotnetBuild.Run(project, options.Release, log);
            PackageSources.Check(project, options.PackageVersion ?? PackageSources.ToolVersion);
            var settings = MapSettingsReader.Read(targetPath, project);
            var map = SourceMap.Open(project, settings, log);

            // The reference decides, not a Moonwell.Objects.dll an earlier build left beside the assembly.
            if (Declarations.UsesObjects(targetPath) || FileStore.ReferencesObjects(targetPath))
            {
                var objectsDll = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(targetPath))!, Declarations.ObjectsAssemblyName + ".dll");
                BuildWithObjects(project, settings, map, targetPath, objectsDll, options, log);
            }
            else
            {
                BuildWithoutObjects(project, settings, map, targetPath, options, log);
            }

            if (Declarations.Needs(targetPath, FramesAssemblyName))
            {
                StageWith(project, () => FramesToc.Write(project.StagedFolder, project.AssetsFolder, Declarations.Needs(targetPath, KitAssemblyName)));
            }

            try
            {
                Archive.Pack(project.StagedFolder, project.ArchivePath);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                throw CannotWrite(project, project.ArchivePath, error);
            }

            log.WriteLine($@"moonwell: built build\{project.Name}.w3x");
            return project.ArchivePath;
        }

        // The map uses Moonwell.Objects: its declarations, ids, Lua, registry and object files are part of the build.
        private static void BuildWithObjects(
            MapProject project,
            MapSettings settings,
            War3Net.Build.Map map,
            string targetPath,
            string objectsDll,
            BuildOptions options,
            TextWriter log)
        {
            var data = GameData.Of(settings.DataSet, settings.DataVersion);
            var standard = StandardData.Read(objectsDll);
            var declarations = Declarations.Read(targetPath, project, standard, data.ValueSet);
            BalanceCheck.Run(declarations, standard, data);

            var sourceMapIds = MapObjectIds.Defined(project);
            var ids = ObjectIds.Assign(declarations.Objects, standard.AllIds(), sourceMapIds);
            var experience = MapMisc.ReadExperience(MapMisc.FolderOf(project), ReferenceEquals(standard, StandardData.Empty) ? HeroExperience.Standard : standard.Experience(data.ValueSet));
            var day = MapMisc.ReadDay(MapMisc.FolderOf(project), GameDay.Standard);
            var installs = Installs(project, map, targetPath, BridgeWriter.Declared(declarations.Objects, ids), standard.AllIds(), experience, day, log);
            installs.AddRange(FileStore.Installs(targetPath, settings, project));
            installs.AddRange(SaveCodecWriter.Write(targetPath, project, settings));
            EntryWriter.Write(project, settings, true, installs, Loading(targetPath));

            // A map whose code casts with a dummy gets the dummy's type in its object files, kept free of its own ids; one whose
            // code uses player files gets the store ability that reads them back.
            var casts = DummyType.UsedBy(targetPath);
            var written = declarations.Objects;
            var writtenIds = ids;
            if (casts)
            {
                DummyType.Check(ids, sourceMapIds);
                written = written.Append(DummyType.Object).ToList();
                writtenIds = new Dictionary<string, string>(writtenIds) { [DummyType.ClassName] = DummyType.Id };
            }

            if (FileStore.UsedBy(targetPath))
            {
                FileStore.Check(ids, sourceMapIds);
                written = written.Append(FileStore.Object).ToList();
                writtenIds = new Dictionary<string, string>(writtenIds) { [FileStore.ClassName] = FileStore.Id };
            }

            if (BonusAbilities.UsedBy(targetPath))
            {
                BonusAbilities.Check(ids, sourceMapIds);
                written = written.Concat(BonusAbilities.Objects).ToList();
                var withBonuses = new Dictionary<string, string>(writtenIds);
                foreach (var bonus in BonusAbilities.Objects)
                {
                    withBonuses[bonus.ClassName] = bonus.PinnedId!;
                }

                writtenIds = withBonuses;
            }

            var watch = BuffWatch.Lines(declarations.Objects, ids, declarations.StandardClassIds, BuffWatch.StandardLists(standard, data.ValueSet, declarations.Objects))
                .Concat(DummyType.Lines(casts, declarations.Used, declarations.Objects, ids, declarations.StandardClassIds, standard, data.ValueSet, casts ? DummyType.MapAbilities(project.MapFolder) : null))
                .ToList();
            RegistryWriter.Write(project, declarations.Used, declarations.Objects, ids, watch);
            LuaCompiler.Compile(project, map, targetPath, options.Release, declarations, ids, standard, data.ValueSet, Declarations.References(targetPath, InteractionsAssemblyName));
            StageWith(project, () => Stage.Write(project, map, written, writtenIds, declarations.StandardClassIds, standard, log));
        }

        // The map does not reference Moonwell.Objects: its own object files are used as they are.
        private static void BuildWithoutObjects(MapProject project, MapSettings settings, War3Net.Build.Map map, string targetPath, BuildOptions options, TextWriter log)
        {
            // A registry or stubs left by an earlier build would not compile any more.
            var registry = Path.Combine(project.GeneratedFolder, "ObjectRegistry.g.cs");
            if (File.Exists(registry))
            {
                File.Delete(registry);
            }

            ObjectStubs.Delete(project);
            SaveCodecWriter.Delete(project);

            // The table of tree types is made from the standard destructable data, which only Moonwell.Objects carries.
            if (Declarations.References(targetPath, InteractionsAssemblyName))
            {
                log.WriteLine("warning: the map uses Moonwell.Interactions without Moonwell.Objects: IsTree needs Moonwell.Objects (its standard data makes the table of tree types), so it is false for every destructable");
            }

            // The build reads no declarations, but a unit's Level is still set only in a declared class's constructor.
            Declarations.CheckSetters(targetPath);

            // Player files are read back through the store ability, which only the object files Moonwell.Objects writes can hold.
            FileStore.CheckWithoutObjects(targetPath);

            // Damage, armor and attack speed go through bonus abilities, which likewise only those object files can hold.
            BonusAbilities.CheckWithoutObjects(targetPath);

            // Without the standard data the map's own constants still count, over the standard ones the runtime has.
            var experience = MapMisc.ReadExperience(MapMisc.FolderOf(project), HeroExperience.Standard);
            var day = MapMisc.ReadDay(MapMisc.FolderOf(project), GameDay.Standard);
            EntryWriter.Write(project, settings, false, Installs(project, map, targetPath, new List<BridgeWriter.OwnType>(), null, experience, day, log), Loading(targetPath));
            LuaCompiler.Compile(project, map, targetPath, options.Release);
            StageWith(project, () => Stage.WriteWithoutObjects(project, map, log));
        }

        // What the map's Main runs before the runtime starts, while the map loads: the keyboard's registrations, about 6,000 for
        // each person playing (0.1 s each), so the loading screen hides them. InstallKeyboard takes them over in the register step.
        private static List<string> Loading(string targetPath)
        {
            var loading = new List<string>();
            if (Declarations.Needs(targetPath, InputAssemblyName) && NamesKeyboard(targetPath))
            {
                loading.Add("global::Moonwell.InputSystem.RegisterKeys();");
            }

            // The first person's date goes out while the map loads, and the game's start waits for it (decision 174).
            if (Declarations.ReferencesType(targetPath, "Moonwell", "RealTime"))
            {
                loading.Add("global::Moonwell.Runtime.ShareStartDate();");
            }

            return loading;
        }

        // Whether the map's code names the keyboard, which only then listens to the keys.
        private static bool NamesKeyboard(string targetPath)
        {
            return Declarations.ReferencesType(targetPath, "Moonwell", "Keyboard") || Declarations.ReferencesType(targetPath, "Moonwell", "IKeyWatch");
        }

        // What the map's Main installs before anything registers: the hero experience table and the day's hours when the map's gameplay constants change them, then each package the map or a library of its own references, in this order. The terrain's grid, the
        // areas' regions, the input's cameras and the physics' bridges are written here too, from the map's ground, regions, cameras and
        // placed destructables (with the destructable types the map declares and the standard ids, when it uses Moonwell.Objects),
        // and a file an earlier build wrote is removed when the map no longer uses the package.
        private static List<string> Installs(MapProject project, War3Net.Build.Map map, string targetPath, IReadOnlyList<BridgeWriter.OwnType> declared, IReadOnlySet<string>? standardIds, HeroExperience experience, GameDay day, TextWriter log)
        {
            var installs = new List<string>();
            var table = experience.Table();
            if (!table.SequenceEqual(HeroExperience.Standard.Table()))
            {
                installs.Add("global::Moonwell.Runtime.InstallHeroExperience(new int[] { " + string.Join(", ", table.Select(total => total.ToString(CultureInfo.InvariantCulture))) + " });");
            }

            if (ReadsLastOrders(targetPath))
            {
                installs.Add("global::Moonwell.Runtime.ListenForOrders();");
            }

            if (ReadsSelection(targetPath))
            {
                installs.Add("global::Moonwell.Runtime.ListenForSelection();");
            }

            if (ReadsTransports(targetPath))
            {
                installs.Add("global::Moonwell.Runtime.ListenForTransports();");
            }

            if (ReadsSummons(targetPath))
            {
                installs.Add("global::Moonwell.Runtime.ListenForSummons();");
            }

            if (day != GameDay.Standard)
            {
                installs.Add(day.Install());
            }

            if (day.Warning != null)
            {
                log.WriteLine(day.Warning);
            }

            if (Declarations.Needs(targetPath, DamageAssemblyName))
            {
                installs.Add("global::Moonwell.DamageSystem.Install();");
            }

            var physics = Declarations.Needs(targetPath, PhysicsAssemblyName);
            if (Declarations.Needs(targetPath, TerrainAssemblyName))
            {
                // The fork's W2 fix refuses a map without a war3map.w3e, so there is always one here.
                TerrainWriter.Write(project, map.Environment!);
                const string Data = "global::Moonwell.Generated.TerrainData";
                installs.Add($"global::Moonwell.Terrain.Install({Data}.Columns, {Data}.Rows, {Data}.Left, {Data}.Bottom, {Data}.Corners());");
                installs.Add($"global::Moonwell.Terrain.InstallTiles({Data}.Tiles());");
            }
            else
            {
                TerrainWriter.Delete(project);
            }

            if (Declarations.Needs(targetPath, AreasAssemblyName))
            {
                // A map with no war3map.w3r has no regions: Regions is null, and the arrays are empty.
                PlaceWriter.WriteAreas(project, map.Regions);
                PlaceNames.Check(project, "AreaShape.Region", map.Regions?.Regions.Select(region => region.Name).ToList() ?? new List<string>(), "region");
                installs.Add("global::Moonwell.AreaSystem.Install(global::Moonwell.Generated.AreaData.Names(), global::Moonwell.Generated.AreaData.Rects());");
            }
            else
            {
                PlaceWriter.DeleteAreas(project);
            }

            if (Declarations.Needs(targetPath, InputAssemblyName))
            {
                // A map that references the package but never names Mouse or IMouseWatch registers no mouse event: the package listens
                // only when asked. One that names them listens to presses and releases, and to the moves, synced events 120 to 240 a
                // second for each player moving the mouse, only when its code needs them (NeedsMouseMoves). A map with no war3map.w3c
                // has no cameras: Cameras is null, and the arrays are empty.
                var mouse = Declarations.ReferencesType(targetPath, "Moonwell", "Mouse") || Declarations.ReferencesType(targetPath, "Moonwell", "IMouseWatch");
                var moves = mouse && NeedsMouseMoves(targetPath);
                PlaceWriter.WriteInput(project, map.Cameras, mouse);
                PlaceNames.Check(project, "CameraSetup.Named", map.Cameras?.Cameras.Select(camera => camera.Name).ToList() ?? new List<string>(), "camera");
                installs.Add("global::Moonwell.InputSystem.Install(global::Moonwell.Generated.InputData.Mouse, " + (moves ? "true" : "false") + ", global::Moonwell.Generated.InputData.CameraNames(), global::Moonwell.Generated.InputData.Cameras());");
                // The keyboard listens for every key with every modifier, about 6,000 registrations for each person, so only a map that
                // names it pays for them.
                if (NamesKeyboard(targetPath))
                {
                    installs.Add("global::Moonwell.InputSystem.InstallKeyboard();");
                }
            }
            else
            {
                PlaceWriter.DeleteInput(project);
            }

            if (Declarations.Needs(targetPath, FramesAssemblyName))
            {
                installs.Add("global::Moonwell.FramesSystem.Install();");
            }

            if (physics)
            {
                // A deck stands on the ground beneath any water at its bridge's place, which the terrain's grid does not keep: the
                // bridges' text carries it.
                BridgeWriter.Write(project, map.Doodads, map.Environment, declared, standardIds, log);
                installs.Add("global::Moonwell.Physics.Install(global::Moonwell.Generated.PhysicsData.Bridges(), global::Moonwell.Generated.PhysicsData.Decks());");
            }
            else
            {
                BridgeWriter.Delete(project);
            }

            return installs;
        }

        /// <summary>
        /// Whether a map's code needs the mouse's move events, read from its built assembly's metadata without loading it: it reads a
        /// mouse's synced position (a member reference to <c>Mouse.X</c>, <c>Y</c>, <c>Position</c> or <c>HasPosition</c>), which only
        /// the moves keep up to date between presses, or it has a method named <c>OnMouseMoved</c>. A press, a release, <c>IsDown</c>,
        /// the cursor and the machine-local reads need none. A read inside a press reaction counts too, as the build cannot tell where
        /// it is read. A library of the map's own counts as its code.
        /// </summary>
        /// <param name="assemblyPath">The map's built assembly.</param>
        internal static bool NeedsMouseMoves(string assemblyPath)
        {
            return CompiledFiles.MapAssemblies(assemblyPath).Any(ReadsMouseMoves);
        }

        private static bool ReadsMouseMoves(string assemblyPath)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
            var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
            foreach (var handle in metadata.MemberReferences)
            {
                var member = metadata.GetMemberReference(handle);
                if (member.Parent.Kind != System.Reflection.Metadata.HandleKind.TypeReference)
                {
                    continue;
                }

                var type = metadata.GetTypeReference((System.Reflection.Metadata.TypeReferenceHandle)member.Parent);
                if (metadata.GetString(type.Name) != "Mouse" || metadata.GetString(type.Namespace) != "Moonwell")
                {
                    continue;
                }

                var name = metadata.GetString(member.Name);
                if (name == "get_X" || name == "get_Y" || name == "get_Position" || name == "get_HasPosition")
                {
                    return true;
                }
            }

            foreach (var handle in metadata.MethodDefinitions)
            {
                var name = metadata.GetString(metadata.GetMethodDefinition(handle).Name);
                if (name == "OnMouseMoved" || name.EndsWith(".OnMouseMoved", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a map's code reads a unit's last orders (a member reference to <c>Unit.LastOrder</c> or <c>Unit.Reissue</c>), which
        /// only listening for every unit's orders from the start fills: order events come for every order of every unit, so a map
        /// that reads none and writes no order reaction listens to none. A library of the map's own counts as its code.
        /// </summary>
        /// <param name="assemblyPath">The map's built assembly.</param>
        internal static bool ReadsLastOrders(string assemblyPath)
        {
            return CompiledFiles.MapAssemblies(assemblyPath).Any(path => References(path, "Unit", "get_LastOrder", "Reissue"));
        }

        /// <summary>
        /// Whether a map's code reads a player's selection (a member reference to <c>Player.Selection</c>), which only listening
        /// for every player's selections from the start fills (decision 203). A library of the map's own counts as its code.
        /// </summary>
        /// <param name="assemblyPath">The map's built assembly.</param>
        internal static bool ReadsSummons(string assemblyPath)
        {
            return CompiledFiles.MapAssemblies(assemblyPath).Any(path => References(path, "Unit", "get_Summoner", "get_Summons"));
        }

        internal static bool ReadsSelection(string assemblyPath)
        {
            return CompiledFiles.MapAssemblies(assemblyPath).Any(path => References(path, "Player", "get_Selection", "get_Selection"));
        }

        /// <summary>Whether a map's code reads a unit's transport or cargo (Unit.Transport or Unit.Cargo), which only listening for loads and unloads from the start fills (decision 219). A library of the map's own counts as its code.</summary>
        /// <param name="assemblyPath">The map's built assembly.</param>
        internal static bool ReadsTransports(string assemblyPath)
        {
            return CompiledFiles.MapAssemblies(assemblyPath).Any(path => References(path, "Unit", "get_Transport", "get_Cargo"));
        }

        /// <summary>Whether an assembly refers to either of two members of a Moonwell class.</summary>
        private static bool References(string assemblyPath, string typeName, string first, string second)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
            var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
            foreach (var handle in metadata.MemberReferences)
            {
                var member = metadata.GetMemberReference(handle);
                if (member.Parent.Kind != System.Reflection.Metadata.HandleKind.TypeReference)
                {
                    continue;
                }

                var type = metadata.GetTypeReference((System.Reflection.Metadata.TypeReferenceHandle)member.Parent);
                if (metadata.GetString(type.Name) != typeName || metadata.GetString(type.Namespace) != "Moonwell")
                {
                    continue;
                }

                var name = metadata.GetString(member.Name);
                if (name == first || name == second)
                {
                    return true;
                }
            }

            return false;
        }

        private static void StageWith(MapProject project, Action stage)
        {
            try
            {
                stage();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                throw CannotWrite(project, project.StagedFolder, error);
            }
        }

        private static BuildException CannotWrite(MapProject project, string path, Exception error)
        {
            var relative = Path.GetRelativePath(project.Folder, path);
            return new BuildException($"cannot write {relative}: {error.Message} (is the game or World Editor using it?)");
        }
    }
}
