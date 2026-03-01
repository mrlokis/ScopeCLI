using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using Spectre.Console;

namespace ScopeLauncher
{
    internal class LauncherLogic
    {
        internal static async Task Run(string nickname, string version)
        {
            var path = new MinecraftPath("./minecraft");
            var launcher = new MinecraftLauncher(path);

            await AnsiConsole.Progress()
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn()
                )
                .StartAsync(async ctx =>
                {
                    var fileTask = ctx.AddTask("[green]Files[/]");
                    var byteTask = ctx.AddTask("[blue]Bytes[/]");

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

                    await launcher.InstallAsync(version);
                });

            var process = await launcher.BuildProcessAsync(version, new MLaunchOption
            {
                Session = MSession.CreateOfflineSession(nickname),
                MaximumRamMb = 4096
            });

            process.Start();
        }
    }
}