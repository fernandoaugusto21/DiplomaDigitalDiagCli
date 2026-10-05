using DiplomaDiagnosticCli.Models;
using DiplomaDiagnosticCli.Tests.Support;

namespace DiplomaDiagnosticCli.Tests;

public class DiplomaTests
{
    [Fact]
    public void SignedFixture_IsXsdValid_FourSignaturesVerify_NoFindings()
    {
        var result = Fixtures.Analyze(Fixtures.SignDiploma(Fixtures.Diploma()));

        Assert.Equal("diploma", result.Metadata.DocumentType);
        Assert.True(result.SchemaValidation.Valid, string.Join("\n", result.SchemaValidation.Errors.Select(e => e.Message)));
        Assert.Equal(["DadosDiploma", "DadosDiploma", "DadosRegistro", "Diploma"], result.Signatures.Select(s => s.ParentElement));
        Assert.All(result.Signatures, s => Assert.True(s.XmldsigValid, s.FailureReason));
        Assert.Empty(result.Diagnostics.Errors);
        Assert.Empty(result.Diagnostics.Warnings);

        var summary = Assert.IsType<DiplomaSummaryDto>(result.Document);
        Assert.Equal("2023-02-03", summary.DataRegistroDiploma);
        Assert.Equal("9999.9999.0a1b2c3d4e5f", summary.CodigoValidacao);
    }

    [Fact]
    public void TamperingDadosRegistro_BreaksOnlyRegistroAndEnvelope()
    {
        string xml = Fixtures.SignDiploma(Fixtures.Diploma()).ToXmlString()
            .Replace("<NumeroRegistro>2023-0001</NumeroRegistro>", "<NumeroRegistro>2023-0999</NumeroRegistro>");

        var result = Fixtures.Analyze(xml);

        Assert.Equal([true, true, false, false], result.Signatures.Select(s => s.XmldsigValid));
    }

    [Fact]
    public void EnvelopeSignedBeforeRegistro_IsOrderError()
    {
        var doc = Fixtures.SignDiploma(Fixtures.Diploma(), envelopeTime: Fixtures.T0.AddMinutes(-10));

        var result = Fixtures.Analyze(doc);

        Assert.Contains("SIG_ORDER_INVALID", result.Diagnostics.Errors.Select(e => e.Id));
    }

    [Fact]
    public void MismatchedDiplomaNumber_IsIdError()
    {
        var doc = Fixtures.SignDiploma(Fixtures.Diploma((d, ns) =>
            d.SetText(ns, "//m:IdDocumentacaoAcademica", "ReqDip99999999999999999999999999999999999999999999")));

        Assert.Contains("DIP_ID_MISMATCH", Fixtures.Analyze(doc).Ids());
    }

    [Fact]
    public void ValidationCodeNotMatchingIes_IsError()
    {
        var doc = Fixtures.SignDiploma(Fixtures.Diploma((d, ns) =>
            d.SetText(ns, "//m:Seguranca/m:CodigoValidacao", "9999.1111.0a1b2c3d4e5f")));

        Assert.Contains("DIP_CODIGO_VALIDACAO_IES_MISMATCH", Fixtures.Analyze(doc).Ids());
    }

    [Fact]
    public void RegistroBeforeExpedicao_IsDateOrderError()
    {
        var doc = Fixtures.SignDiploma(Fixtures.Diploma((d, ns) =>
            d.SetText(ns, "//m:DataRegistroDiploma", "2023-01-30")));

        Assert.Contains("DIP_DATE_ORDER", Fixtures.Analyze(doc).Ids());
    }
}
