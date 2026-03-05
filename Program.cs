using Humanizer;
using Newtonsoft.Json;
using ScopeLauncher;
using Spectre.Console;
using System.Text;
using System.Text.Json;

namespace ScopeCLI
{
    /// <summary>
    /// The main entry point for the ScopeCLI application, which handles mod downloading,
    /// user interaction, and launching the game with optional system optimizations.
    /// </summary>
    public class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// Downloads a file from the specified URL to a local path while reporting progress
        /// to a Spectre.Console ProgressTask.
        /// </summary>
        /// <param name="url">The URL of the file to download.</param>
        /// <param name="destinationPath">The local file path where the downloaded file will be saved.</param>
        /// <param name="task">The Spectre.Console ProgressTask used to report download progress.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the download operation.</param>
        /// <returns>A task representing the asynchronous download operation.</returns>
        /// <exception cref="HttpRequestException">Thrown if the HTTP response is not successful.</exception>
        private static async Task DownloadFileWithProgressAsync(string url, string destinationPath, ProgressTask task, CancellationToken cancellationToken = default)
        {
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            task.MaxValue(totalBytes > 0 ? totalBytes : 0);

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;
                if (totalBytes > 0)
                    task.Increment(bytesRead);
            }
        }

        /// <summary>
        /// The main asynchronous entry point of the application.
        /// Handles user input, downloads mods, configures launch settings,
        /// and optionally launches the game with RAM optimization.
        /// </summary>
        /// <param name="args">
        /// Command-line arguments. If exactly one argument is provided, it is expected to be
        /// a Base64-encoded JSON string containing launch settings (nickname, game version, mod loader)
        /// for an administrator-restarted instance.
        /// </param>
        public static async Task Main(string[] args)
        {
            string relaunchOptions = string.Empty;
            if (args.Length == 1)
            {
                relaunchOptions = Encoding.UTF8.GetString(Convert.FromBase64String(args[0]));
            }

            AnsiConsole.MarkupLine("[yellow]ScopeLauncher[/] by mrlok_");
            AnsiConsole.MarkupLine("Launcher version: [yellow]0.00.1-dev[/]");

            string nickname = string.Empty;
            string gameVersion = string.Empty;
            string modLoader = string.Empty;
            GlobalOptimizer.AdminLaunchSettings launchSettings = new GlobalOptimizer.AdminLaunchSettings();

            // If relaunch options were provided, deserialize them and extract values.
            if (relaunchOptions != string.Empty)
            {
                launchSettings = JsonConvert.DeserializeObject<GlobalOptimizer.AdminLaunchSettings>(relaunchOptions);
                nickname = launchSettings.accountNickName;
                gameVersion = launchSettings.gameVersion;
                modLoader = launchSettings.modLoader;
            }

            // Prompt for nickname if not already set.
            if (string.IsNullOrEmpty(nickname))
                nickname = AnsiConsole.Ask<string>("Enter your nickname:");

            // Prompt for mod loader if not already provided.
            if (string.IsNullOrEmpty(modLoader))
            {
                modLoader = AnsiConsole.Prompt<string>(new SelectionPrompt<string>()
                    .Title("Select mod loader:")
                    .AddChoices("Vanilla", "Forge", "NeoForge", "Fabric"));
            }

            // Only ask for mods list and download if a modded loader is selected.
            if (!modLoader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase))
            {
                // Ask for the mods list source (URL or local file path).
                string modsListFile = AnsiConsole.Ask<string>("Enter mods list (URL/File) [gray](you can drag and drop a file here)[/]:", string.Empty);

                // Remove surrounding quotes if present (common when dragging files into the terminal).
                if (!string.IsNullOrEmpty(modsListFile))
                {
                    modsListFile = modsListFile.Trim();
                    if (modsListFile.StartsWith('"') && modsListFile.EndsWith('"'))
                    {
                        modsListFile = modsListFile.Substring(1, modsListFile.Length - 2);
                    }
                    else if (modsListFile.StartsWith('\'') && modsListFile.EndsWith('\''))
                    {
                        modsListFile = modsListFile.Substring(1, modsListFile.Length - 2);
                    }

                    string modsListRaw = string.Empty;
                    if (modsListFile.StartsWith("http"))
                    {
                        // Download the mods list from a URL with progress display.
                        AnsiConsole.MarkupLine("Downloading mods list from [yellow]URL[/]...");
                        AnsiConsole.Progress()
                            .Columns(
                                new TaskDescriptionColumn(),
                                new ProgressBarColumn(),
                                new DownloadedColumn(),
                                new RemainingTimeColumn()
                            )
                            .Start(ctx =>
                            {
                                using (var httpClient = new HttpClient())
                                {
                                    var response = httpClient.GetAsync(modsListFile).Result;
                                    var totalBytes = response.Content.Headers.ContentLength ?? 0;

                                    var task = ctx.AddTask("Downloading mods list...", maxValue: totalBytes);

                                    using (var stream = response.Content.ReadAsStreamAsync().Result)
                                    using (var ms = new MemoryStream())
                                    {
                                        var buffer = new byte[8192];
                                        int bytesRead;
                                        long totalBytesRead = 0;

                                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                                        {
                                            ms.Write(buffer, 0, bytesRead);
                                            totalBytesRead += bytesRead;
                                            task.Increment(bytesRead);
                                        }

                                        modsListRaw = Encoding.UTF8.GetString(ms.ToArray());
                                    }
                                }
                            });
                    }
                    else
                    {
                        // Read the mods list from a local file.
                        if (!File.Exists(modsListFile))
                        {
                            AnsiConsole.MarkupLine("[red]File not found. Exiting.[/]");
                            return;
                        }

                        try
                        {
                            AnsiConsole.MarkupLine("Reading mods list from [yellow]file[/]...");
                            modsListRaw = File.ReadAllText(modsListFile);
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLineInterpolated($"[red]Error reading file: {ex.Message}[/]");
                            return;
                        }
                    }

                    // Parse the mods list into a dictionary (filename -> download URL).
                    Dictionary<string, string> modsList = new Dictionary<string, string>();
                    string[] lines = modsListRaw.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (line.TrimStart().StartsWith('#')) continue;

                        string[] parts = line.Split('|', StringSplitOptions.TrimEntries);
                        if (parts.Length == 2)
                            modsList.TryAdd(parts[0], parts[1]);
                    }

                    AnsiConsole.MarkupLineInterpolated($"[yellow]Found {modsList.Count} mod(s) in the list.[/]");
                    if (modsList.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[red]No mods to download. Check the file format and content.[/]");
                        return;
                    }

                    AnsiConsole.MarkupLine("");
                    string modsDir = "./modsTmp";
                    Directory.CreateDirectory(modsDir);

                    // Download all mods in parallel with progress reporting.
                    await AnsiConsole.Progress()
                        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new DownloadedColumn(), new RemainingTimeColumn())
                        .StartAsync(async ctx =>
                        {
                            var tasks = modsList.Select(async kvp =>
                            {
                                var fileName = kvp.Key;
                                var fileUrl = kvp.Value;
                                var filePath = Path.Combine(modsDir, fileName);
                                var task = ctx.AddTask($"[green]Downloading[/] {fileName}");

                                try
                                {
                                    await DownloadFileWithProgressAsync(fileUrl, filePath, task);
                                }
                                catch (Exception ex)
                                {
                                    if (File.Exists(filePath))
                                        File.Delete(filePath);
                                    AnsiConsole.MarkupLineInterpolated($"[red]Error downloading {fileName}: {ex.Message}[/]");
                                }
                                finally
                                {
                                    task.StopTask();
                                }
                            });

                            await Task.WhenAll(tasks);
                        });

                    AnsiConsole.MarkupLine("[green]All mods downloaded successfully![/]");
                    AnsiConsole.MarkupLine("");
                }
                else
                {
                    // User didn't provide a mods list – just skip download, but continue.
                    AnsiConsole.MarkupLine("[yellow]No mods list provided – skipping mod download.[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Vanilla selected – no mods will be downloaded.[/]");
            }

            // --- Determine the final game version with loader suffix ---
            string finalGameVersion;

            if (string.IsNullOrEmpty(gameVersion))
            {
                // No pre-set version – ask user.
                string baseVersion = AnsiConsole.Ask<string>($"Enter game version [gray]({modLoader})[/]:");
                if (modLoader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase))
                {
                    finalGameVersion = baseVersion;
                }
                else
                {
                    finalGameVersion = $"{baseVersion}-{modLoader.ToLower()}";
                }
            }
            else
            {
                // Version already known (from relaunch arguments or previous input).
                if (modLoader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase))
                {
                    finalGameVersion = gameVersion;
                }
                else
                {
                    string suffix = "-" + modLoader.ToLower();
                    // Avoid adding suffix twice if it's already there.
                    finalGameVersion = gameVersion.EndsWith(suffix) ? gameVersion : gameVersion + suffix;
                }

                AnsiConsole.MarkupLineInterpolated($"Selected game version: [green]{finalGameVersion}[/]");
            }

            // Display system RAM information.
            AnsiConsole.MarkupLine("[gray]================================================[/]");
            long freeRam = (long)NativeHelpers.GetAvailableRam();
            long totalRam = (long)NativeHelpers.GetDeviceRam();
            AnsiConsole.MarkupLineInterpolated($"Free RAM count: [yellow]{freeRam.Bytes()}/{totalRam.Bytes()}[/]");

            // Check if free RAM is less than 20% of total RAM and show a warning.
            if (totalRam > 0)
            {
                double freePercent = (double)freeRam / totalRam * 100;
                if (freePercent < 20)
                {
                    AnsiConsole.MarkupLine("[red]Warning:[/] Free RAM is below 20% of total system memory. Consider enabling global optimization for better performance.");
                }
            }
            AnsiConsole.MarkupLine("[gray]================================================[/]");

            bool optimizeRamUsage = false;
            if (!NativeHelpers.IsAdministrator())
            {
                optimizeRamUsage = AnsiConsole.Confirm("Enable [red]global[/] optimization? [gray](require admin and restart)[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Global[/] optimization is enabled!");
                AnsiConsole.MarkupLine("[green]You need to restart your computer[/] [underline]after the game[/][green] to get everything back![/]");
            }

            // If optimization is requested but not running as admin, relaunch with admin rights.
            if (optimizeRamUsage && !NativeHelpers.IsAdministrator())
            {
                GlobalOptimizer.AdminLaunchSettings relaunchSettings = new GlobalOptimizer.AdminLaunchSettings
                {
                    accountNickName = nickname,
                    gameVersion = finalGameVersion,   // use the fully qualified version
                    modLoader = modLoader
                };
                string jsonRelaunch = JsonConvert.SerializeObject(relaunchSettings);
                NativeHelpers.RequestAdministrator(Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonRelaunch)));

                return;
            }

            // Ask whether to launch the game.
            bool launchGame = AnsiConsole.Confirm("Launch the game?");

            if (launchGame)
            {
                await LauncherLogic.Run(nickname, finalGameVersion);
            }
        }
    }
}