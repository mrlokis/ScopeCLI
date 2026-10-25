using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using ScopeCLI.Services.Interfaces;
using Spectre.Console;

namespace ScopeCLI.Services
{
    public class SystemOptimizer : ISystemOptimizer
    {
        private readonly HashSet<string> _allowedPaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _allowedProcesses = new(StringComparer.OrdinalIgnoreCase);
        private bool _listsLoaded;

        public Task<OptimizationResult> OptimizeAsync(bool aggressiveKill = false, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                if (!OperatingSystem.IsWindows())
                {
                    AnsiConsole.MarkupLine("[yellow]System optimization is only available on Windows.[/]");
                    return new OptimizationResult(0, 0, 0);
                }

                long ramBefore = NativeHelpers.GetAvailableRam();
                int trimmedCount = 0;
                int terminatedCount = 0;

                AnsiConsole.MarkupLine("[yellow]Trimming process working sets...[/]");
                trimmedCount = TrimWorkingSets();

                // Trigger GC in current process
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                if (aggressiveKill)
                {
                    AnsiConsole.MarkupLine("[yellow]Analyzing high-memory background processes...[/]");
                    EnsureAllowedListsLoaded();
                    terminatedCount = TerminateExcessiveProcesses();
                }

                long ramAfter = NativeHelpers.GetAvailableRam();
                long freedBytes = Math.Max(0, ramAfter - ramBefore);

                return new OptimizationResult(trimmedCount, terminatedCount, freedBytes);
            }, cancellationToken);
        }

        private static int TrimWorkingSets()
        {
            int currentPid = Environment.ProcessId;
            int trimmed = 0;
            var processes = Process.GetProcesses();

            foreach (var process in processes)
            {
                try
                {
                    if (process.Id == currentPid || process.Id == 0)
                        continue;

                    if (NativeHelpers.EmptyWorkingSet(process.Handle))
                    {
                        trimmed++;
                    }
                }
                catch
                {
                    // Access denied or process exited - safe to ignore
                }
                finally
                {
                    process.Dispose();
                }
            }

            return trimmed;
        }

        private int TerminateExcessiveProcesses()
        {
            var candidates = GetProcessesToKill();
            if (candidates.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]No candidate processes need to be terminated.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[red]Found {candidates.Count} non-whitelisted process(es) using > 100MB RAM.[/]");
            int killed = 0;

            foreach (var proc in candidates)
            {
                try
                {
                    string name = proc.ProcessName;
                    int pid = proc.Id;

                    if (OperatingSystem.IsWindows())
                    {
                        KillChildProcesses(pid);
                    }

                    proc.Kill();
                    proc.WaitForExit(3000);
                    killed++;
                    AnsiConsole.MarkupLine($"[red]Terminated:[/] {name.EscapeMarkup()} (PID: {pid})");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[grey]Could not terminate {proc.ProcessName.EscapeMarkup()}: {ex.Message.EscapeMarkup()}[/]");
                }
                finally
                {
                    proc.Dispose();
                }
            }

            return killed;
        }

        private List<Process> GetProcessesToKill()
        {
            var processesToKill = new List<Process>();
            var allProcesses = Process.GetProcesses();
            int currentPid = Environment.ProcessId;

            foreach (var process in allProcesses)
            {
                try
                {
                    if (process.Id == currentPid || process.Id == 0)
                        continue;

                    if (process.WorkingSet64 <= 100L * 1024 * 1024)
                        continue;

                    if (!IsProcessAllowed(process))
                    {
                        processesToKill.Add(process);
                    }
                }
                catch
                {
                    // Ignore processes we can't inspect
                }
            }

            return processesToKill;
        }

        private bool IsProcessAllowed(Process process)
        {
            if (process.Id == Environment.ProcessId)
                return true;

            if (_allowedProcesses.Contains(process.ProcessName))
                return true;

            try
            {
                string? path = process.MainModule?.FileName;
                if (!string.IsNullOrEmpty(path))
                {
                    string normalized = path.Replace('/', '\\');
                    if (_allowedPaths.Any(allowed => normalized.Contains(allowed, StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Access denied on system process — treat as allowed/safe
                return true;
            }

            return false;
        }

        [SupportedOSPlatform("windows")]
        private static void KillChildProcesses(int parentPid)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {parentPid}");
                foreach (ManagementObject obj in searcher.Get())
                {
                    int childPid = Convert.ToInt32(obj["ProcessId"]);
                    try
                    {
                        KillChildProcesses(childPid);
                        using var child = Process.GetProcessById(childPid);
                        child.Kill();
                        child.WaitForExit(2000);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void EnsureAllowedListsLoaded()
        {
            if (_listsLoaded)
                return;

            var assembly = Assembly.GetExecutingAssembly();
            string[] resourceNames = {
                "ScopeCLI.Resources.proclist.System.psl",
                "ScopeCLI.Resources.proclist.Social.psl"
            };

            foreach (var resourceName in resourceNames)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    continue;

                using var reader = new StreamReader(stream);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (line.StartsWith('@'))
                    {
                        string path = line.Substring(1).Trim().Replace('/', '\\');
                        if (!string.IsNullOrEmpty(path))
                        {
                            _allowedPaths.Add(path);
                        }
                    }
                    else
                    {
                        _allowedProcesses.Add(line);
                    }
                }
            }

            _listsLoaded = true;
        }
    }
}
