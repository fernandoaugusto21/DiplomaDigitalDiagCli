using System.Text.RegularExpressions;
using DiplomaDiagnosticCli.Documents;
using DiplomaDiagnosticCli.Models;

namespace DiplomaDiagnosticCli.Analyzers;

/// <summary>Summary + business rules for the Diploma (diplomado's XML: DadosDiploma + DadosRegistro).</summary>
public sealed partial class DiplomaAnalyzer : IDocumentAnalyzer
{
    public void Analyze(MecDocument document, AnalysisContext context)
    {
        if (document.Kind != DocumentKind.Diploma || document.InfElement is null)
            return;

        // DadosDiploma / DadosDiplomaNSF / DadosDiplomaPorDecisaoJudicial (and the same for DadosRegistro).
        var dados = document.Element("m:*[starts-with(local-name(), 'DadosDiploma')]");
        var registro = document.Element("m:*[starts-with(local-name(), 'DadosRegistro')]");
        var livro = document.Element("m:LivroRegistro", registro);

        var emissora = RuleHelpers.ReadIes(document, document.Element("m:IesEmissora", dados), "IesEmissora", context);
        var registradora = RuleHelpers.ReadIes(document, document.Element("m:IesRegistradora", registro), "IesRegistradora", context);
        string? codigoValidacao = document.Text(".//m:Seguranca/m:CodigoValidacao", registro);

        var summary = new DiplomaSummaryDto
        {
            Diplomado = RuleHelpers.ReadPessoa(document, document.Element("m:Diplomado", dados), "Diplomado", context),
            Curso = RuleHelpers.ReadCurso(document, document.Element("m:DadosCurso", dados)),
            IesEmissora = emissora,
            IesRegistradora = registradora,
            DataConclusao = document.Text("m:DataConclusao", dados),
            DataColacaoGrau = document.Text("m:DataColacaoGrau", livro),
            DataExpedicaoDiploma = document.Text("m:DataExpedicaoDiploma", livro),
            DataRegistroDiploma = document.Text("m:DataRegistroDiploma", livro),
            LivroRegistro = document.Text("m:LivroRegistro", livro),
            CodigoValidacao = codigoValidacao
        };
        context.Document = summary;

        RuleHelpers.ReadPessoa(document, document.Element("m:ResponsavelRegistro", livro), "LivroRegistro/ResponsavelRegistro", context);

        RuleHelpers.CheckChronology(context, "DIP",
            ("Diplomado/DataNascimento", document.Text("m:Diplomado/m:DataNascimento", dados)),
            ("DataConclusao", summary.DataConclusao),
            ("DataColacaoGrau", summary.DataColacaoGrau),
            ("DataExpedicaoDiploma", summary.DataExpedicaoDiploma),
            ("DataRegistroDiploma", summary.DataRegistroDiploma));

        if (codigoValidacao is not null)
        {
            string[] parts = codigoValidacao.Split('.');
            if (parts.Length >= 3 && emissora.CodigoMec is not null && registradora.CodigoMec is not null
                && (parts[0] != emissora.CodigoMec || parts[1] != registradora.CodigoMec))
            {
                context.Error("DIP_CODIGO_VALIDACAO_IES_MISMATCH",
                    $"CodigoValidacao '{codigoValidacao}' should start with '{emissora.CodigoMec}.{registradora.CodigoMec}.' (e-MEC code of emissora, then registradora).",
                    "element::Seguranca/CodigoValidacao");
            }
        }

        CheckIds(document, context, dados, registro);
    }

    /// <summary>infDiploma (VDip+44), DadosDiploma (Dip+44), DadosRegistro (RDip+44) and
    /// IdDocumentacaoAcademica (ReqDip+44) all carry the same 44-digit number.</summary>
    private static void CheckIds(MecDocument document, AnalysisContext context, System.Xml.XmlElement? dados, System.Xml.XmlElement? registro)
    {
        var ids = new List<(string Where, string? Value)>
        {
            ("infDiploma/@id", document.InfElement?.GetAttribute("id")),
            ("DadosDiploma/@id", dados?.GetAttribute("id")),
            ("DadosRegistro/@id", registro?.GetAttribute("id")),
            ("DadosRegistro/IdDocumentacaoAcademica", document.Text("m:IdDocumentacaoAcademica", registro))
        };
        var numbers = ids
            .Where(i => !string.IsNullOrEmpty(i.Value))
            .Select(i => (i.Where, Number: DigitsRegex().Match(i.Value!).Value))
            .ToList();
        if (numbers.Select(n => n.Number).Distinct().Count() > 1)
        {
            context.Error("DIP_ID_MISMATCH",
                "The 44-digit diploma number differs between ids: " + string.Join("; ", numbers.Select(n => $"{n.Where}={n.Number}")),
                "attribute::id",
                "infDiploma, DadosDiploma, DadosRegistro and IdDocumentacaoAcademica must share the same number (VDip/Dip/RDip/ReqDip prefixes).");
        }
    }

    [GeneratedRegex(@"\d+$")]
    private static partial Regex DigitsRegex();
}
