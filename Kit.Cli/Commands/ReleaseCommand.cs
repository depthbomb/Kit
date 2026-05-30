using System.Text.Json;
using System.IO.Compression;

namespace Kit.Cli.Commands;

internal static class ReleaseCommand
{
    public static int Run(RootCommand command)
    {
        var appDir      = CommandLine.GetRequiredOption(command.Options, "app-dir");
        var configPath  = CommandLine.GetRequiredOption(command.Options, "config");
        var updaterPath = CommandLine.GetRequiredOption(command.Options, "updater");

        var fullAppDir      = Path.GetFullPath(appDir);
        var fullConfigPath  = Path.GetFullPath(configPath);
        var fullUpdaterPath = Path.GetFullPath(updaterPath);

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

        // 1. Resolve and Auto-Detect Version
        var configDirectory    = Path.GetDirectoryName(fullConfigPath) ?? Environment.CurrentDirectory;
        var stampConfiguration = StampConfigurationLoader.Load(fullConfigPath);

        string version;
        if (command.Options.TryGetValue("version", out var versionOpt) && !string.IsNullOrWhiteSpace(versionOpt))
        {
            version = versionOpt;
        }
        else
        {
            version = AutoDetectVersion(fullAppDir, stampConfiguration.LaunchExecutable ?? "");
            Console.WriteLine($"Auto-detected version: {version}");
        }

        // Validate stamp configuration before building payload
        StampPayloadValidator.Validate(stampConfiguration, configDirectory);

        // 2. Resolve paths and Zip Application Payload
        var outputDir     = command.Options.GetValueOrDefault("output-dir", "./out");
        var packageName   = command.Options.GetValueOrDefault("package-name", "app-package.zip");
        var fullOutputDir = Path.GetFullPath(outputDir);
        if (IsSameOrChildPath(fullOutputDir, fullAppDir))
        {
            throw new InvalidOperationException("The output directory cannot be inside the application directory being zipped.");
        }

        Directory.CreateDirectory(fullOutputDir);
        var zipPath = Path.Combine(fullOutputDir, packageName);

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
        if (command.Options.TryGetValue("installer-command", out var installerCommand) && !string.IsNullOrWhiteSpace(installerCommand))
        {
            var installerArgs = command.Options.GetValueOrDefault("installer-args", "");
            var formattedArgs = installerArgs
                                .Replace("{Version}", version, StringComparison.OrdinalIgnoreCase)
                                .Replace("{OutputDir}", fullOutputDir, StringComparison.OrdinalIgnoreCase)
                                .Replace("{AppDir}", fullAppDir, StringComparison.OrdinalIgnoreCase);

            Console.WriteLine($"Executing installer command: {installerCommand} {formattedArgs}");

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName               = installerCommand,
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
                throw new InvalidOperationException($"Failed to start installer compiler process: {installerCommand}");
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
        if (command.Options.TryGetValue("installer-path", out var installerPathOption) && !string.IsNullOrWhiteSpace(installerPathOption))
        {
            var formattedPath = installerPathOption
                                .Replace("{Version}", version, StringComparison.OrdinalIgnoreCase)
                                .Replace("{OutputDir}", fullOutputDir, StringComparison.OrdinalIgnoreCase)
                                .Replace("{AppDir}", fullAppDir, StringComparison.OrdinalIgnoreCase);

            resolvedInstallerPath = Path.GetFullPath(formattedPath);
            if (!File.Exists(resolvedInstallerPath))
            {
                throw new FileNotFoundException("The specified installer file was not found.", resolvedInstallerPath);
            }
        }

        var updaterUpdateRequired = command.Options.TryGetValue("updater-update-required", out var requiredText)
                                    && ParseBoolean(requiredText, "updater-update-required");

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

    private static bool ParseBoolean(string value, string optionName)
    {
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException("Option --" + optionName + " must be either true or false.");
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

