using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ScopeCLI.Models;

namespace ScopeCLI.Services.Interfaces
{
    public record ModSyncResult(int DownloadedCount, int DeletedCount, int AlreadyPresentCount, List<string> Errors);

    public interface IModSynchronizer
    {
        Task<ModSyncResult> SynchronizeAsync(List<ModEntry> requiredMods, string modsDir, CancellationToken cancellationToken = default);
        Task<int> ClearModsFolderAsync(string modsDir, CancellationToken cancellationToken = default);
        Task<List<ModEntry>> ParseModListAsync(string pathOrUrl, CancellationToken cancellationToken = default);
    }
}
