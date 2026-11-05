using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Xunit;

namespace Moonwell.Build.Tests
{
    public sealed class ArchiveTests : IDisposable
    {
        private readonly TestFolders folders = new TestFolders();

        public void Dispose() => folders.Dispose();

        // A folder that refuses new files, where the old archive can still be deleted: the write of the new one fails.
        [Fact]
        [SupportedOSPlatform("windows")]
        public void a_failed_pack_keeps_the_old_archive()
        {
            var folder = folders.TemporaryFolderOutsideRepository();
            var staged = Directory.CreateDirectory(Path.Combine(folder, "staged")).FullName;
            File.WriteAllText(Path.Combine(staged, "war3map.lua"), "print(1)");
            var output = Directory.CreateDirectory(Path.Combine(folder, "build"));
            var archivePath = Path.Combine(output.FullName, "Map.w3x");
            File.WriteAllText(archivePath, "the old map");

            var refuse = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.CreateFiles, AccessControlType.Deny);
            var security = output.GetAccessControl();
            security.AddAccessRule(refuse);
            output.SetAccessControl(security);
            try
            {
                Assert.ThrowsAny<UnauthorizedAccessException>(() => Archive.Pack(staged, archivePath));
            }
            finally
            {
                security.RemoveAccessRule(refuse);
                output.SetAccessControl(security);
            }

            Assert.Equal(new[] { archivePath }, Directory.GetFiles(output.FullName));
            Assert.Equal("the old map", File.ReadAllText(archivePath));
        }
    }
}
