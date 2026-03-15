using ScopeCLI.Models;
using Spectre.Console;

namespace ScopeCLI.Services
{
    public class ModSynchronizer
    {
        private readonly DownloadService _downloader = new DownloadService();

        public async Task SynchronizeAsync(List<ModEntry> requiredMods, string modsDir)
        {
            Directory.CreateDirectory(modsDir);

            var existingFiles = Directory.GetFiles(modsDir, "*", SearchOption.TopDirectoryOnly)
                                         .Select(Path.GetFileName)
                                         .Where(f => f != null)
                                         .ToHashSet();

            var requiredFileNames = requiredMods.Select(m => m.FileName).ToHashSet();

            var toDelete = existingFiles.Except(requiredFileNames).ToList();
            if (toDelete.Any())
            {
                await AnsiConsole.Progress()
                    .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn())
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask($"[yellow]Removing {toDelete.Count} excess mod(s)[/]", maxValue: toDelete.Count);
                        foreach (var file in toDelete)
                        {
                            try
                            {
                                File.Delete(Path.Combine(modsDir, file));
                            }
                            catch { }
                            task.Increment(1);
                        }
                    });
            }

            var missing = requiredMods.Where(m => !File.Exists(Path.Combine(modsDir, m.FileName))).ToList();
            if (missing.Any())
            {
                AnsiConsole.MarkupLine($"[yellow]Found {missing.Count} missing mod(s). Downloading...[/]");
                await AnsiConsole.Progress()
                    .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new DownloadedColumn(), new RemainingTimeColumn())
                    .StartAsync(async ctx =>
                    {
                        var tasks = missing.Select(async mod =>
                        {
                            var task = ctx.AddTask($"[green]Downloading[/] {mod.FileName}");
                            try
                            {
                                await _downloader.DownloadFileWithProgressAsync(mod.Url, Path.Combine(modsDir, mod.FileName), task);
                            }
                            catch (Exception ex)
                            {
                                if (File.Exists(Path.Combine(modsDir, mod.FileName)))
                                    File.Delete(Path.Combine(modsDir, mod.FileName));
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

            if (!toDelete.Any() && !missing.Any())
                AnsiConsole.MarkupLine("[green]All mods are synchronized.[/]");
            else
                AnsiConsole.MarkupLine("[green]Mods synchronization completed.[/]");
        }

        public async Task ClearModsFolder(string modsDir)
        {
            if (!Directory.Exists(modsDir))
                return;

            var files = Directory.GetFiles(modsDir, "*", SearchOption.AllDirectories);
            if (!files.Any())
            {
                AnsiConsole.MarkupLine("[green]Mods folder is already empty.[/]");
                return;
            }

            await AnsiConsole.Progress()
                .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask($"[yellow]Removing all mods ({files.Length} files)[/]", maxValue: files.Length);
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch { }
                        task.Increment(1);
                    }
                });

            var dirs = Directory.GetDirectories(modsDir, "*", SearchOption.AllDirectories)
                                .OrderByDescending(d => d.Length);
            foreach (var dir in dirs)
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir);
                }
                catch { }
            }

            AnsiConsole.MarkupLine("[green]Mods folder cleared.[/]");
        }
    }
}