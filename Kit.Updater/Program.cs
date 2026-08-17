using Shared;
using Kit.Updater.Forms;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace Kit.Updater;

internal static class Program
{
    // ReSharper disable InconsistentNaming
    private const int SW_RESTORE = 9;
    private const int SW_SHOW    = 5;
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

    [STAThread]
    private static void Main()
    {
        DiagnosticLog.Initialize("updater");
        DiagnosticLog.Info("updater.start",
            new KeyValuePair<string, string?>("version", typeof(Program).Assembly.GetName().Version?.ToString()));

        string  mutexName   = "Global\\KitUpdater-Unstamped";
        string? windowTitle = null;

        try
        {
            var executablePath    = Assembly.GetExecutingAssembly().Location;
            var configurationJson = StampPayload.ReadConfigurationJson(executablePath);
            var serializer        = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var configuration     = serializer.Deserialize<UpdaterConfiguration>(configurationJson);
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
        catch
        {
            // Fall back to defaults if reading/parsing configuration fails
        }

        using (new Mutex(true, mutexName, out var createdNew))
        {
            if (!createdNew)
            {
                DiagnosticLog.Info("updater.duplicate_instance");
                RestoreAndFocusExistingInstance(windowTitle);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
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
