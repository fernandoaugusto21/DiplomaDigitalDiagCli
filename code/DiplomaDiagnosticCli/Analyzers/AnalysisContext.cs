using DiplomaDiagnosticCli.Models;
using DiplomaDiagnosticCli.Privacy;

namespace DiplomaDiagnosticCli.Analyzers;

/// <summary>Shared, mutable state that every <see cref="IDocumentAnalyzer"/> reads from and writes into.</summary>
public sealed class AnalysisContext
{
    public AnalysisContext(PiiRedactor? redactor = null)
    {
        Redactor = redactor ?? new PiiRedactor(enabled: true);
    }

    public PiiRedactor Redactor { get; }
    public object? Document { get; set; }
    public SchemaValidationDto SchemaValidation { get; } = new();
    public List<SignatureDto> Signatures { get; } = [];
    public List<Finding> Errors { get; } = [];
    public List<Finding> Warnings { get; } = [];
    public List<Finding> Info { get; } = [];

    /// <summary>Reference date for "date in the future" checks; injectable for tests.</summary>
    public DateOnly Today { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public Action<string, string> Trace { get; init; } = (_, _) => { };

    public void Error(string id, string message, string? element = null, string? suggestion = null) =>
        Errors.Add(Make(id, "ERROR", message, element, suggestion));

    public void Warning(string id, string message, string? element = null, string? suggestion = null) =>
        Warnings.Add(Make(id, "WARNING", message, element, suggestion));

    public void Information(string id, string message, string? element = null, string? suggestion = null) =>
        Info.Add(Make(id, "INFO", message, element, suggestion));

    public IEnumerable<Finding> AllFindings => Errors.Concat(Warnings).Concat(Info);

    private Finding Make(string id, string severity, string message, string? element, string? suggestion) => new()
    {
        Id = id,
        Severity = severity,
        Message = Redactor.Scrub(message),
        AffectedElement = element,
        Suggestion = suggestion
    };
}
