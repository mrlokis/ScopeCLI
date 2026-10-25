using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ScopeCLI.Models;
using ScopeCLI.Services.Interfaces;
using Spectre.Console;

namespace ScopeCLI.Services
{
    public class ModSynchronizer : IModSynchronizer
    {
        private readonly IDownloadService _downloader;
        private const int MaxConcurrentDownloads = 3;

        public ModSynchronizer(IDownloadService? downloader = null)
        {
            _downloader = downloader ?? new DownloadService();
        }

        public async Task<List<ModEntry>> ParseModListAsync(string pathOrUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl))
                return new List<ModEntry>();

            string cleaned = pathOrUrl.Trim().Trim('"', '\'');
            string content;

            if (cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[yellow]Downloading mod list from URL...[/]");
                content = await _downloader.DownloadStringAsync(cleaned, cancellationToken);
            }
            else
            {
                if (!File.Exists(cleaned))
                    throw new FileNotFoundException($"Mod list file was not found: {cleaned}", cleaned);

                content = await File.ReadAllTextAsync(cleaned, cancellationToken);
            }

            var result = new List<ModEntry>();
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    continue;

                var parts = line.Split('|', StringSplitOptions.TrimEntries);
                if (parts.Length >= 2)
                {
                    result.Add(new ModEntry(parts[0], parts[1]));
                }
            }

            return result;
        }

        public async Task<ModSyncResult> SynchronizeAsync(List<ModEntry> requiredMods, string modsDir, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(modsDir);

            var existingFiles = Directory.GetFiles(modsDir, "*", SearchOption.TopDirectoryOnly)
                                         .Select(Path.GetFileName)
                                         .Where(f => !string.IsNullOrEmpty(f))
                                         .Cast<string>()
                                         .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var requiredFileMap = requiredMods
                .Where(m => !string.IsNullOrWhiteSpace(m.FileName))
                .GroupBy(m => m.FileName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // 1. Identify and delete excess files
            var toDelete = existingFiles.Where(f => !requiredFileMap.ContainsKey(f)).ToList();
            int deletedCount = 0;

            if (toDelete.Count > 0)
            {
                await AnsiConsole.Progress()
                    .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask($"[yellow]Removing {toDelete.Count} excess mod(s)[/]", maxValue: toDelete.Count);
                        foreach (var file in toDelete)
                        {
                            try
                            {
                                File.Delete(Path.Combine(modsDir, file));
                                deletedCount++;
                            }
                            catch
                            {
                                // Silently skip locked files
                            }
                            task.Increment(1);
                        }
                        await Task.Yield();
                    });
            }

            // 2. Identify missing mods
            var missing = requiredFileMap.Values
                .Where(m => !File.Exists(Path.Combine(modsDir, m.FileName)))
                .ToList();

            int alreadyPresent = requiredFileMap.Count - missing.Count;
            var errors = new ConcurrentBag<string>();
            int downloadedCount = 0;

            if (missing.Count > 0)
            {
                AnsiConsole.MarkupLine($"[cyan]Synchronizing mods:[/] [yellow]{missing.Count} missing[/], [green]{alreadyPresent} already up-to-date[/].");

                int activeConcurrency = Math.Min(MaxConcurrentDownloads, missing.Count);
                var queue = new ConcurrentQueue<ModEntry>(missing);

                await AnsiConsole.Progress()
                    .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new DownloadedColumn(), new RemainingTimeColumn())
                    .StartAsync(async ctx =>
                    {
                        var overallTask = ctx.AddTask($"[bold yellow]Total progress (0/{missing.Count})[/]", maxValue: missing.Count);
                        var slotTasks = new ProgressTask[activeConcurrency];

                        for (int i = 0; i < activeConcurrency; i++)
                        {
                            slotTasks[i] = ctx.AddTask($"[grey]Slot {i + 1}: Waiting...[/]", autoStart: false);
                        }

                        var workers = Enumerable.Range(0, activeConcurrency).Select(async slotIndex =>
                        {
                            var slotTask = slotTasks[slotIndex];

                            while (queue.TryDequeue(out var mod))
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                slotTask.StartTask();
                                slotTask.Description = $"[green]Slot {slotIndex + 1}:[/] [white]{mod.FileName}[/]";
                                slotTask.Value = 0;

                                string destPath = Path.Combine(modsDir, mod.FileName);
                                try
                                {
                                    await _downloader.DownloadFileWithProgressAsync(mod.Url, destPath, slotTask, cancellationToken);
                                    Interlocked.Increment(ref downloadedCount);
                                }
                                catch (Exception ex)
                                {
                                    if (File.Exists(destPath))
                                    {
                                        try { File.Delete(destPath); } catch { }
                                    }
                                    errors.Add($"{mod.FileName}: {ex.Message}");
                                }
                                finally
                                {
                                    slotTask.StopTask();
                                    lock (overallTask)
                                    {
                                        overallTask.Increment(1);
                                        overallTask.Description = $"[bold yellow]Total progress ({downloadedCount}/{missing.Count})[/]";
                                    }
                                }
                            }

                            slotTask.Description = $"[grey]Slot {slotIndex + 1}: Done[/]";
                        });

                        await Task.WhenAll(workers);
                    });
            }

            if (errors.Count > 0)
            {
                AnsiConsole.MarkupLine($"[red]Completed with {errors.Count} error(s):[/]");
                foreach (var err in errors.Take(5))
                {
                    AnsiConsole.MarkupLine($"  [red]• {err.EscapeMarkup()}[/]");
                }
                if (errors.Count > 5)
                {
                    AnsiConsole.MarkupLine($"  [gray]... and {errors.Count - 5} more error(s)[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[green]Mods are fully synchronized.[/]");
            }

            return new ModSyncResult(downloadedCount, deletedCount, alreadyPresent, errors.ToList());
        }

        public async Task<int> ClearModsFolderAsync(string modsDir, CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(modsDir))
                return 0;

            var files = Directory.GetFiles(modsDir, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                AnsiConsole.MarkupLine("[green]Mods folder is already empty.[/]");
                return 0;
            }

            int deleted = 0;
            await AnsiConsole.Progress()
                .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask($"[yellow]Removing all mods ({files.Length} files)[/]", maxValue: files.Length);
                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            File.Delete(file);
                            deleted++;
                        }
                        catch { }
                        task.Increment(1);
                    }
                    await Task.Yield();
                });

            var subDirs = Directory.GetDirectories(modsDir, "*", SearchOption.AllDirectories)
                                   .OrderByDescending(d => d.Length);
            foreach (var dir in subDirs)
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir);
                }
                catch { }
            }

            AnsiConsole.MarkupLine($"[green]Mods folder cleared ({deleted} files removed).[/]");
            return deleted;
        }
    }
}
