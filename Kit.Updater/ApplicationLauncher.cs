using Shared;
using System.Diagnostics;

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

    private static string QuoteArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return "\"\"";
        }

        return argument.IndexOfAny([' ', '\t', '"']) >= 0
            ? "\"" + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
            : argument;
    }
}
