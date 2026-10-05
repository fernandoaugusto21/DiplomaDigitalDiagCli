using System.Xml;
using DiplomaDiagnosticCli.Documents;
using DiplomaDiagnosticCli.Models;
using DiplomaDiagnosticCli.Validation;

namespace DiplomaDiagnosticCli.Analyzers;

/// <summary>Summary + business rules for the three Histórico Escolar Digital variants (Parcial, Final, Segunda Via Nato Físico).</summary>
public sealed class HistoricoAnalyzer : IDocumentAnalyzer
{
    public void Analyze(MecDocument document, AnalysisContext context)
    {
        if (!document.IsHistorico || document.InfElement is null)
            return;

        var hist = document.Element("m:HistoricoEscolar");
        var situacaoAtual = document.Element("m:SituacaoAtualDiscente", hist);
        var situacao = situacaoAtual?.ChildNodes.OfType<XmlElement>().LastOrDefault(e => e.LocalName != "PeriodoLetivo");
        var formado = situacao?.LocalName == "Formado" ? situacao : null;

        var elementos = document.Element("m:ElementosHistorico", hist);
        var disciplinas = document.Elements("m:Disciplina", elementos).ToList();
        var aprovadas = disciplinas.Where(d => document.Element("m:Aprovado", d) is not null).ToList();
        var pendentes = disciplinas.Where(d => document.Element("m:Pendente", d) is not null).ToList();
        var atividades = document.Elements("m:AtividadeComplementar", elementos).ToList();
        var estagios = document.Elements("m:Estagio", elementos).ToList();

        var somada = aprovadas.SelectMany(d => document.Elements("m:CargaHoraria", d).Select(c => RuleHelpers.ReadCarga(c)))
            .Concat(atividades.SelectMany(a => document.Elements("m:CargaHorariaEmHoraRelogio", a).Select(c => RuleHelpers.ReadCarga(c))))
            .Concat(estagios.SelectMany(e => document.Elements("m:CargaHorariaEmHorasRelogio", e).Select(c => RuleHelpers.ReadCarga(c))))
            .OfType<CargaHorariaDto>()
            .GroupBy(c => c.Unidade)
            .Select(g => new CargaHorariaDto { Unidade = g.Key, Valor = g.Sum(c => c.Valor) })
            .OrderBy(c => c.Unidade)
            .ToList();

        var cargaCurso = RuleHelpers.ReadCarga(document.Element("m:CargaHorariaCurso", hist));
        var cargaIntegralizada = RuleHelpers.ReadCarga(document.Element("m:CargaHorariaCursoIntegralizada", hist));
        var ies = RuleHelpers.ReadIes(document, document.Element("m:IesEmissora"), "IesEmissora", context);
        string? codigoValidacao = document.Text("m:SegurancaHistorico/m:CodigoValidacao");

        var summary = new HistoricoSummaryDto
        {
            Aluno = RuleHelpers.ReadPessoa(document, document.Element("m:Aluno"), "Aluno", context),
            Curso = RuleHelpers.ReadCurso(document, document.Element("m:DadosCurso | m:DadosCursoNSF")),
            IesEmissora = ies,
            CodigoCurriculo = document.Text("m:CodigoCurriculo", hist),
            DataEmissao = document.Text("m:DataEmissaoHistorico", hist),
            DataIngresso = document.Text("m:IngressoCurso/m:Data", hist),
            SituacaoAtual = situacao?.LocalName,
            DataConclusaoCurso = document.Text("m:DataConclusaoCurso", formado),
            DataColacaoGrau = document.Text("m:DataColacaoGrau", formado),
            DataExpedicaoDiploma = document.Text("m:DataExpedicaoDiploma", formado),
            CargaHorariaCurso = cargaCurso,
            CargaHorariaIntegralizada = cargaIntegralizada,
            CargaHorariaAprovadaSomada = somada,
            Elementos = new ElementosCountDto
            {
                Disciplinas = disciplinas.Count,
                DisciplinasAprovadas = aprovadas.Count,
                DisciplinasPendentes = pendentes.Count,
                DisciplinasReprovadas = disciplinas.Count(d => document.Element("m:Reprovado", d) is not null),
                AtividadesComplementares = atividades.Count,
                Estagios = estagios.Count
            },
            CodigoValidacao = codigoValidacao
        };
        context.Document = summary;

        if (document.InfElement.GetAttribute("ambiente") == "Homologação")
            context.Warning("DOC_AMBIENTE_HOMOLOGACAO", "ambiente=\"Homologação\": this histórico has no legal validity (only \"Produção\" documents are valid).", "attribute::ambiente");

        CheckSituacao(document, context, summary, formado, pendentes.Count);
        CheckCargaHoraria(context, summary, somada, cargaCurso, cargaIntegralizada, formado is not null);

        RuleHelpers.CheckChronology(context, "HIST",
            ("Aluno/DataNascimento", document.Text("m:Aluno/m:DataNascimento")),
            ("IngressoCurso/Data", summary.DataIngresso),
            ("Formado/DataConclusaoCurso", summary.DataConclusaoCurso),
            ("Formado/DataColacaoGrau", summary.DataColacaoGrau),
            ("Formado/DataExpedicaoDiploma", summary.DataExpedicaoDiploma));
        RuleHelpers.CheckChronology(context, "HIST",
            ("IngressoCurso/Data", summary.DataIngresso),
            ("DataEmissaoHistorico", summary.DataEmissao));

        foreach (var periodo in atividades.Concat(estagios))
        {
            if (MecDocument.ParseDate(document.Text("m:DataInicio", periodo)) is { } inicio
                && MecDocument.ParseDate(document.Text("m:DataFim", periodo)) is { } fim && fim < inicio)
            {
                string code = document.Text("m:CodigoAtividadeComplementar | m:CodigoUnidadeCurricular", periodo) ?? "?";
                context.Error("HIST_PERIODO_INVERTIDO", $"{periodo.LocalName} {code}: DataFim ({fim:yyyy-MM-dd}) is before DataInicio ({inicio:yyyy-MM-dd}).", $"element::{periodo.LocalName}[{code}]");
            }
        }

        foreach (var dup in disciplinas
                     .GroupBy(d => (Codigo: document.Text("m:CodigoDisciplina", d), Periodo: document.Text("m:PeriodoLetivo", d)))
                     .Where(g => g.Key.Codigo is not null && g.Count() > 1))
        {
            context.Warning("HIST_DISCIPLINA_DUPLICADA", $"Disciplina {dup.Key.Codigo} appears {dup.Count()} times in período {dup.Key.Periodo}.", $"element::Disciplina[{dup.Key.Codigo}]");
        }

        foreach (var docente in document.Elements(".//m:Docente", elementos))
        {
            string? cpf = document.Text("m:CPF", docente);
            if (cpf is not null && !DocumentNumbers.IsValidCpf(cpf))
                context.Warning("CPF_CHECKDIGIT_INVALID", $"Docente CPF {context.Redactor.Cpf(cpf)} has invalid check digits.", "element::Docente/CPF");
        }

        if (codigoValidacao is not null && ies.CodigoMec is not null && codigoValidacao.Split('.')[0] != ies.CodigoMec)
        {
            context.Error("HIST_CODIGO_VALIDACAO_IES_MISMATCH",
                $"CodigoValidacao '{codigoValidacao}' starts with '{codigoValidacao.Split('.')[0]}', but IesEmissora/CodigoMEC is {ies.CodigoMec}.",
                "element::SegurancaHistorico/CodigoValidacao",
                "The validation code is \"<e-MEC code of the emissora>.<hex code>\".");
        }
    }

