using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace ScopeCLI.Services.Interfaces
{
    public interface IDownloadService
    {
        Task DownloadFileWithProgressAsync(string url, string destinationPath, ProgressTask? task = null, CancellationToken cancellationToken = default);
        Task<string> DownloadStringAsync(string url, CancellationToken cancellationToken = default);
    }
}
