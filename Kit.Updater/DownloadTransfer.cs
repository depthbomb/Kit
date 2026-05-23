namespace Kit.Updater;

internal static class DownloadTransfer
{
    public static async Task CopyToFileAsync(Stream               source,
                                             string               targetPath,
                                             long?                contentLength,
                                             CancellationToken    ct,
                                             Action<long, long?>? onProgress = null)
    {
        using (var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var  buffer    = new byte[81920];
            long totalRead = 0;
            int  bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                totalRead += bytesRead;
                onProgress?.Invoke(totalRead, contentLength);
            }
        }
    }
}
