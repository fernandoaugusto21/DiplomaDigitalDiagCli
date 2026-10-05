using DiplomaDiagnosticCli.Tests.Support;

namespace DiplomaDiagnosticCli.Tests;

public class DocumentAndSignatureTests
{
    [Fact]
    public void HttpsNamespace_IsReportedAsGovBrRewrite()
    {
        string xml = Fixtures.SignHistorico(Fixtures.Historico()).ToXmlString()
            .Replace(Fixtures.Mec, "https://portal.mec.gov.br/diplomadigital/arquivos-em-xsd");

        var result = Fixtures.Analyze(xml);

        Assert.Equal("historico_final", result.Metadata.DocumentType);
        Assert.Contains("XSD_NAMESPACE_HTTPS_REWRITE", result.Diagnostics.Errors.Select(e => e.Id));
        Assert.False(result.SchemaValidation.Validated);
        Assert.Null(result.SchemaValidation.Valid);
    }

    [Fact]
    public void OtherLayoutVersion_IsDetected_NotValidated()
    {
        var doc = Fixtures.SignHistorico(Fixtures.Historico(edit: (d, ns) =>
            ((System.Xml.XmlElement)d.SelectSingleNode("//m:infHistoricoEscolar", ns)!).SetAttribute("versao", "1.04")));

        var result = Fixtures.Analyze(doc);

        Assert.Equal("1.04", result.Metadata.LayoutVersion);
        Assert.False(result.SchemaValidation.Validated);
        Assert.Null(result.SchemaValidation.Valid);
        Assert.Contains("XSD_VERSION_UNSUPPORTED", result.Ids());
    }

    [Fact]
    public void OtherMecDocument_IsUnsupported()
    {
        var result = Fixtures.Analyze($"<DocumentacaoAcademicaRegistro xmlns=\"{Fixtures.Mec}\"/>");

        Assert.Equal("unsupported", result.Metadata.DocumentType);
        Assert.Contains("DOCUMENT_TYPE_UNSUPPORTED", result.Ids());
    }

    [Fact]
    public void ForeignXPathTransform_IsNotExecuted()
    {
        var doc = Fixtures.Historico();
        FixtureSigner.Sign(doc, doc.DocumentElement!, "", FixtureSigner.SecretariaECpf.Value, Fixtures.T0,
            xpath: "true()");

        var result = Fixtures.Analyze(doc);

        var sig = Assert.Single(result.Signatures);
        Assert.False(sig.XmldsigValid);
        Assert.StartsWith("unsupported XPath transform", sig.FailureReason);
        Assert.Contains("SIG_TRANSFORM_UNSUPPORTED", result.Ids());
    }

    [Fact]
    public void SigningTimeOutsideCertificateValidity_IsError()
    {
        var doc = Fixtures.Historico();
        FixtureSigner.Sign(doc, doc.DocumentElement!, "", FixtureSigner.SecretariaECpf.Value, new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Contains("SIG_CERT_NOT_VALID_AT_SIGNING", Fixtures.Analyze(doc).Ids());
    }

    [Fact]
    public void Signatures_OutOfChronologicalOrder_IsWarning()
    {
        var doc = Fixtures.Historico();
        FixtureSigner.Sign(doc, doc.DocumentElement!, "", FixtureSigner.SecretariaECpf.Value, Fixtures.T0);
        FixtureSigner.Sign(doc, doc.DocumentElement!, "", FixtureSigner.IesECnpj.Value, Fixtures.T0.AddDays(-1), "PA_AD_RA_v2_4");

        Assert.Contains("SIG_ORDER_INVALID", Fixtures.Analyze(doc).Ids());
    }

    [Fact]
    public void UnsignedDocument_IsError()
    {
        var result = Fixtures.Analyze(Fixtures.Historico());

        Assert.Contains("SIG_NONE", result.Ids());
        Assert.False(result.SchemaValidation.Valid); // the XSD requires ds:Signature
    }
}
