# DiplomaDiagCli

CLI .NET que analisa os XMLs do **Diploma Digital** e do **Histórico Escolar
Digital** do MEC (leiaute XSD v1.05) e emite um diagnóstico estruturado em
JSON, para consumo por Claude Code ou outro LLM agêntico — com **dados
pessoais mascarados por padrão**.

## Por que existe

O XML do diploma já é legível; o problema é outro. O verificador oficial do
MEC valida **só a estrutura**, exige enviar o arquivo (com os dados do aluno)
para um site externo e não tem API. Um LLM lendo o XML cru "acha" que a carga
horária fecha e não consegue verificar uma assinatura. Esta ferramenta faz,
localmente e de forma determinística:

| O quê | Como |
|---|---|
| Validação estrutural | XSD v1.05 oficial embutido, offline |
| Assinaturas XAdES | inventário (quem assinou qual nó, quando, política ICP-Brasil) + verificação XMLDSig de cada uma — e, por comparação, **qual nó foi alterado** depois de assinado |
| Regras de negócio | dígitos de CPF/CNPJ (inclusive CNPJ alfanumérico), cronologia de datas, soma de carga horária, formado com disciplina pendente, código de validação × e-MEC da IES, ids do diploma, ambiente de homologação |
| Privacidade (LGPD) | CPF → `***.456.789-**`, nomes → iniciais, nascimento → ano; inclusive dentro das mensagens do validador XSD |

Documentos suportados: `Diploma`, `DocumentoHistoricoEscolarParcial`,
`DocumentoHistoricoEscolarFinal`, `DocumentoHistoricoEscolarSegundaViaNatoFisico`.

## Estrutura do repositório

```
.
├── code/      # CLI, testes e XSDs embutidos (ver code/README.md)
├── docs/      # Especificação do JSON e catálogo de diagnósticos
├── samples/   # XMLs sintéticos assinados com certificado de TESTE (dados fictícios)
└── scripts/   # Download dos exemplos públicos da UFSC para os testes de interoperabilidade
```

## Início rápido

```powershell
dotnet run --project code/DiplomaDiagnosticCli -- --file samples/historico-final-com-problemas.xml --output -
```

```
✓ Parsed historico-final-com-problemas.xml (historico_final, layout 1.05)
✓ Signatures: 0/2 XMLDSig-valid; XSD: valid
✓ Analysis complete: 3 errors, 1 warnings, 0 info (personal data masked)
```

```json
"errors": [
  { "id": "HIST_FORMADO_COM_PENDENCIA", "message": "Student is Formado but 1 disciplina(s) are still Pendente." },
  { "id": "SIG_XMLDSIG_INVALID", "message": "Signature 1 (in DocumentoHistoricoEscolarFinal, covering (document)) does not verify: ..." }
],
"warnings": [
  { "id": "HIST_CARGA_HORARIA_SOMA_DIVERGENTE", "message": "Sum of approved workload is 3200 HoraRelogio, but CargaHorariaCursoIntegralizada says 4000 HoraRelogio." }
]
```

Detalhes de build, testes e opções: [`code/README.md`](code/README.md).
Schema do JSON e catálogo de diagnósticos: [`docs/EspecificacaoAgentica.md`](docs/EspecificacaoAgentica.md).

## Fora do escopo

- Cadeia ICP-Brasil, revogação (LCR/OCSP) e carimbo de tempo: reportados como
  `"icp_brasil_chain": "unvalidated"`. A verificação é do núcleo XMLDSig.
- Documentação Acadêmica de Registro, Currículo Escolar, Lista de Diplomas
  Anulados, Arquivo de Fiscalização.
- Validação estrutural de leiautes anteriores ao 1.05 (são detectados e
  reportados, não validados).

## Licença

[MIT](LICENSE). Os XSDs em `code/DiplomaDiagnosticCli/Schemas/` são documentos
públicos do MEC — ver [`Schemas/README.md`](code/DiplomaDiagnosticCli/Schemas/README.md).
