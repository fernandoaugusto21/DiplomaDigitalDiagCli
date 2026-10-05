using System.Globalization;
using System.Xml;
using DiplomaDiagnosticCli.Documents;
using DiplomaDiagnosticCli.Models;
using DiplomaDiagnosticCli.Validation;

namespace DiplomaDiagnosticCli.Analyzers;

/// <summary>Readers and checks shared by the Diploma and Histórico rule analyzers.</summary>
internal static class RuleHelpers
{
    public static PessoaDto ReadPessoa(MecDocument doc, XmlElement? person, string path, AnalysisContext context)
    {
        if (person is null)
            return new PessoaDto();

        string? cpf = doc.Text("m:CPF", person);
        bool? cpfValid = cpf is null ? null : DocumentNumbers.IsValidCpf(cpf);
        if (cpfValid == false)
        {
            context.Error("CPF_CHECKDIGIT_INVALID", $"CPF {context.Redactor.Cpf(cpf)} at {path}/CPF has invalid check digits.",
                $"element::{path}/CPF", "Check the CPF in the source system; the MEC registration rejects an invalid CPF.");
        }

        return new PessoaDto
        {
            Nome = context.Redactor.Name(doc.Text("m:Nome", person)),
            NomeSocial = context.Redactor.Name(doc.Text("m:NomeSocial", person)),
            Cpf = context.Redactor.Cpf(cpf),
            CpfValid = cpfValid,
            DataNascimento = context.Redactor.BirthDate(doc.Text("m:DataNascimento", person))
        };
    }

    public static IesDto ReadIes(MecDocument doc, XmlElement? ies, string path, AnalysisContext context)
    {
        if (ies is null)
            return new IesDto();

        string? cnpj = doc.Text("m:CNPJ", ies);
        bool? cnpjValid = cnpj is null ? null : DocumentNumbers.IsValidCnpj(cnpj);
        if (cnpjValid == false)
            context.Error("CNPJ_CHECKDIGIT_INVALID", $"CNPJ {cnpj} at {path}/CNPJ has invalid check digits.", $"element::{path}/CNPJ");

        string? mantenedoraCnpj = doc.Text("m:Mantenedora/m:CNPJ", ies);
        if (mantenedoraCnpj is not null && !DocumentNumbers.IsValidCnpj(mantenedoraCnpj))
            context.Error("CNPJ_CHECKDIGIT_INVALID", $"CNPJ {mantenedoraCnpj} at {path}/Mantenedora/CNPJ has invalid check digits.", $"element::{path}/Mantenedora/CNPJ");

        return new IesDto
        {
            Nome = doc.Text("m:Nome", ies),
            CodigoMec = doc.Text("m:CodigoMEC", ies),
            Cnpj = cnpj,
            CnpjValid = cnpjValid
        };
    }

    public static CursoDto ReadCurso(MecDocument doc, XmlElement? curso) => curso is null ? new CursoDto() : new CursoDto
    {
        Nome = doc.Text("m:NomeCurso", curso),
        CodigoEmec = doc.Text("m:CodigoCursoEMEC", curso)
    };

    /// <summary>Reads a TCargaHoraria-like element: either a HoraAula/HoraRelogio child, or the element's own text
    /// (THoraRelogioComEtiqueta). <paramref name="defaultUnit"/> names the unit for the latter.</summary>
    public static CargaHorariaDto? ReadCarga(XmlElement? element, string defaultUnit = "HoraRelogio")
    {
        if (element is null)
            return null;
        var child = element.ChildNodes.OfType<XmlElement>().FirstOrDefault(e => e.LocalName is "HoraAula" or "HoraRelogio");
        string unit = child?.LocalName ?? defaultUnit;
        string text = (child ?? element).InnerText.Trim();
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
            ? new CargaHorariaDto { Unidade = unit, Valor = value }
            : null;
    }

    /// <summary>Checks that the dates are non-decreasing in the given order; skips missing ones.</summary>
    public static void CheckChronology(AnalysisContext context, string idPrefix, params (string Label, string? Value)[] dates)
    {
        (string Label, DateOnly Date)? previous = null;
        foreach (var (label, value) in dates)
        {
            if (MecDocument.ParseDate(value) is not { } date)
                continue;
            if (date > context.Today)
                context.Error($"{idPrefix}_DATE_IN_FUTURE", $"{label} ({value}) is in the future.", $"element::{label}");
            if (previous is { } p && date < p.Date)
                context.Error($"{idPrefix}_DATE_ORDER", $"{label} ({value}) is before {p.Label} ({p.Date:yyyy-MM-dd}).", $"element::{label}",
                    $"Expected chronology: {string.Join(" ≤ ", dates.Select(d => d.Label))}.");
            previous = (label, date);
        }
    }
}
