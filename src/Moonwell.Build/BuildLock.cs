namespace Moonwell.Build
{
    /// <summary>The lock a build of a map holds from start to end, so a second build of the same map waits for the first rather than deleting its staged folder under it.</summary>
    internal static class BuildLock
    {
        private const int SharingViolation = 32;

        /// <summary>Takes the map's lock, <c>build\.lock</c>, waiting while another build holds it. Windows frees it when a build ends or its process dies, so no lock outlives its build.</summary>
        /// <param name="project">The map project.</param>
        /// <param name="log">Where the note that it waits goes.</param>
        /// <returns>The lock; disposing it frees it.</returns>
        public static IDisposable Take(MapProject project, TextWriter log)
        {
            Directory.CreateDirectory(project.BuildFolder);
            var path = Path.Combine(project.BuildFolder, ".lock");
            var told = false;
            while (true)
            {
                try
                {
                    return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
                }
                catch (IOException error) when ((error.HResult & 0xFFFF) == SharingViolation)
                {
                    if (!told)
                    {
                        log.WriteLine("moonwell: another build of this map is running; waiting for it to end");
                        told = true;
                    }

                    Thread.Sleep(250);
                }
            }
        }
    }
}
