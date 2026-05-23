using Shared;

namespace Kit.Cli;

internal static class StampedUpdaterWriter
{
    public static void Write(string inputPath, string outputPath, string payloadJson, string? iconPath)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(outputDirectory);

        var temporaryPath = Path.Combine(outputDirectory, Path.GetFileName(outputPath) + ".tmp." + Guid.NewGuid().ToString("B"));
        try
        {
            File.Copy(inputPath, temporaryPath, true);

            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                IconResourceWriter.WriteIcon(temporaryPath, iconPath);
            }

            StampPayload.WriteConfigurationJson(temporaryPath, payloadJson);
            ReplaceOutput(temporaryPath, outputPath);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static void ReplaceOutput(string temporaryPath, string outputPath)
    {
        if (File.Exists(outputPath))
        {
            File.Replace(temporaryPath, outputPath, null);
            return;
        }

        File.Move(temporaryPath, outputPath);
    }

    private static void TryDelete(string path)
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
            /*Ignored*/
        }
    }
}
