using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using Spectre.Console;

namespace ScopeCLI
{
    public class GlobalOptimizer
    {
        private readonly HashSet<string> _allowedPaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _allowedProcesses = new(StringComparer.OrdinalIgnoreCase);

        public void Optimize()
        {
            try
            {
                AnsiConsole.MarkupLine("[yellow]Loading allowed process lists...[/]");
                Debug.WriteLine("Loading embedded resource files.");

                LoadAllowedLists();

                AnsiConsole.MarkupLine("[yellow]Scanning running processes...[/]");
                Debug.WriteLine("Retrieving list of running processes.");

                var processesToKill = GetProcessesToKill();

                if (!processesToKill.Any())
                {
                    AnsiConsole.MarkupLine("[green]No processes need to be terminated.[/]");
                    Debug.WriteLine("Optimization completed: no processes to kill.");
                    return;
                }

                AnsiConsole.MarkupLine($"[red]Found {processesToKill.Count} process(es) to terminate.[/]");
                Debug.WriteLine($"Processes to kill: {string.Join(", ", processesToKill.Select(p => $"{p.ProcessName} (PID: {p.Id})"))}");

                int totalKilled = 0;
                foreach (var process in processesToKill)
                {
                    try
                    {
                        KillChildProcesses(process.Id);

                        process.Kill();
                        process.WaitForExit(5000);

                        totalKilled++;
                        AnsiConsole.MarkupLine($"[red]Terminated:[/] {process.ProcessName} (PID: {process.Id})");
                        Debug.WriteLine($"Successfully terminated process {process.ProcessName} (PID: {process.Id})");
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Failed to terminate {process.ProcessName} (PID: {process.Id}):[/] {ex.Message}");
                        Debug.WriteLine($"Error terminating process {process.ProcessName} (PID: {process.Id}): {ex}");
                    }
                }

                AnsiConsole.MarkupLine($"[green]Optimization completed. Total processes terminated: {totalKilled}.[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Critical error during optimization:[/] {ex.Message}");
                Debug.WriteLine($"Critical error: {ex}");
            }
        }

        private void LoadAllowedLists()
        {
            var assembly = Assembly.GetExecutingAssembly();
            string[] resourceNames = {
                "ScopeCLI.Resources.proclist.System.psl",
                "ScopeCLI.Resources.proclist.Social.psl"
            };

            foreach (var resourceName in resourceNames)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    Debug.WriteLine($"Resource not found: {resourceName}");
                    continue;
                }

                using var reader = new StreamReader(stream);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (line.StartsWith("@"))
                    {
                        string path = line.Substring(1).Trim();
                        if (!string.IsNullOrEmpty(path))
                            _allowedPaths.Add(path);
                    }
                    else
                    {
                        _allowedProcesses.Add(line);
                    }
                }
            }

            Debug.WriteLine($"Loaded {_allowedPaths.Count} allowed paths and {_allowedProcesses.Count} allowed process names.");
        }

        private List<Process> GetProcessesToKill()
        {
            var processesToKill = new List<Process>();
            var allProcesses = Process.GetProcesses();

            foreach (var process in allProcesses)
            {
                try
                {
                    if (process.WorkingSet64 <= 100L * 1024 * 1024)
                        continue;

                    if (!IsProcessAllowed(process))
                    {
                        processesToKill.Add(process);
                        Debug.WriteLine($"Marked for termination: {process.ProcessName} (PID: {process.Id}) using {process.WorkingSet64 / 1024 / 1024} MB");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error checking process {process.ProcessName} (PID: {process.Id}): {ex.Message}");
                }
            }

            var explorerProcess = allProcesses.FirstOrDefault(p =>
            {
                try
                {
                    return p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) && IsProcessAllowed(p);
                }
                catch
                {
                    return false;
                }
            });

            if (explorerProcess != null)
            {
                Debug.WriteLine($"Explorer.exe (PID: {explorerProcess.Id}) is allowed, checking its children...");
                var explorerChildren = GetChildProcessesRecursively(explorerProcess.Id);
                foreach (int childPid in explorerChildren)
                {
                    try
                    {
                        Process child = Process.GetProcessById(childPid);
                        if (processesToKill.Any(p => p.Id == childPid))
                            continue;

                        if (!IsProcessAllowed(child))
                        {
                            processesToKill.Add(child);
                            Debug.WriteLine($"Marked for termination (child of explorer): {child.ProcessName} (PID: {child.Id})");
                        }
                    }
                    catch (ArgumentException)
                    {

                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error checking child process PID {childPid}: {ex.Message}");
                    }
                }
            }

            return processesToKill;
        }

        private bool IsProcessAllowed(Process process)
        {
            try
            {
                string path = process.MainModule?.FileName;
                if (!string.IsNullOrEmpty(path) && _allowedPaths.Contains(path))
                    return true;
            }
            catch
            {

            }

            return _allowedProcesses.Contains(process.ProcessName);
        }

        private List<int> GetChildProcessesRecursively(int parentPid)
        {
            var children = new List<int>();
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {parentPid}");
                foreach (ManagementObject obj in searcher.Get())
                {
                    int childPid = Convert.ToInt32(obj["ProcessId"]);
                    children.Add(childPid);

                    children.AddRange(GetChildProcessesRecursively(childPid));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error enumerating child processes for PID {parentPid}: {ex.Message}");
            }
            return children;
        }

        private void KillChildProcesses(int parentPid)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {parentPid}");

                foreach (ManagementObject obj in searcher.Get())
                {
                    int childPid = Convert.ToInt32(obj["ProcessId"]);
                    try
                    {
                        Process childProcess = Process.GetProcessById(childPid);
                        KillChildProcesses(childPid);
                        childProcess.Kill();
                        childProcess.WaitForExit(2000);
                        Debug.WriteLine($"Terminated child process PID {childPid} of parent {parentPid}");
                    }
                    catch (ArgumentException)
                    {

                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to kill child process PID {childPid}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error enumerating child processes for PID {parentPid}: {ex.Message}");
            }
        }
    }
}