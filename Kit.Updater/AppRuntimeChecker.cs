using Microsoft.Win32;
using System.Net.Http;
using System.Diagnostics;

namespace Kit.Updater;

internal static class AppRuntimeChecker
{
    private const string PackagesPath = @"SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\" +
                                        @"CurrentVersion\AppModel\Repository\Packages";

    internal static bool IsWindowsAppRuntimeInstalled(Version requiredVersion)
        => IsVersionInstalled(NormaliseVersion(requiredVersion));

    internal static async Task DownloadAndInstallAppRuntimeAsync(Version                         requiredVersion,
                                                                 IProgress<InstallationProgress> progress,
                                                                 CancellationToken               ct)
    {
        var url          = GetDownloadUrl(requiredVersion);
        var versionLabel = $"{requiredVersion.Major}.{requiredVersion.Minor}";
        var tempPath     = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():B}.exe");

        try
        {
            progress.Report(new InstallationProgress(
                InstallationPhase.DownloadingAppRuntime,
                versionLabel,
                0,
                null));

            using (var response = await UpdaterHttpClient.Shared.GetAsync(
                       url,
                       HttpCompletionOption.ResponseHeadersRead,
                       ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength;

                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    await DownloadTransfer.CopyToFileAsync(
                                              stream,
                                              tempPath,
                                              total,
                                              ct,
                                              (read, size) =>
                                                  progress.Report(new InstallationProgress(
                                                      InstallationPhase.DownloadingAppRuntime,
                                                      versionLabel,
                                                      read,
                                                      size)))
                                          .ConfigureAwait(false);
                }
            }

            progress.Report(new InstallationProgress(
                InstallationPhase.InstallingAppRuntime,
                versionLabel,
                null,
                null));

            var startInfo = new ProcessStartInfo
            {
                FileName        = tempPath,
                Arguments       = "--quiet",
                UseShellExecute = true,
                Verb            = "runas"
            };

            var result = await ProcessExecution.RunAsync(startInfo, ct).ConfigureAwait(false);
            if (result.ExitCode != 0 && result.ExitCode != 3010)
            {
                throw new InvalidOperationException(
                    $"Windows App Runtime installer failed with exit code {result.ExitCode}.");
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                /* Ignored */
            }
        }
    }

    private static bool IsVersionInstalled(Version requiredVersion)
    {
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using (var root = hive.OpenSubKey(PackagesPath))
            {
                if (root == null)
                {
                    continue;
                }

                foreach (var name in root.GetSubKeyNames())
                {
                    // Framework packages:
                    //   Microsoft.WindowsAppRuntime.1.8_...
                    //   Microsoft.WindowsAppRuntime.2_...
                    // Main (installer-deployed) packages:
                    //   MicrosoftCorporationII.WinAppRuntime.Main.2_...

                    if (!name.StartsWith("Microsoft.WindowsAppRuntime.", StringComparison.OrdinalIgnoreCase) &&
                        !name.StartsWith("MicrosoftCorporationII.WinAppRuntime.Main.", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var version = TryExtractVersion(name);
                    if (version == null)
                    {
                        continue;
                    }

                    if (version >= requiredVersion)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    // Normalizes a Version to exactly 4 components, replacing the -1 sentinel that .NET uses for unspecified components
    // with 0. This ensures equality comparisons work correctly regardless of how many components the caller specified
    // (e.g. "2.2.0" and "2.2.0.0" compare as equal).
    private static Version NormaliseVersion(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    private static Version? TryExtractVersion(string packageName)
    {
        // Package naming schemes differ by SDK generation:
        //
        //   Old (1.x):  Microsoft.WindowsAppRuntime.{Major}.{Minor}_{build}_...
        //               e.g. "Microsoft.WindowsAppRuntime.1.8_8000.879.2017.0_x64__8wekyb3d8bbwe"
        //               → versionPart before first '_' = "1.8"  ✓
        //
        //   New (2.x+): Microsoft.WindowsAppRuntime.{Major}_{Major}.{Minor}.{Patch}.{Build}_...
        //               e.g. "Microsoft.WindowsAppRuntime.2_2.2.0.0_x64__8wekyb3d8bbwe"
        //               → versionPart before first '_' = "2"  (single integer, not a dotted version)
        //               → must derive Major.Minor from the NEXT field ("2.2.0.0")
        //
        // In both cases the field between the first and second '_' must NOT be used directly for version
        // comparison in the old scheme - it is always a large build number.

        // Determine which known prefix this package uses and strip it.
        const string frameworkPrefix = "Microsoft.WindowsAppRuntime.";
        const string mainPrefix      = "MicrosoftCorporationII.WinAppRuntime.Main.";

        var prefix = packageName.StartsWith(mainPrefix, StringComparison.OrdinalIgnoreCase)
            ? mainPrefix
            : frameworkPrefix;

        var rest            = packageName.Substring(prefix.Length);
        var firstUnderscore = rest.IndexOf('_');

        // No underscore at all, fall back to treating the whole string as a version.
        if (firstUnderscore < 0)
        {
            return Version.TryParse(rest, out var fallback) ? fallback : null;
        }

        var firstField = rest.Substring(0, firstUnderscore);
        // Old scheme: the first field is already a dotted version (e.g. "1.8").
        // Note: 1.x package names only encode Major.Minor - there is no patch version in the key name. The extracted
        // version is therefore normalized to Major.Minor.0.0, meaning an exact match against a required version with a
        // non-zero patch (e.g. "1.8.3") will never succeed for a 1.x package. In practice this is intentional: 1.x
        // installers should be configured with only a Major.Minor requirement (e.g. "1.8" or "1.8.0").
        if (firstField.Contains('.'))
        {
            return Version.TryParse(firstField, out var oldV) ? NormaliseVersion(oldV) : null;
        }

        // New scheme: the first field is a plain major integer (e.g. "2").
        // The full semantic version sits in the field between the first and second '_' (e.g. "2.2.0.0").
        if (int.TryParse(firstField, out _))
        {
            var afterFirst       = rest.Substring(firstUnderscore + 1);
            var secondUnderscore = afterFirst.IndexOf('_');
            var buildField       = secondUnderscore >= 0 ? afterFirst.Substring(0, secondUnderscore) : afterFirst;
            // buildField is e.g. "2.2.0.0", normalize to 4 components.
            if (Version.TryParse(buildField, out var full))
            {
                return NormaliseVersion(full);
            }
        }

        return null;
    }

    private static string GetDownloadUrl(Version version)
        => $"https://aka.ms/windowsappsdk/{version.Major}.{version.Minor}/{version}/windowsappruntimeinstall-x64.exe";
}
