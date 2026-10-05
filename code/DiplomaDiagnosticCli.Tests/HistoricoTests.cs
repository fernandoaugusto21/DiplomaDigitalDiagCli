using DiplomaDiagnosticCli.Models;
using DiplomaDiagnosticCli.Tests.Support;

namespace DiplomaDiagnosticCli.Tests;

public class HistoricoTests
{
    [Theory]
    [InlineData("DocumentoHistoricoEscolarFinal", "historico_final")]
    [InlineData("DocumentoHistoricoEscolarParcial", "historico_parcial")]
    [InlineData("DocumentoHistoricoEscolarSegundaViaNatoFisico", "historico_segunda_via_nato_fisico")]
    public void SignedFixture_IsXsdValid_SignaturesVerify_NoFindings(string root, string expectedType)
    {
        // A partial histórico belongs to a student still enrolled.
        var doc = Fixtures.SignHistorico(root == "DocumentoHistoricoEscolarParcial"
            ? Fixtures.Historico(root, StillEnrolled)
            : Fixtures.Historico(root));

        var result = Fixtures.Analyze(doc);

        Assert.Equal(expectedType, result.Metadata.DocumentType);
        Assert.True(result.SchemaValidation.Validated);
        Assert.True(result.SchemaValidation.Valid, string.Join("\n", result.SchemaValidation.Errors.Select(e => e.Message)));
        Assert.Equal(2, result.Signatures.Count);
        Assert.All(result.Signatures, s => Assert.True(s.XmldsigValid, s.FailureReason));
        Assert.Empty(result.Diagnostics.Errors);
        Assert.Empty(result.Diagnostics.Warnings);
    }

    [Fact]
    public void Summary_SumsApprovedWorkload_AndReadsSituacao()
    {
        var result = Fixtures.Analyze(Fixtures.SignHistorico(Fixtures.Historico()));
        var summary = Assert.IsType<HistoricoSummaryDto>(result.Document);

        Assert.Equal("Formado", summary.SituacaoAtual);
        Assert.Equal(3, summary.Elementos.DisciplinasAprovadas);
        var soma = Assert.Single(summary.CargaHorariaAprovadaSomada);
        Assert.Equal(("HoraRelogio", 4000m), (soma.Unidade, soma.Valor));
        Assert.Equal("2.16.76.1.7.1.10.2.4", result.Signatures[1].Policy!.Oid);
        Assert.Equal("PA_AD_RA_v2_4", result.Signatures[1].Policy!.Name);
        Assert.Equal("e-CNPJ", result.Signatures[1].Signer!.CertificateType);
    }

    [Fact]
    public void ContentChangedAfterSigning_BothSignaturesFail()
    {
        var doc = Fixtures.SignHistorico(Fixtures.Historico());
        string tampered = doc.ToXmlString().Replace("<Nota>7.00</Nota>", "<Nota>9.50</Nota>");

        var result = Fixtures.Analyze(tampered);

        Assert.True(result.SchemaValidation.Valid);
        Assert.All(result.Signatures, s => Assert.False(s.XmldsigValid));
        Assert.Equal(2, result.Diagnostics.Errors.Count(e => e.Id == "SIG_XMLDSIG_INVALID"));
    }

    [Fact]
    public void FormadoWithPendingDisciplina_IsError()
    {
        var doc = Fixtures.SignHistorico(Fixtures.Historico(edit: (d, ns) =>
        {
            var aprovado = d.SelectSingleNode("//m:Disciplina[m:CodigoDisciplina='TST301']/m:Aprovado", ns)!;
            aprovado.ParentNode!.ReplaceChild(d.CreateElement("Pendente", Fixtures.Mec), aprovado);
        }));

        var result = Fixtures.Analyze(doc);

        Assert.Contains("HIST_FORMADO_COM_PENDENCIA", result.Ids());
        // The pending disciplina no longer counts toward the approved sum.
        Assert.Contains("HIST_CARGA_HORARIA_SOMA_DIVERGENTE", result.Ids());
    }

    [Fact]
    public void ParcialWithFormado_IsWarning()
    {
        var result = Fixtures.Analyze(Fixtures.SignHistorico(Fixtures.Historico("DocumentoHistoricoEscolarParcial")));

        Assert.Contains("HIST_PARCIAL_FORMADO", result.Ids());
    }

