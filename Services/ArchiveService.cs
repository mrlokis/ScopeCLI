using System.IO.Compression;
using Spectre.Console;

namespace ScopeCLI.Services
{
    public class ArchiveService
    {
        public async Task CreateConfigArchiveAsync(string profileId, string sourceMinecraftDir, ProgressTask task)
        {
            string profilesDataDir = Path.Combine("profiles_data", profileId);
            Directory.CreateDirectory(profilesDataDir);
            string archivePath = Path.Combine(profilesDataDir, "config.zip");

            var filesToArchive = new List<string>();

            string optionsTxt = Path.Combine(sourceMinecraftDir, "options.txt");
            if (File.Exists(optionsTxt))
                filesToArchive.Add(optionsTxt);

            string configDir = Path.Combine(sourceMinecraftDir, "config");
            if (Directory.Exists(configDir))
                filesToArchive.AddRange(Directory.GetFiles(configDir, "*", SearchOption.AllDirectories));

            task.MaxValue(filesToArchive.Count);

            if (filesToArchive.Count == 0)
            {
                task.Value = 0;
                return;
            }

            using (var zipStream = new FileStream(archivePath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                foreach (var filePath in filesToArchive)
                {
                    string entryName = Path.GetRelativePath(sourceMinecraftDir, filePath);
                    archive.CreateEntryFromFile(filePath, entryName);
                    task.Increment(1);
                }
            }
        }

        public async Task ExtractConfigArchiveAsync(string profileId, string targetMinecraftDir, ProgressTask task)
        {
            string archivePath = Path.Combine("profiles_data", profileId, "config.zip");
            if (!File.Exists(archivePath))
            {
                task.MaxValue(0);
                return;
            }

            using (var zipStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                var entries = archive.Entries;
                task.MaxValue(entries.Count);

                foreach (var entry in entries)
                {
                    string destinationPath = Path.Combine(targetMinecraftDir, entry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                    entry.ExtractToFile(destinationPath, overwrite: true);
                    task.Increment(1);
                }
            }
        }

        public async Task CreateModsArchiveAsync(string profileId, string sourceMinecraftDir, ProgressTask task)
        {
            string profilesDataDir = Path.Combine("profiles_data", profileId);
            Directory.CreateDirectory(profilesDataDir);
            string archivePath = Path.Combine(profilesDataDir, "mods.zip");

            string modsDir = Path.Combine(sourceMinecraftDir, "mods");
            if (!Directory.Exists(modsDir))
            {
                task.MaxValue(0);
                return;
            }

            var modFiles = Directory.GetFiles(modsDir, "*", SearchOption.AllDirectories);
            task.MaxValue(modFiles.Length);

            if (modFiles.Length == 0)
            {
                task.Value = 0;
                return;
            }

            using (var zipStream = new FileStream(archivePath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                foreach (var filePath in modFiles)
                {
                    string entryName = Path.GetRelativePath(sourceMinecraftDir, filePath);
                    archive.CreateEntryFromFile(filePath, entryName);
                    task.Increment(1);
                }
            }
        }

        public async Task ExtractModsArchiveAsync(string profileId, string targetMinecraftDir, ProgressTask task)
        {
            string archivePath = Path.Combine("profiles_data", profileId, "mods.zip");
            if (!File.Exists(archivePath))
            {
                task.MaxValue(0);
                return;
            }

            using (var zipStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                var entries = archive.Entries;
                task.MaxValue(entries.Count);

                foreach (var entry in entries)
                {
                    string destinationPath = Path.Combine(targetMinecraftDir, entry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                    entry.ExtractToFile(destinationPath, overwrite: true);
                    task.Increment(1);
                }
            }
        }
    }
}