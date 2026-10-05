using System.Text.Json;
using DiplomaDiagnosticCli.Commands;
using DiplomaDiagnosticCli.Tests.Support;

namespace DiplomaDiagnosticCli.Tests;

public class CommandTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("diploma-diag-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Run_WritesJson_WithSchemaTag()
    {
        string input = Path.Combine(_dir, "hist.xml");
        string output = Path.Combine(_dir, "out.json");
        File.WriteAllText(input, Fixtures.SignHistorico(Fixtures.Historico()).ToXmlString());

        int code = new DiagnoseCommand().Run(input, output, verbose: false, format: "json", mask: true);

        Assert.Equal(ExitCode.Success, code);
        using var json = JsonDocument.Parse(File.ReadAllText(output));
        Assert.Equal("diploma-diagnostic-v1", json.RootElement.GetProperty("$schema").GetString());
        Assert.Equal("historico_final", json.RootElement.GetProperty("metadata").GetProperty("document_type").GetString());
    }

    [Fact]
    public void Run_MissingFile_IsInvalidFile() =>
        Assert.Equal(ExitCode.InvalidFile, new DiagnoseCommand().Run(Path.Combine(_dir, "nope.xml"), null, false, "json", true));

    [Fact]
    public void Run_NotXml_IsParseError()
    {
        string input = Path.Combine(_dir, "bad.xml");
        File.WriteAllText(input, "not xml at all");
        Assert.Equal(ExitCode.ParseError, new DiagnoseCommand().Run(input, Path.Combine(_dir, "o.json"), false, "json", true));
    }

    [Fact]
    public void Run_XmlWithDtd_IsRejected()
    {
        string input = Path.Combine(_dir, "dtd.xml");
        File.WriteAllText(input, "<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY e \"boom\">]><x>&e;</x>");
        Assert.Equal(ExitCode.ParseError, new DiagnoseCommand().Run(input, Path.Combine(_dir, "o.json"), false, "json", true));
    }

    [Fact]
    public void Run_NonMecXml_IsInvalidFile()
    {
        string input = Path.Combine(_dir, "other.xml");
        File.WriteAllText(input, "<NFe xmlns=\"http://www.portalfiscal.inf.br/nfe\"/>");
        Assert.Equal(ExitCode.InvalidFile, new DiagnoseCommand().Run(input, Path.Combine(_dir, "o.json"), false, "json", true));
    }
}
