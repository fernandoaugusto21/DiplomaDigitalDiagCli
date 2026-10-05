# Diploma Diagnostic CLI — Especificação LLM-Agêntica

**Objetivo**: CLI .NET que analisa XMLs do Diploma Digital / Histórico Escolar
Digital do MEC e emite diagnóstico estruturado (JSON) para consumo por Claude
Code ou outro LLM agêntico em pipeline local.

**Escopo**: detecção do documento, validação XSD v1.05, inventário e
verificação XMLDSig das assinaturas, regras de negócio e mascaramento de
dados pessoais. Sem validação de cadeia ICP-Brasil, sem renderização de RVDD.

---

## 1. Premissas

- **Entrada**: um XML (`Diploma`, `DocumentoHistoricoEscolarParcial`,
  `DocumentoHistoricoEscolarFinal` ou `DocumentoHistoricoEscolarSegundaViaNatoFisico`),
  tratado como **não confiável**: DTD proibido, sem resolver externo, XPath de
  transform só o padrão MEC.
- **Saída**: JSON (`$schema: diploma-diagnostic-v1`), previsível e documentado
  abaixo.
- **Privacidade**: o JSON existe para ser colado em um LLM, então os dados
  pessoais saem mascarados por padrão (`--no-mask` desliga).
- **Ambiente**: .NET 10, Windows ou Linux, offline.

## 2. Arquitetura

```
diploma-diagnostic --file <xml> [--output <json|->] [--format json|json-pretty] [--no-mask] [--verbose]
 │
 ├─ MecDocument            raiz + namespace → tipo de documento; atributo versao → leiaute
 ├─ PiiCollector           registra CPFs, nomes de pessoas, RG, nascimento (roda primeiro)
 ├─ SchemaAnalyzer         XSD v1.05 embutido; outras versões: detectadas, não validadas
 ├─ DiplomaAnalyzer        resumo + regras do Diploma
 ├─ HistoricoAnalyzer      resumo + regras das 3 variantes de Histórico
 ├─ SignatureAnalyzer      inventário + XMLDSig + layout esperado das assinaturas
 └─ JsonEmitter            snake_case, UTF-8 (inclusive em stdout)
```

### 2.1 Verificação de assinatura — dois ajustes ao `SignedXml`

Os assinadores usados com o Diploma Digital excluem as assinaturas aninhadas de
cada digest com o transform XPath `not(ancestor-or-self::ds:Signature)`. O .NET:

1. considera transforms XPath inseguros e os rejeita → `MecSignedXml` libera o
   transform, e o `SignatureAnalyzer` só o executa se a expressão for
   **exatamente** essa (qualquer outra → `SIG_TRANSFORM_UNSUPPORTED`, sem executar);
2. só lê declarações de namespace postas diretamente no `<ds:XPath>` → a cópia
   usada na verificação declara `xmlns:ds` ali. Sob c14n exclusiva a declaração
   é redundante (o prefixo já está em escopo), então a forma canônica não muda.

Validado contra os exemplos públicos da UFSC (assinaturas reais ICP-Brasil):
7/7 assinaturas verificam; alterar o nome do diplomado invalida exatamente as
assinaturas de `DadosDiploma` e do envelope, mantendo a de `DadosRegistro`.

## 3. Saída JSON

