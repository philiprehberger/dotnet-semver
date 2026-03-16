# Philiprehberger.Semver

[![CI](https://github.com/philiprehberger/dotnet-semver/actions/workflows/ci.yml/badge.svg)](https://github.com/philiprehberger/dotnet-semver/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Philiprehberger.Semver.svg)](https://www.nuget.org/packages/Philiprehberger.Semver)
[![License](https://img.shields.io/github/license/philiprehberger/dotnet-semver)](LICENSE)

Semantic versioning parser, comparator, and range matcher — fully compliant with the semver 2.0 spec.

## Install

```bash
dotnet add package Philiprehberger.Semver
```

## Usage

### Parsing and comparing versions

```csharp
using Philiprehberger.Semver;

var version = SemVersion.Parse("1.2.3-beta.1+build.456");

Console.WriteLine(version.Major);         // 1
Console.WriteLine(version.Minor);         // 2
Console.WriteLine(version.Patch);         // 3
Console.WriteLine(version.PreRelease);    // beta.1
Console.WriteLine(version.BuildMetadata); // build.456
Console.WriteLine(version.IsPreRelease);  // True

var a = SemVersion.Parse("1.0.0");
var b = SemVersion.Parse("2.0.0");
Console.WriteLine(a < b);  // True
Console.WriteLine(a >= b); // False
```

### Bumping versions

```csharp
var v = SemVersion.Parse("1.2.3");

Console.WriteLine(v.BumpMajor()); // 2.0.0
Console.WriteLine(v.BumpMinor()); // 1.3.0
Console.WriteLine(v.BumpPatch()); // 1.2.4

Console.WriteLine(v.WithPreRelease("rc.1")); // 1.2.3-rc.1
Console.WriteLine(v.WithBuildMetadata("20260315")); // 1.2.3+20260315
```

### Range matching

```csharp
var range = SemRange.Parse("^1.2.0");
var version = SemVersion.Parse("1.5.3");

Console.WriteLine(range.IsSatisfied(version)); // True

// Find the highest matching version
var versions = new[]
{
    SemVersion.Parse("1.0.0"),
    SemVersion.Parse("1.2.5"),
    SemVersion.Parse("1.9.0"),
    SemVersion.Parse("2.0.0"),
};

var best = SemRange.MaxSatisfying(versions, SemRange.Parse("^1.0.0"));
Console.WriteLine(best); // 1.9.0
```

### Supported range expressions

| Expression | Meaning |
|------------|---------|
| `>=1.0.0 <2.0.0` | Comparator set |
| `^1.2.3` | Compatible with 1.2.3 (>=1.2.3 <2.0.0) |
| `~1.2.3` | Approximately 1.2.3 (>=1.2.3 <1.3.0) |
| `1.x`, `1.2.x` | Any matching version |
| `*` | Any version |
| `1.0.0 - 2.0.0` | Hyphen range (inclusive) |
| `>=1.0.0 \|\| >=2.0.0` | Union of ranges |

### JSON serialization

```csharp
using System.Text.Json;

var options = new JsonSerializerOptions();
options.Converters.Add(new SemVersionJsonConverter());

var json = JsonSerializer.Serialize(SemVersion.Parse("1.2.3"), options);
// "1.2.3"

var version = JsonSerializer.Deserialize<SemVersion>(json, options);
// SemVersion { Major = 1, Minor = 2, Patch = 3 }
```

## API

### `SemVersion`

| Member | Description |
|--------|-------------|
| `int Major` | Major version number |
| `int Minor` | Minor version number |
| `int Patch` | Patch version number |
| `string? PreRelease` | Pre-release label, or null |
| `string? BuildMetadata` | Build metadata, or null |
| `bool IsPreRelease` | Whether a pre-release label is present |
| `Parse(string)` | Parse a semver string (throws on failure) |
| `TryParse(string, out SemVersion?)` | Parse without throwing |
| `BumpMajor()` | Increment major, reset minor and patch |
| `BumpMinor()` | Increment minor, reset patch |
| `BumpPatch()` | Increment patch |
| `WithPreRelease(string)` | Set pre-release label |
| `WithBuildMetadata(string)` | Set build metadata |
| `==`, `!=`, `<`, `>`, `<=`, `>=` | Comparison operators |

### `SemRange`

| Member | Description |
|--------|-------------|
| `Parse(string)` | Parse a range expression |
| `IsSatisfied(SemVersion)` | Check if a version matches the range |
| `MaxSatisfying(IEnumerable<SemVersion>, SemRange)` | Find the highest matching version |

### `SemVersionJsonConverter`

| Member | Description |
|--------|-------------|
| `Read(...)` | Deserialize a JSON string to `SemVersion` |
| `Write(...)` | Serialize a `SemVersion` to a JSON string |

## License

MIT
