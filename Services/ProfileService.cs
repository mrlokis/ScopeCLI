using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ScopeCLI.Common;
using ScopeCLI.Models;
using ScopeCLI.Services.Interfaces;
using Spectre.Console;
using Profile = ScopeCLI.Models.Profile;

namespace ScopeCLI.Services
{
    public class ProfileService : IProfileService
    {
        private readonly IArchiveService _archiveService;
        private readonly IModSynchronizer _modSynchronizer;

        public ProfileService(IArchiveService? archiveService = null, IModSynchronizer? modSynchronizer = null)
        {
            _archiveService = archiveService ?? new ArchiveService();
            _modSynchronizer = modSynchronizer ?? new ModSynchronizer();
            AppPaths.EnsureDirectoriesExist();
        }

        public async Task<List<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
        {
            AppPaths.EnsureDirectoriesExist();
            var result = new List<Profile>();
            var files = Directory.GetFiles(AppPaths.ProfilesDir, "*.upf");

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    string json = await File.ReadAllTextAsync(file, cancellationToken);
                    var profile = JsonConvert.DeserializeObject<Profile>(json);
                    if (profile != null)
                    {
                        if (string.IsNullOrEmpty(profile.ShortId))
                        {
                            profile.ShortId = Path.GetFileNameWithoutExtension(file);
                        }
                        result.Add(profile);
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[grey]Warning: Failed to load profile file {Path.GetFileName(file)}: {ex.Message}[/]");
                }
            }

            return result.OrderBy(p => p.Name).ToList();
        }

        public async Task<Profile?> GetProfileByIdAsync(string shortId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(shortId))
                return null;

            string path = AppPaths.GetProfilePath(shortId);
            if (!File.Exists(path))
            {
                var all = await GetAllProfilesAsync(cancellationToken);
                return all.FirstOrDefault(p => string.Equals(p.ShortId, shortId, StringComparison.OrdinalIgnoreCase));
            }

            try
            {
                string json = await File.ReadAllTextAsync(path, cancellationToken);
                return JsonConvert.DeserializeObject<Profile>(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task<Profile?> FindProfileByNameOrIdAsync(string query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return null;

            var all = await GetAllProfilesAsync(cancellationToken);
            return all.FirstOrDefault(p =>
                string.Equals(p.ShortId, query, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, query, StringComparison.OrdinalIgnoreCase))
                ?? all.FirstOrDefault(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        public async Task SaveProfileAsync(Profile profile, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(profile);

            if (string.IsNullOrWhiteSpace(profile.ShortId))
            {
                profile.ShortId = GenerateUniqueShortId();
            }

            AppPaths.EnsureDirectoriesExist();
            string path = AppPaths.GetProfilePath(profile.ShortId);
            string json = JsonConvert.SerializeObject(profile, Formatting.Indented);
            await File.WriteAllTextAsync(path, json, cancellationToken);
        }

        public async Task<bool> DeleteProfileAsync(string shortId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(shortId))
                return false;

            string profileFile = AppPaths.GetProfilePath(shortId);
            bool deleted = false;

            if (File.Exists(profileFile))
            {
                File.Delete(profileFile);
                deleted = true;
            }

            string dataDir = AppPaths.GetProfileDataDir(shortId);
            if (Directory.Exists(dataDir))
            {
                try
                {
                    Directory.Delete(dataDir, recursive: true);
                }
                catch { }
            }

            await Task.Yield();
            return deleted;
        }

        public async Task SaveStateAsync(Profile? profile, CancellationToken cancellationToken = default)
        {
            if (profile == null || !profile.IsIsolationEnabled)
                return;

            AnsiConsole.MarkupLine($"[yellow]Saving isolated state for profile [bold]{profile.Name.EscapeMarkup()}[/]...[/]");
            await AnsiConsole.Progress()
                .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
                .StartAsync(async ctx =>
                {
                    var configTask = ctx.AddTask("[green]Archiving config[/]");
                    await _archiveService.CreateConfigArchiveAsync(profile.ShortId, AppPaths.MinecraftDir, configTask, cancellationToken);

                    var modsTask = ctx.AddTask("[green]Archiving mods[/]");
                    await _archiveService.CreateModsArchiveAsync(profile.ShortId, AppPaths.MinecraftDir, modsTask, cancellationToken);
                });

            AnsiConsole.MarkupLine("[green]Profile state saved successfully.[/]");
        }

        public async Task RestoreStateAsync(Profile? profile, CancellationToken cancellationToken = default)
        {
            if (profile == null)
                return;

            if (profile.IsIsolationEnabled)
            {
                string configZip = AppPaths.GetProfileConfigArchive(profile.ShortId);
                if (File.Exists(configZip))
                {
                    await AnsiConsole.Progress()
                        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
                        .StartAsync(async ctx =>
                        {
                            var task = ctx.AddTask("[green]Restoring configuration[/]");
                            await _archiveService.ExtractConfigArchiveAsync(profile.ShortId, AppPaths.MinecraftDir, task, cancellationToken);
                        });
                }

                string modsZip = AppPaths.GetProfileModsArchive(profile.ShortId);
                if (File.Exists(modsZip))
                {
                    await AnsiConsole.Progress()
                        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
                        .StartAsync(async ctx =>
                        {
                            var task = ctx.AddTask("[green]Restoring mods from archive[/]");
                            await _archiveService.ExtractModsArchiveAsync(profile.ShortId, AppPaths.MinecraftDir, task, cancellationToken);
                        });
                }
            }

            // Synchronize mods if specified in profile
            if (profile.Mods != null && profile.Mods.Count > 0)
            {
                await _modSynchronizer.SynchronizeAsync(profile.Mods, AppPaths.ModsDir, cancellationToken);
            }
        }

        public async Task SwitchProfileAsync(Profile? oldProfile, Profile? newProfile, CancellationToken cancellationToken = default)
        {
            if (oldProfile != null && oldProfile.IsIsolationEnabled && !string.Equals(oldProfile.ShortId, newProfile?.ShortId, StringComparison.OrdinalIgnoreCase))
            {
                await SaveStateAsync(oldProfile, cancellationToken);
            }

            if (newProfile != null)
            {
                await RestoreStateAsync(newProfile, cancellationToken);
            }
        }

        public string GenerateUniqueShortId()
        {
            AppPaths.EnsureDirectoriesExist();
            string shortId;
            do
            {
                byte[] bytes = RandomNumberGenerator.GetBytes(4);
                shortId = Convert.ToHexString(bytes).ToLowerInvariant().Substring(0, 6);
            } while (File.Exists(AppPaths.GetProfilePath(shortId)));

            return shortId;
        }
    }
}