```jsonc
{
  "$schema": "diploma-diagnostic-v1",
  "metadata": {
    "file_path": "...", "file_size_bytes": 13784, "file_sha256": "...",
    "document_type": "historico_final",      // diploma | historico_parcial | historico_final | historico_segunda_via_nato_fisico | unsupported
    "root_element": "DocumentoHistoricoEscolarFinal",
    "layout_version": "1.05",
    "ambiente": "Produção",                   // só Histórico
    "parsed_at_utc": "...", "parser_version": "diploma-diagnostic-0.1.0",
    "pii_masked": true
  },
  "document": { /* resumo — formato depende do tipo, ver 3.1 */ },
  "schema_validation": {
    "validated": true, "xsd_version": "1.05", "valid": true,
    "error_count": 0,
    "errors": [ { "line": 12, "position": 8, "severity": "ERROR", "message": "..." } ]   // até 50
  },
  "signatures": [
    {
      "index": 1,
      "parent_element": "DadosDiploma",
      "signed_elements": ["DadosDiploma"],     // "(document)" para URI=""
      "reference_uris": ["#Dip..."],
      "transforms": ["...enveloped-signature", "...REC-xpath-19991116", "...xml-exc-c14n#"],
      "signer": {
        "name": "J. E. M.", "certificate_type": "e-CPF", "document": "***.355.939-**",
        "issuer": "Autoridade Certificadora SERPRORFBv5",
        "not_before": "...", "not_after": "..."
      },
      "signing_time": "2021-05-17T21:13:14Z",
      "policy": { "oid": "2.16.76.1.7.1.6.2.4", "name": "PA_AD_RB_v2_4", "uri": "http://politicas.icpbrasil.gov.br/PA_AD_RB_v2_4.xml" },
      "has_signed_properties": true,
      "xmldsig_valid": true,
      "failure_reason": null,                  // omitido quando válida
      "icp_brasil_chain": "unvalidated"
    }
  ],
  "diagnostics": { "errors": [], "warnings": [], "info": [] }
}
```

Cada diagnóstico: `id` (estável), `severity`, `message`, `affected_element`
(`element::X`, `attribute::X`, `signature::N(Pai)`) e `suggestion` opcional.

### 3.1 `document`

**Diploma**: `diplomado {nome, nome_social, cpf, cpf_valid, data_nascimento}`,
`curso {nome, codigo_emec}`, `ies_emissora` e `ies_registradora`
`{nome, codigo_mec, cnpj, cnpj_valid}`, `data_conclusao`, `data_colacao_grau`,
`data_expedicao_diploma`, `data_registro_diploma`, `livro_registro`,
`codigo_validacao`.

**Histórico**: `aluno`, `curso`, `ies_emissora`, `codigo_curriculo`,
`data_emissao`, `data_ingresso`, `situacao_atual` (nome do elemento escolhido em
`SituacaoAtualDiscente`), `data_conclusao_curso`/`data_colacao_grau`/`data_expedicao_diploma`
(só quando `Formado`), `carga_horaria_curso`, `carga_horaria_integralizada`,
`carga_horaria_aprovada_somada` (soma por unidade de disciplinas aprovadas +
atividades complementares + estágios), `elementos` (contagens), `codigo_validacao`.

### 3.2 Mascaramento

| Dado | Mascarado como |
|---|---|
| CPF (aluno, docente, assinante, responsável pelo registro, certificado e-CPF) | `***.982.247-**` |
| Nome de pessoa física | iniciais (`M. F. S. T.`) |
| Data de nascimento | `2000-**-**` |
| RG / outro documento | `*********` |

Nomes de IES/mantenedora e CNPJs não são mascarados (são públicos). Todo valor
mascarado também é substituído em texto livre — inclusive nas mensagens do
validador XSD, que citam o valor inválido literalmente.

## 4. Catálogo de diagnósticos

### Documento e XSD
| id | sev. | quando |
|---|---|---|
| `DOCUMENT_TYPE_UNSUPPORTED` | ERROR | raiz do MEC fora dos 4 tipos suportados |
| `XSD_NAMESPACE_HTTPS_REWRITE` | ERROR | namespace `https://portal.mec.gov.br/...` (assinatura do XSD reescrito pelo gov.br) |
| `XSD_VERSION_UNSUPPORTED` | WARNING | `versao` ≠ 1.05 — validação estrutural pulada |
| `XSD_VALIDATION_FAILED` | ERROR | erros contra o XSD v1.05 (detalhes em `schema_validation.errors`) |
| `DOC_AMBIENTE_HOMOLOGACAO` | WARNING | histórico com `ambiente="Homologação"` (sem validade legal) |
| `CPF_CHECKDIGIT_INVALID` | ERROR / WARNING (docente) | dígito verificador |
| `CNPJ_CHECKDIGIT_INVALID` | ERROR | IES ou mantenedora |

### Diploma
| id | sev. | quando |
|---|---|---|
| `DIP_DATE_ORDER` | ERROR | fora de nascimento ≤ conclusão ≤ colação ≤ expedição ≤ registro |
| `DIP_DATE_IN_FUTURE` | ERROR | data posterior a hoje |
| `DIP_CODIGO_VALIDACAO_IES_MISMATCH` | ERROR | código não começa com `<e-MEC emissora>.<e-MEC registradora>.` |
| `DIP_ID_MISMATCH` | ERROR | número de 44 dígitos difere entre `VDip`/`Dip`/`RDip`/`ReqDip` |

