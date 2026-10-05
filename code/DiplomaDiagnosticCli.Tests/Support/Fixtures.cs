using System.Xml;
using DiplomaDiagnosticCli.Commands;
using DiplomaDiagnosticCli.Documents;
using DiplomaDiagnosticCli.Models;

namespace DiplomaDiagnosticCli.Tests.Support;

/// <summary>Builds signed fixture variants and runs the analysis pipeline on them.</summary>
public static class Fixtures
{
    public const string Mec = "http://portal.mec.gov.br/diplomadigital/arquivos-em-xsd";
    public static readonly DateOnly Today = new(2026, 10, 1);
    public static readonly DateTimeOffset T0 = new(2023, 2, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Unsigned Histórico, optionally retargeted to another root (Parcial / SegundaViaNatoFisico) and edited.</summary>
    public static XmlDocument Historico(string root = "DocumentoHistoricoEscolarFinal", Action<XmlDocument, XmlNamespaceManager>? edit = null)
    {
        var doc = FixtureSigner.Load("historico-final.xml");
        if (root != "DocumentoHistoricoEscolarFinal")
            doc = RenameRoot(doc, root);
        edit?.Invoke(doc, Ns(doc));
        return doc;
    }

    /// <summary>Secretaria (e-CPF, AD-RB) then IES (e-CNPJ, AD-RA), both over the whole document.</summary>
    public static XmlDocument SignHistorico(XmlDocument doc, bool institutional = true)
    {
        FixtureSigner.Sign(doc, doc.DocumentElement!, "", FixtureSigner.SecretariaECpf.Value, T0);
        if (institutional)
            FixtureSigner.Sign(doc, doc.DocumentElement!, "", FixtureSigner.IesECnpj.Value, T0.AddMinutes(5), "PA_AD_RA_v2_4", "2.16.76.1.7.1.10.2.4");
        return doc;
    }

    public static XmlDocument Diploma(Action<XmlDocument, XmlNamespaceManager>? edit = null)
    {
        var doc = FixtureSigner.Load("diploma.xml");
        edit?.Invoke(doc, Ns(doc));
        return doc;
    }

    /// <summary>DadosDiploma: dirigente (e-CPF) + IES emissora (e-CNPJ); DadosRegistro: registrador (e-CPF);
    /// envelope: IES registradora (e-CNPJ, AD-RA). <paramref name="envelopeTime"/> overrides the envelope SigningTime.</summary>
    public static XmlDocument SignDiploma(XmlDocument doc, DateTimeOffset? envelopeTime = null)
    {
        var ns = Ns(doc);
        var dados = (XmlElement)doc.SelectSingleNode("//m:DadosDiploma", ns)!;
        var registro = (XmlElement)doc.SelectSingleNode("//m:DadosRegistro", ns)!;
        FixtureSigner.Sign(doc, dados, "#" + dados.GetAttribute("id"), FixtureSigner.SecretariaECpf.Value, T0);
        FixtureSigner.Sign(doc, dados, "#" + dados.GetAttribute("id"), FixtureSigner.IesECnpj.Value, T0.AddMinutes(1));
        FixtureSigner.Sign(doc, registro, "#" + registro.GetAttribute("id"), FixtureSigner.SecretariaECpf.Value, T0.AddMinutes(2));
        FixtureSigner.Sign(doc, doc.DocumentElement!, "", FixtureSigner.IesECnpj.Value, envelopeTime ?? T0.AddMinutes(3), "PA_AD_RA_v2_4", "2.16.76.1.7.1.10.2.4");
        return doc;
    }

    public static DiagnosticResult Analyze(XmlDocument doc, bool mask = true) =>
        Analyze(doc.ToXmlString(), mask);

    public static DiagnosticResult Analyze(string xml, bool mask = true) =>
        DiagnoseCommand.Analyze(MecDocument.LoadXml(xml), "fixture.xml", mask, today: Today);

    public static IEnumerable<string> Ids(this DiagnosticResult result) =>
        result.Diagnostics.Errors.Concat(result.Diagnostics.Warnings).Concat(result.Diagnostics.Info).Select(f => f.Id);

    public static XmlNamespaceManager Ns(XmlDocument doc)
    {
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("m", Mec);
        ns.AddNamespace("ds", FixtureSigner.Ds);
        return ns;
    }

    public static void SetText(this XmlDocument doc, XmlNamespaceManager ns, string xpath, string value) =>
        doc.SelectSingleNode(xpath, ns)!.InnerText = value;

    private static XmlDocument RenameRoot(XmlDocument doc, string newRoot)
    {
        var renamed = new XmlDocument { PreserveWhitespace = true };
        var oldRoot = doc.DocumentElement!;
        var root = renamed.CreateElement(newRoot, Mec);
        foreach (XmlNode child in oldRoot.ChildNodes)
            root.AppendChild(renamed.ImportNode(child, deep: true));
        if (doc.FirstChild is XmlDeclaration decl)
            renamed.AppendChild(renamed.ImportNode(decl, deep: true));
        renamed.AppendChild(root);
        return renamed;
    }
}
