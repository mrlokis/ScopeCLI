using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.Installer.NeoForge.Installers;
using CmlLib.Core.Installers;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ProcessBuilder;
using ScopeCLI.Common;
using ScopeCLI.Models;
using ScopeCLI.Services.Interfaces;
using Spectre.Console;
using Profile = ScopeCLI.Models.Profile;

namespace ScopeCLI.Services
{
    public class LauncherService : ILauncherService
    {
        private readonly MinecraftPath _minecraftPath;
        private readonly MinecraftLauncher _launcher;

        public LauncherService(string? customPath = null)
        {
            _minecraftPath = new MinecraftPath(customPath ?? AppPaths.MinecraftDir);
            _launcher = new MinecraftLauncher(_minecraftPath);
        }

        public async Task<string> EnsureInstalledAsync(string version, string modLoader, CancellationToken cancellationToken = default)
        {
            string versionsDir = Path.Combine(_minecraftPath.BasePath, "versions");
            string? installedVersion = null;

            if (Directory.Exists(versionsDir))
            {
                installedVersion = FindInstalledVersion(version, modLoader, versionsDir);
            }

            if (!string.IsNullOrEmpty(installedVersion))
            {
                AnsiConsole.MarkupLine($"[green]Using existing installed version:[/] [cyan]{installedVersion}[/]");
                return installedVersion;
            }

            string versionName = version;

            await AnsiConsole.Progress()
                .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
                .StartAsync(async ctx =>
                {
                    if (string.Equals(modLoader, "NeoForge", StringComparison.OrdinalIgnoreCase) ||
                        version.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
                    {
                        string baseVersion = CleanVersionString(version, "neoforge");

                        var neoFileTask = ctx.AddTask("[purple]NeoForge: preparing files[/]");
                        var neoByteTask = ctx.AddTask("[purple]NeoForge: downloading[/]");
                        var neoLogTask = ctx.AddTask("[purple]NeoForge: status[/]");
                        neoLogTask.IsIndeterminate = true;

                        var neoInstaller = new NeoForgeInstaller(_launcher);
                        var neoOptions = new NeoForgeInstallOptions
                        {
                            CancellationToken = cancellationToken,
                            FileProgress = new Progress<InstallerProgressChangedEventArgs>(e =>
                            {
                                neoFileTask.MaxValue = Math.Max(1, e.TotalTasks);
                                neoFileTask.Value = e.ProgressedTasks;
                                neoFileTask.Description = $"[purple]NeoForge: {e.Name ?? "files"}[/]";
                            }),
                            ByteProgress = new Progress<ByteProgress>(e =>
                            {
                                neoByteTask.MaxValue = Math.Max(1, e.TotalBytes);
                                neoByteTask.Value = e.ProgressedBytes;
                            }),
                            InstallerOutput = new Progress<string>(msg =>
                            {
                                neoLogTask.Description = $"[purple]NeoForge: {msg.EscapeMarkup()}[/]";
                            })
                        };

                        versionName = await neoInstaller.Install(baseVersion, neoOptions);

                        neoFileTask.Value = neoFileTask.MaxValue;
                        neoByteTask.Value = neoByteTask.MaxValue;
                        neoLogTask.IsIndeterminate = false;
                        neoLogTask.Description = "[green]NeoForge installation complete[/]";
                    }
                    else if (string.Equals(modLoader, "Forge", StringComparison.OrdinalIgnoreCase) ||
                             version.Contains("forge", StringComparison.OrdinalIgnoreCase))
                    {
                        string baseVersion = CleanVersionString(version, "forge");

                        var forgeFileTask = ctx.AddTask("[yellow]Forge: preparing files[/]");
                        var forgeByteTask = ctx.AddTask("[yellow]Forge: downloading[/]");
                        var forgeLogTask = ctx.AddTask("[yellow]Forge: status[/]");
                        forgeLogTask.IsIndeterminate = true;

                        var forgeInstaller = new ForgeInstaller(_launcher);
                        var forgeOptions = new ForgeInstallOptions
                        {
                            FileProgress = new Progress<InstallerProgressChangedEventArgs>(e =>
                            {
                                forgeFileTask.MaxValue = Math.Max(1, e.TotalTasks);
                                forgeFileTask.Value = e.ProgressedTasks;
                                forgeFileTask.Description = $"[yellow]Forge: {e.Name ?? "files"}[/]";
                            }),
                            ByteProgress = new Progress<ByteProgress>(e =>
                            {
                                forgeByteTask.MaxValue = Math.Max(1, e.TotalBytes);
                                forgeByteTask.Value = e.ProgressedBytes;
                            }),
                            InstallerOutput = new Progress<string>(msg =>
                            {
                                forgeLogTask.Description = $"[yellow]Forge: {msg.EscapeMarkup()}[/]";
                            })
                        };

                        versionName = await forgeInstaller.Install(baseVersion, forgeOptions);

                        forgeFileTask.Value = forgeFileTask.MaxValue;
                        forgeByteTask.Value = forgeByteTask.MaxValue;
                        forgeLogTask.IsIndeterminate = false;
                        forgeLogTask.Description = "[green]Forge installation complete[/]";
                    }
                    else if (string.Equals(modLoader, "Fabric", StringComparison.OrdinalIgnoreCase) ||
                             version.Contains("fabric", StringComparison.OrdinalIgnoreCase))
                    {
                        string baseVersion = CleanVersionString(version, "fabric");

                        var fabricFileTask = ctx.AddTask("[aqua]Fabric: preparing files[/]");
                        var fabricByteTask = ctx.AddTask("[aqua]Fabric: downloading[/]");
                        var fabricLogTask = ctx.AddTask("[aqua]Fabric: status[/]");
                        fabricLogTask.IsIndeterminate = true;

                        using var httpClient = new HttpClient();
                        var fabricInstaller = new FabricInstaller(httpClient);
                        versionName = await fabricInstaller.Install(baseVersion, _minecraftPath);

                        fabricFileTask.Value = fabricFileTask.MaxValue;
                        fabricByteTask.Value = fabricByteTask.MaxValue;
                        fabricLogTask.IsIndeterminate = false;
                        fabricLogTask.Description = "[green]Fabric installation complete[/]";
                    }

                    // Install vanilla assets and libraries
                    var mcFileTask = ctx.AddTask("[green]Minecraft: files[/]");
                    var mcByteTask = ctx.AddTask("[blue]Minecraft: downloading[/]");

                    _launcher.FileProgressChanged += (sender, args) =>
                    {
                        mcFileTask.MaxValue = Math.Max(1, args.TotalTasks);
                        mcFileTask.Value = args.ProgressedTasks;
                    };

                    _launcher.ByteProgressChanged += (sender, args) =>
                    {
                        mcByteTask.MaxValue = Math.Max(1, args.TotalBytes);
                        mcByteTask.Value = args.ProgressedBytes;
                    };

                    await _launcher.InstallAsync(versionName);
                });

            return versionName;
        }

        public async Task<Process> LaunchAsync(Profile profile, int? maxRamMb = null, string? serverAddress = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(profile);

            AnsiConsole.MarkupLine($"[cyan]Preparing to launch Minecraft for profile:[/] [bold]{profile.Name.EscapeMarkup()}[/]");

            string resolvedVersion = await EnsureInstalledAsync(profile.GameVersion, profile.ModLoader, cancellationToken);

            int ramAllocation = maxRamMb ?? profile.MaxRamMb ?? CalculateSafeDefaultRam();

            var launchOptions = new MLaunchOption
            {
                Session = MSession.CreateOfflineSession(profile.Nickname),
                MaximumRamMb = ramAllocation
            };

            if (!string.IsNullOrWhiteSpace(profile.JavaPath) && File.Exists(profile.JavaPath))
            {
                launchOptions.JavaPath = profile.JavaPath;
            }

            string? effectiveServer = !string.IsNullOrWhiteSpace(serverAddress) ? serverAddress : profile.ServerAddress;
            if (!string.IsNullOrWhiteSpace(effectiveServer))
            {
                ApplyServerAddress(launchOptions, effectiveServer);
            }

            AnsiConsole.MarkupLine($"[grey]Allocating RAM:[/] [yellow]{ramAllocation} MB[/]");
            AnsiConsole.MarkupLine($"[grey]Player nickname:[/] [yellow]{profile.Nickname.EscapeMarkup()}[/]");
            if (!string.IsNullOrWhiteSpace(effectiveServer))
            {
                AnsiConsole.MarkupLine($"[grey]Auto-connect server:[/] [yellow]{effectiveServer.EscapeMarkup()}[/]");
            }

            var process = await _launcher.BuildProcessAsync(resolvedVersion, launchOptions);

            AnsiConsole.MarkupLine("[bold green]Starting Minecraft process...[/]");
            process.Start();

            return process;
        }

        private static int CalculateSafeDefaultRam()
        {
            long availableRam = NativeHelpers.GetAvailableRam();
            if (availableRam <= 0)
                return 4096;

            int availableMb = (int)(availableRam / (1024 * 1024));

            // Allocate up to 60% of available RAM, clamped between 2048 MB and 8192 MB
            int calculated = (int)(availableMb * 0.60);
            return Math.Clamp(calculated, 2048, 8192);
        }

        private static void ApplyServerAddress(MLaunchOption launchOptions, string serverAddress)
        {
            var parts = serverAddress.Trim().Split(':');
            launchOptions.ServerIp = parts[0].Trim();
            if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out int port))
            {
                launchOptions.ServerPort = port;
            }
        }

