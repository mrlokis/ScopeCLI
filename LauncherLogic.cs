using System.Diagnostics;
using System.Threading.Tasks;
using ScopeCLI.Models;
using ScopeCLI.Services;

namespace ScopeCLI
{
    internal static class LauncherLogic
    {
        private static readonly LauncherService LauncherService = new();

        internal static async Task Run(string nickname, string version, int maxRamMB)
        {
            var profile = new Profile
            {
                Name = "DirectLaunch",
                Nickname = nickname,
                GameVersion = version,
                ModLoader = DeduceModLoader(version),
                MaxRamMb = maxRamMB
            };

            var process = await LauncherService.LaunchAsync(profile, maxRamMB);
            await Task.Yield();
        }

        private static string DeduceModLoader(string version)
        {
            if (version.Contains("neoforge", System.StringComparison.OrdinalIgnoreCase))
                return "NeoForge";
            if (version.Contains("forge", System.StringComparison.OrdinalIgnoreCase))
                return "Forge";
            if (version.Contains("fabric", System.StringComparison.OrdinalIgnoreCase))
                return "Fabric";
            return "Vanilla";
        }
    }
}
