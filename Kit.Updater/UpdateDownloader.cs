using System.Net.Http;
using System.Security.Cryptography;
using System.Net;

namespace Kit.Updater;

internal sealed class UpdateDownloader
{
    public async Task DownloadFileAsync(string                           url,
                                        string                           targetPath,
                                        string                           version,
                                        IProgress<InstallationProgress>? progress,
                                        CancellationToken                ct)
    {
        const int maximumTransferAttempts = 3;
        for (var attempt = 1; attempt <= maximumTransferAttempts; attempt++)
        {
            try
            {
                await DownloadFileAttemptAsync(url, targetPath, version, progress, ct).ConfigureAwait(false);
                return;
            }
            catch (Exception exception) when (attempt < maximumTransferAttempts
                                               && !ct.IsCancellationRequested
                                               && (exception is IOException || exception is HttpRequestException))
            {
                DiagnosticLog.Warning("download.retry",
                    new KeyValuePair<string, string?>("attempt", attempt.ToString()),
                    new KeyValuePair<string, string?>("reason", exception.GetType().Name));
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), ct).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("The download retry loop completed unexpectedly.");
    }

    private async Task DownloadFileAttemptAsync(string                           url,
                                                string                           targetPath,
                                                string                           version,
                                                IProgress<InstallationProgress>? progress,
                                                CancellationToken                ct)
    {
        DiagnosticLog.Info("download.started",
            new KeyValuePair<string, string?>("host", Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null),
            new KeyValuePair<string, string?>("version", version));
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? Path.GetTempPath());

        var etagPath        = targetPath + ".etag";
        var existingLength  = File.Exists(targetPath) ? new FileInfo(targetPath).Length : 0;
        var existingEtag    = File.Exists(etagPath) ? File.ReadAllText(etagPath).Trim() : null;
        using (var response = await UpdaterHttpClient.GetAsync(
                   url,
                   HttpCompletionOption.ResponseHeadersRead,
                   ct,
                   existingLength > 0 ? existingLength : null,
                   existingEtag).ConfigureAwait(false))
        {
            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                DeleteDownloadFiles(targetPath);
                throw new IOException("The server rejected the saved partial download range.");
            }

            response.EnsureSuccessStatusCode();

            var isPartial     = response.StatusCode == HttpStatusCode.PartialContent && existingLength > 0;
            var initialBytes  = isPartial ? existingLength : 0;
            var contentLength = response.Content.Headers.ContentRange?.Length
                                ?? (response.Content.Headers.ContentLength.HasValue
                                    ? response.Content.Headers.ContentLength.Value + initialBytes
                                    : (long?)null);

            var responseEtag = response.Headers.ETag?.ToString();
            if (!string.IsNullOrWhiteSpace(responseEtag))
            {
                File.WriteAllText(etagPath, responseEtag);
            }

            using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                await DownloadTransfer.CopyToFileAsync(
                                          responseStream,
                                          targetPath,
                                          contentLength,
                                          ct,
                                          (totalRead, total) => progress?.Report(new InstallationProgress(InstallationPhase.Downloading, version, totalRead, total)),
                                          isPartial,
                                          initialBytes)
                                      .ConfigureAwait(false);
            }
        }

        DiagnosticLog.Info("download.completed",
            new KeyValuePair<string, string?>("version", version),
            new KeyValuePair<string, string?>("bytes", new FileInfo(targetPath).Length.ToString()));
    }

    public static void DeleteDownloadFiles(string targetPath)
    {
        TryDelete(targetPath);
        TryDelete(targetPath + ".etag");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup; If-Range prevents appending to a changed remote object.
        }
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
