using System;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Threading.Tasks;
using ScopeCLI.Services;
using Xunit;

namespace ScopeCLI.Tests
{
    public class ArchiveServiceTests
    {
        [Fact]
        public async Task ExtractConfigArchiveAsync_ZipSlipAttack_ThrowsSecurityException()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string extractTargetDir = Path.Combine(tempDir, "target");
            string profilesDataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles_data", "test-zipslip");
            string maliciousZip = Path.Combine(profilesDataDir, "config.zip");

            Directory.CreateDirectory(extractTargetDir);
            Directory.CreateDirectory(profilesDataDir);

            try
            {
                using (var zipStream = new FileStream(maliciousZip, FileMode.Create))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    // Create entry attempting path traversal outside target directory
                    var entry = archive.CreateEntry("../../../evil.txt");
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write("pwned");
                }

                var archiveService = new ArchiveService();

                await Assert.ThrowsAsync<SecurityException>(() =>
                    archiveService.ExtractConfigArchiveAsync("test-zipslip", extractTargetDir));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);

                if (Directory.Exists(profilesDataDir))
                    Directory.Delete(profilesDataDir, recursive: true);
            }
        }

        [Fact]
        public async Task CreateAndExtractConfigArchive_RoundTrip_PreservesFiles()
        {
            string profileId = "roundtrip-test";
            string tempBase = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string sourceMinecraftDir = Path.Combine(tempBase, "source_mc");
            string targetMinecraftDir = Path.Combine(tempBase, "target_mc");
            string configDir = Path.Combine(sourceMinecraftDir, "config");

            Directory.CreateDirectory(configDir);
            Directory.CreateDirectory(targetMinecraftDir);

            string testOptionFile = Path.Combine(sourceMinecraftDir, "options.txt");
            string testConfigFile = Path.Combine(configDir, "settings.json");

            await File.WriteAllTextAsync(testOptionFile, "gamma:1.0");
            await File.WriteAllTextAsync(testConfigFile, "{\"theme\":\"dark\"}");

            var archiveService = new ArchiveService();

            try
            {
                await archiveService.CreateConfigArchiveAsync(profileId, sourceMinecraftDir);
                await archiveService.ExtractConfigArchiveAsync(profileId, targetMinecraftDir);

                string extractedOption = Path.Combine(targetMinecraftDir, "options.txt");
                string extractedConfig = Path.Combine(targetMinecraftDir, "config", "settings.json");

                Assert.True(File.Exists(extractedOption));
                Assert.True(File.Exists(extractedConfig));

                Assert.Equal("gamma:1.0", await File.ReadAllTextAsync(extractedOption));
                Assert.Equal("{\"theme\":\"dark\"}", await File.ReadAllTextAsync(extractedConfig));
            }
            finally
            {
                if (Directory.Exists(tempBase))
                    Directory.Delete(tempBase, recursive: true);

                string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles_data", profileId);
                if (Directory.Exists(dataDir))
                    Directory.Delete(dataDir, recursive: true);
            }
        }
    }
}
