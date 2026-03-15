using Profile = ScopeCLI.Models.Profile;
using Spectre.Console;

namespace ScopeCLI.Services
{
    public class ProfileService
    {
        private readonly ArchiveService _archiveService = new ArchiveService();

        public async Task SaveStateAsync(Profile profile)
        {
            if (profile == null || !profile.IsIsolationEnabled)
                return;

            AnsiConsole.MarkupLine($"[yellow]Saving state for profile {profile.Name}...[/]");
            await AnsiConsole.Progress()
                .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn())
                .StartAsync(async ctx =>
                {
                    var configTask = ctx.AddTask("[green]Saving configuration[/]");
                    await _archiveService.CreateConfigArchiveAsync(profile.ShortId, "./minecraft", configTask);

                    var modsTask = ctx.AddTask("[green]Saving mods[/]");
                    await _archiveService.CreateModsArchiveAsync(profile.ShortId, "./minecraft", modsTask);
                });
            AnsiConsole.MarkupLine("[green]Profile state saved.[/]");
        }

        public async Task RestoreStateAsync(Profile profile)
        {
            if (profile == null || !profile.IsIsolationEnabled)
                return;

            if (File.Exists(Path.Combine("profiles_data", profile.ShortId, "config.zip")))
            {
                await AnsiConsole.Progress()
                    .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn())
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask("[green]Restoring configuration[/]");
                        await _archiveService.ExtractConfigArchiveAsync(profile.ShortId, "./minecraft", task);
                    });
            }

            if (File.Exists(Path.Combine("profiles_data", profile.ShortId, "mods.zip")))
            {
                await AnsiConsole.Progress()
                    .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn())
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask("[green]Restoring mods from archive[/]");
                        await _archiveService.ExtractModsArchiveAsync(profile.ShortId, "./minecraft", task);
                    });
            }
        }

        public async Task SwitchProfileAsync(Profile oldProfile, Profile newProfile)
        {
            if (oldProfile != null && oldProfile.IsIsolationEnabled)
                await SaveStateAsync(oldProfile);

            if (newProfile != null && newProfile.IsIsolationEnabled)
                await RestoreStateAsync(newProfile);
        }
    }
}