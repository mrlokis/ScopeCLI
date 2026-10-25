using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ScopeCLI.Common;
using ScopeCLI.Models;
using ScopeCLI.Services.Interfaces;
using Spectre.Console;
using Profile = ScopeCLI.Models.Profile;

namespace ScopeCLI.UI
{
    public class InteractiveApp
    {
        private readonly IProfileService _profileService;
        private readonly ILauncherService _launcherService;
        private readonly ISystemOptimizer _optimizer;
        private readonly IModSynchronizer _modSynchronizer;
        private Profile? _activeProfile;

        public InteractiveApp(
            IProfileService profileService,
            ILauncherService launcherService,
            ISystemOptimizer optimizer,
            IModSynchronizer modSynchronizer)
        {
            _profileService = profileService;
            _launcherService = launcherService;
            _optimizer = optimizer;
            _modSynchronizer = modSynchronizer;
        }

        public async Task RunAsync()
        {
            if (!AnsiConsole.Profile.Capabilities.Interactive && Console.IsInputRedirected)
            {
                ConsoleUi.RenderHeader();
                AnsiConsole.MarkupLine("[yellow]Terminal is running in non-interactive mode.[/]");
                AnsiConsole.MarkupLine("[grey]Use command-line arguments to launch ScopeCLI (e.g. 'ScopeCLI --help' or 'ScopeCLI launch <profile>').[/]");
                return;
            }

            while (true)
            {
                ConsoleUi.RenderHeader();
                ConsoleUi.RenderMemoryStatus();

                var profiles = await _profileService.GetAllProfilesAsync();
                var profile = await SelectOrCreateProfileAsync(profiles);

                if (profile == null)
                {
                    if (_activeProfile != null)
                    {
                        await _profileService.SaveStateAsync(_activeProfile);
                    }
                    AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                    return;
                }

                await _profileService.SwitchProfileAsync(_activeProfile, profile);
                _activeProfile = profile;

                ConsoleUi.RenderHeader();
                RenderProfileSummary(profile);
                ConsoleUi.RenderMemoryStatus();

                if (!NativeHelpers.IsAdministrator())
                {
                    bool optimize = AnsiConsole.Confirm("Enable [red]global RAM optimization[/]? [gray](requires admin restart)[/]", false);
                    if (optimize)
                    {
                        RequestAdminRelaunch(profile);
                        return;
                    }
                }
                else
                {
                    bool runOpt = AnsiConsole.Confirm("Run memory optimization now?", true);
                    if (runOpt)
                    {
                        var optResult = await _optimizer.OptimizeAsync(aggressiveKill: false);
                        AnsiConsole.MarkupLine($"[green]Optimization completed:[/] trimmed {optResult.TrimmedProcessesCount} processes.");
                    }
                }

                string serverAddress = profile.ServerAddress ?? string.Empty;
                bool connectToServer = AnsiConsole.Confirm($"Connect to server? {(string.IsNullOrEmpty(serverAddress) ? "" : $"[grey]({serverAddress})[/]")}");
                if (connectToServer)
                {
                    serverAddress = AnsiConsole.Ask("Enter server address (host:port):", serverAddress);
                }

                bool launch = AnsiConsole.Confirm("Launch Minecraft now?", true);
                if (launch)
                {
                    try
                    {
                        var proc = await _launcherService.LaunchAsync(profile, profile.MaxRamMb, serverAddress);
                        AnsiConsole.MarkupLine("[bold green]✔ Minecraft has been successfully launched![/]");
                        if (!Console.IsInputRedirected)
                        {
                            AnsiConsole.MarkupLine("[grey]Press any key to return to menu or exit...[/]");
                            Console.ReadKey(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[bold red]Launch failed:[/] {ex.Message.EscapeMarkup()}");
                        if (!Console.IsInputRedirected)
                        {
                            AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
                            Console.ReadKey(true);
                        }
                    }
                }
            }
        }

        private static void RenderProfileSummary(Profile profile)
        {
            var panel = new Panel(
                $"Nickname: [cyan]{profile.Nickname.EscapeMarkup()}[/]\n" +
                $"Mod Loader: [green]{profile.ModLoader.EscapeMarkup()}[/]\n" +
                $"Game Version: [yellow]{profile.GameVersion.EscapeMarkup()}[/]\n" +
                $"Mods: [white]{profile.Mods.Count}[/] configured\n" +
                $"Isolation: {(profile.IsIsolationEnabled ? "[green]Enabled[/]" : "[grey]Disabled[/]")}\n" +
                $"Server: {(string.IsNullOrEmpty(profile.ServerAddress) ? "[grey]None[/]" : $"[yellow]{profile.ServerAddress.EscapeMarkup()}[/]")}"
            )
            {
                Header = new PanelHeader($"[bold white]Active Profile: {profile.Name.EscapeMarkup()} ({profile.ShortId})[/]"),
                Border = BoxBorder.Rounded
            };

            AnsiConsole.Write(panel);
        }

        private async Task<Profile?> SelectOrCreateProfileAsync(List<Profile> profiles)
        {
            var choices = new List<string>();
            var profileMap = new Dictionary<string, Profile>();

            foreach (var p in profiles)
            {
                string label = $"{p.Name} [grey]({p.ModLoader} {p.GameVersion}, ID: {p.ShortId})[/]";
                choices.Add(label);
                profileMap[label] = p;
            }

            const string createNew = "[bold green]+ Create new profile[/]";
            const string manage = "[bold blue]⚙ Manage profiles[/]";
            const string exit = "[red]Exit[/]";

            choices.Add(createNew);
            if (profiles.Count > 0)
            {
                choices.Add(manage);
            }
            choices.Add(exit);

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]Choose an option or profile:[/]")
                    .PageSize(10)
                    .AddChoices(choices));

            if (selected == exit)
                return null;

            if (selected == createNew)
                return await CreateNewProfileAsync();

            if (selected == manage)
            {
                await ManageProfilesAsync(profiles);
                return await SelectOrCreateProfileAsync(await _profileService.GetAllProfilesAsync());
            }

            return profileMap[selected];
        }

