using System.Security.Cryptography.X509Certificates;

namespace DiplomaDiagnosticCli.Signatures;

/// <summary>Reads ICP-Brasil conventions out of a certificate subject.</summary>
public static class CertificateSubject
{
    /// <summary>
    /// ICP-Brasil end-entity CNs are "HOLDER NAME:DOCUMENT" — 11 digits for e-CPF, 14 chars for e-CNPJ.
    /// Accepts a bare CN or a full distinguished name.
    /// </summary>
    public static (string Name, string? Document) SplitCommonName(string subjectOrCn)
    {
        string cn = ExtractCn(subjectOrCn);
        int colon = cn.LastIndexOf(':');
        if (colon > 0)
        {
            string doc = cn[(colon + 1)..].Trim();
            if (doc.Length is 11 or 14 && doc.All(char.IsAsciiLetterOrDigit))
                return (cn[..colon].Trim(), doc);
        }
        return (cn.Trim(), null);
    }

    public static string CertificateType(X509Certificate2 cert)
    {
        var (_, doc) = SplitCommonName(cert.Subject);
        if (cert.Subject.Contains("e-CPF", StringComparison.OrdinalIgnoreCase) || doc is { Length: 11 })
            return "e-CPF";
        if (cert.Subject.Contains("e-CNPJ", StringComparison.OrdinalIgnoreCase) || doc is { Length: 14 })
            return "e-CNPJ";
        return "unknown";
    }

    public static string ExtractCn(string subject)
    {
        foreach (string part in SplitRdns(subject))
        {
            string p = part.Trim();
            if (p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                return p[3..].Trim('"');
        }
        return subject;
    }

    private static IEnumerable<string> SplitRdns(string dn)
    {
        // Minimal DN split honoring quotes ("O=Foo, Inc." style values).
        var current = new System.Text.StringBuilder();
        bool quoted = false;
        foreach (char c in dn)
        {
            if (c == '"') quoted = !quoted;
            if (c == ',' && !quoted)
            {
                yield return current.ToString();
                current.Clear();
                continue;
            }
            current.Append(c);
        }
        yield return current.ToString();
    }
}
