namespace DiplomaDiagnosticCli.Privacy;

/// <summary>
/// Masks personal data (LGPD) before it reaches the JSON — the output is meant to be pasted into an
/// LLM, so student data must not leak by default. Every value masked through this class is also
/// remembered, so free text coming from elsewhere (XSD validator messages quote offending values
/// verbatim) can be scrubbed with <see cref="Scrub"/>.
/// </summary>
public sealed class PiiRedactor(bool enabled)
{
    private readonly Dictionary<string, string> _replacements = new(StringComparer.Ordinal);

    public bool Enabled { get; } = enabled;

    /// <summary>CPF → ***.456.789-** (the middle six digits, the same form gov.br uses).</summary>
    public string? Cpf(string? cpf)
    {
        if (!Enabled || string.IsNullOrEmpty(cpf))
            return cpf;
        string masked = cpf.Length == 11 && cpf.All(char.IsAsciiDigit)
            ? $"***.{cpf[3..6]}.{cpf[6..9]}-**"
            : new string('*', cpf.Length);
        return Remember(cpf, masked);
    }

    /// <summary>Name → initials ("Maria da Silva Souza" → "M. S. S."); particles (da, de, dos...) are dropped.</summary>
    public string? Name(string? name)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(name))
            return name;
        string[] particles = ["da", "de", "do", "das", "dos", "e"];
        var initials = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !particles.Contains(p.ToLowerInvariant()))
            .Select(p => char.ToUpperInvariant(p[0]) + ".");
        return Remember(name, string.Join(' ', initials));
    }

    /// <summary>Birth date → year only.</summary>
    public string? BirthDate(string? date)
    {
        if (!Enabled || string.IsNullOrEmpty(date))
            return date;
        return Remember(date, date.Length >= 4 ? date[..4] + "-**-**" : "****");
    }

    /// <summary>Remembers a value that must never appear verbatim (e.g. RG number) without exposing it anywhere.</summary>
    public void Register(string? value)
    {
        if (Enabled && !string.IsNullOrEmpty(value) && value.Length >= 3)
            Remember(value, new string('*', value.Length));
    }

    /// <summary>Replaces every remembered personal value found in free text (longest first).</summary>
    public string Scrub(string text)
    {
        if (!Enabled)
            return text;
        foreach (var (original, masked) in _replacements.OrderByDescending(r => r.Key.Length))
            text = text.Replace(original, masked, StringComparison.Ordinal);
        return text;
    }

    private string Remember(string original, string masked)
    {
        _replacements[original] = masked;
        return masked;
    }
}