        private async Task ManageProfilesAsync(List<Profile> profiles)
        {
            ConsoleUi.RenderProfilesTable(profiles);

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]Manage Profiles:[/]")
                    .AddChoices("Delete a profile", "Back"));

            if (action == "Delete a profile")
            {
                var target = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select profile to delete:")
                        .AddChoices(profiles.Select(p => $"{p.Name} ({p.ShortId})").Append("Cancel")));

                if (target != "Cancel")
                {
                    string shortId = target.Substring(target.LastIndexOf('(') + 1).TrimEnd(')');
                    if (AnsiConsole.Confirm($"Are you sure you want to delete profile {target}?", false))
                    {
                        await _profileService.DeleteProfileAsync(shortId);
                        AnsiConsole.MarkupLine("[green]Profile deleted.[/]");
                    }
                }
            }
        }

        private async Task<Profile?> CreateNewProfileAsync()
        {
            AnsiConsole.MarkupLine("\n[bold cyan]--- Create New Profile ---[/]");

            string nickname = AnsiConsole.Ask<string>("Enter your player nickname:");
            string modLoader = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select mod loader:")
                    .AddChoices("Vanilla", "Forge", "NeoForge", "Fabric"));

            var mods = new List<ModEntry>();
            if (!modLoader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase))
            {
                string modsInput = AnsiConsole.Ask<string>("Enter mods list (File/URL) [gray]or leave empty to skip[/]:", string.Empty);
                if (!string.IsNullOrWhiteSpace(modsInput))
                {
                    try
                    {
                        mods = await _modSynchronizer.ParseModListAsync(modsInput);
                        AnsiConsole.MarkupLine($"[green]Loaded {mods.Count} mod(s) from list.[/]");
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Failed to parse mod list:[/] {ex.Message.EscapeMarkup()}");
                        if (!AnsiConsole.Confirm("Continue without these mods?", true))
                            return null;
                    }
                }
            }

            string baseVersion = AnsiConsole.Ask<string>($"Enter game version [gray](e.g. 1.20.1)[/]:");
            string finalGameVersion = modLoader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase)
                ? baseVersion
                : $"{baseVersion}-{modLoader.ToLowerInvariant()}";

            string profileName = AnsiConsole.Ask<string>("Enter profile name:", $"{modLoader} {baseVersion}");
            string serverAddress = AnsiConsole.Ask<string>("Enter default server address [gray](optional, host:port)[/]:", string.Empty);
            bool enableIsolation = AnsiConsole.Confirm("Enable profile isolation? [gray](saves config/mods separately per profile)[/]", true);

            string shortId = _profileService.GenerateUniqueShortId();

            var profile = new Profile
            {
                ShortId = shortId,
                Name = profileName,
                Nickname = nickname,
                GameVersion = finalGameVersion,
                ModLoader = modLoader,
                ServerAddress = string.IsNullOrWhiteSpace(serverAddress) ? null : serverAddress,
                Mods = mods,
                IsIsolationEnabled = enableIsolation
            };

            await _profileService.SaveProfileAsync(profile);
            AnsiConsole.MarkupLine($"[bold green]✔ Profile '{profile.Name.EscapeMarkup()}' created and saved ({profile.ShortId}.upf)![/]");

            return profile;
        }

        private static void RequestAdminRelaunch(Profile profile)
        {
            var settings = new AdminLaunchSettings(profile);
            string json = JsonConvert.SerializeObject(settings);
            string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            NativeHelpers.RequestAdministrator($"--admin-relaunch {base64}");
        }

        public async Task HandleAdminRelaunchAsync(string payload)
        {
            try
            {
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                var settings = JsonConvert.DeserializeObject<AdminLaunchSettings>(json);

                ConsoleUi.RenderHeader();
                AnsiConsole.MarkupLine("[bold red]⚡ ScopeCLI elevated administrator mode active[/]");

                if (settings == null)
                {
                    AnsiConsole.MarkupLine("[red]Invalid admin launch payload.[/]");
                    await RunAsync();
                    return;
                }

                Profile? profile = null;
                if (!string.IsNullOrEmpty(settings.ProfileId))
                {
                    profile = await _profileService.GetProfileByIdAsync(settings.ProfileId);
                }

                profile ??= new Profile
                {
                    Name = "AdminSession",
                    Nickname = settings.AccountNickName,
                    GameVersion = settings.GameVersion,
                    ModLoader = settings.ModLoader,
                    ServerAddress = settings.ServerAddress,
                    MaxRamMb = settings.MaxRamMb
                };

                ConsoleUi.RenderMemoryStatus();

                AnsiConsole.MarkupLine("[yellow]Executing RAM optimization...[/]");
                var optResult = await _optimizer.OptimizeAsync(aggressiveKill: false);
                AnsiConsole.MarkupLine($"[green]Optimization finished:[/] trimmed {optResult.TrimmedProcessesCount} processes.");

                ConsoleUi.RenderMemoryStatus();

                string server = profile.ServerAddress ?? string.Empty;
                if (AnsiConsole.Confirm($"Connect to server? {(string.IsNullOrEmpty(server) ? "" : $"[grey]({server})[/]")}"))
                {
                    server = AnsiConsole.Ask("Enter server address (host:port):", server);
                }

                if (AnsiConsole.Confirm("Launch Minecraft now?", true))
                {
                    await _launcherService.LaunchAsync(profile, profile.MaxRamMb, server);
                    AnsiConsole.MarkupLine("[bold green]✔ Minecraft started![/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error during admin relaunch:[/] {ex.Message.EscapeMarkup()}");
                await RunAsync();
            }
        }
    }
}
