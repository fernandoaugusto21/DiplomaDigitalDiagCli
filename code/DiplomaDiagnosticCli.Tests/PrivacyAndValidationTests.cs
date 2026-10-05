using DiplomaDiagnosticCli.Output;
using DiplomaDiagnosticCli.Privacy;
using DiplomaDiagnosticCli.Tests.Support;
using DiplomaDiagnosticCli.Validation;

namespace DiplomaDiagnosticCli.Tests;

public class PrivacyAndValidationTests
{
    // Every personal value present in the fixtures + test certificates.
    private static readonly string[] PersonalValues =
    [
        "52998224725", "Maria Ficticia da Silva Teste", "2000-03-15", "RGX987654",
        "11144477735", "Joao Docente Ficticio", "Ana Docente Ficticia",
        "39053344705", "MARIA SECRETARIA FICTICIA", "12345678909", "Carlos Registrador Ficticio"
    ];

    [Theory]
    [InlineData("historico")]
    [InlineData("diploma")]
    public void MaskedOutput_ContainsNoPersonalValue(string kind)
    {
        var doc = kind == "diploma" ? Fixtures.SignDiploma(Fixtures.Diploma()) : Fixtures.SignHistorico(Fixtures.Historico());

        string json = JsonEmitter.Serialize(Fixtures.Analyze(doc, mask: true), pretty: true);

        Assert.Empty(PersonalValues.Where(json.Contains));
        Assert.Contains("***.982.247-**", json);
    }

    [Fact]
    public void UnmaskedOutput_KeepsPersonalValues()
    {
        string json = JsonEmitter.Serialize(Fixtures.Analyze(Fixtures.SignHistorico(Fixtures.Historico()), mask: false), pretty: true);

        Assert.Contains("52998224725", json);
        Assert.Contains("Maria Ficticia da Silva Teste", json);
    }

    [Fact]
    public void XsdMessagesQuotingPersonalValues_AreScrubbed()
    {
        // 10-digit CPF: the XSD validator quotes the offending value in its message.
        var doc = Fixtures.SignHistorico(Fixtures.Historico(edit: (d, ns) => d.SetText(ns, "//m:Aluno/m:CPF", "5299822472")));

        var result = Fixtures.Analyze(doc, mask: true);

        Assert.False(result.SchemaValidation.Valid);
        Assert.DoesNotContain("5299822472", JsonEmitter.Serialize(result, pretty: false));
    }

    [Theory]
    [InlineData("Maria da Silva Souza", "M. S. S.")]
    [InlineData("JOÃO DOS SANTOS", "J. S.")]
    public void Names_AreReducedToInitials(string name, string expected) =>
        Assert.Equal(expected, new PiiRedactor(true).Name(name));

    [Theory]
    [InlineData("52998224725", true)]
    [InlineData("11144477735", true)]
    [InlineData("52998224726", false)]
    [InlineData("01111111111", false)] // the fictitious CPF of the public UFSC/MEC sample diploma
    [InlineData("11111111111", false)]
    [InlineData("5299822472", false)]
    public void Cpf_CheckDigits(string cpf, bool valid) => Assert.Equal(valid, DocumentNumbers.IsValidCpf(cpf));

    [Theory]
    [InlineData("11222333000181", true)]
    [InlineData("83899526000182", true)]
    [InlineData("12ABC34501DE35", true)] // alphanumeric CNPJ (Receita Federal example)
    [InlineData("11222333000182", false)]
    [InlineData("00000000000000", false)]
    public void Cnpj_CheckDigits(string cnpj, bool valid) => Assert.Equal(valid, DocumentNumbers.IsValidCnpj(cnpj));
}
