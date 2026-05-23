using Shared;
using System.Diagnostics;

namespace Kit.Updater;

internal sealed class PostInstallRunner
{
    private readonly UpdaterConfiguration _configuration;
    private readonly string               _baseDirectory;

    public PostInstallRunner(UpdaterConfiguration configuration, string baseDirectory)
    {
        _configuration = configuration;
        _baseDirectory = baseDirectory;
    }

    public async Task RunAsync(string preparedDirectory, string version, CancellationToken cancellationToken)
    {
        var command = _configuration.Installation.PostInstallCommand.Trim();
        if (command.Length == 0)
        {
            return;
        }

        command = ReplaceTemplateTokens(command, version, preparedDirectory);

        var arguments = ReplaceTemplateTokens(_configuration.Installation.PostInstallArguments, version, preparedDirectory);

        ResolvePostInstallCommand(preparedDirectory, command, out var resolvedCommandPath, out var useCommandShell);

        var startInfo = new ProcessStartInfo
        {
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            WorkingDirectory       = preparedDirectory
        };

        if (useCommandShell)
        {
            startInfo.FileName  = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            startInfo.Arguments = "/c \"" + resolvedCommandPath + (string.IsNullOrWhiteSpace(arguments) ? string.Empty : " " + arguments) + "\"";
        }
        else
        {
            startInfo.FileName  = resolvedCommandPath;
            startInfo.Arguments = arguments;
        }

        using (var process = new Process())
        {
            process.StartInfo           = startInfo;
            process.EnableRaisingEvents = true;
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask  = process.StandardError.ReadToEndAsync();

            using (cancellationToken.Register(() =>
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
                await WaitForExitAsync(process, cancellationToken).ConfigureAwait(false);
            }

            var output = await outputTask.ConfigureAwait(false);
            var error  = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                var details = string.IsNullOrWhiteSpace(error) ? output : error;
                throw new InvalidOperationException("The post-install command failed." + (string.IsNullOrWhiteSpace(details) ? string.Empty : Environment.NewLine + details.Trim()));
            }
        }
    }

    private void ResolvePostInstallCommand(string preparedDirectory, string command, out string resolvedCommandPath, out bool useCommandShell)
    {
        useCommandShell     = false;
        resolvedCommandPath = command;

        if (!Path.IsPathRooted(resolvedCommandPath))
        {
            var appRelativePath = Path.Combine(preparedDirectory, resolvedCommandPath);
            if (File.Exists(appRelativePath))
            {
                resolvedCommandPath = appRelativePath;
            }
            else
            {
                var baseRelativePath = Path.Combine(_baseDirectory, resolvedCommandPath);
                if (File.Exists(baseRelativePath))
                {
                    resolvedCommandPath = baseRelativePath;
                }
            }
        }

        var extension = Path.GetExtension(resolvedCommandPath);
        if (
            string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase)
        )
        {
            useCommandShell = true;
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

    private static string ReplaceTemplateTokens(string? value, string version, string appDirectory)
        => (value ?? string.Empty)
           .Replace("{Version}", version)
           .Replace("{AppDirectory}", appDirectory)
           .Replace("{BaseDirectory}", AppDomain.CurrentDomain.BaseDirectory);
}
