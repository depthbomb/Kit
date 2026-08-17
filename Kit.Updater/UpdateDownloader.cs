using System.Net.Http;
using System.Security.Cryptography;

namespace Kit.Updater;

internal sealed class UpdateDownloader
{
    public async Task DownloadFileAsync(string                           url,
                                        string                           targetPath,
                                        string                           version,
                                        IProgress<InstallationProgress>? progress,
                                        CancellationToken                ct)
    {
        DiagnosticLog.Info("download.started",
            new KeyValuePair<string, string?>("host", Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null),
            new KeyValuePair<string, string?>("version", version));
        using (var response = await UpdaterHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;

            using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                await DownloadTransfer.CopyToFileAsync(
                                          responseStream,
                                          targetPath,
                                          contentLength,
                                          ct,
                                          (totalRead, total) => progress?.Report(new InstallationProgress(InstallationPhase.Downloading, version, totalRead, total)))
                                      .ConfigureAwait(false);
            }
        }

        DiagnosticLog.Info("download.completed",
            new KeyValuePair<string, string?>("version", version),
            new KeyValuePair<string, string?>("bytes", new FileInfo(targetPath).Length.ToString()));
    }

    public async Task VerifyIntegrityAsync(AvailableUpdate   update,
                                           string            archivePath,
                                           bool              requireIntegrityVerification,
                                           CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(update.Sha256))
        {
            if (requireIntegrityVerification)
            {
                throw new InvalidOperationException("Integrity verification is required, but the update source did not provide a SHA-256 checksum.");
            }

            return;
        }

        string actualHash;
        using (var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var algorithm = SHA256.Create())
        {
            var buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
            {
                algorithm.TransformBlock(buffer, 0, bytesRead, buffer, 0);
            }

            algorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var hashBytes = algorithm.Hash!;
            actualHash = BitConverter.ToString(hashBytes).Replace("-", string.Empty);
        }

        if (!string.Equals(UpdateSourceParsing.NormalizeSha256(actualHash), UpdateSourceParsing.NormalizeSha256(update.Sha256), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The downloaded update failed integrity verification. The SHA-256 checksum did not match.");
        }
    }
}
