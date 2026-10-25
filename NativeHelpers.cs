using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using Spectre.Console;

namespace ScopeCLI
{
    internal static class NativeHelpers
    {
        internal static long GetDeviceRam()
        {
            if (!OperatingSystem.IsWindows())
            {
                return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            }

            var memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();

            if (GlobalMemoryStatusEx(ref memStatus))
            {
                return (long)memStatus.ullTotalPhys;
            }

            return 0;
        }

        internal static long GetAvailableRam()
        {
            if (!OperatingSystem.IsWindows())
            {
                return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            }

            var memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();

            if (GlobalMemoryStatusEx(ref memStatus))
            {
                return (long)memStatus.ullAvailPhys;
            }

            return 0;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EmptyWorkingSet(IntPtr hProcess);

        internal static bool IsAdministrator()
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            return CheckWindowsAdministrator();
        }

        [SupportedOSPlatform("windows")]
        private static bool CheckWindowsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        internal static bool RequestAdministrator(string? launchArgs = null)
        {
            if (IsAdministrator())
            {
                return true;
            }

            if (!OperatingSystem.IsWindows())
            {
                AnsiConsole.MarkupLine("[yellow]Elevation is only supported on Windows.[/]");
                return false;
            }

            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    using var proc = Process.GetCurrentProcess();
                    exePath = proc.MainModule?.FileName;
                }

                if (string.IsNullOrEmpty(exePath))
                {
                    AnsiConsole.MarkupLine("[red]Unable to determine application executable path for elevation.[/]");
                    return false;
                }

                var startInfo = new ProcessStartInfo(exePath, launchArgs ?? string.Empty)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Environment.CurrentDirectory
                };

                var elevatedProcess = Process.Start(startInfo);
                if (elevatedProcess != null)
                {
                    Environment.Exit(0);
                    return true;
                }
            }
            catch (Win32Exception)
            {
                AnsiConsole.MarkupLine("[yellow]Administrator privileges request was cancelled by user.[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to launch with administrator privileges:[/] {ex.Message.EscapeMarkup()}");
            }

            return false;
        }
    }
}
