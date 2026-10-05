using DiplomaDiagnosticCli.Tests.Support;

namespace DiplomaDiagnosticCli.Tests;

/// <summary>
/// Regenerates the signed synthetic examples in /samples (used by the README). Not a test: runs only
/// when DIPLOMA_DIAG_WRITE_SAMPLES=1. Signatures use throwaway self-signed TEST certificates.
/// </summary>
public class SampleGenerator
{
    [SkippableFact]
    public void WriteSamples()
    {
        Skip.IfNot(Environment.GetEnvironmentVariable("DIPLOMA_DIAG_WRITE_SAMPLES") == "1", "set DIPLOMA_DIAG_WRITE_SAMPLES=1 to regenerate /samples");

        string dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples"));
        Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(dir, "historico-final-assinado.xml"),
            Fixtures.SignHistorico(Fixtures.Historico()).ToXmlString());
        File.WriteAllText(Path.Combine(dir, "diploma-assinado.xml"),
            Fixtures.SignDiploma(Fixtures.Diploma()).ToXmlString());

        // Same histórico with typical support-desk problems: a pending disciplina on a graduate,
        // a workload total that no longer adds up, and content edited after signing.
        var problematic = Fixtures.SignHistorico(Fixtures.Historico(edit: (d, ns) =>
        {
            var aprovado = d.SelectSingleNode("//m:Disciplina[m:CodigoDisciplina='TST301']/m:Aprovado", ns)!;
            aprovado.ParentNode!.ReplaceChild(d.CreateElement("Pendente", Fixtures.Mec), aprovado);
        }));
        File.WriteAllText(Path.Combine(dir, "historico-final-com-problemas.xml"),
            problematic.ToXmlString().Replace("<Nota>9.00</Nota>", "<Nota>9.50</Nota>"));
    }
}
