using Microsoft.Win32;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Kit.Setup;

internal static class SetupWorkflow
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr      hWnd,
        uint        msg,
        UIntPtr     wParam,
        string      lParam,
        uint        fuFlags,
        uint        uTimeout,
        out UIntPtr lpdwResult);

    // ReSharper disable InconsistentNaming
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;
    // ReSharper enable InconsistentNaming

    public static void InstallSync(
        SetupConfiguration config,
        string             installDir,
        bool               addToPath,
        bool               desktopShortcut,
        bool               startMenuShortcut,
        IProgress<string>  progress)
    {
        // 1. Run Pre-Install Command if configured
        if (!string.IsNullOrWhiteSpace(config.PreInstallCommand))
        {
            progress.Report("Running pre-install command...");
            ExecuteCommand(config.PreInstallCommand, config.PreInstallArguments, installDir);
        }

        // 2. Create target directory
        progress.Report("Creating installation folder...");
        Directory.CreateDirectory(installDir);

        // 3. Extract Zip package
        if (!string.IsNullOrWhiteSpace(config.PackageZipBase64))
        {
            progress.Report("Extracting application files...");
            var tempZip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                File.WriteAllBytes(tempZip, Convert.FromBase64String(config.PackageZipBase64));

                // Unzip into target directory (overwriting existing files)
                using (var archive = ZipFile.OpenRead(tempZip))
                {
                    foreach (var entry in archive.Entries)
                    {
                        var targetPath = Path.GetFullPath(Path.Combine(installDir, entry.FullName));

                        // Prevent directory traversal attacks
                        if (!targetPath.StartsWith(Path.GetFullPath(installDir), StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || entry.FullName.EndsWith("\\", StringComparison.Ordinal))
                        {
                            Directory.CreateDirectory(targetPath);
                            continue;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                        entry.ExtractToFile(targetPath, true);
                    }
                }
            }
            finally
            {
                TryDeleteFile(tempZip);
            }
        }

        // 4. Configure PATH
        if (addToPath && !string.IsNullOrWhiteSpace(config.AddToPath))
        {
            progress.Report("Configuring environment PATH...");
            var targetPath = Path.GetFullPath(Path.Combine(installDir, config.AddToPath));
            try
            {
                var currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
                var pathList = (currentPath ?? string.Empty)
                               .Split([';'], StringSplitOptions.RemoveEmptyEntries)
                               .Select(p => p.Trim())
                               .ToList();

                if (!pathList.Any(p => string.Equals(p, targetPath, StringComparison.OrdinalIgnoreCase)))
                {
                    currentPath = string.IsNullOrEmpty(currentPath) ? targetPath : currentPath.TrimEnd(';') + ";" + targetPath;
                    Environment.SetEnvironmentVariable("PATH", currentPath, EnvironmentVariableTarget.User);
                    BroadcastSettingChange();
                }
            }
            catch (Exception ex)
            {
                progress.Report("Warning: Could not write to PATH registry: " + ex.Message);
            }
        }

        // 5. Create Shortcuts
        progress.Report("Creating shortcuts...");
        var targetExe = Path.Combine(installDir, config.LaunchExecutable);
        if (desktopShortcut)
        {
            var desktopDir   = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcutPath = Path.Combine(desktopDir, config.ApplicationName + ".lnk");
            CreateShortcut(shortcutPath, targetExe, config.LaunchArguments, $"Launcher for {config.ApplicationName}");
        }

        if (startMenuShortcut)
        {
            var startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), config.OrganizationName);

            Directory.CreateDirectory(startMenuDir);

            var shortcutPath = Path.Combine(startMenuDir, config.ApplicationName + ".lnk");

            CreateShortcut(shortcutPath, targetExe, config.LaunchArguments, $"Launcher for {config.ApplicationName}");
        }

        // 6. Register Uninstaller
        progress.Report("Registering uninstaller...");
        var runningExe       = Assembly.GetExecutingAssembly().Location;
        var uninstallExePath = Path.Combine(installDir, "uninstall.exe");
        try
        {
            File.Copy(runningExe, uninstallExePath, true);

            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + config.ApplicationName))
            {
                if (key != null)
                {
                    key.SetValue("DisplayName", config.ApplicationName);
                    key.SetValue("DisplayVersion", "1.0.0");
                    key.SetValue("Publisher", config.OrganizationName);
                    key.SetValue("UninstallString", $"\"{uninstallExePath}\" --uninstall");
                    key.SetValue("InstallLocation", installDir);
                    key.SetValue("DisplayIcon", targetExe);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
        }
        catch (Exception ex)
        {
            progress.Report("Warning: Could not register uninstaller: " + ex.Message);
        }

        // 7. Run Post-Install Command
        if (!string.IsNullOrWhiteSpace(config.PostInstallCommand))
        {
            progress.Report("Running post-install command...");
            var formattedArgs = config.PostInstallArguments.Replace("{AppDir}", installDir).Replace("{appdir}", installDir);
            ExecuteCommand(config.PostInstallCommand, formattedArgs, installDir);
        }

        progress.Report("Installation completed successfully!");
    }

    public static void UninstallSync(SetupConfiguration config,
                                     string             installDir,
                                     IProgress<string>  progress)
    {
        // 1. Remove Registry entry
        progress.Report("Removing uninstaller registration...");
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + config.ApplicationName, false);
        }
        catch (Exception ex)
        {
            progress.Report("Warning: Could not remove registry key: " + ex.Message);
        }

        // 2. Remove shortcuts
        progress.Report("Removing shortcuts...");
        var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        TryDeleteFile(Path.Combine(desktopDir, config.ApplicationName + ".lnk"));

        var startMenuOrgDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            config.OrganizationName);
        TryDeleteFile(Path.Combine(startMenuOrgDir, config.ApplicationName + ".lnk"));

        try
        {
            if (Directory.Exists(startMenuOrgDir) && !Directory.EnumerateFileSystemEntries(startMenuOrgDir).Any())
            {
                Directory.Delete(startMenuOrgDir);
            }
        }
        catch
        {
            /* Ignored */
        }

        // 3. Remove PATH modification
        if (!string.IsNullOrWhiteSpace(config.AddToPath))
        {
            progress.Report("Removing PATH modifications...");
            var targetPath = Path.GetFullPath(Path.Combine(installDir, config.AddToPath));
            try
            {
                var currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
                if (!string.IsNullOrEmpty(currentPath))
                {
                    var pathList = currentPath.Split([';'], StringSplitOptions.RemoveEmptyEntries)
                                              .Select(p => p.Trim())
                                              .Where(p => !string.Equals(p, targetPath, StringComparison.OrdinalIgnoreCase))
                                              .ToList();

                    var newPath = string.Join(";", pathList);
                    Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);
                    BroadcastSettingChange();
                }
            }
            catch (Exception ex)
            {
                progress.Report("Warning: Could not update PATH environment: " + ex.Message);
            }
        }

        // 4. Delete all files except uninstall.exe (which is currently running)
        progress.Report("Cleaning installation directory...");
        if (Directory.Exists(installDir))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(installDir))
                {
                    if (string.Equals(Path.GetFileName(file), "uninstall.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Can't delete self while running
                    }

                    TryDeleteFile(file);
                }

                foreach (var dir in Directory.EnumerateDirectories(installDir))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch
                    {
                        /* Ignored */
                    }
                }
            }
            catch (Exception ex)
            {
                progress.Report("Warning during directory cleanup: " + ex.Message);
            }
        }

        // 5. Spawn a detached self-delete process via cmd.exe that:
        //    - Waits for a short delay (giving this process time to exit)
        //    - Deletes the uninstaller exe
        //    - Removes the install dir and parent org dir if empty
        progress.Report("Scheduling final cleanup...");

        var runningExe = Assembly.GetExecutingAssembly().Location;
        var parentDir  = Path.GetDirectoryName(installDir);

        // Build a batch script that waits, then cleans up
        var cleanupParts = new System.Text.StringBuilder();
        cleanupParts.Append("/C ping -n 3 127.0.0.1 > nul");
        cleanupParts.Append(" & del /F /Q \"" + runningExe + "\"");
        cleanupParts.Append(" & rmdir /Q \""  + installDir + "\"");
        if (parentDir != null)
        {
            cleanupParts.Append(" & rmdir /Q \"" + parentDir + "\"");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName        = "cmd.exe",
            Arguments       = cleanupParts.ToString(),
            CreateNoWindow  = true,
            UseShellExecute = true, // UseShellExecute=true so it fully detaches
            WindowStyle     = ProcessWindowStyle.Hidden
        };

        try
        {
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            progress.Report("Warning: Self-clean scheduling failed: " + ex.Message);
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string description)
    {
        try
        {
            // Escape single quotes for PowerShell
            var escapedShortcut = shortcutPath.Replace("'", "''");
            var escapedTarget   = targetPath.Replace("'", "''");
            var escapedArgs     = arguments.Replace("'", "''");
            var escapedDesc     = description.Replace("'", "''");

            var script = $"$WshShell = New-Object -ComObject WScript.Shell; "           +
                         $"$Shortcut = $WshShell.CreateShortcut('{escapedShortcut}'); " +
                         $"$Shortcut.TargetPath = '{escapedTarget}'; "                  +
                         $"$Shortcut.Arguments = '{escapedArgs}'; "                     +
                         $"$Shortcut.Description = '{escapedDesc}'; "                   +
                         $"$Shortcut.Save();";

            var startInfo = new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                CreateNoWindow  = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit(5000);
        }
        catch
        {
            /* Ignored */
        }
    }

    private static void ExecuteCommand(string command, string arguments, string workingDir)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName         = command,
                Arguments        = arguments,
                WorkingDirectory = workingDir,
                CreateNoWindow   = true,
                UseShellExecute  = false
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit(15000);
        }
        catch
        {
            /* Ignored */
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            /* Ignored */
        }
    }

    private static void BroadcastSettingChange()
    {
        try
        {
            SendMessageTimeout(
                (IntPtr)0xffff, // HWND_BROADCAST
                WM_SETTINGCHANGE,
                UIntPtr.Zero,
                "Environment",
                SMTO_ABORTIFHUNG,
                2000,
                out _);
        }
        catch
        {
            /* Ignored */
        }
    }
}
