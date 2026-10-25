using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using ScopeCLI.Common;
using ScopeCLI.Services.Interfaces;
using Spectre.Console;

namespace ScopeCLI.Services
{
    public class ArchiveService : IArchiveService
    {
        public Task CreateConfigArchiveAsync(string profileId, string sourceMinecraftDir, ProgressTask? task = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                string profilesDataDir = AppPaths.GetProfileDataDir(profileId);
                Directory.CreateDirectory(profilesDataDir);
                string archivePath = AppPaths.GetProfileConfigArchive(profileId);

                var filesToArchive = new List<string>();

                string optionsTxt = Path.Combine(sourceMinecraftDir, "options.txt");
                if (File.Exists(optionsTxt))
                    filesToArchive.Add(optionsTxt);

                string configDir = Path.Combine(sourceMinecraftDir, "config");
                if (Directory.Exists(configDir))
                    filesToArchive.AddRange(Directory.GetFiles(configDir, "*", SearchOption.AllDirectories));

                string defaultConfigsDir = Path.Combine(sourceMinecraftDir, "defaultconfigs");
                if (Directory.Exists(defaultConfigsDir))
                    filesToArchive.AddRange(Directory.GetFiles(defaultConfigsDir, "*", SearchOption.AllDirectories));

                if (task != null)
                {
                    task.MaxValue = Math.Max(1, filesToArchive.Count);
                }

                if (filesToArchive.Count == 0)
                {
                    if (task != null) task.Value = task.MaxValue;
                    return;
                }

                string tempArchive = archivePath + ".tmp";
                if (File.Exists(tempArchive))
                    File.Delete(tempArchive);

                using (var zipStream = new FileStream(tempArchive, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    foreach (var filePath in filesToArchive)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string entryName = Path.GetRelativePath(sourceMinecraftDir, filePath);
                        archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
                        task?.Increment(1);
                    }
                }

                if (File.Exists(archivePath))
                    File.Delete(archivePath);

                File.Move(tempArchive, archivePath);
            }, cancellationToken);
        }

        public Task ExtractConfigArchiveAsync(string profileId, string targetMinecraftDir, ProgressTask? task = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                string archivePath = AppPaths.GetProfileConfigArchive(profileId);
                if (!File.Exists(archivePath))
                {
                    if (task != null)
                    {
                        task.MaxValue = 1;
                        task.Value = 1;
                    }
                    return;
                }

                SafeExtractZip(archivePath, targetMinecraftDir, task, cancellationToken);
            }, cancellationToken);
        }

        public Task CreateModsArchiveAsync(string profileId, string sourceMinecraftDir, ProgressTask? task = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                string profilesDataDir = AppPaths.GetProfileDataDir(profileId);
                Directory.CreateDirectory(profilesDataDir);
                string archivePath = AppPaths.GetProfileModsArchive(profileId);

                string modsDir = Path.Combine(sourceMinecraftDir, "mods");
                if (!Directory.Exists(modsDir))
                {
                    if (task != null)
                    {
                        task.MaxValue = 1;
                        task.Value = 1;
                    }
                    return;
                }

                var modFiles = Directory.GetFiles(modsDir, "*", SearchOption.AllDirectories);
                if (task != null)
                {
                    task.MaxValue = Math.Max(1, modFiles.Length);
                }

                if (modFiles.Length == 0)
                {
                    if (task != null) task.Value = task.MaxValue;
                    return;
                }

                string tempArchive = archivePath + ".tmp";
                if (File.Exists(tempArchive))
                    File.Delete(tempArchive);

                using (var zipStream = new FileStream(tempArchive, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    foreach (var filePath in modFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string entryName = Path.GetRelativePath(sourceMinecraftDir, filePath);
                        archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Fastest);
                        task?.Increment(1);
                    }
                }

                if (File.Exists(archivePath))
                    File.Delete(archivePath);

                File.Move(tempArchive, archivePath);
            }, cancellationToken);
        }

        public Task ExtractModsArchiveAsync(string profileId, string targetMinecraftDir, ProgressTask? task = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                string archivePath = AppPaths.GetProfileModsArchive(profileId);
                if (!File.Exists(archivePath))
                {
                    if (task != null)
                    {
                        task.MaxValue = 1;
                        task.Value = 1;
                    }
                    return;
                }

                SafeExtractZip(archivePath, targetMinecraftDir, task, cancellationToken);
            }, cancellationToken);
        }

        private static void SafeExtractZip(string zipFilePath, string targetDirectory, ProgressTask? task, CancellationToken cancellationToken)
        {
            string fullTargetDir = Path.GetFullPath(targetDirectory);
            if (!fullTargetDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                fullTargetDir += Path.DirectorySeparatorChar;
            }

            using var zipStream = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            var entries = archive.Entries;
            if (task != null)
            {
                task.MaxValue = Math.Max(1, entries.Count);
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    task?.Increment(1);
                    continue;
                }

                string destinationPath = Path.GetFullPath(Path.Combine(targetDirectory, entry.FullName));
                if (!destinationPath.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase))
                {
                    throw new SecurityException($"Zip entry '{entry.FullName}' traverses outside target directory '{targetDirectory}'.");
                }

                string? dir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                entry.ExtractToFile(destinationPath, overwrite: true);
                task?.Increment(1);
            }
        }
    }
}
