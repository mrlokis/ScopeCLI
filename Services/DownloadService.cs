using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Spectre.Console;

namespace ScopeCLI.Services
{
    public class DownloadService
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public async Task DownloadFileWithProgressAsync(string url, string destinationPath, ProgressTask task, CancellationToken cancellationToken = default)
        {
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            task.MaxValue(totalBytes > 0 ? totalBytes : 0);

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;
                if (totalBytes > 0)
                    task.Increment(bytesRead);
            }
        }
    }
}