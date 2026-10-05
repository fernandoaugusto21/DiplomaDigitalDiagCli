using System.Text.Json.Serialization;

namespace DiplomaDiagnosticCli.Models;

public sealed class Finding
{
    public string Id { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("affected_element")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AffectedElement { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Suggestion { get; init; }
}
