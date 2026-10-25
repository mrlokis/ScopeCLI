using System;
using System.Threading.Tasks;
using ScopeCLI.Common;
using ScopeCLI.Models;
using ScopeCLI.Services;
using ScopeCLI.Services.Interfaces;
using ScopeCLI.UI;
using Spectre.Console;
using Profile = ScopeCLI.Models.Profile;

namespace ScopeCLI
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            // Ensure core paths exist
            AppPaths.EnsureDirectoriesExist();

            // Initialize services
            IDownloadService downloadService = new DownloadService();
            IArchiveService archiveService = new ArchiveService();
            IModSynchronizer modSynchronizer = new ModSynchronizer(downloadService);
            IProfileService profileService = new ProfileService(archiveService, modSynchronizer);
            ILauncherService launcherService = new LauncherService();
            ISystemOptimizer optimizer = new SystemOptimizer();

            var interactiveApp = new InteractiveApp(profileService, launcherService, optimizer, modSynchronizer);

            var cliArgs = CommandLineArgs.Parse(args);

            switch (cliArgs.Command)
            {
                case CliCommandType.Help:
                    ConsoleUi.RenderHelp();
                    return;

                case CliCommandType.AdminRelaunch:
                    if (!string.IsNullOrEmpty(cliArgs.AdminPayload))
                    {
                        await interactiveApp.HandleAdminRelaunchAsync(cliArgs.AdminPayload);
                    }
                    else
                    {
                        await interactiveApp.RunAsync();
                    }
                    return;

                case CliCommandType.ListProfiles:
                    var profiles = await profileService.GetAllProfilesAsync();
                    ConsoleUi.RenderHeader();
                    ConsoleUi.RenderProfilesTable(profiles);
                    return;

                case CliCommandType.ShowProfile:
                    if (string.IsNullOrEmpty(cliArgs.ProfileQuery))
                    {
                        AnsiConsole.MarkupLine("[red]Please specify a profile name or ID.[/]");
                        return;
                    }
                    var p = await profileService.FindProfileByNameOrIdAsync(cliArgs.ProfileQuery);
                    if (p == null)
                    {
                        AnsiConsole.MarkupLine($"[red]Profile not found:[/] {cliArgs.ProfileQuery.EscapeMarkup()}");
                        return;
                    }
                    ConsoleUi.RenderHeader();
                    ConsoleUi.RenderProfilesTable(new() { p });
                    return;

                case CliCommandType.DeleteProfile:
                    if (string.IsNullOrEmpty(cliArgs.ProfileQuery))
                    {
                        AnsiConsole.MarkupLine("[red]Please specify a profile ID to delete.[/]");
                        return;
                    }
                    bool deleted = await profileService.DeleteProfileAsync(cliArgs.ProfileQuery);
                    if (deleted)
                    {
                        AnsiConsole.MarkupLine($"[green]Profile {cliArgs.ProfileQuery.EscapeMarkup()} deleted.[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Profile {cliArgs.ProfileQuery.EscapeMarkup()} not found.[/]");
                    }
                    return;

                case CliCommandType.Optimize:
                    ConsoleUi.RenderHeader();
                    ConsoleUi.RenderMemoryStatus();
                    AnsiConsole.MarkupLine("[yellow]Starting memory optimization...[/]");
                    var optResult = await optimizer.OptimizeAsync(aggressiveKill: cliArgs.AggressiveKill);
                    AnsiConsole.MarkupLine($"[green]Optimization completed:[/] trimmed {optResult.TrimmedProcessesCount} processes, terminated {optResult.TerminatedProcessesCount} processes.");
                    ConsoleUi.RenderMemoryStatus();
                    return;

                case CliCommandType.Launch:
                    await RunHeadlessLaunchAsync(cliArgs, profileService, launcherService, optimizer);
                    return;

                case CliCommandType.Interactive:
                default:
                    await interactiveApp.RunAsync();
                    return;
            }
        }

        private static async Task RunHeadlessLaunchAsync(
            CommandLineArgs args,
            IProfileService profileService,
            ILauncherService launcherService,
            ISystemOptimizer optimizer)
        {
            if (string.IsNullOrEmpty(args.ProfileQuery))
            {
                AnsiConsole.MarkupLine("[red]Profile not specified. Use --profile <id_or_name> or run without arguments for interactive mode.[/]");
                return;
            }

            var profile = await profileService.FindProfileByNameOrIdAsync(args.ProfileQuery);
            if (profile == null)
            {
                AnsiConsole.MarkupLine($"[red]Profile not found:[/] {args.ProfileQuery.EscapeMarkup()}");
                return;
            }

            if (args.Optimize)
            {
                AnsiConsole.MarkupLine("[yellow]Optimizing memory before launch...[/]");
                await optimizer.OptimizeAsync(aggressiveKill: false);
            }

            await profileService.RestoreStateAsync(profile);

            try
            {
                var proc = await launcherService.LaunchAsync(profile, args.MaxRamMb, args.ServerAddress);
                AnsiConsole.MarkupLine("[bold green]✔ Minecraft launched successfully in headless mode.[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[bold red]Launch failed:[/] {ex.Message.EscapeMarkup()}");
            }
        }
    }
}
