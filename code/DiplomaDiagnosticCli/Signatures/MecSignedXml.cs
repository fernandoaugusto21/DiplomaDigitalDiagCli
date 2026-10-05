using System.Security.Cryptography.Xml;
using System.Xml;
using DiplomaDiagnosticCli.Documents;

namespace DiplomaDiagnosticCli.Signatures;

/// <summary>
/// <see cref="SignedXml"/> adjusted to the conventions of MEC diploma signatures:
/// <list type="bullet">
/// <item>MEC nodes are identified by a lowercase <c>id</c> attribute (DadosDiploma, DadosRegistro,
/// infDiploma); <see cref="SignedXml.GetIdElement"/> only knows Id/ID/id via DTD-less lookup in some
/// cases, so lookups fall back to an explicit search over <c>id</c>, <c>Id</c> and <c>ID</c>.</item>
/// <item>Nested signatures are excluded from each digest with the XPath transform
/// <c>not(ancestor-or-self::ds:Signature)</c>. .NET treats XPath transforms as unsafe and rejects
/// them; this class allows the XPath transform, and <see cref="Analyzers.SignatureAnalyzer"/> only lets it run when the
/// expression is exactly that one.</item>
/// </list>
/// </summary>
internal sealed class MecSignedXml : SignedXml
{
    public MecSignedXml(XmlDocument document) : base(document)
    {
        SafeCanonicalizationMethods.Add(XmlDsigXPathTransformUrl);
    }

    public override XmlElement? GetIdElement(XmlDocument? document, string idValue)
    {
        var element = base.GetIdElement(document, idValue);
        if (element is not null || document is null || idValue.Contains('\''))
            return element;
        return document.SelectSingleNode($"//*[@id='{idValue}' or @Id='{idValue}' or @ID='{idValue}']") as XmlElement;
    }

    /// <summary>The only XPath expression accepted in a transform (the MEC/ICP-Brasil signer convention).</summary>
    public const string AllowedXPath = "not(ancestor-or-self::ds:Signature)";

    /// <summary>
    /// Builds a private copy of the document fit for verification. The .NET XPath transform only
    /// reads namespace declarations placed directly on the &lt;ds:XPath&gt; element, not inherited
    /// ones, so <c>ds</c> is declared there. That declaration is redundant under exclusive c14n
    /// (the prefix is already in scope on SignedInfo), so the canonical form — and therefore the
    /// signature — is unchanged.
    /// </summary>
    public static XmlDocument PrepareForVerification(MecDocument document)
    {
        var copy = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        using (var reader = XmlReader.Create(new MemoryStream(document.RawBytes), MecDocument.SafeReaderSettings()))
            copy.Load(reader);

        var ns = new XmlNamespaceManager(copy.NameTable);
        ns.AddNamespace("ds", MecDocument.DsNamespace);
        foreach (XmlElement xpath in copy.SelectNodes("//ds:Transform/ds:XPath", ns)!)
        {
            if (!xpath.HasAttribute("xmlns:ds"))
                xpath.SetAttribute("xmlns:ds", MecDocument.DsNamespace);
        }
        return copy;
    }
}
