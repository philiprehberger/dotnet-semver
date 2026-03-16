using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Philiprehberger.Semver;

/// <summary>
/// Represents a semantic version conforming to the semver 2.0 specification.
/// Immutable value type that supports parsing, comparison, and version bumping.
/// </summary>
public readonly record struct SemVersion : IComparable<SemVersion>
{
    private static readonly Regex SemVerPattern = new(
        @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)" +
        @"(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?" +
        @"(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$",
        RegexOptions.Compiled);

    /// <summary>
    /// Gets the major version number. Incremented for incompatible API changes.
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// Gets the minor version number. Incremented for backwards-compatible feature additions.
    /// </summary>
    public int Minor { get; }

    /// <summary>
    /// Gets the patch version number. Incremented for backwards-compatible bug fixes.
    /// </summary>
    public int Patch { get; }

    /// <summary>
    /// Gets the pre-release label, or <c>null</c> if this is a release version.
    /// </summary>
    public string? PreRelease { get; }

    /// <summary>
    /// Gets the build metadata, or <c>null</c> if none is specified.
    /// Build metadata is ignored in version precedence comparisons.
    /// </summary>
    public string? BuildMetadata { get; }

    /// <summary>
    /// Gets a value indicating whether this version has a pre-release label.
    /// </summary>
    public bool IsPreRelease => PreRelease is not null;

    /// <summary>
    /// Initializes a new <see cref="SemVersion"/> with the specified components.
    /// </summary>
    /// <param name="major">The major version number.</param>
    /// <param name="minor">The minor version number.</param>
    /// <param name="patch">The patch version number.</param>
    /// <param name="preRelease">The optional pre-release label.</param>
    /// <param name="buildMetadata">The optional build metadata.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when major, minor, or patch is negative.</exception>
    public SemVersion(int major, int minor, int patch, string? preRelease = null, string? buildMetadata = null)
    {
        if (major < 0) throw new ArgumentOutOfRangeException(nameof(major), "Major version must be non-negative.");
        if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor), "Minor version must be non-negative.");
        if (patch < 0) throw new ArgumentOutOfRangeException(nameof(patch), "Patch version must be non-negative.");

        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = string.IsNullOrEmpty(preRelease) ? null : preRelease;
        BuildMetadata = string.IsNullOrEmpty(buildMetadata) ? null : buildMetadata;
    }

    /// <summary>
    /// Parses a semantic version string into a <see cref="SemVersion"/>.
    /// </summary>
    /// <param name="version">The version string to parse (e.g., "1.2.3-beta.1+build.456").</param>
    /// <returns>The parsed <see cref="SemVersion"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="version"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="version"/> is not a valid semver string.</exception>
    public static SemVersion Parse(string version)
    {
        ArgumentNullException.ThrowIfNull(version);

        if (!TryParse(version, out var result))
            throw new FormatException($"Invalid semantic version: '{version}'");

        return result.Value;
    }

    /// <summary>
    /// Attempts to parse a semantic version string into a <see cref="SemVersion"/>.
    /// </summary>
    /// <param name="version">The version string to parse.</param>
    /// <param name="result">When successful, contains the parsed version; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParse(string? version, [NotNullWhen(true)] out SemVersion? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(version))
            return false;

        var match = SemVerPattern.Match(version);
        if (!match.Success)
            return false;

        var major = int.Parse(match.Groups["major"].Value);
        var minor = int.Parse(match.Groups["minor"].Value);
        var patch = int.Parse(match.Groups["patch"].Value);
        var preRelease = match.Groups["prerelease"].Success ? match.Groups["prerelease"].Value : null;
        var buildMetadata = match.Groups["buildmetadata"].Success ? match.Groups["buildmetadata"].Value : null;

        result = new SemVersion(major, minor, patch, preRelease, buildMetadata);
        return true;
    }

    /// <summary>
    /// Returns a new version with the major version incremented and minor, patch, pre-release, and build metadata reset.
    /// </summary>
    /// <returns>A new <see cref="SemVersion"/> with major incremented by one.</returns>
    public SemVersion BumpMajor() => new(Major + 1, 0, 0);

    /// <summary>
    /// Returns a new version with the minor version incremented and patch, pre-release, and build metadata reset.
    /// </summary>
    /// <returns>A new <see cref="SemVersion"/> with minor incremented by one.</returns>
    public SemVersion BumpMinor() => new(Major, Minor + 1, 0);

    /// <summary>
    /// Returns a new version with the patch version incremented and pre-release and build metadata reset.
    /// </summary>
    /// <returns>A new <see cref="SemVersion"/> with patch incremented by one.</returns>
    public SemVersion BumpPatch() => new(Major, Minor, Patch + 1);

    /// <summary>
    /// Returns a new version with the specified pre-release label.
    /// </summary>
    /// <param name="preRelease">The pre-release label to set.</param>
    /// <returns>A new <see cref="SemVersion"/> with the given pre-release label.</returns>
    public SemVersion WithPreRelease(string preRelease) => new(Major, Minor, Patch, preRelease, BuildMetadata);

    /// <summary>
    /// Returns a new version with the specified build metadata.
    /// </summary>
    /// <param name="buildMetadata">The build metadata to set.</param>
    /// <returns>A new <see cref="SemVersion"/> with the given build metadata.</returns>
    public SemVersion WithBuildMetadata(string buildMetadata) => new(Major, Minor, Patch, PreRelease, buildMetadata);

    /// <summary>
    /// Compares this version with another following semver 2.0 precedence rules.
    /// Build metadata is ignored in comparisons.
    /// </summary>
    /// <param name="other">The version to compare with.</param>
    /// <returns>A negative value if this precedes <paramref name="other"/>, zero if equal, or a positive value if this follows <paramref name="other"/>.</returns>
    public int CompareTo(SemVersion other)
    {
        var result = Major.CompareTo(other.Major);
        if (result != 0) return result;

        result = Minor.CompareTo(other.Minor);
        if (result != 0) return result;

        result = Patch.CompareTo(other.Patch);
        if (result != 0) return result;

        // No pre-release has higher precedence than pre-release
        if (PreRelease is null && other.PreRelease is null) return 0;
        if (PreRelease is null) return 1;
        if (other.PreRelease is null) return -1;

        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    private static int ComparePreRelease(string left, string right)
    {
        var leftParts = left.Split('.');
        var rightParts = right.Split('.');

        var length = Math.Min(leftParts.Length, rightParts.Length);
        for (var i = 0; i < length; i++)
        {
            var leftIsNumeric = int.TryParse(leftParts[i], out var leftNum);
            var rightIsNumeric = int.TryParse(rightParts[i], out var rightNum);

            if (leftIsNumeric && rightIsNumeric)
            {
                var cmp = leftNum.CompareTo(rightNum);
                if (cmp != 0) return cmp;
            }
            else if (leftIsNumeric)
            {
                // Numeric identifiers have lower precedence than alphanumeric
                return -1;
            }
            else if (rightIsNumeric)
            {
                return 1;
            }
            else
            {
                var cmp = string.Compare(leftParts[i], rightParts[i], StringComparison.Ordinal);
                if (cmp != 0) return cmp;
            }
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }

    /// <summary>
    /// Determines whether two versions are equal. Build metadata is ignored.
    /// </summary>
    public bool Equals(SemVersion other) =>
        Major == other.Major && Minor == other.Minor && Patch == other.Patch &&
        PreRelease == other.PreRelease;

    /// <summary>
    /// Returns the hash code for this version. Build metadata is ignored.
    /// </summary>
    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, PreRelease);

    /// <summary>
    /// Returns the semver 2.0 string representation of this version (e.g., "1.2.3-beta.1+build.456").
    /// </summary>
    public override string ToString()
    {
        var result = $"{Major}.{Minor}.{Patch}";
        if (PreRelease is not null) result += $"-{PreRelease}";
        if (BuildMetadata is not null) result += $"+{BuildMetadata}";
        return result;
    }

    /// <summary>Determines whether the left version is less than the right version.</summary>
    public static bool operator <(SemVersion left, SemVersion right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left version is greater than the right version.</summary>
    public static bool operator >(SemVersion left, SemVersion right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left version is less than or equal to the right version.</summary>
    public static bool operator <=(SemVersion left, SemVersion right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left version is greater than or equal to the right version.</summary>
    public static bool operator >=(SemVersion left, SemVersion right) => left.CompareTo(right) >= 0;
}