    private static void CheckSituacao(MecDocument document, AnalysisContext context, HistoricoSummaryDto summary, XmlElement? formado, int pendentes)
    {
        if (document.Kind == DocumentKind.HistoricoParcial && formado is not null)
            context.Warning("HIST_PARCIAL_FORMADO", "Histórico Parcial with SituacaoAtualDiscente = Formado; a graduate's histórico is normally the Final one.", "element::SituacaoAtualDiscente");

        if (document.Kind == DocumentKind.HistoricoFinal && formado is null)
            context.Information("HIST_FINAL_SEM_FORMATURA", $"Histórico Final with SituacaoAtualDiscente = {summary.SituacaoAtual ?? "(missing)"} (allowed when the student leaves without graduating).", "element::SituacaoAtualDiscente");

        if (formado is not null && pendentes > 0)
            context.Error("HIST_FORMADO_COM_PENDENCIA", $"Student is Formado but {pendentes} disciplina(s) are still Pendente.", "element::ElementosHistorico",
                "Close the pending disciplinas (Aprovado/Reprovado) before issuing the histórico of a graduate.");
    }

    private static void CheckCargaHoraria(AnalysisContext context, HistoricoSummaryDto summary, List<CargaHorariaDto> somada,
        CargaHorariaDto? curso, CargaHorariaDto? integralizada, bool formado)
    {
        if (integralizada is not null)
        {
            if (somada.Count > 1)
            {
                context.Information("HIST_CARGA_HORARIA_UNIDADES_MISTAS",
                    $"Approved workload mixes units ({string.Join(" + ", somada.Select(s => $"{s.Valor} {s.Unidade}"))}); the sum cannot be compared to CargaHorariaCursoIntegralizada without the course's hora-aula duration.",
                    "element::CargaHorariaCursoIntegralizada");
            }
            else if (somada.Count == 1 && somada[0].Unidade == integralizada.Unidade && somada[0].Valor != integralizada.Valor)
            {
                context.Warning("HIST_CARGA_HORARIA_SOMA_DIVERGENTE",
                    $"Sum of approved workload is {somada[0].Valor} {somada[0].Unidade}, but CargaHorariaCursoIntegralizada says {integralizada.Valor} {integralizada.Unidade}.",
                    "element::CargaHorariaCursoIntegralizada",
                    "Check for disciplinas/atividades missing from ElementosHistorico, or an integralizada total computed with different rules.");
            }
        }

        if (curso is not null && integralizada is not null && curso.Unidade == integralizada.Unidade)
        {
            if (formado && integralizada.Valor < curso.Valor)
                context.Warning("HIST_FORMADO_CARGA_INCOMPLETA",
                    $"Student is Formado with {integralizada.Valor} {integralizada.Unidade} integralizadas out of {curso.Valor} required by the course.",
                    "element::CargaHorariaCursoIntegralizada");
            else if (integralizada.Valor > curso.Valor)
                context.Information("HIST_CARGA_INTEGRALIZADA_EXCEDE_CURSO",
                    $"CargaHorariaCursoIntegralizada ({integralizada.Valor}) exceeds CargaHorariaCurso ({curso.Valor}).",
                    "element::CargaHorariaCursoIntegralizada");
        }
    }
}
