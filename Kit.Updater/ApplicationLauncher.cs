using Shared;
using System.Diagnostics;
using System.Text;

namespace Kit.Updater;

internal sealed class ApplicationLauncher
{
    private readonly UpdaterConfiguration   _configuration;
    private readonly InstallationStateStore _installationState;

    public ApplicationLauncher(UpdaterConfiguration configuration, InstallationStateStore installationState)
    {
        _configuration     = configuration;
        _installationState = installationState;
    }

    public bool IsApplicationRunning()
    {
        var processName = _installationState.ResolveProcessName();
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        Process[]? processes = null;
        try
        {
            processes = Process.GetProcessesByName(processName);
            return processes.Any(process => process.Id != Process.GetCurrentProcess().Id);
        }
        finally
        {
            if (processes != null)
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }
    }

    public string GetApplicationProcessName() => _installationState.ResolveProcessName();

    public void Launch(LocalApplicationInstallation installation)
    {
        if (!File.Exists(installation.ExecutablePath))
        {
            throw new FileNotFoundException("The configured application executable was not found.", installation.ExecutablePath);
        }

        _installationState.PersistCurrentVersion(installation.Version.NormalizedValue);

        var arguments = BuildLaunchArguments();
        var startInfo = new ProcessStartInfo
        {
            FileName         = installation.ExecutablePath,
            WorkingDirectory = installation.DirectoryPath,
            UseShellExecute  = true,
            Arguments        = arguments
        };

        Process.Start(startInfo);
    }

    public async Task<bool> LaunchAndVerifyAsync(LocalApplicationInstallation  installation,
                                                 LocalApplicationInstallation? previousInstallation,
                                                 CancellationToken              ct)
    {
        var timeoutSeconds = _configuration.Installation.LaunchHealthTimeoutSeconds;
        if (timeoutSeconds <= 0)
        {
            Launch(installation);
            return true;
        }

        if (!File.Exists(installation.ExecutablePath))
        {
            throw new FileNotFoundException("The configured application executable was not found.", installation.ExecutablePath);
        }

        var previousVersion = previousInstallation?.Version.NormalizedValue;
        _installationState.BeginActivation(installation.Version.NormalizedValue, previousVersion);

        try
        {
            using (var process = Process.Start(new ProcessStartInfo
                   {
                       FileName         = installation.ExecutablePath,
                       WorkingDirectory = installation.DirectoryPath,
                       UseShellExecute  = true,
                       Arguments        = BuildLaunchArguments()
                   }) ?? throw new InvalidOperationException("Failed to launch the updated application."))
            {
                await Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), ct).ConfigureAwait(false);
                if (process.HasExited)
                {
                    _installationState.RollbackActivation(previousVersion);
                    DiagnosticLog.Warning("activation.health_check_failed",
                        new KeyValuePair<string, string?>("version", installation.Version.NormalizedValue),
                        new KeyValuePair<string, string?>("exitCode", process.ExitCode.ToString()));
                    return false;
                }
            }

            _installationState.CommitActivation();
            DiagnosticLog.Info("activation.health_check_passed",
                new KeyValuePair<string, string?>("version", installation.Version.NormalizedValue));
            return true;
        }
        catch
        {
            _installationState.RollbackActivation(previousVersion);
            throw;
        }
    }

    public void LaunchUpdaterInstaller(string installerPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName        = installerPath,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }

    private string BuildLaunchArguments()
    {
        var inheritedArguments = Environment.GetCommandLineArgs().Skip(1).Select(QuoteArgument);
        var configuredArguments = string.IsNullOrWhiteSpace(_configuration.LaunchArguments)
            ? string.Empty
            : _configuration.LaunchArguments.Trim();

        var combined = string.Join(" ", inheritedArguments.Where(argument => argument.Length > 0));
        if (string.IsNullOrWhiteSpace(configuredArguments))
        {
            return combined;
        }

        if (string.IsNullOrWhiteSpace(combined))
        {
            return configuredArguments;
        }

        return configuredArguments + " " + combined;
    }

    internal static string QuoteArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return "\"\"";
        }

        if (argument.IndexOfAny([' ', '\t', '"']) < 0)
        {
            return argument;
        }

        var builder          = new StringBuilder(argument.Length + 2);
        var backslashCount   = 0;
        builder.Append('"');

        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', backslashCount * 2 + 1);
                builder.Append('"');
            }
            else
            {
                builder.Append('\\', backslashCount);
                builder.Append(character);
            }

            backslashCount = 0;
        }

        builder.Append('\\', backslashCount * 2);
        builder.Append('"');
        return builder.ToString();
    }
}
