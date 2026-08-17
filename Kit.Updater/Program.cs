using Kit.Updater.Forms;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Kit.Updater;

internal static class Program
{
    // ReSharper disable InconsistentNaming
    private const int SW_RESTORE = 9;
    private const int SW_SHOW    = 5;
    private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;
    // ReSharper enable InconsistentNaming

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);

    [STAThread]
    private static int Main(string[] args)
    {
        DiagnosticLog.Initialize("updater");
        DiagnosticLog.Info("updater.start",
            new KeyValuePair<string, string?>("version", typeof(Program).Assembly.GetName().Version?.ToString()));

        if (!UpdaterCommandLineOptions.TryParse(args, out var options, out var parseError))
        {
            AttachParentConsole();
            Console.Error.WriteLine(parseError);
            return UpdaterExitCode.InvalidArguments;
        }

        if (options.Mode != UpdaterCommandMode.UserInterface && !options.Silent)
        {
            AttachParentConsole();
        }

        string  mutexName   = "Global\\KitUpdater-Unstamped";
        string? windowTitle = null;
        Shared.UpdaterConfiguration? configuration = null;

        try
        {
            var executablePath = Assembly.GetExecutingAssembly().Location;
            configuration = UpdaterConfigurationLoader.Load(executablePath);
            if (!string.IsNullOrWhiteSpace(options.Channel))
            {
                configuration.UpdateSource.Channel = options.Channel!;
            }
            if (configuration != null)
            {
                var appName = configuration.ApplicationName;
                if (!string.IsNullOrWhiteSpace(appName))
                {
                    DiagnosticLog.Initialize(appName);
                    mutexName = "Global\\KitUpdater-" + appName.Replace("\\", "_");

                    var resolver = new UiTextResolver();
                    windowTitle = resolver.Resolve(configuration, UiTextKey.WindowTitle, "{ApplicationName} Bootstrapper");
                }
            }
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("configuration.load_failed", exception);
            if (options.Mode != UpdaterCommandMode.UserInterface)
            {
                if (!options.Silent)
                {
                    Console.Error.WriteLine(exception.Message);
                }

                return UpdaterExitCode.Failure;
            }

            // Fall back to defaults if reading/parsing configuration fails
        }

        using (new Mutex(true, mutexName, out var createdNew))
        {
            if (!createdNew)
            {
                DiagnosticLog.Info("updater.duplicate_instance");
                RestoreAndFocusExistingInstance(windowTitle);
                return options.Mode == UpdaterCommandMode.UserInterface
                    ? UpdaterExitCode.Success
                    : UpdaterExitCode.Failure;
            }

            if (options.Mode != UpdaterCommandMode.UserInterface)
            {
                try
                {
                    return new HeadlessUpdater().RunAsync(configuration!, options, CancellationToken.None).GetAwaiter().GetResult();
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Error("headless.failed", exception);
                    if (!options.Silent)
                    {
                        Console.Error.WriteLine(exception.Message);
                    }

                    return UpdaterExitCode.Failure;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return UpdaterExitCode.Success;
        }
    }

    private static void AttachParentConsole()
    {
        try
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError(), Encoding.UTF8) { AutoFlush = true });
        }
        catch
        {
            // Redirected standard streams may already be available.
        }
    }

    private static void RestoreAndFocusExistingInstance(string? windowTitle)
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var targetHwnd     = IntPtr.Zero;

            // 1. Try to find the process by name and executable path
            var processes = Process.GetProcessesByName(currentProcess.ProcessName);
            foreach (var process in processes)
            {
                if (process.Id == currentProcess.Id)
                {
                    continue;
                }

                try
                {
                    if (string.Equals(process.MainModule?.FileName, currentProcess.MainModule?.FileName, StringComparison.OrdinalIgnoreCase))
                    {
                        var hwnd = process.MainWindowHandle;
                        if (hwnd != IntPtr.Zero)
                        {
                            targetHwnd = hwnd;
                            break;
                        }
                    }
                }
                catch
                {
                    /* Ignored */
                }
            }

            // 2. Fall back to FindWindow using expected window title if targetHwnd is still zero
            if (targetHwnd == IntPtr.Zero && !string.IsNullOrEmpty(windowTitle))
            {
                targetHwnd = FindWindow(null, windowTitle);
            }

            if (targetHwnd != IntPtr.Zero)
            {
                ShowWindow(targetHwnd, IsIconic(targetHwnd) ? SW_RESTORE : SW_SHOW);

                SetForegroundWindow(targetHwnd);
            }
        }
        catch
        {
            /* Ignored */
        }
    }
}
