using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ScopeCLI.Models;

namespace ScopeCLI.Services.Interfaces
{
    public interface ILauncherService
    {
        Task<Process> LaunchAsync(Profile profile, int? maxRamMb = null, string? serverAddress = null, CancellationToken cancellationToken = default);
        Task<string> EnsureInstalledAsync(string version, string modLoader, CancellationToken cancellationToken = default);
    }
}
