using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ScopeLauncher
{
    internal class NativeHelpers
    {
        internal static ulong GetDeviceRam()
        {
            ulong installedMemory = 0;
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();

            if (GlobalMemoryStatusEx(memStatus))
            {
                installedMemory = memStatus.ullTotalPhys;
            }

            return installedMemory;
        }

        internal static ulong GetAvailableRam()
        {
            ulong availableMemory = 0;
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();

            if (GlobalMemoryStatusEx(memStatus))
            {
                availableMemory = memStatus.ullAvailPhys;
            }

            return availableMemory;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
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
            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(NativeHelpers.MEMORYSTATUSEX));
            }
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        internal static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        internal static void RequestAdministrator(string launchArgs = null)
        {
            if (IsAdministrator() == false) 
            {
                System.AppDomain.CurrentDomain.SetPrincipalPolicy(PrincipalPolicy.WindowsPrincipal);
                ProcessStartInfo startInfo = new ProcessStartInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, launchArgs);
                startInfo.UseShellExecute = true;
                startInfo.Verb = "runas";
                System.Diagnostics.Process.Start(startInfo);
                Environment.Exit(0);
            }
        }
    }
}