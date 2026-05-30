using System.Globalization;

namespace Kit.Updater;

internal sealed class ApplicationVersion : IComparable<ApplicationVersion>
{
    private readonly List<string> _preReleaseSegments;

    private ApplicationVersion(string             originalValue,
                               string             normalizedValue,
                               IReadOnlyList<int> numericSegments,
                               List<string>       preReleaseSegments)
    {
        OriginalValue       = originalValue;
        NormalizedValue     = normalizedValue;
        NumericSegments     = numericSegments;
        _preReleaseSegments = preReleaseSegments;
    }

    public string OriginalValue { get; }

    public string NormalizedValue { get; }

    public IReadOnlyList<int> NumericSegments { get; }

    public bool IsPrerelease => _preReleaseSegments.Count > 0;

    public static bool TryParse(string value, out ApplicationVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 1 && char.IsDigit(trimmed[1]))
        {
            trimmed = trimmed.Substring(1);
        }

        var buildSplit      = trimmed.Split(['+'], 2);
        var versionPortion  = buildSplit[0];
        var prereleaseSplit = versionPortion.Split(['-'], 2);
        var numericPart     = prereleaseSplit[0];
        var numericSegments = new List<int>();

        foreach (var segment in numericPart.Split('.'))
        {
            if (segment.Length == 0 || !int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSegment))
            {
                return false;
            }

            numericSegments.Add(parsedSegment);
        }

        if (numericSegments.Count == 0)
        {
            return false;
        }

        var prereleaseSegments = prereleaseSplit.Length == 2
            ? prereleaseSplit[1].Split('.').Where(segment => segment.Length > 0).ToList()
            : [];

        version = new ApplicationVersion(value, trimmed, numericSegments, prereleaseSegments);
        return true;
    }

    public int CompareTo(ApplicationVersion? other)
    {
        if (other == null)
        {
            return 1;
        }

        var maxCount = Math.Max(NumericSegments.Count, other.NumericSegments.Count);
        for (var index = 0; index < maxCount; index++)
        {
            var left       = index < NumericSegments.Count ? NumericSegments[index] : 0;
            var right      = index < other.NumericSegments.Count ? other.NumericSegments[index] : 0;
            var comparison = left.CompareTo(right);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        if (!IsPrerelease && !other.IsPrerelease)
        {
            return 0;
        }

        if (!IsPrerelease)
        {
            return 1;
        }

        if (!other.IsPrerelease)
        {
            return -1;
        }

        var preReleaseCount = Math.Max(_preReleaseSegments.Count, other._preReleaseSegments.Count);
        for (var index = 0; index < preReleaseCount; index++)
        {
            if (index >= _preReleaseSegments.Count)
            {
                return -1;
            }

            if (index >= other._preReleaseSegments.Count)
            {
                return 1;
            }

            var leftSegment    = _preReleaseSegments[index];
            var rightSegment   = other._preReleaseSegments[index];
            var leftIsNumeric  = int.TryParse(leftSegment, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumeric);
            var rightIsNumeric = int.TryParse(rightSegment, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumeric);

            int comparison;
            if (leftIsNumeric && rightIsNumeric)
            {
                comparison = leftNumeric.CompareTo(rightNumeric);
            }
            else if (leftIsNumeric)
            {
                comparison = -1;
            }
            else if (rightIsNumeric)
            {
                comparison = 1;
            }
            else
            {
                comparison = string.Compare(leftSegment, rightSegment, StringComparison.OrdinalIgnoreCase);
            }

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    public override string ToString() => NormalizedValue;
}