        private static string CleanVersionString(string version, string loaderName)
        {
            return version.Replace($"-{loaderName}", "", StringComparison.OrdinalIgnoreCase)
                          .Replace(loaderName, "", StringComparison.OrdinalIgnoreCase)
                          .Trim('-', ' ');
        }

        private static string? FindInstalledVersion(string versionInput, string modLoader, string versionsDir)
        {
            if (!Directory.Exists(versionsDir))
                return null;

            var directories = Directory.GetDirectories(versionsDir)
                                       .Select(Path.GetFileName)
                                       .Where(d => !string.IsNullOrEmpty(d))
                                       .Cast<string>()
                                       .ToArray();

            bool isNeoForge = string.Equals(modLoader, "NeoForge", StringComparison.OrdinalIgnoreCase) ||
                             versionInput.Contains("neoforge", StringComparison.OrdinalIgnoreCase);

            bool isForge = !isNeoForge && (string.Equals(modLoader, "Forge", StringComparison.OrdinalIgnoreCase) ||
                           versionInput.Contains("forge", StringComparison.OrdinalIgnoreCase));

            bool isFabric = string.Equals(modLoader, "Fabric", StringComparison.OrdinalIgnoreCase) ||
                            versionInput.Contains("fabric", StringComparison.OrdinalIgnoreCase);

            if (!isNeoForge && !isForge && !isFabric)
            {
                return directories.FirstOrDefault(d => string.Equals(d, versionInput, StringComparison.OrdinalIgnoreCase));
            }

            if (isNeoForge)
            {
                string baseVersion = CleanVersionString(versionInput, "neoforge");
                return directories.FirstOrDefault(d =>
                    d.Contains(baseVersion, StringComparison.OrdinalIgnoreCase) &&
                    d.Contains("neoforge", StringComparison.OrdinalIgnoreCase));
            }

            if (isForge)
            {
                string baseVersion = CleanVersionString(versionInput, "forge");
                return directories.FirstOrDefault(d =>
                    d.Contains(baseVersion, StringComparison.OrdinalIgnoreCase) &&
                    d.Contains("forge", StringComparison.OrdinalIgnoreCase) &&
                    !d.Contains("neoforge", StringComparison.OrdinalIgnoreCase));
            }

            if (isFabric)
            {
                string baseVersion = CleanVersionString(versionInput, "fabric");
                return directories.FirstOrDefault(d =>
                    d.Contains(baseVersion, StringComparison.OrdinalIgnoreCase) &&
                    d.Contains("fabric", StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }
    }
}
