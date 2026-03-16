using System.Text.Json;
using System.Text.Json.Serialization;

namespace Philiprehberger.Semver;

/// <summary>
/// A <see cref="JsonConverter{T}"/> for <see cref="SemVersion"/> that serializes
/// and deserializes semantic versions as plain strings (e.g., "1.2.3-beta.1+build.456").
/// </summary>
/// <remarks>
/// Register this converter on <see cref="JsonSerializerOptions.Converters"/> or
/// annotate a property with <c>[JsonConverter(typeof(SemVersionJsonConverter))]</c>.
/// </remarks>
public sealed class SemVersionJsonConverter : JsonConverter<SemVersion>
{
    /// <summary>
    /// Reads a JSON string and converts it to a <see cref="SemVersion"/>.
    /// </summary>
    /// <param name="reader">The JSON reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">The serializer options.</param>
    /// <returns>The parsed <see cref="SemVersion"/>.</returns>
    /// <exception cref="JsonException">Thrown when the JSON token is not a string or the value is not a valid semver string.</exception>
    public override SemVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected a string value for SemVersion.");

        var value = reader.GetString();
        if (value is null || !SemVersion.TryParse(value, out var version))
            throw new JsonException($"Invalid semantic version: '{value}'");

        return version.Value;
    }

    /// <summary>
    /// Writes a <see cref="SemVersion"/> as a JSON string.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The version to write.</param>
    /// <param name="options">The serializer options.</param>
    public override void Write(Utf8JsonWriter writer, SemVersion value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
