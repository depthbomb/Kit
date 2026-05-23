using System.Diagnostics;
using System.IO.Compression;

namespace Kit.Updater;

internal sealed class ArchiveExtractor
{
    private readonly string _baseDirectory;

    public ArchiveExtractor(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }

    public static string GetArchiveExtension(string downloadUrl)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri))
        {
            return ".zip";
        }

        var extension = Path.GetExtension(uri.AbsolutePath);

        return string.IsNullOrWhiteSpace(extension) ? ".zip" : extension;
    }

    public async Task ExtractAsync(string archivePath, string archiveExtension, string destinationDirectory, CancellationToken ct)
    {
        var sevenZipPath = Path.Combine(_baseDirectory, "bin", "7za.exe");
        var hasSevenZip  = File.Exists(sevenZipPath);
        if (hasSevenZip)
        {
            await ExtractWithSevenZipAsync(sevenZipPath, archivePath, destinationDirectory, ct).ConfigureAwait(false);
            return;
        }

        if (string.Equals(archiveExtension, ".7z", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The downloaded update is a .7z archive, but no extractor was found. Ship bin\\7za.exe next to the updater to enable .7z extraction.");
        }

        if (!string.Equals(archiveExtension, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unsupported update archive format: " + archiveExtension + ". Ship bin\\7za.exe next to the updater to enable external extraction.");
        }

        ZipFile.ExtractToDirectory(archivePath, destinationDirectory);
    }

    private static async Task ExtractWithSevenZipAsync(string sevenZipPath, string archivePath, string destinationDirectory, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName               = sevenZipPath,
            Arguments              = "x -y -o\"" + destinationDirectory + "\" \"" + archivePath + "\"",
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true
        };

        using (var process = new Process())
        {
            process.StartInfo           = startInfo;
            process.EnableRaisingEvents = true;
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask  = process.StandardError.ReadToEndAsync();
            using (ct.Register(() =>
                   {
                       try
                       {
                           if (!process.HasExited)
                           {
                               process.Kill();
                           }
                       }
                       catch
                       {
                           /*Ignored*/
                       }
                   }))
            {
                await WaitForExitAsync(process, ct).ConfigureAwait(false);
            }

            var output = await outputTask.ConfigureAwait(false);
            var error  = await errorTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var details = string.IsNullOrWhiteSpace(error) ? output : error;
                throw new InvalidOperationException("7za.exe failed to extract the update archive." + (string.IsNullOrWhiteSpace(details) ? string.Empty : Environment.NewLine + details.Trim()));
            }
        }
    }

    private static Task WaitForExitAsync(Process process, CancellationToken ct)
    {
        var taskCompletionSource = new TaskCompletionSource<bool>();

        EventHandler handler = delegate
        {
            taskCompletionSource.TrySetResult(true);
        };

        process.Exited += handler;

        if (process.HasExited)
        {
            process.Exited -= handler;
            return Task.CompletedTask;
        }

        ct.Register(() => taskCompletionSource.TrySetCanceled(ct));

        return taskCompletionSource.Task.ContinueWith(task =>
        {
            process.Exited -= handler;
            return task;
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap();
    }
}
