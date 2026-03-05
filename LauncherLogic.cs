using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installers;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ProcessBuilder;
using Spectre.Console;

namespace ScopeLauncher
{
    /// <summary>
    /// Provides the core launcher logic for installing and running Minecraft,
    /// including support for Forge and Fabric mod loaders.
    /// </summary>
    internal class LauncherLogic
    {
        /// <summary>
        /// Asynchronously runs the Minecraft launcher with the specified player nickname and version.
        /// Automatically detects and installs Forge or Fabric if present in the version string.
        /// </summary>
        /// <param name="nickname">The player's nickname for offline session.</param>
        /// <param name="version">The Minecraft version identifier (e.g., "1.19.2", "1.18.2-forge", "1.20.1-fabric").</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal static async Task Run(string nickname, string version)
        {
            var path = new MinecraftPath("./minecraft");
            var launcher = new MinecraftLauncher(path);
            var forgeInstaller = new ForgeInstaller(launcher);
            string versionName = version;

            await AnsiConsole.Progress()
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn()
                )
                .StartAsync(async ctx =>
                {
                    ProgressTask? forgeFileTask = null;
                    ProgressTask? forgeByteTask = null;
                    ProgressTask? forgeLogTask = null;

                    ProgressTask? fabricFileTask = null;
                    ProgressTask? fabricByteTask = null;
                    ProgressTask? fabricLogTask = null;

                    // 1. Forge installation
                    if (version.Contains("forge"))
                    {
                        string baseVersion = version.Replace("-forge", "");

                        forgeFileTask = ctx.AddTask("[yellow]Forge: preparing files[/]");
                        forgeByteTask = ctx.AddTask("[yellow]Forge: downloading[/]");
                        forgeLogTask = ctx.AddTask("[yellow]Forge: log[/]");
                        forgeLogTask.IsIndeterminate = true;

                        var forgeFileProgress = new Progress<InstallerProgressChangedEventArgs>(e =>
                        {
                            if (forgeFileTask != null)
                            {
                                forgeFileTask.MaxValue = e.TotalTasks;
                                forgeFileTask.Value = e.ProgressedTasks;
                                forgeFileTask.Description = $"[yellow]Forge: {e.Name ?? "files"}[/]";
                            }
                        });

                        var forgeByteProgress = new Progress<ByteProgress>(e =>
                        {
                            if (forgeByteTask != null)
                            {
                                forgeByteTask.MaxValue = e.TotalBytes;
                                forgeByteTask.Value = e.ProgressedBytes;
                            }
                        });

                        var forgeOutput = new Progress<string>(msg =>
                        {
                            if (forgeLogTask != null)
                            {
                                forgeLogTask.Description = $"[yellow]Forge: {msg.EscapeMarkup()}[/]";
                            }
                        });

                        versionName = await forgeInstaller.Install(baseVersion, new ForgeInstallOptions
                        {
                            FileProgress = forgeFileProgress,
                            ByteProgress = forgeByteProgress,
                            InstallerOutput = forgeOutput
                        });

                        if (forgeFileTask != null)
                        {
                            forgeFileTask.Value = forgeFileTask.MaxValue;
                            forgeFileTask.Description = "[green]Forge: files ready[/]";
                        }
                        if (forgeByteTask != null)
                        {
                            forgeByteTask.Value = forgeByteTask.MaxValue;
                            forgeByteTask.Description = "[green]Forge: download complete[/]";
                        }
                        if (forgeLogTask != null)
                        {
                            forgeLogTask.Description = "[green]Forge: installation complete[/]";
                            forgeLogTask.IsIndeterminate = false;
                            forgeLogTask.Value = 100;
                        }
                    }

                    // 2. Fabric installation
                    else if (version.Contains("fabric"))
                    {
                        string baseVersion = version.Replace("-fabric", "");

                        // Create progress tasks for Fabric
                        fabricFileTask = ctx.AddTask("[aqua]Fabric: preparing files[/]");
                        fabricByteTask = ctx.AddTask("[aqua]Fabric: downloading[/]");
                        fabricLogTask = ctx.AddTask("[aqua]Fabric: log[/]");
                        fabricLogTask.IsIndeterminate = true;

                        var fabricFileProgress = new Progress<InstallerProgressChangedEventArgs>(e =>
                        {
                            if (fabricFileTask != null)
                            {
                                fabricFileTask.MaxValue = e.TotalTasks;
                                fabricFileTask.Value = e.ProgressedTasks;
                                fabricFileTask.Description = $"[aqua]Fabric: {e.Name ?? "files"}[/]";
                            }
                        });

                        var fabricByteProgress = new Progress<ByteProgress>(e =>
                        {
                            if (fabricByteTask != null)
                            {
                                fabricByteTask.MaxValue = e.TotalBytes;
                                fabricByteTask.Value = e.ProgressedBytes;
                            }
                        });

                        var fabricOutput = new Progress<string>(msg =>
                        {
                            if (fabricLogTask != null)
                            {
                                fabricLogTask.Description = $"[aqua]Fabric: {msg.EscapeMarkup()}[/]";
                            }
                        });

                        // Create Fabric installer
                        var fabricInstaller = new FabricInstaller(new HttpClient());

                        // Install the latest Fabric Loader for the specified Minecraft version
                        // (use overload with loaderVersion for a specific version)
                        versionName = await fabricInstaller.Install(baseVersion, path);

                        // Finalize progress tasks
                        if (fabricFileTask != null)
                        {
                            fabricFileTask.Value = fabricFileTask.MaxValue;
                            fabricFileTask.Description = "[green]Fabric: files ready[/]";
                        }
                        if (fabricByteTask != null)
                        {
                            fabricByteTask.Value = fabricByteTask.MaxValue;
                            fabricByteTask.Description = "[green]Fabric: download complete[/]";
                        }
                        if (fabricLogTask != null)
                        {
                            fabricLogTask.Description = "[green]Fabric: installation complete[/]";
                            fabricLogTask.IsIndeterminate = false;
                            fabricLogTask.Value = 100;
                        }
                    }

                    // 3. Main Minecraft installation (always performed)
                    var fileTask = ctx.AddTask("[green]Minecraft: files[/]");
                    var byteTask = ctx.AddTask("[blue]Minecraft: downloading[/]");

                    launcher.FileProgressChanged += (sender, args) =>
                    {
                        fileTask.MaxValue = args.TotalTasks;
                        fileTask.Value = args.ProgressedTasks;
                    };

                    launcher.ByteProgressChanged += (sender, args) =>
                    {
                        byteTask.MaxValue = args.TotalBytes;
                        byteTask.Value = args.ProgressedBytes;
                    };

                    await launcher.InstallAsync(versionName);
                });

            // 4. Launch the game
            var process = await launcher.BuildProcessAsync(versionName, new MLaunchOption
            {
                Session = MSession.CreateOfflineSession(nickname),
                MaximumRamMb = 4096
            });

            process.Start();
        }
    }
}