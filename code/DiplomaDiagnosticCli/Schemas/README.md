# XSDs do Diploma Digital (MEC) — v1.05

Subconjunto do pacote XSD v1.05 publicado pelo MEC
(<https://www.gov.br/mec/pt-br/diploma-digital/dados>, versão vigente
conferida em 02/10/2026), limitado ao necessário para Diploma e Histórico
Escolar. Embutidos no executável como recursos; a validação nunca acessa a
rede.

| Arquivo | SHA-256 |
|---|---|
| `DiplomaDigital_v1.05.xsd` | `ed950536d87194f37b8ebae14f85e28a739a08bc432e3ad745da556bfc7a59c4` |
| `HistoricoEscolarDigital_v1.05.xsd` | `fa358a29acd6e9e024814c2ff242d5348a0ae99643f800346997092efc7651ea` |
| `leiauteDiplomaDigital_v1.05.xsd` | `c239fa0676a9da191a2f8941b80a8fff5dafda3e28dc52d382d7cdbd12325fcc` |
| `leiauteHistoricoEscolar_v1.05.xsd` | `c22beadd5d41e70b6f73067995fa442b8411982577c66ec971899b3fca37f86c` |
| `tiposBasicos_v1.05.xsd` | `65cdb9adea3d2355ae929252c250bdbbd4d8d4b4d7686c5a3aea1ca2c864c447` |
| `xmldsig-core-schema_v1.1.xsd` | `35cf8197da812c85e40d57891b35c94187569ed474a2dac813ce5090dafcd35c` |

## Por que não são os arquivos baixados direto do gov.br

O portal gov.br serve os XSDs com **todo `http://` reescrito para
`https://`** — inclusive URIs de namespace. O resultado não é um XSD válido:

```
xmlns:xs="https://www.w3.org/2001/XMLSchema"          ← deveria ser http://
targetNamespace="https://portal.mec.gov.br/..."       ← não bate com os XMLs reais (http://)
```

`XmlSchemaSet` recusa o arquivo: *"The root element of a W3C XML Schema should
be &lt;schema&gt; and its namespace should be 'http://www.w3.org/2001/XMLSchema'"*.

Os arquivos aqui são a versão com `http://` (cópia do repositório
`h3nrique/DiplomaDigitalModel`, MIT). Comparação feita em 02/10/2026: após
desfazer a reescrita `https→http`, os arquivos do gov.br são byte a byte
idênticos a estes (a única diferença residual é um comentário de
`tiposBasicos` que já era `https` no original).

O próprio CLI reporta `XSD_NAMESPACE_HTTPS_REWRITE` quando encontra um XML
gerado com o namespace reescrito.
