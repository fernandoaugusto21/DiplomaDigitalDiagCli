using DiplomaDiagnosticCli.Documents;
using DiplomaDiagnosticCli.Signatures;

namespace DiplomaDiagnosticCli.Analyzers;

/// <summary>
/// Runs first: registers every personal value in the document with the redactor, so that any text
/// produced later (finding messages, XSD validator messages that quote offending values) is scrubbed
/// even if the analyzer that produced it never masked the value itself.
/// </summary>
public sealed class PiiCollector : IDocumentAnalyzer
{
    // Elements whose <Nome> is a natural person (IES/Mantenedora names are public and stay visible).
    private static readonly string[] PersonParents =
        ["Diplomado", "Aluno", "Docente", "ResponsavelRegistro", "Assinante", "Concedente"];

    public void Analyze(MecDocument document, AnalysisContext context)
    {
        var redactor = context.Redactor;
        if (!redactor.Enabled)
            return;

        var root = document.Xml.DocumentElement!;
        foreach (var cpf in document.Elements(".//m:CPF", root))
            redactor.Cpf(cpf.InnerText.Trim());

        foreach (var nome in document.Elements(".//m:Nome | .//m:NomeSocial", root))
            if (nome.ParentNode is { LocalName: var parent } && PersonParents.Contains(parent))
                redactor.Name(nome.InnerText.Trim());

        foreach (var date in document.Elements(".//m:DataNascimento", root))
            redactor.BirthDate(date.InnerText.Trim());

        foreach (var rg in document.Elements(".//m:RG/m:Numero | .//m:OutroDocumentoIdentificacao/*", root))
            redactor.Register(rg.InnerText.Trim());

        // Signer names/CPFs live in X509SubjectName (and inside the certificate itself).
        foreach (var subject in document.Elements(".//ds:X509SubjectName", root))
        {
            var (name, docNumber) = CertificateSubject.SplitCommonName(subject.InnerText);
            if (docNumber is { Length: 11 })
            {
                redactor.Name(name);
                redactor.Cpf(docNumber);
            }
        }
    }
}
