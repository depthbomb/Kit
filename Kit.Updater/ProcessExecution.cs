using System.Diagnostics;

namespace Kit.Updater;

internal sealed class ProcessExecutionResult
{
    public ProcessExecutionResult(int exitCode, string standardOutput, string standardError)
    {
        ExitCode       = exitCode;
        StandardOutput = standardOutput;
        StandardError  = standardError;
    }

    public int ExitCode { get; }

    public string StandardOutput { get; }

    public string StandardError { get; }
}

internal static class ProcessExecution
{
    public static async Task<ProcessExecutionResult> RunAsync(ProcessStartInfo startInfo, CancellationToken ct)
    {
        using (var process = new Process())
        {
            process.StartInfo           = startInfo;
            process.EnableRaisingEvents = true;

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start process: " + startInfo.FileName);
            }

            var outputTask = startInfo.RedirectStandardOutput ? process.StandardOutput.ReadToEndAsync() : Task.FromResult(string.Empty);
            var errorTask  = startInfo.RedirectStandardError ? process.StandardError.ReadToEndAsync() : Task.FromResult(string.Empty);

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

            return new ProcessExecutionResult(process.ExitCode, output, error);
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
