using System.Text;
using Newtonsoft.Json;
using Spectre.Console;
using ScopeCLI.Models;
using ScopeCLI.Services;
using Humanizer;
using Profile = ScopeCLI.Models.Profile;

namespace ScopeCLI
{
    public static class Program
    {
        private static readonly ProfileService _profileService = new ProfileService();
        private static Profile? _activeProfile;

        public static async Task Main(string[] args)
        {
            while (true)
            {
                if (args.Length == 1 && !string.IsNullOrEmpty(args[0]))
                {
                    await HandleAdminRelaunch(args[0]);
                    return;
                }

                AnsiConsole.MarkupLine("[yellow]ScopeLauncher[/] by mrlok_");
                AnsiConsole.MarkupLine("Launcher version: [yellow]0.00.1-dev[/]");

                var profile = await SelectOrCreateProfileAsync();
                if (profile == null)
                {
                    await _profileService.SaveStateAsync(_activeProfile);
                    AnsiConsole.MarkupLine("[yellow]Exiting...[/]");
                    return;
                }

                await _profileService.SwitchProfileAsync(_activeProfile, profile);
                _activeProfile = profile;

                ShowMemoryInfo();

                if (!NativeHelpers.IsAdministrator())
                {
                    bool optimize = AnsiConsole.Confirm("Enable [red]global[/] optimization? [gray](require admin and restart)[/]", false);
                    if (optimize)
                    {
                        RequestAdminRelaunch(profile);
                        return;
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Global[/] optimization is enabled!");
                    AnsiConsole.MarkupLine("[green]You need to restart your computer[/] [underline]after the game[/][green] to get everything back![/]");
                }

                bool connectToServer = AnsiConsole.Confirm("Connect to server?");
                string serverAddress = profile.ServerAddress ?? "";
                if (connectToServer)
                {
                    serverAddress = AnsiConsole.Ask("Enter server address (address:port):", serverAddress);
                }

                bool launchGame = AnsiConsole.Confirm("Launch the game?");
                if (launchGame)
                {
                    await LauncherLogic.Run(profile.Nickname, profile.GameVersion, (int)(NativeHelpers.GetAvailableRam() / (1024 * 1024)));
                    return;
                }

                AnsiConsole.MarkupLine("[yellow]Returning to profile selection...[/]");
                Console.Clear();
            }
        }

        private static async Task<Profile?> SelectOrCreateProfileAsync()
        {
            string profilesDir = "profiles";
            Directory.CreateDirectory(profilesDir);
            var profileFiles = Directory.GetFiles(profilesDir, "*.upf").ToList();
            var profileChoices = new List<string>();
            var profileMap = new Dictionary<string, string>();

            foreach (var file in profileFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var profile = JsonConvert.DeserializeObject<Profile>(json);
                    if (profile != null)
                    {
                        string display = $"{profile.Name} ({profile.ShortId})";
                        profileChoices.Add(display);
                        profileMap[display] = file;
                    }
                }
                catch { }
            }

            profileChoices.Add("Create new profile");
            profileChoices.Add("Exit");

            var selectedDisplay = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Select profile")
                .AddChoices(profileChoices));

            if (selectedDisplay == "Exit")
                return null;

            if (selectedDisplay == "Create new profile")
            {
                return await CreateNewProfileAsync();
            }
            else
            {
                string path = profileMap[selectedDisplay];
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<Profile>(json);
            }
        }

        private static async Task<Profile> CreateNewProfileAsync()
        {
            string nickname = AnsiConsole.Ask<string>("Enter your nickname:");
            string modLoader = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Select mod loader:")
                .AddChoices("Vanilla", "Forge", "Fabric"));

