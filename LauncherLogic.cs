using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installers;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ProcessBuilder;
using Spectre.Console;

namespace ScopeCLI
{
    internal class LauncherLogic
    {
        internal static async Task Run(string nickname, string version)
        {
            var path = new MinecraftPath("./minecraft");
            var launcher = new MinecraftLauncher(path);
            var forgeInstaller = new ForgeInstaller(launcher);
            string versionName = version;
            bool isModLoader = version.Contains("forge") || version.Contains("fabric");

            await AnsiConsole.Progress()
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn()
                )
                .StartAsync(async ctx =>
                {
                    string versionsDir = Path.Combine(path.BasePath, "versions");
                    string? installedVersion = null;

                    if (Directory.Exists(versionsDir))
                    {
                        installedVersion = FindInstalledVersion(version, versionsDir);
                    }

                    if (installedVersion != null)
                    {
                        versionName = installedVersion;
                        var infoTask = ctx.AddTask("[grey]Checking installed versions...[/]");
                        infoTask.MaxValue = 1;
                        infoTask.Value = 1;
                        infoTask.Description = $"[green]Found existing version: {versionName}[/]";
                    }
                    else
                    {
                        if (version.Contains("forge"))
                        {
                            string baseVersion = version.Replace("-forge", "");

                            var forgeFileTask = ctx.AddTask("[yellow]Forge: preparing files[/]");
                            var forgeByteTask = ctx.AddTask("[yellow]Forge: downloading[/]");
                            var forgeLogTask = ctx.AddTask("[yellow]Forge: log[/]");
                            forgeLogTask.IsIndeterminate = true;

                            var forgeFileProgress = new Progress<InstallerProgressChangedEventArgs>(e =>
                            {
                                forgeFileTask.MaxValue = e.TotalTasks;
                                forgeFileTask.Value = e.ProgressedTasks;
                                forgeFileTask.Description = $"[yellow]Forge: {e.Name ?? "files"}[/]";
                            });

                            var forgeByteProgress = new Progress<ByteProgress>(e =>
                            {
                                forgeByteTask.MaxValue = e.TotalBytes;
                                forgeByteTask.Value = e.ProgressedBytes;
                            });

                            var forgeOutput = new Progress<string>(msg =>
                            {
                                forgeLogTask.Description = $"[yellow]Forge: {msg.EscapeMarkup()}[/]";
                            });

                            versionName = await forgeInstaller.Install(baseVersion, new ForgeInstallOptions
                            {
                                FileProgress = forgeFileProgress,
                                ByteProgress = forgeByteProgress,
                                InstallerOutput = forgeOutput
                            });

                            forgeFileTask.Value = forgeFileTask.MaxValue;
                            forgeFileTask.Description = "[green]Forge: files ready[/]";
                            forgeByteTask.Value = forgeByteTask.MaxValue;
                            forgeByteTask.Description = "[green]Forge: download complete[/]";
                            forgeLogTask.Description = "[green]Forge: installation complete[/]";
                            forgeLogTask.IsIndeterminate = false;
                            forgeLogTask.Value = 100;
                        }
                        else if (version.Contains("fabric"))
                        {
                            string baseVersion = version.Replace("-fabric", "");

                            var fabricFileTask = ctx.AddTask("[aqua]Fabric: preparing files[/]");
                            var fabricByteTask = ctx.AddTask("[aqua]Fabric: downloading[/]");
                            var fabricLogTask = ctx.AddTask("[aqua]Fabric: log[/]");
                            fabricLogTask.IsIndeterminate = true;

                            var fabricFileProgress = new Progress<InstallerProgressChangedEventArgs>(e =>
                            {
                                fabricFileTask.MaxValue = e.TotalTasks;
                                fabricFileTask.Value = e.ProgressedTasks;
                                fabricFileTask.Description = $"[aqua]Fabric: {e.Name ?? "files"}[/]";
                            });

                            var fabricByteProgress = new Progress<ByteProgress>(e =>
                            {
                                fabricByteTask.MaxValue = e.TotalBytes;
                                fabricByteTask.Value = e.ProgressedBytes;
                            });

                            var fabricOutput = new Progress<string>(msg =>
                            {
                                fabricLogTask.Description = $"[aqua]Fabric: {msg.EscapeMarkup()}[/]";
                            });

                            var fabricInstaller = new FabricInstaller(new HttpClient());

                            versionName = await fabricInstaller.Install(baseVersion, path);

                            fabricFileTask.Value = fabricFileTask.MaxValue;
                            fabricFileTask.Description = "[green]Fabric: files ready[/]";
                            fabricByteTask.Value = fabricByteTask.MaxValue;
                            fabricByteTask.Description = "[green]Fabric: download complete[/]";
                            fabricLogTask.Description = "[green]Fabric: installation complete[/]";
                            fabricLogTask.IsIndeterminate = false;
                            fabricLogTask.Value = 100;
                        }

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
                    }

                    if (isModLoader)
                    {
                        var modCopyTask = ctx.AddTask("[yellow]Copying mods[/]");
                        string sourceModsDir = Path.Combine(Directory.GetCurrentDirectory(), "modsTmp");
                        string targetModsDir = Path.Combine(path.BasePath, "mods");

                        if (Directory.Exists(sourceModsDir))
                        {
                            try
                            {
                                string[] files = Directory.GetFiles(sourceModsDir, "*", SearchOption.AllDirectories);
                                modCopyTask.MaxValue = files.Length;
                                modCopyTask.Value = 0;

                                Directory.CreateDirectory(targetModsDir);

                                for (int i = 0; i < files.Length; i++)
                                {
                                    string filePath = files[i];
                                    string destFile = filePath.Replace(sourceModsDir, targetModsDir);
                                    string destDir = Path.GetDirectoryName(destFile)!;
                                    Directory.CreateDirectory(destDir);
                                    File.Copy(filePath, destFile, overwrite: true);

                                    modCopyTask.Value = i + 1;
                                    modCopyTask.Description = $"[yellow]Copying mods: {Path.GetFileName(filePath)}[/]";
                                }

                                Directory.Delete(sourceModsDir, recursive: true);
                                modCopyTask.Description = "[green]Mods copied and temporary folder removed[/]";
                            }
                            catch (Exception ex)
                            {
                                modCopyTask.Description = $"[red]Error copying mods: {ex.Message.EscapeMarkup()}[/]";
                                modCopyTask.Value = modCopyTask.MaxValue;
                            }
                        }
                        else
                        {
                            modCopyTask.MaxValue = 1;
                            modCopyTask.Value = 1;
                            modCopyTask.Description = "[grey]No mods to copy[/]";
                        }
                    }
                });

            // 6. Запуск игры
            var process = await launcher.BuildProcessAsync(versionName, new MLaunchOption
            {
                Session = MSession.CreateOfflineSession(nickname),
                MaximumRamMb = 2048
            });

            process.Start();
        }

        private static string? FindInstalledVersion(string versionInput, string versionsDir)
        {
            var directories = Directory.GetDirectories(versionsDir)
                                       .Select(Path.GetFileName)
                                       .ToArray();

            if (!versionInput.Contains("forge") && !versionInput.Contains("fabric"))
            {
                return directories.FirstOrDefault(d => d == versionInput);
            }

            if (versionInput.Contains("forge"))
            {
                string baseVersion = versionInput.Replace("-forge", "");

                return directories.FirstOrDefault(d =>
                    d.Contains(baseVersion) && d.Contains("forge", StringComparison.OrdinalIgnoreCase));
            }

            if (versionInput.Contains("fabric"))
            {
                string baseVersion = versionInput.Replace("-fabric", "");

                return directories.FirstOrDefault(d =>
                    d.Contains(baseVersion) && d.Contains("fabric", StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }
    }
}