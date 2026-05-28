using Kit.Setup.Forms;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace Kit.Setup;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var lowerArgs   = args.Select(a => a.ToLowerInvariant()).ToList();
        var isUninstall = lowerArgs.Contains("--uninstall") || lowerArgs.Contains("-u");
        var isSilent    = lowerArgs.Contains("--silent")    || lowerArgs.Contains("-s");

        SetupConfiguration config;

        bool isDemoMode = false;

        try
        {
            var executablePath    = Assembly.GetExecutingAssembly().Location;
            var configurationJson = StampPayload.ReadConfigurationJson(executablePath);
            var serializer = new JavaScriptSerializer
            {
                // Allow larger payloads
                MaxJsonLength = int.MaxValue
            };
            config = serializer.Deserialize<SetupConfiguration>(configurationJson)
                     ?? throw new InvalidOperationException("Decoded setup configuration is empty.");
        }
        catch (Exception ex)
        {
            // Fallback for development/testing when running unstamped Setup.exe
            if (isSilent)
            {
                Console.Error.WriteLine("Error: Setup failed to load: " + ex);
                return 1;
            }

            isDemoMode = true;
            config = new SetupConfiguration
            {
                ApplicationName         = "Demo Application",
                OrganizationName        = "Demo Org",
                InstallLocation         = "%LOCALAPPDATA%",
                ProcessName             = "DemoApp",
                AddToPath               = "bin",
                LaunchExecutable        = "DemoApp.exe",
                LaunchArguments         = "",
                CreateDesktopShortcut   = true,
                CreateStartMenuShortcut = true,
                PreInstallCommand       = "cmd.exe",
                PreInstallArguments     = "/c echo Pre-install running...",
                PostInstallCommand      = "cmd.exe",
                PostInstallArguments    = "/c echo Post-install running...",
                WelcomeText             = "Welcome to the Demo Application Setup Wizard.\n\nThis is a mock installer running because the executable was not stamped.",
                PackageZipBase64        = string.Empty
            };
        }

        if (isSilent)
        {
            return RunSilent(config, isUninstall);
        }

        using (var mainForm = new MainForm(config, isUninstall, isDemoMode))
        {
            Application.Run(mainForm);
        }

        return 0;
    }

    private static int RunSilent(SetupConfiguration config, bool isUninstall)
    {
        try
        {
            var baseLoc = config.InstallLocation;
            if (string.Equals(baseLoc, "%APPDATA%", StringComparison.OrdinalIgnoreCase))
            {
                baseLoc = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }
            else if (string.Equals(baseLoc, "%LOCALAPPDATA%", StringComparison.OrdinalIgnoreCase))
            {
                baseLoc = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            else
            {
                baseLoc = Environment.ExpandEnvironmentVariables(baseLoc);
            }

            var installDir = Path.Combine(baseLoc, config.OrganizationName, config.ApplicationName);

            if (isUninstall)
            {
                var progress = new SynchronousProgress<string>(msg => Console.WriteLine("[Uninstall] " + msg));
                SetupWorkflow.UninstallSync(config, installDir, progress);
            }
            else
            {
                // If there's a running process, should we wait or kill it?
                if (!string.IsNullOrWhiteSpace(config.ProcessName))
                {
                    var processes = Process.GetProcessesByName(config.ProcessName);
                    foreach (var p in processes)
                    {
                        try
                        {
                            p.Kill();
                            p.WaitForExit(5_000);
                        }
                        catch
                        {
                            /* Ignored */
                        }
                    }
                }

                SetupWorkflow.InstallSync(
                    config,
                    installDir,
                    !string.IsNullOrWhiteSpace(config.AddToPath),
                    config.CreateDesktopShortcut,
                    config.CreateStartMenuShortcut,
                    new SynchronousProgress<string>(msg => Console.WriteLine("[Install] " + msg)));
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Silent Setup Error: " + ex.Message);
            return 1;
        }
    }
}

internal sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> _callback;

    public SynchronousProgress(Action<T> callback)
    {
        _callback = callback;
    }

    public void Report(T value)
    {
        _callback(value);
    }
}