            List<ModEntry> mods = new List<ModEntry>();
            if (!modLoader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase))
            {
                string modsListFile = AnsiConsole.Ask<string>("Enter mods list (URL/File) [gray]you can drag and drop a file here[/]", string.Empty);
                if (!string.IsNullOrEmpty(modsListFile))
                {
                    modsListFile = modsListFile.Trim().Trim('"').Trim('\'');
                    string modsListRaw = await ReadModsListContentAsync(modsListFile);
                    if (string.IsNullOrEmpty(modsListRaw))
                        return null;

                    var lines = modsListRaw.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;
                        var parts = line.Split('|', StringSplitOptions.TrimEntries);
                        if (parts.Length == 2)
                            mods.Add(new ModEntry { FileName = parts[0], Url = parts[1] });
                    }

                    AnsiConsole.MarkupLineInterpolated($"[yellow]Found {mods.Count} mod(s) in the list.[/]");
                    if (mods.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[red]No mods to download. Check the file format and content.[/]");
                        return null;
                    }

                    var downloader = new DownloadService();
                    string modsDir = "./minecraft/mods";
                    Directory.CreateDirectory(modsDir);

                    await AnsiConsole.Progress()
                        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new DownloadedColumn(), new RemainingTimeColumn())
                        .StartAsync(async ctx =>
                        {
                            var tasks = mods.Select(async mod =>
                            {
                                var task = ctx.AddTask($"[green]Downloading[/] {mod.FileName}");
                                try
                                {
                                    await downloader.DownloadFileWithProgressAsync(mod.Url, Path.Combine(modsDir, mod.FileName), task);
                                }
                                catch (Exception ex)
                                {
                                    AnsiConsole.MarkupLineInterpolated($"[red]Error downloading {mod.FileName}: {ex.Message}[/]");
                                }
                                finally
                                {
                                    task.StopTask();
                                }
                            });
                            await Task.WhenAll(tasks);
                        });
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]No mods list provided – skipping mod download.[/]");
                    string modsFolder = Path.Combine("minecraft", "mods");
                    if (Directory.Exists(modsFolder))
                    {
                        Directory.Delete(modsFolder, true);
                    }
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Vanilla selected – no mods will be downloaded.[/]");
            }

            string baseVersion = AnsiConsole.Ask<string>($"Enter game version [gray]({modLoader})[/]:");
            string finalGameVersion = modLoader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase)
                ? baseVersion
                : $"{baseVersion}-{modLoader.ToLower()}";

            string serverAddress = AnsiConsole.Ask<string>("Enter server address (address:port) (optional):", "");
            string profileName = AnsiConsole.Ask<string>("Enter profile name:");

            bool saveProfile = AnsiConsole.Confirm("Save profile?");
            if (!saveProfile)
            {
                AnsiConsole.MarkupLine("[yellow]Profile not saved. Returning to selection...[/]");
                return null;
            }

            string shortId;
            do
            {
                shortId = Guid.NewGuid().ToString("N").Substring(0, 6);
            } while (File.Exists(Path.Combine("profiles", $"{shortId}.upf")));

            bool enableIsolation = AnsiConsole.Confirm("Enable profile isolation? (config and mods will be saved/restored automatically)", true);

            var profile = new Profile
            {
                ShortId = shortId,
                Name = profileName,
                Nickname = nickname,
                GameVersion = finalGameVersion,
                ModLoader = modLoader,
                ServerAddress = serverAddress,
                Mods = mods,
                IsIsolationEnabled = enableIsolation
            };

            string json = JsonConvert.SerializeObject(profile, Formatting.Indented);
            File.WriteAllText(Path.Combine("profiles", $"{shortId}.upf"), json);
            AnsiConsole.MarkupLine($"[green]Profile saved as {shortId}.upf[/]");

            return profile;
        }

        private static async Task<string> ReadModsListContentAsync(string pathOrUrl)
        {
            if (pathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("Downloading mods list from [yellow]URL[/]...");
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(pathOrUrl);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            else
            {
                if (!File.Exists(pathOrUrl))
                {
                    AnsiConsole.MarkupLine("[red]File not found.[/]");
                    return null;
                }
                return await File.ReadAllTextAsync(pathOrUrl);
            }
        }

        private static void ShowMemoryInfo()
        {
            long freeRam = NativeHelpers.GetAvailableRam();
            long totalRam = NativeHelpers.GetDeviceRam();
            AnsiConsole.MarkupLine("[gray]================================================[/]");
            AnsiConsole.MarkupLineInterpolated($"Free RAM count: [yellow]{freeRam.Bytes()}/{totalRam.Bytes()}[/]");
            if (totalRam > 0)
            {
                double freePercent = (double)freeRam / totalRam * 100;
                if (freePercent < 20)
                    AnsiConsole.MarkupLine("[red]Warning:[/] Free RAM is below 20% of total system memory. Consider enabling global optimization for better performance.");
            }
            AnsiConsole.MarkupLine("[gray]================================================[/]");
        }

        private static void RequestAdminRelaunch(Profile profile)
        {
            var settings = new AdminLaunchSettings
            {
                accountNickName = profile.Nickname,
                gameVersion = profile.GameVersion,
                modLoader = profile.ModLoader
            };
            string json = JsonConvert.SerializeObject(settings);
            NativeHelpers.RequestAdministrator(Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
        }

        private static async Task HandleAdminRelaunch(string base64)
        {
            string json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            var settings = JsonConvert.DeserializeObject<AdminLaunchSettings>(json);

            AnsiConsole.MarkupLine("[gray]================================================[/]");
            long freeRam = NativeHelpers.GetAvailableRam();
            long totalRam = NativeHelpers.GetDeviceRam();
            AnsiConsole.MarkupLineInterpolated($"Free RAM count: [yellow]{freeRam.Bytes()}/{totalRam.Bytes()}[/]");
            if (totalRam > 0)
            {
                double freePercent = (double)freeRam / totalRam * 100;
                if (freePercent < 20)
                    AnsiConsole.MarkupLine("[red]Warning:[/] Free RAM is below 20% of total system memory. Consider enabling global optimization for better performance.");
            }
            AnsiConsole.MarkupLine("[gray]================================================[/]");

            if (!NativeHelpers.IsAdministrator())
            {
                AnsiConsole.MarkupLine("[red]Administrator privileges required but not present. Exiting.[/]");
                return;
            }

            GlobalOptimizer opt = new GlobalOptimizer();
            opt.Optimize();

            bool connectToServer = AnsiConsole.Confirm("Connect to server?");
            string serverAddress = "";
            if (connectToServer)
            {
                serverAddress = AnsiConsole.Ask<string>("Enter server address (address:port):", serverAddress);
            }

            bool launchGame = AnsiConsole.Confirm("Launch the game?");
            if (launchGame)
            {
                await LauncherLogic.Run(settings.accountNickName, settings.gameVersion, (int)(freeRam / (1024 * 1024)));
            }
        }
    }
}