using System.Text.RegularExpressions;

namespace Philiprehberger.Semver;

/// <summary>
/// Represents a semantic version range expression that can match against <see cref="SemVersion"/> values.
/// Supports caret (^), tilde (~), x-ranges, hyphen ranges, and comparator sets.
/// </summary>
public sealed class SemRange
{
    private readonly List<List<Comparator>> _comparatorSets;

    private SemRange(List<List<Comparator>> comparatorSets)
    {
        _comparatorSets = comparatorSets;
    }

    /// <summary>
    /// Parses a range expression string into a <see cref="SemRange"/>.
    /// </summary>
    /// <param name="rangeExpression">
    /// The range expression to parse. Supported formats:
    /// <list type="bullet">
    /// <item><description><c>"&gt;=1.0.0 &lt;2.0.0"</c> — comparator set</description></item>
    /// <item><description><c>"^1.2.3"</c> — caret range (compatible with version)</description></item>
    /// <item><description><c>"~1.2.3"</c> — tilde range (approximately equivalent)</description></item>
    /// <item><description><c>"1.x"</c>, <c>"1.2.x"</c>, <c>"*"</c> — x-range (wildcard)</description></item>
    /// <item><description><c>"1.0.0 - 2.0.0"</c> — hyphen range (inclusive)</description></item>
    /// </list>
    /// Multiple ranges can be joined with <c>||</c> to form a union.
    /// </param>
    /// <returns>A <see cref="SemRange"/> that can test versions against the expression.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rangeExpression"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="rangeExpression"/> is invalid.</exception>
    public static SemRange Parse(string rangeExpression)
    {
        ArgumentNullException.ThrowIfNull(rangeExpression);

        var sets = new List<List<Comparator>>();

        var orParts = rangeExpression.Split("||", StringSplitOptions.TrimEntries);
        foreach (var orPart in orParts)
        {
            var comparators = ParseComparatorSet(orPart);
            if (comparators.Count == 0)
                throw new FormatException($"Invalid range expression: '{rangeExpression}'");
            sets.Add(comparators);
        }

        if (sets.Count == 0)
            throw new FormatException($"Invalid range expression: '{rangeExpression}'");

        return new SemRange(sets);
    }

