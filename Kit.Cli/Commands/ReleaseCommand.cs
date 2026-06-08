using System.Text.Json;
using System.IO.Compression;

namespace Kit.Cli.Commands;

internal static class ReleaseCommand
{
    public static int Run(RootCommand command)
    {
        var appDir      = KitRcOptionResolver.GetRequiredPath(command, "app-dir", section => section.AppDir);
        var configPath  = KitRcOptionResolver.GetRequiredPath(command, "config", section => section.Config);
        var updaterPath = KitRcOptionResolver.GetRequiredPath(command, "updater", section => section.Updater);

        var fullAppDir      = appDir.ResolvePath();
        var fullConfigPath  = configPath.ResolvePath();
        var fullUpdaterPath = updaterPath.ResolvePath();

        if (!Directory.Exists(fullAppDir))
        {
            throw new DirectoryNotFoundException($"Application directory was not found: {fullAppDir}");
        }

        if (!File.Exists(fullConfigPath))
        {
            throw new FileNotFoundException("Stamp configuration file was not found.", fullConfigPath);
        }

        if (!File.Exists(fullUpdaterPath))
        {
            throw new FileNotFoundException("Blank updater executable was not found.", fullUpdaterPath);
        }

        var versionOption = KitRcOptionResolver.GetOptionalValue(command, "version", section => section.Version, "");
        if (!string.IsNullOrWhiteSpace(versionOption.Value) && !StampVersion.TryParse(versionOption.Value))
        {
            throw new InvalidOperationException("--version must be a valid version string.");
        }

        // 1. Resolve and Auto-Detect Version
        var configDirectory    = Path.GetDirectoryName(fullConfigPath) ?? Environment.CurrentDirectory;
        var stampConfiguration = StampConfigurationLoader.Load(fullConfigPath);

        string version;
        if (!string.IsNullOrWhiteSpace(versionOption.Value))
        {
            version = versionOption.Value;
        }
        else
        {
            version = AutoDetectVersion(fullAppDir, stampConfiguration.LaunchExecutable ?? "");
            Console.WriteLine($"Auto-detected version: {version}");
        }

        // Validate stamp configuration before building payload
        StampPayloadValidator.Validate(stampConfiguration, configDirectory);

        // 2. Resolve paths and Zip Application Payload
        var outputDir     = KitRcOptionResolver.GetOptionalValue(command, "output-dir", section => section.OutputDir, "./out");
        var packageName   = KitRcOptionResolver.GetOptionalValue(command, "package-name", section => section.PackageName, "app-package.zip");
        var fullOutputDir = outputDir.ResolvePath();
        if (IsSameOrChildPath(fullOutputDir, fullAppDir))
        {
            throw new InvalidOperationException("The output directory cannot be inside the application directory being zipped.");
        }

        Directory.CreateDirectory(fullOutputDir);
        var packageFileName = Path.GetFileName(packageName.Value);
        if (!string.Equals(packageFileName, packageName.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("--package-name must be a file name, not a path.");
        }

        var zipPath = Path.Combine(fullOutputDir, packageFileName);

        Console.WriteLine($"Zipping application directory '{fullAppDir}' to '{zipPath}'...");
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        ZipFile.CreateFromDirectory(fullAppDir, zipPath);

        // 3. Stamp Updater
        var buildResult = StampPayloadBuilder.Build(stampConfiguration, configDirectory, version);
        var payloadJson = JsonSerializer.Serialize(buildResult.Payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Console.WriteLine($"Stamping updater to '{fullUpdaterPath}'...");
        StampedUpdaterWriter.Write(fullUpdaterPath, fullUpdaterPath, payloadJson, buildResult.ResolvedIconPath);

        // 4. Execute Installer Compilation (Optional)
        var installerCommand = KitRcOptionResolver.GetOptionalValue(command, "installer-command", section => section.InstallerCommand, "");
        if (!string.IsNullOrWhiteSpace(installerCommand.Value))
        {
            var installerArgs = KitRcOptionResolver.GetOptionalValue(command, "installer-args", section => section.InstallerArgs, "");
            var formattedArgs = installerArgs
                                .Value
                                .Replace("{Version}", version, StringComparison.OrdinalIgnoreCase)
                                .Replace("{OutputDir}", fullOutputDir, StringComparison.OrdinalIgnoreCase)
                                .Replace("{AppDir}", fullAppDir, StringComparison.OrdinalIgnoreCase);

            Console.WriteLine($"Executing installer command: {installerCommand.Value} {formattedArgs}");

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName               = installerCommand.Value,
                Arguments              = formattedArgs,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };

            using var process = new System.Diagnostics.Process();
            process.StartInfo = startInfo;
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) Console.WriteLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) Console.Error.WriteLine(e.Data);
            };

            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start installer compiler process: {installerCommand.Value}");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Installer compiler process failed with exit code {process.ExitCode}.");
            }
        }

        // 5. Generate Release Manifest
        string? resolvedInstallerPath = null;
        var installerPathOption = KitRcOptionResolver.GetOptionalValue(command, "installer-path", section => section.InstallerPath, "");
        if (!string.IsNullOrWhiteSpace(installerPathOption.Value))
        {
            var formattedPath = installerPathOption.Value
                                .Replace("{Version}", version, StringComparison.OrdinalIgnoreCase)
                                .Replace("{OutputDir}", fullOutputDir, StringComparison.OrdinalIgnoreCase)
                                .Replace("{AppDir}", fullAppDir, StringComparison.OrdinalIgnoreCase);

            resolvedInstallerPath = KitRcPathResolver.ResolvePath(formattedPath, installerPathOption.BaseDirectory);
            if (!File.Exists(resolvedInstallerPath))
            {
                throw new FileNotFoundException("The specified installer file was not found.", resolvedInstallerPath);
            }
        }

        var updaterUpdateRequired = KitRcOptionResolver.GetOptionalBoolean(command, "updater-update-required", section => section.UpdaterUpdateRequired);

        var manifest = ReleaseManifestBuilder.Build(
            version,
            fullUpdaterPath,
            zipPath,
            resolvedInstallerPath,
            updaterUpdateRequired);

        var manifestPath = Path.Combine(fullOutputDir, "release-manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));

        Console.WriteLine("Release completed successfully!");
        Console.WriteLine($"  App ZIP Package:  {zipPath}");
        Console.WriteLine($"  Stamped Updater:  {fullUpdaterPath}");
        if (resolvedInstallerPath != null)
        {
            Console.WriteLine($"  Setup Installer:  {resolvedInstallerPath}");
        }

        Console.WriteLine($"  Release Manifest: {manifestPath}");

        return 0;
    }

    private static string AutoDetectVersion(string appDir, string launchExecutable)
    {
        if (string.IsNullOrWhiteSpace(launchExecutable))
        {
            throw new InvalidOperationException("LaunchExecutable is not specified in the stamp configuration, cannot auto-detect version.");
        }

        var searchFiles = new List<string>();

        // 1. Direct path
        var directPath = Path.Combine(appDir, launchExecutable);
        searchFiles.Add(directPath);

        // 2. If it ends with .exe, try .dll
        if (launchExecutable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var dllPath = Path.Combine(appDir, Path.ChangeExtension(launchExecutable, ".dll"));
            searchFiles.Add(dllPath);
        }
        // 3. If it doesn't end with .exe or .dll, try appending both
        else
        {
            searchFiles.Add(Path.Combine(appDir, launchExecutable + ".exe"));
            searchFiles.Add(Path.Combine(appDir, launchExecutable + ".dll"));
        }

        foreach (var file in searchFiles)
        {
            if (File.Exists(file))
            {
                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(file);
                var version     = versionInfo.FileVersion ?? versionInfo.ProductVersion;
                if (!string.IsNullOrWhiteSpace(version))
                {
                    var sanitized = version.Trim().Split('+')[0];
                    if (!string.IsNullOrWhiteSpace(sanitized))
                    {
                        return sanitized;
                    }
                }
            }
        }

        throw new InvalidOperationException($"Could not auto-detect version. LaunchExecutable file not found or contains no version info at: {directPath}");
    }

    private static bool IsSameOrChildPath(string candidatePath, string parentPath)
    {
        var relativePath = Path.GetRelativePath(parentPath, candidatePath);
        return relativePath.Length == 0
               || string.Equals(relativePath, ".", StringComparison.Ordinal)
               || (!relativePath.StartsWith("..", StringComparison.Ordinal)
                   && !Path.IsPathRooted(relativePath));
    }
}

