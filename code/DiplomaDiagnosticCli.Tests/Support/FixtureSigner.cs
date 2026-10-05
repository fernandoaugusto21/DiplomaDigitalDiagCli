using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace DiplomaDiagnosticCli.Tests.Support;

/// <summary>
/// Signs synthetic fixtures the way MEC/ICP-Brasil signers do (XAdES with SignedProperties; data
/// reference with enveloped-signature + XPath not(ancestor-or-self::ds:Signature) + exc-c14n), but
/// with self-signed TEST certificates — no real e-CPF/e-CNPJ is ever involved.
/// </summary>
public static class FixtureSigner
{
    public const string Ds = "http://www.w3.org/2000/09/xmldsig#";
    public const string Xades = "http://uri.etsi.org/01903/v1.3.2#";

    public static readonly DateTimeOffset CertNotBefore = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset CertNotAfter = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // CPF/CNPJ with valid check digits; names are fictitious.
    public static readonly Lazy<X509Certificate2> SecretariaECpf =
        new(() => CreateCertificate("CN=MARIA SECRETARIA FICTICIA:39053344705, OU=e-CPF A3 TESTE, O=ICP-Brasil TESTE, C=BR"));
    public static readonly Lazy<X509Certificate2> IesECnpj =
        new(() => CreateCertificate("CN=FACULDADE FICTICIA DE TESTES:11222333000181, OU=e-CNPJ A1 TESTE, O=ICP-Brasil TESTE, C=BR"));

    public static X509Certificate2 CreateCertificate(string subject, DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(notBefore ?? CertNotBefore, notAfter ?? CertNotAfter);
    }

    public static XmlDocument Load(string fixtureName)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName));
        return doc;
    }

    /// <summary>
    /// Signs <paramref name="referenceUri"/> ("" = whole document, "#id" = element) and appends the
    /// ds:Signature as last child of <paramref name="parent"/>.
    /// </summary>
    public static XmlElement Sign(XmlDocument doc, XmlElement parent, string referenceUri, X509Certificate2 cert,
        DateTimeOffset signingTime, string policy = "PA_AD_RB_v2_4", string policyOid = "2.16.76.1.7.1.6.2.4",
        string xpath = "not(ancestor-or-self::ds:Signature)")
    {
        string sigId = "id-" + Guid.NewGuid().ToString("N");
        string propsId = "xades-" + sigId;

        // QualifyingProperties built in the target document but not attached yet; the reference to
        // SignedProperties is resolved through FixtureSignedXml.GetIdElement.
        var qualifying = BuildQualifyingProperties(doc, sigId, propsId, signingTime, policy, policyOid);
        var signedProperties = (XmlElement)qualifying.FirstChild!;

        using var rsa = cert.GetRSAPrivateKey()!;
        var signedXml = new FixtureSignedXml(doc, propsId, signedProperties) { SigningKey = rsa };
        signedXml.Signature.Id = sigId;
        signedXml.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;
        signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;

        var data = new Reference(referenceUri) { DigestMethod = SignedXml.XmlDsigSHA256Url };
        data.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        data.AddTransform(XPathTransform(xpath));
        data.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(data);

        var props = new Reference("#" + propsId) { DigestMethod = SignedXml.XmlDsigSHA256Url, Type = "http://uri.etsi.org/01903#SignedProperties" };
        props.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(props);

        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(cert));
        signedXml.KeyInfo = keyInfo;

        signedXml.AddObject(new DataObject { Data = new[] { qualifying }.Select(q => (XmlNode)q).ToNodeList() });
        signedXml.ComputeSignature();

        var signature = signedXml.GetXml();
        return (XmlElement)parent.AppendChild(doc.ImportNode(signature, deep: true))!;
    }

    private static XmlDsigXPathTransform XPathTransform(string expression)
    {
        var holder = new XmlDocument();
        var xpath = holder.CreateElement("ds", "XPath", Ds);
        xpath.SetAttribute("xmlns:ds", Ds);
        xpath.InnerText = expression;
        var transform = new XmlDsigXPathTransform();
        transform.LoadInnerXml(new[] { (XmlNode)xpath }.ToNodeList());
        return transform;
    }

    private static XmlElement BuildQualifyingProperties(XmlDocument doc, string sigId, string propsId,
        DateTimeOffset signingTime, string policy, string policyOid)
    {
        var q = doc.CreateElement("xades", "QualifyingProperties", Xades);
        q.SetAttribute("Target", "#" + sigId);
        var sp = Append(doc, q, "SignedProperties");
        sp.SetAttribute("Id", propsId);
        var ssp = Append(doc, sp, "SignedSignatureProperties");
        Append(doc, ssp, "SigningTime").InnerText = signingTime.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var spi = Append(doc, ssp, "SignaturePolicyIdentifier");
        var spid = Append(doc, spi, "SignaturePolicyId");
        var sigPolicyId = Append(doc, spid, "SigPolicyId");
        Append(doc, sigPolicyId, "Identifier").InnerText = "urn:oid:" + policyOid;
        var qualifiers = Append(doc, spid, "SigPolicyQualifiers");
        var qualifier = Append(doc, qualifiers, "SigPolicyQualifier");
        Append(doc, qualifier, "SPURI").InnerText = $"http://politicas.icpbrasil.gov.br/{policy}.xml";
        return q;
    }

    private static XmlElement Append(XmlDocument doc, XmlElement parent, string localName) =>
        (XmlElement)parent.AppendChild(doc.CreateElement("xades", localName, Xades))!;

    private static XmlNodeList ToNodeList(this IEnumerable<XmlNode> nodes) => new ListNodeList(nodes.ToList());

    private sealed class ListNodeList(List<XmlNode> nodes) : XmlNodeList
    {
        public override int Count => nodes.Count;
        public override XmlNode? Item(int index) => index >= 0 && index < nodes.Count ? nodes[index] : null;
        public override System.Collections.IEnumerator GetEnumerator() => nodes.GetEnumerator();
    }

    private sealed class FixtureSignedXml(XmlDocument doc, string propsId, XmlElement signedProperties) : SignedXml(doc)
    {
        public override XmlElement? GetIdElement(XmlDocument? document, string idValue)
        {
            if (idValue == propsId)
                return signedProperties;
            return base.GetIdElement(document, idValue)
                   ?? document?.SelectSingleNode($"//*[@id='{idValue}']") as XmlElement;
        }
    }

    public static string ToXmlString(this XmlDocument doc)
    {
        using var sw = new Utf8StringWriter();
        doc.Save(sw);
        return sw.ToString();
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override System.Text.Encoding Encoding => new System.Text.UTF8Encoding(false);
    }
}
