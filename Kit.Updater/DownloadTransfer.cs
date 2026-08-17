namespace Kit.Updater;

internal static class DownloadTransfer
{
    public static async Task CopyToFileAsync(Stream               source,
                                             string               targetPath,
                                             long?                contentLength,
                                             CancellationToken    ct,
                                             Action<long, long?>? onProgress  = null,
                                             bool                  append      = false,
                                             long                  initialBytes = 0)
    {
        using (var fileStream = new FileStream(targetPath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var  buffer    = new byte[81920];
            long totalRead = initialBytes;
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
