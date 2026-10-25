using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ScopeCLI.Services.Interfaces;
using Spectre.Console;

namespace ScopeCLI.Services
{
    public class DownloadService : IDownloadService
    {
        private static readonly HttpClient HttpClient = CreateConfiguredHttpClient();

        private static HttpClient CreateConfiguredHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                AutomaticDecompression = System.Net.DecompressionMethods.All
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(3)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd("ScopeCLI/0.00.1 (Windows NT; +https://github.com/mrlokis/ScopeCLI)");
            return client;
        }

        public async Task<string> DownloadStringAsync(string url, CancellationToken cancellationToken = default)
        {
            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseContentRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }

        public async Task DownloadFileWithProgressAsync(string url, string destinationPath, ProgressTask? task = null, CancellationToken cancellationToken = default)
        {
            string? dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string tempFilePath = destinationPath + ".downloading";
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }

            try
            {
                using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                if (task != null)
                {
                    if (totalBytes > 0)
                    {
                        task.MaxValue = totalBytes;
                    }
                    else
                    {
                        task.IsIndeterminate = true;
                    }
                }

                await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                await using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
                {
                    var buffer = new byte[65536];
                    int bytesRead;
                    long pendingIncrement = 0;

                    while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        pendingIncrement += bytesRead;

                        if (task != null && totalBytes > 0 && pendingIncrement >= 65536)
                        {
                            task.Increment(pendingIncrement);
                            pendingIncrement = 0;
                        }
                    }

                    if (task != null && totalBytes > 0 && pendingIncrement > 0)
                    {
                        task.Increment(pendingIncrement);
                    }
                }

                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }

                File.Move(tempFilePath, destinationPath);

                if (task != null)
                {
                    if (task.IsIndeterminate)
                    {
                        task.IsIndeterminate = false;
                        task.MaxValue = 1;
                        task.Value = 1;
                    }
                    else
                    {
                        task.Value = task.MaxValue;
                    }
                }
            }
            catch
            {
                if (File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); } catch { }
                }
                throw;
            }
        }
    }
}
