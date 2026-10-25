using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace ScopeCLI.Services.Interfaces
{
    public interface IArchiveService
    {
        Task CreateConfigArchiveAsync(string profileId, string sourceMinecraftDir, ProgressTask? task = null, CancellationToken cancellationToken = default);
        Task ExtractConfigArchiveAsync(string profileId, string targetMinecraftDir, ProgressTask? task = null, CancellationToken cancellationToken = default);
        Task CreateModsArchiveAsync(string profileId, string sourceMinecraftDir, ProgressTask? task = null, CancellationToken cancellationToken = default);
        Task ExtractModsArchiveAsync(string profileId, string targetMinecraftDir, ProgressTask? task = null, CancellationToken cancellationToken = default);
    }
}
