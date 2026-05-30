using Shared;
using System.Reflection;

namespace Kit.Updater;

internal sealed class UiTextResolver
{
    private static readonly IReadOnlyDictionary<string, Func<TextConfiguration, string>> Accessors =
        typeof(TextConfiguration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string) && property.CanRead)
            .ToDictionary(
                property => property.Name,
                CreateAccessor,
                StringComparer.Ordinal);

    public string Resolve(UpdaterConfiguration configuration,
                          UiTextKey            key,
                          string               fallback,
                          string?              version      = null,
                          int?                 percent      = null,
                          string?              processName  = null,
                          string?              runtimeNames = null,
                          string?              runtimeName  = null)
    {
        var template = ResolveTemplate(configuration.Text, key.ToString(), fallback);

        return template
               .Replace("{ApplicationName}", configuration.ApplicationName)
               .Replace("{Version}", version ?? string.Empty)
               .Replace("{Percent}", percent.HasValue ? percent.Value.ToString() : string.Empty)
               .Replace("{ProcessName}", processName   ?? string.Empty)
               .Replace("{RuntimeNames}", runtimeNames ?? string.Empty)
               .Replace("{RuntimeName}", runtimeName   ?? string.Empty);
    }

    private static string ResolveTemplate(TextConfiguration text, string key, string fallback)
    {
        if (Accessors.TryGetValue(key, out var accessor))
        {
            var configured = accessor(text);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }
        }

        return fallback;
    }

    private static Func<TextConfiguration, string> CreateAccessor(PropertyInfo property)
        => text => (string?)property.GetValue(text) ?? string.Empty;
}
