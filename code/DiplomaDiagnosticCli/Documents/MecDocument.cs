using System.Xml;

namespace DiplomaDiagnosticCli.Documents;

public enum DocumentKind
{
    Unsupported,
    Diploma,
    HistoricoParcial,
    HistoricoFinal,
    HistoricoSegundaViaNatoFisico
}

/// <summary>
/// A loaded MEC XML plus what the sniffer found out about it: which document it is, which layout
/// version it declares and which namespace it uses. All XPath helpers are namespace-aware and use
/// the prefix <c>m:</c> for the MEC namespace and <c>ds:</c> for XMLDSig.
/// </summary>
public sealed class MecDocument
{
    public const string MecNamespace = "http://portal.mec.gov.br/diplomadigital/arquivos-em-xsd";
    /// <summary>What the MEC namespace becomes after the gov.br portal's blind http→https rewrite of the XSD files.</summary>
    public const string MecNamespaceHttps = "https://portal.mec.gov.br/diplomadigital/arquivos-em-xsd";
    public const string DsNamespace = "http://www.w3.org/2000/09/xmldsig#";
    public const string XadesNamespace = "http://uri.etsi.org/01903/v1.3.2#";

    public XmlDocument Xml { get; }
    public XmlNamespaceManager Ns { get; }
    public DocumentKind Kind { get; }
    public string RootName { get; }
    public string RootNamespace { get; }
    public XmlElement? InfElement { get; }
    public string? LayoutVersion { get; }

    private MecDocument(XmlDocument xml)
    {
        Xml = xml;
        var root = xml.DocumentElement ?? throw new XmlException("Document has no root element");
        RootName = root.LocalName;
        RootNamespace = root.NamespaceURI;

        Ns = new XmlNamespaceManager(xml.NameTable);
        // Bind m: to whatever MEC namespace variant the file actually uses, so the analyzers keep
        // working (and can report it) even when a file was produced against the rewritten XSD.
        Ns.AddNamespace("m", RootNamespace == MecNamespaceHttps ? MecNamespaceHttps : MecNamespace);
        Ns.AddNamespace("ds", DsNamespace);
        Ns.AddNamespace("xades", XadesNamespace);

        Kind = IsMecNamespace ? RootName switch
        {
            "Diploma" => DocumentKind.Diploma,
            "DocumentoHistoricoEscolarParcial" => DocumentKind.HistoricoParcial,
            "DocumentoHistoricoEscolarFinal" => DocumentKind.HistoricoFinal,
            "DocumentoHistoricoEscolarSegundaViaNatoFisico" => DocumentKind.HistoricoSegundaViaNatoFisico,
            _ => DocumentKind.Unsupported
        } : DocumentKind.Unsupported;

        InfElement = Kind switch
        {
            DocumentKind.Diploma => root.SelectSingleNode("m:infDiploma", Ns) as XmlElement,
            DocumentKind.Unsupported => null,
            _ => root.SelectSingleNode("m:infHistoricoEscolar", Ns) as XmlElement
        };
        LayoutVersion = InfElement?.GetAttributeNode("versao")?.Value;
    }

    public bool IsMecNamespace => RootNamespace is MecNamespace or MecNamespaceHttps;
    public bool UsesHttpsNamespace => RootNamespace == MecNamespaceHttps;
    public bool IsHistorico => Kind is DocumentKind.HistoricoParcial or DocumentKind.HistoricoFinal or DocumentKind.HistoricoSegundaViaNatoFisico;

    public string KindLabel => Kind switch
    {
        DocumentKind.Diploma => "diploma",
        DocumentKind.HistoricoParcial => "historico_parcial",
        DocumentKind.HistoricoFinal => "historico_final",
        DocumentKind.HistoricoSegundaViaNatoFisico => "historico_segunda_via_nato_fisico",
        _ => "unsupported"
    };

    /// <summary>The file exactly as read, so XSD validation can report line numbers of the original
    /// and honor its declared encoding.</summary>
    public byte[] RawBytes { get; private init; } = [];

    public static MecDocument Load(string path) => LoadBytes(File.ReadAllBytes(path));

    public static MecDocument LoadXml(string content) => LoadBytes(System.Text.Encoding.UTF8.GetBytes(content));

    /// <summary>Loads with DTDs prohibited and no resolver: the input is untrusted.</summary>
    public static MecDocument LoadBytes(byte[] bytes)
    {
        var xml = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        using var reader = XmlReader.Create(new MemoryStream(bytes), SafeReaderSettings());
        xml.Load(reader);
        return new MecDocument(xml) { RawBytes = bytes };
    }

    public static XmlReaderSettings SafeReaderSettings() =>
        new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

    /// <summary>Trimmed inner text of the first node matching <paramref name="xpath"/> (relative to <paramref name="context"/> or the inf* node).</summary>
    public string? Text(string xpath, XmlNode? context = null)
    {
        var node = (context ?? InfElement ?? (XmlNode)Xml).SelectSingleNode(xpath, Ns);
        string? text = node?.InnerText.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    public XmlElement? Element(string xpath, XmlNode? context = null) =>
        (context ?? InfElement ?? (XmlNode)Xml).SelectSingleNode(xpath, Ns) as XmlElement;

    public IEnumerable<XmlElement> Elements(string xpath, XmlNode? context = null) =>
        (context ?? InfElement ?? (XmlNode)Xml).SelectNodes(xpath, Ns)!.OfType<XmlElement>();

    public static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", out var d) ? d : null;
}
