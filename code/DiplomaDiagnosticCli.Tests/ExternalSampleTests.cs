using DiplomaDiagnosticCli.Commands;
using DiplomaDiagnosticCli.Documents;
using DiplomaDiagnosticCli.Tests.Support;

namespace DiplomaDiagnosticCli.Tests;

/// <summary>
/// Interoperability with signatures produced by a real signer: the public sample diplomas published
/// by UFSC (fictitious student, signed with a real ICP-Brasil e-CPF). They are not redistributed in
/// this repository; run scripts/Get-ExternalSamples.ps1 to download them. Skipped when absent.
/// </summary>
public class ExternalSampleTests
{
    private static readonly string Dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "external-samples"));

    private static string Sample(string name)
    {
        string path = Path.Combine(Dir, name);
        Skip.IfNot(File.Exists(path), $"{name} not downloaded (scripts/Get-ExternalSamples.ps1)");
        return path;
    }

    [SkippableTheory]
    [InlineData("ufsc-diploma-v1.00-assinado.xml", 3)]
    [InlineData("ufsc-diploma-v1.01-assinado.xml", 4)]
    public void RealSignatures_AllVerify(string file, int expected)
    {
        var result = DiagnoseCommand.Analyze(MecDocument.Load(Sample(file)), file, mask: true, today: Fixtures.Today);

        Assert.Equal(expected, result.Signatures.Count);
        Assert.All(result.Signatures, s => Assert.True(s.XmldsigValid, s.FailureReason));
        Assert.Contains("CPF_CHECKDIGIT_INVALID", result.Ids()); // the sample's fictitious CPF 011.111.111-11
        Assert.Contains("XSD_VERSION_UNSUPPORTED", result.Ids());
    }

    [SkippableFact]
    public void RealSignatures_TamperedDadosDiploma_IsLocalized()
    {
        string xml = File.ReadAllText(Sample("ufsc-diploma-v1.00-assinado.xml"))
            .Replace("<Nome>João Carlos</Nome>", "<Nome>João Karlos</Nome>");

        var result = Fixtures.Analyze(xml);

        // DadosDiploma and envelope fail; DadosRegistro (which does not cover DadosDiploma) still verifies.
        Assert.Equal([false, true, false], result.Signatures.Select(s => s.XmldsigValid));
    }
}
