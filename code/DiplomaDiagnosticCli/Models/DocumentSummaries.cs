using System.Text.Json.Serialization;

namespace DiplomaDiagnosticCli.Models;

/// <summary>Person data shared by Diplomado (Diploma) and Aluno (Histórico). Masked by default.</summary>
public sealed class PessoaDto
{
    public string? Nome { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NomeSocial { get; init; }

    public string? Cpf { get; init; }

    [JsonPropertyName("cpf_valid")]
    public bool? CpfValid { get; init; }

    [JsonPropertyName("data_nascimento")]
    public string? DataNascimento { get; init; }
}

public sealed class IesDto
{
    public string? Nome { get; init; }

    [JsonPropertyName("codigo_mec")]
    public string? CodigoMec { get; init; }

    public string? Cnpj { get; init; }

    [JsonPropertyName("cnpj_valid")]
    public bool? CnpjValid { get; init; }
}

public sealed class CursoDto
{
    public string? Nome { get; init; }

    [JsonPropertyName("codigo_emec")]
    public string? CodigoEmec { get; init; }
}

public sealed class DiplomaSummaryDto
{
    public PessoaDto Diplomado { get; init; } = new();
    public CursoDto Curso { get; init; } = new();

    [JsonPropertyName("ies_emissora")]
    public IesDto IesEmissora { get; init; } = new();

    [JsonPropertyName("ies_registradora")]
    public IesDto IesRegistradora { get; init; } = new();

    [JsonPropertyName("data_conclusao")]
    public string? DataConclusao { get; init; }

    [JsonPropertyName("data_colacao_grau")]
    public string? DataColacaoGrau { get; init; }

    [JsonPropertyName("data_expedicao_diploma")]
    public string? DataExpedicaoDiploma { get; init; }

    [JsonPropertyName("data_registro_diploma")]
    public string? DataRegistroDiploma { get; init; }

    [JsonPropertyName("livro_registro")]
    public string? LivroRegistro { get; init; }

    [JsonPropertyName("codigo_validacao")]
    public string? CodigoValidacao { get; init; }
}

public sealed class HistoricoSummaryDto
{
    public PessoaDto Aluno { get; init; } = new();
    public CursoDto Curso { get; init; } = new();

    [JsonPropertyName("ies_emissora")]
    public IesDto IesEmissora { get; init; } = new();

    [JsonPropertyName("codigo_curriculo")]
    public string? CodigoCurriculo { get; init; }

    [JsonPropertyName("data_emissao")]
    public string? DataEmissao { get; init; }

    [JsonPropertyName("data_ingresso")]
    public string? DataIngresso { get; init; }

    /// <summary>Element name chosen inside SituacaoAtualDiscente (Formado, Trancamento, MatriculadoEmDisciplina...).</summary>
    [JsonPropertyName("situacao_atual")]
    public string? SituacaoAtual { get; init; }

    [JsonPropertyName("data_conclusao_curso")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataConclusaoCurso { get; init; }

    [JsonPropertyName("data_colacao_grau")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataColacaoGrau { get; init; }

    [JsonPropertyName("data_expedicao_diploma")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataExpedicaoDiploma { get; init; }

    [JsonPropertyName("carga_horaria_curso")]
    public CargaHorariaDto? CargaHorariaCurso { get; init; }

    [JsonPropertyName("carga_horaria_integralizada")]
    public CargaHorariaDto? CargaHorariaIntegralizada { get; init; }

    /// <summary>Sum of the workload of approved Disciplinas + AtividadesComplementares + Estagios, per unit.</summary>
    [JsonPropertyName("carga_horaria_aprovada_somada")]
    public List<CargaHorariaDto> CargaHorariaAprovadaSomada { get; init; } = [];

    public ElementosCountDto Elementos { get; init; } = new();

    [JsonPropertyName("codigo_validacao")]
    public string? CodigoValidacao { get; init; }
}

public sealed class CargaHorariaDto
{
    /// <summary>HoraAula | HoraRelogio</summary>
    public string Unidade { get; init; } = string.Empty;
    public decimal Valor { get; init; }
}

public sealed class ElementosCountDto
{
    public int Disciplinas { get; init; }

    [JsonPropertyName("disciplinas_aprovadas")]
    public int DisciplinasAprovadas { get; init; }

    [JsonPropertyName("disciplinas_pendentes")]
    public int DisciplinasPendentes { get; init; }

    [JsonPropertyName("disciplinas_reprovadas")]
    public int DisciplinasReprovadas { get; init; }

    [JsonPropertyName("atividades_complementares")]
    public int AtividadesComplementares { get; init; }

    public int Estagios { get; init; }
}
