using System.Net.Http;
using System.Security.Cryptography;

namespace Kit.Updater;

internal sealed class UpdateDownloader
{
    public async Task DownloadFileAsync(string url, string targetPath, string version, IProgress<InstallationProgress>? progress, CancellationToken ct)
    {
        using (var response = await UpdaterHttpClient.Shared.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;

            using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var  buffer    = new byte[81920];
                long totalRead = 0;
                int  bytesRead;
                while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                    totalRead += bytesRead;
                    progress?.Report(new InstallationProgress(InstallationPhase.Downloading, version, totalRead, contentLength));
                }
            }
        }
    }

    public async Task VerifyIntegrityAsync(AvailableUpdate update, string archivePath, bool requireIntegrityVerification, CancellationToken ct)
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
            var hashBytes = await Task.Run(() => algorithm.ComputeHash(stream), ct).ConfigureAwait(false);
            actualHash = BitConverter.ToString(hashBytes).Replace("-", string.Empty);
        }

        if (!string.Equals(UpdateSourceParsing.NormalizeSha256(actualHash), UpdateSourceParsing.NormalizeSha256(update.Sha256), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The downloaded update failed integrity verification. The SHA-256 checksum did not match.");
        }
    }
}
