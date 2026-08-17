using System.Globalization;
using System.Text;

namespace Kit.Updater;

internal static class DiagnosticLog
{
    private const long MaximumFileBytes = 1024 * 1024;
    private const int RetainedFiles = 3;

    private static readonly object Gate = new();
    private static string _filePath = BuildLogPath("updater");

    public static string FilePath
    {
        get
        {
            lock (Gate)
            {
                return _filePath;
            }
        }
    }

    public static void Initialize(string applicationName)
    {
        lock (Gate)
        {
            _filePath = BuildLogPath(applicationName);
        }
    }

    public static void Info(string eventName, params KeyValuePair<string, string?>[] properties)
        => Write("information", eventName, null, properties);

    public static void Warning(string eventName, params KeyValuePair<string, string?>[] properties)
        => Write("warning", eventName, null, properties);

    public static void Error(string eventName, Exception exception, params KeyValuePair<string, string?>[] properties)
        => Write("error", eventName, exception, properties);

    private static void Write(string level, string eventName, Exception? exception, IEnumerable<KeyValuePair<string, string?>> properties)
    {
        try
        {
            var line = BuildJsonLine(level, eventName, exception, properties);
            lock (Gate)
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                RotateIfNeeded(Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length);
                File.AppendAllText(_filePath, line + Environment.NewLine, new UTF8Encoding(false));
            }
        }
        catch
        {
            // Diagnostics must never prevent the updater from running.
        }
    }

    private static string BuildJsonLine(string level, string eventName, Exception? exception, IEnumerable<KeyValuePair<string, string?>> properties)
    {
        var builder = new StringBuilder(256);
        builder.Append('{');
        AppendProperty(builder, "timestamp", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), false);
        AppendProperty(builder, "level", level, true);
        AppendProperty(builder, "event", eventName, true);

        foreach (var property in properties)
        {
            AppendProperty(builder, property.Key, property.Value, true);
        }

        if (exception != null)
        {
            AppendProperty(builder, "exceptionType", exception.GetType().FullName, true);
            AppendProperty(builder, "message", exception.Message, true);
            AppendProperty(builder, "stackTrace", exception.StackTrace, true);
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendProperty(StringBuilder builder, string name, string? value, bool prependComma)
    {
        if (prependComma)
        {
            builder.Append(',');
        }

        builder.Append('"').Append(Escape(name)).Append("\":");
        if (value == null)
        {
            builder.Append("null");
            return;
        }

        builder.Append('"').Append(Escape(value)).Append('"');
    }

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (character < ' ')
                    {
                        builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    private static void RotateIfNeeded(int incomingBytes)
    {
        if (!File.Exists(_filePath) || new FileInfo(_filePath).Length + incomingBytes <= MaximumFileBytes)
        {
            return;
        }

        for (var index = RetainedFiles - 1; index >= 1; index--)
        {
            var source = _filePath + "." + index;
            var destination = _filePath + "." + (index + 1);
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            if (File.Exists(source))
            {
                File.Move(source, destination);
            }
        }

        File.Move(_filePath, _filePath + ".1");
    }

    private static string BuildLogPath(string applicationName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeName = new string((applicationName ?? string.Empty)
                                  .Trim()
                                  .Select(character => invalidCharacters.Contains(character) ? '_' : character)
                                  .Take(80)
                                  .ToArray());
        if (safeName.Length == 0)
        {
            safeName = "updater";
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        return Path.Combine(localAppData, "Kit", "Logs", safeName + "-updater.jsonl");
    }
}