### Histórico
| id | sev. | quando |
|---|---|---|
| `HIST_DATE_ORDER` / `HIST_DATE_IN_FUTURE` | ERROR | nascimento ≤ ingresso ≤ conclusão ≤ colação ≤ expedição; ingresso ≤ emissão |
| `HIST_FORMADO_COM_PENDENCIA` | ERROR | `Formado` com disciplina `Pendente` |
| `HIST_PERIODO_INVERTIDO` | ERROR | atividade/estágio com `DataFim` < `DataInicio` |
| `HIST_CODIGO_VALIDACAO_IES_MISMATCH` | ERROR | código não começa com o e-MEC da emissora |
| `HIST_CARGA_HORARIA_SOMA_DIVERGENTE` | WARNING | soma aprovada ≠ `CargaHorariaCursoIntegralizada` (mesma unidade) |
| `HIST_FORMADO_CARGA_INCOMPLETA` | WARNING | formado com integralizada < carga do curso |
| `HIST_PARCIAL_FORMADO` | WARNING | histórico parcial de aluno já formado |
| `HIST_DISCIPLINA_DUPLICADA` | WARNING | mesma disciplina 2× no mesmo período |
| `HIST_CARGA_HORARIA_UNIDADES_MISTAS` | INFO | hora-aula + hora-relógio: soma não comparável |
| `HIST_CARGA_INTEGRALIZADA_EXCEDE_CURSO` | INFO | integralizada > carga do curso |
| `HIST_FINAL_SEM_FORMATURA` | INFO | final sem `Formado` (permitido no desligamento) |

### Assinaturas
| id | sev. | quando |
|---|---|---|
| `SIG_NONE` | ERROR | nenhuma `ds:Signature` |
| `SIG_XMLDSIG_INVALID` | ERROR | digest/valor não confere — conteúdo alterado após assinar |
| `SIG_TRANSFORM_UNSUPPORTED` | ERROR | XPath diferente de `not(ancestor-or-self::ds:Signature)` (não executado) |
| `SIG_REFERENCE_UNRESOLVED` | ERROR | `URI="#x"` sem elemento com esse id |
| `SIG_NO_CERTIFICATE` / `SIG_CERTIFICATE_UNREADABLE` | ERROR | sem certificado utilizável no `KeyInfo` |
| `SIG_CERT_NOT_VALID_AT_SIGNING` | ERROR | `SigningTime` fora da validade do certificado |
| `SIG_MISSING_DADOS_DIPLOMA` / `SIG_MISSING_DADOS_REGISTRO` / `SIG_MISSING_ENVELOPE` | ERROR | etapa de assinatura ausente no Diploma |
| `SIG_ENVELOPE_NOT_LAST` | ERROR | envelope não é a última assinatura |
| `SIG_ORDER_INVALID` | ERROR (Diploma) / WARNING (Histórico) | etapa posterior assinada antes da anterior |
| `SIG_EMISSORA_ECNPJ_MISSING` | WARNING | `DadosDiploma` sem e-CNPJ da emissora |
| `SIG_ENVELOPE_NOT_ECNPJ` / `SIG_ENVELOPE_POLICY_NOT_ADRA` | WARNING | envelope deveria ser e-CNPJ com AD-RA |
| `SIG_HISTORICO_COUNT` / `SIG_LAST_NOT_ECNPJ` | WARNING | histórico final/2ª via sem secretaria (e-CPF) + IES (e-CNPJ) |
| `SIG_NO_SIGNING_TIME` | WARNING | sem `xades:SigningTime` |
| `SIG_NOT_XADES` | WARNING | sem referência a `xades:SignedProperties` |

## 5. Exit codes

| Código | Significado |
|---|---|
| 0 | Diagnóstico emitido (mesmo com erros no documento) |
| 1 | Arquivo inexistente, ou XML que não é do MEC |
| 2 | XML mal formado (ou com DTD) |
| 3 | Erro interno de análise |
| 64 | Erro de argumentos |