    [Fact]
    public void FinalWithoutGraduation_IsOnlyInfo()
    {
        var result = Fixtures.Analyze(Fixtures.SignHistorico(Fixtures.Historico(edit: StillEnrolled)));

        Assert.Contains("HIST_FINAL_SEM_FORMATURA", result.Diagnostics.Info.Select(i => i.Id));
        Assert.Empty(result.Diagnostics.Warnings);
    }

    private static void StillEnrolled(System.Xml.XmlDocument d, System.Xml.XmlNamespaceManager ns)
    {
        var situacao = d.SelectSingleNode("//m:SituacaoAtualDiscente", ns)!;
        situacao.InnerXml = "";
        situacao.AppendChild(d.CreateElement("MatriculadoEmDisciplina", Fixtures.Mec));
    }

    [Fact]
    public void WorkloadSumMismatch_IsWarning()
    {
        var doc = Fixtures.SignHistorico(Fixtures.Historico(edit: (d, ns) =>
            d.SetText(ns, "//m:CargaHorariaCursoIntegralizada/m:HoraRelogio", "4200")));

        var result = Fixtures.Analyze(doc);

        Assert.Contains("HIST_CARGA_HORARIA_SOMA_DIVERGENTE", result.Ids());
        Assert.Contains("HIST_CARGA_INTEGRALIZADA_EXCEDE_CURSO", result.Ids());
    }

    [Fact]
    public void ColacaoBeforeConclusao_IsDateOrderError()
    {
        var doc = Fixtures.SignHistorico(Fixtures.Historico(edit: (d, ns) =>
            d.SetText(ns, "//m:Formado/m:DataColacaoGrau", "2022-11-01")));

        var result = Fixtures.Analyze(doc);

        var finding = Assert.Single(result.Diagnostics.Errors, e => e.Id == "HIST_DATE_ORDER");
        Assert.Contains("DataColacaoGrau", finding.Message);
    }

    [Fact]
    public void ValidationCodeFromAnotherIes_IsError()
    {
        var doc = Fixtures.SignHistorico(Fixtures.Historico(edit: (d, ns) =>
            d.SetText(ns, "//m:SegurancaHistorico/m:CodigoValidacao", "1234.0a1b2c3d4e5f")));

        Assert.Contains("HIST_CODIGO_VALIDACAO_IES_MISMATCH", Fixtures.Analyze(doc).Ids());
    }

    [Fact]
    public void HomologacaoEnvironment_IsWarning()
    {
        var doc = Fixtures.SignHistorico(Fixtures.Historico(edit: (d, ns) =>
            ((System.Xml.XmlElement)d.SelectSingleNode("//m:infHistoricoEscolar", ns)!).SetAttribute("ambiente", "Homologação")));

        var result = Fixtures.Analyze(doc);

        Assert.Equal("Homologação", result.Metadata.Ambiente);
        Assert.Contains("DOC_AMBIENTE_HOMOLOGACAO", result.Ids());
    }

    [Fact]
    public void InvalidStudentCpf_IsError_ButXsdStillValid()
    {
        var doc = Fixtures.SignHistorico(Fixtures.Historico(edit: (d, ns) =>
            d.SetText(ns, "//m:Aluno/m:CPF", "52998224726")));

        var result = Fixtures.Analyze(doc);

        Assert.True(result.SchemaValidation.Valid);
        Assert.Contains("CPF_CHECKDIGIT_INVALID", result.Ids());
        Assert.False(((HistoricoSummaryDto)result.Document!).Aluno.CpfValid);
    }

    [Fact]
    public void FinalWithOnlySecretariaSignature_IsWarning()
    {
        var result = Fixtures.Analyze(Fixtures.SignHistorico(Fixtures.Historico(), institutional: false));

        Assert.Contains("SIG_HISTORICO_COUNT", result.Ids());
        Assert.Contains("SIG_LAST_NOT_ECNPJ", result.Ids());
    }

    [Fact]
    public void ParcialWithOnlySecretariaSignature_IsFine()
    {
        var result = Fixtures.Analyze(Fixtures.SignHistorico(Fixtures.Historico("DocumentoHistoricoEscolarParcial"), institutional: false));

        Assert.DoesNotContain("SIG_HISTORICO_COUNT", result.Ids());
    }
}
