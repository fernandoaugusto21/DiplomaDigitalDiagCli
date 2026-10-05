# DiplomaDiagnosticCli

CLI .NET que analisa XMLs do Diploma Digital / Histórico Escolar Digital do MEC
e emite um diagnóstico estruturado em JSON. Especificação completa em
[`../docs/EspecificacaoAgentica.md`](../docs/EspecificacaoAgentica.md).

## Estrutura

```
code/
├── DiplomaDiagnosticCli/            # CLI principal
│   ├── Program.cs, ExitCode.cs
│   ├── Commands/DiagnoseCommand.cs
│   ├── Documents/MecDocument.cs     # detecção de tipo/versão, carga segura (sem DTD)
│   ├── Analyzers/                   # PiiCollector, Schema, Diploma, Historico, Signature
│   ├── Signatures/                  # MecSignedXml (ajustes ao SignedXml), CertificateSubject
│   ├── Privacy/PiiRedactor.cs
│   ├── Validation/DocumentNumbers.cs  # CPF / CNPJ (inclusive alfanumérico)
│   ├── Models/                      # DTOs da saída JSON (schema diploma-diagnostic-v1)
│   ├── Output/JsonEmitter.cs
│   └── Schemas/v1.05/               # XSDs oficiais embutidos (ver Schemas/README.md)
└── DiplomaDiagnosticCli.Tests/      # xUnit
    ├── Fixtures/                    # XMLs sintéticos SEM assinatura (dados fictícios)
    └── Support/FixtureSigner.cs     # assina na hora com certificados autoassinados de TESTE
```

## Build

```powershell
dotnet build code/DiplomaDiagnosticCli
```

## Testes

```powershell
dotnet test code/DiplomaDiagnosticCli.slnx
```

As fixtures são assinadas durante o teste com certificados autoassinados
(e-CPF / e-CNPJ fictícios), no mesmo formato dos assinadores reais: XAdES com
`SignedProperties`, referência com `enveloped-signature` + XPath
`not(ancestor-or-self::ds:Signature)` + c14n exclusiva.

Os testes de interoperabilidade (`ExternalSampleTests`) usam os diplomas de
exemplo publicados pela UFSC, assinados com certificado ICP-Brasil real. Eles
**não são redistribuídos** aqui (sem licença explícita; o certificado é de uma
pessoa real) — baixe-os para uma pasta ignorada pelo git:

```powershell
pwsh scripts/Get-ExternalSamples.ps1
```

Sem eles, esses 3 testes aparecem como ignorados.

Para regenerar os exemplos de `samples/`:

```powershell
$env:DIPLOMA_DIAG_WRITE_SAMPLES = '1'; dotnet test code/DiplomaDiagnosticCli.Tests --filter SampleGenerator
```

## Uso

```powershell
dotnet run --project code/DiplomaDiagnosticCli -- `
  --file historico.xml `
  --output historico_diagnostic.json `
  --verbose
```

Opções:
- `--file <path>` (obrigatório): XML do Diploma ou Histórico
- `--output <path>`: JSON de saída (padrão: `<arquivo>_diagnostic.json`); `-` escreve no stdout (UTF-8) e o resumo vai para o stderr
- `--format <json|json-pretty>` (padrão: `json-pretty`)
- `--no-mask`: **não** mascara dados pessoais (padrão: mascara)
- `--verbose`: traces `[TRACE]`/`[INFO]` em stderr

### Exit codes

| Código | Significado |
|---|---|
| 0 | Diagnóstico emitido (mesmo que o documento tenha erros) |
| 1 | Arquivo inexistente ou XML que não é do MEC |
| 2 | XML mal formado (ou com DTD — recusado) |
| 3 | Erro interno de análise |
| 64 | Erro de argumentos de CLI |

## Publish (executável único)

```powershell
dotnet publish code/DiplomaDiagnosticCli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Changelog

### 0.1.0 — 2026-10-05 — MVP

- Tipos: Diploma; Histórico Parcial, Final e Segunda Via Nato Físico.
- XSD v1.05 embutido; outras versões detectadas e reportadas.
- Inventário + verificação XMLDSig das assinaturas, com os dois ajustes ao
  `SignedXml` necessários para o padrão MEC (ver especificação, seção 2.1).
- Regras de negócio de Diploma e Histórico (catálogo na especificação, seção 4).
- Mascaramento de dados pessoais por padrão, inclusive em mensagens do XSD.
- Validação: 53 testes (50 com fixtures sintéticas + 3 de interoperabilidade
  com assinaturas reais).
