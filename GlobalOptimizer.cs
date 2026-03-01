using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;  // требуется добавить ссылку на System.Management

namespace ScopeLauncher
{
    public class GlobalOptimizer
    {
        public struct AdminLaunchSettings
        {
            public string accountNickName;
            public string gameVersion;
            public string modLoader;
        }

        public static List<ProcessInfo> GetTopMemoryProcesses(int count = 10, bool excludeSystem = true)
        {
            var processList = new List<ProcessInfo>();

            try
            {
                Process[] processes = Process.GetProcesses();

                foreach (Process process in processes)
                {
                    try
                    {
                        if (excludeSystem && IsSystemProcess(process))
                            continue;
                        if (IsSocialProcess(process))
                            continue;

                        long memoryUsage = process.WorkingSet64;
                        if (memoryUsage == 0)
                            continue;

                        processList.Add(new ProcessInfo
                        {
                            Name = process.ProcessName,
                            MemoryBytes = memoryUsage,
                            MemoryMB = memoryUsage / (1024.0 * 1024.0),
                            Id = process.Id,
                            IsSocial = IsSocialProcess(process)
                        });
                    }
                    catch
                    {
                    }
                }

                processList = processList
                    .OrderByDescending(p => p.MemoryBytes)
                    .Take(count)
                    .ToList();
            }
            catch
            {
            }

            return processList;
        }

        public static bool KillProcessSafely(int processId, bool killChildrenForNonSocial = true)
        {
            try
            {
                Process proc = Process.GetProcessById(processId);
                bool isSocial = IsSocialProcess(proc);

                if (isSocial)
                {
                    proc.Kill();
                    proc.WaitForExit(5000);
                    return true;
                }
                else
                {
                    if (killChildrenForNonSocial)
                    {
                        KillProcessTree(processId);
                    }
                    else
                    {
                        proc.Kill();
                        proc.WaitForExit(5000);
                    }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void KillProcessTree(int parentId)
        {
            try
            {
                var childIds = GetChildProcessIds(parentId);
                foreach (int childId in childIds)
                {
                    KillProcessTree(childId);
                }

                // Убиваем сам процесс
                Process proc = Process.GetProcessById(parentId);
                proc.Kill();
                proc.WaitForExit(1000);
            }
            catch
            {

            }
        }

        private static List<int> GetChildProcessIds(int parentId)
        {
            var children = new List<int>();
            try
            {
                string query = $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {parentId}";
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        children.Add(Convert.ToInt32(obj["ProcessId"]));
                    }
                }
            }
            catch
            {

            }
            return children;
        }

        public class ProcessInfo
        {
            public string Name { get; set; }
            public long MemoryBytes { get; set; }
            public double MemoryMB { get; set; }
            public int Id { get; set; }
            public bool IsSocial { get; set; } 
        }

        private static bool IsSystemProcess(Process process)
        {
            try
            {
                string processName = process.ProcessName.ToLower();
                string[] systemProcesses = {
                    "system", "idle", "smss", "csrss", "wininit", "services",
                    "lsass", "svchost", "winlogon", "explorer", "spoolsv",
                    "taskhost", "taskhostw", "dwm", "conhost", "sihost",
                    "fontdrvhost", "runtimebroker", "searchui", "startmenuexperiencehost",
                    "shellexperiencehost", "systemsettings", "applicationframehost",
                    "windowsinternal", "securityhealth", "wmi", "wisptis",
                    "tabtip", "ctfmon", "userinit", "dllhost", "wmiprvse"
                };

                if (systemProcesses.Contains(processName))
                    return true;

                try
                {
                    string path = process.MainModule?.FileName?.ToLower() ?? "";
                    if (path.Contains("system32") || path.Contains("syswow64") ||
                        path.Contains("windows\\system") || path.Contains("microsoft\\windows"))
                    {
                        return true;
                    }
                }
                catch
                {

                }
            }
            catch
            {
            }

            return false;
        }


        private static bool IsSocialProcess(Process process)
        {
            try
            {
                string processName = process.ProcessName.ToLower();
                string[] socialProcesses = {
                    "discord", "telegram", "whatsapp", "vk", "vkontakte", "skype",
                    "viber", "slack", "zoom", "teams", "microsoftteams", "icq",
                    "pidgin", "trillian", "facebook", "messenger", "line", "wechat",
                    "signal", "element", "riot", "wire", "threema", "instagram"
                };

                if (socialProcesses.Contains(processName))
                    return true;
            }
            catch
            {

            }

            return false;
        }
    }
}