    /// <summary>
    /// Determines whether the specified version satisfies this range.
    /// </summary>
    /// <param name="version">The version to test.</param>
    /// <returns><c>true</c> if the version satisfies at least one comparator set in this range; otherwise, <c>false</c>.</returns>
    public bool IsSatisfied(SemVersion version)
    {
        // A version satisfies the range if it satisfies any one of the comparator sets (OR logic)
        foreach (var set in _comparatorSets)
        {
            if (set.All(c => c.IsSatisfied(version)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the highest version from <paramref name="versions"/> that satisfies the specified <paramref name="range"/>,
    /// or <c>null</c> if no version matches.
    /// </summary>
    /// <param name="versions">The collection of versions to evaluate.</param>
    /// <param name="range">The range to match against.</param>
    /// <returns>The highest satisfying version, or <c>null</c> if none match.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="versions"/> or <paramref name="range"/> is null.</exception>
    public static SemVersion? MaxSatisfying(IEnumerable<SemVersion> versions, SemRange range)
    {
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(range);

        SemVersion? max = null;
        foreach (var version in versions)
        {
            if (range.IsSatisfied(version))
            {
                if (max is null || version.CompareTo(max.Value) > 0)
                    max = version;
            }
        }

        return max;
    }

    private static List<Comparator> ParseComparatorSet(string expression)
    {
        expression = expression.Trim();

        if (string.IsNullOrEmpty(expression))
            return [];

        // Check for hyphen range: "1.0.0 - 2.0.0"
        var hyphenMatch = Regex.Match(expression, @"^(\S+)\s+-\s+(\S+)$");
        if (hyphenMatch.Success)
        {
            return ParseHyphenRange(hyphenMatch.Groups[1].Value, hyphenMatch.Groups[2].Value);
        }

        var comparators = new List<Comparator>();
        var parts = Regex.Split(expression, @"\s+");

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            comparators.AddRange(ParseSingleComparator(part));
        }

        return comparators;
    }

    private static List<Comparator> ParseHyphenRange(string low, string high)
    {
        var lowVersion = ParsePartialVersion(low);
        var highVersion = ParsePartialVersion(high);

        var comparators = new List<Comparator>
        {
            new(CompareOp.Gte, new SemVersion(lowVersion.Major, lowVersion.Minor ?? 0, lowVersion.Patch ?? 0))
        };

        if (highVersion.Patch.HasValue)
        {
            comparators.Add(new Comparator(CompareOp.Lte, new SemVersion(highVersion.Major, highVersion.Minor ?? 0, highVersion.Patch.Value)));
        }
        else if (highVersion.Minor.HasValue)
        {
            comparators.Add(new Comparator(CompareOp.Lt, new SemVersion(highVersion.Major, highVersion.Minor.Value + 1, 0)));
        }
        else
        {
            comparators.Add(new Comparator(CompareOp.Lt, new SemVersion(highVersion.Major + 1, 0, 0)));
        }

        return comparators;
    }

    private static List<Comparator> ParseSingleComparator(string expression)
    {
        // Caret range: ^1.2.3
        if (expression.StartsWith('^'))
            return ParseCaretRange(expression[1..]);

        // Tilde range: ~1.2.3
        if (expression.StartsWith('~'))
            return ParseTildeRange(expression[1..]);

        // Operator-prefixed: >=1.0.0, <2.0.0, =1.0.0, etc.
        var opMatch = Regex.Match(expression, @"^(>=|<=|>|<|=)(.+)$");
        if (opMatch.Success)
        {
            var op = opMatch.Groups[1].Value switch
            {
                ">=" => CompareOp.Gte,
                "<=" => CompareOp.Lte,
                ">" => CompareOp.Gt,
                "<" => CompareOp.Lt,
                "=" => CompareOp.Eq,
                _ => throw new FormatException($"Unknown operator: {opMatch.Groups[1].Value}")
            };
            var version = SemVersion.Parse(opMatch.Groups[2].Value);
            return [new Comparator(op, version)];
        }

        // X-range or plain version: *, 1.x, 1.2.x, 1.x.x, 1.2.3
        return ParseXRange(expression);
    }

    private static List<Comparator> ParseCaretRange(string version)
    {
        var pv = ParsePartialVersion(version);
        var major = pv.Major;
        var minor = pv.Minor ?? 0;
        var patch = pv.Patch ?? 0;

        var lower = new SemVersion(major, minor, patch);

        SemVersion upper;
        if (major != 0)
        {
            upper = new SemVersion(major + 1, 0, 0);
        }
        else if (pv.Minor.HasValue && minor != 0)
        {
            upper = new SemVersion(0, minor + 1, 0);
        }
        else if (pv.Patch.HasValue)
        {
            upper = new SemVersion(0, 0, patch + 1);
        }
        else if (pv.Minor.HasValue)
        {
            upper = new SemVersion(0, minor + 1, 0);
        }
        else
        {
            upper = new SemVersion(major + 1, 0, 0);
        }

        return
        [
            new Comparator(CompareOp.Gte, lower),
            new Comparator(CompareOp.Lt, upper)
        ];
    }

    private static List<Comparator> ParseTildeRange(string version)
    {
        var pv = ParsePartialVersion(version);
        var major = pv.Major;
        var minor = pv.Minor ?? 0;
        var patch = pv.Patch ?? 0;

        var lower = new SemVersion(major, minor, patch);

        SemVersion upper;
        if (pv.Minor.HasValue)
        {
            upper = new SemVersion(major, minor + 1, 0);
        }
        else
        {
            upper = new SemVersion(major + 1, 0, 0);
        }

        return
        [
            new Comparator(CompareOp.Gte, lower),
            new Comparator(CompareOp.Lt, upper)
        ];
    }

    private static List<Comparator> ParseXRange(string expression)
    {
        // Wildcard: *, x, X
        if (expression is "*" or "x" or "X")
            return [new Comparator(CompareOp.Gte, new SemVersion(0, 0, 0))];

        var parts = expression.Split('.');

        if (parts.Length == 1)
        {
            if (IsWildcard(parts[0]))
                return [new Comparator(CompareOp.Gte, new SemVersion(0, 0, 0))];

            var major = int.Parse(parts[0]);
            return
            [
                new Comparator(CompareOp.Gte, new SemVersion(major, 0, 0)),
                new Comparator(CompareOp.Lt, new SemVersion(major + 1, 0, 0))
            ];
        }

        if (parts.Length == 2)
        {
            var major = int.Parse(parts[0]);
            if (IsWildcard(parts[1]))
            {
                return
                [
                    new Comparator(CompareOp.Gte, new SemVersion(major, 0, 0)),
                    new Comparator(CompareOp.Lt, new SemVersion(major + 1, 0, 0))
                ];
            }

            var minor = int.Parse(parts[1]);
            return
            [
                new Comparator(CompareOp.Gte, new SemVersion(major, minor, 0)),
                new Comparator(CompareOp.Lt, new SemVersion(major, minor + 1, 0))
            ];
        }

        if (parts.Length == 3)
        {
            var major = int.Parse(parts[0]);
            var minor = int.Parse(parts[1]);

            if (IsWildcard(parts[2]))
            {
                return
                [
                    new Comparator(CompareOp.Gte, new SemVersion(major, minor, 0)),
                    new Comparator(CompareOp.Lt, new SemVersion(major, minor + 1, 0))
                ];
            }

            // Exact version
            var version = SemVersion.Parse(expression);
            return [new Comparator(CompareOp.Eq, version)];
        }

        throw new FormatException($"Invalid version expression: '{expression}'");
    }

    private static bool IsWildcard(string value) => value is "*" or "x" or "X";

    private static PartialVersion ParsePartialVersion(string version)
    {
        var parts = version.Split('.');
        var major = int.Parse(parts[0]);
        int? minor = parts.Length > 1 && !IsWildcard(parts[1]) ? int.Parse(parts[1]) : null;
        int? patch = parts.Length > 2 && !IsWildcard(parts[2]) ? int.Parse(parts[2].Split('-')[0].Split('+')[0]) : null;
        return new PartialVersion(major, minor, patch);
    }

    private readonly record struct PartialVersion(int Major, int? Minor, int? Patch);

    private enum CompareOp
    {
        Eq,
        Gt,
        Gte,
        Lt,
        Lte
    }

    private readonly record struct Comparator(CompareOp Op, SemVersion Version)
    {
        public bool IsSatisfied(SemVersion version)
        {
            // Pre-release versions only match ranges that include the same major.minor.patch
            if (version.IsPreRelease && !Version.IsPreRelease)
            {
                if (version.Major != Version.Major || version.Minor != Version.Minor || version.Patch != Version.Patch)
                    return false;
            }

            var cmp = version.CompareTo(Version);
            return Op switch
            {
                CompareOp.Eq => cmp == 0,
                CompareOp.Gt => cmp > 0,
                CompareOp.Gte => cmp >= 0,
                CompareOp.Lt => cmp < 0,
                CompareOp.Lte => cmp <= 0,
                _ => false
            };
        }
    }
}
