using System.Text.Json.Serialization;

namespace DiplomaDiagnosticCli.Models;

public sealed class DiagnosticResult
{
    [JsonPropertyName("$schema")]
    public string Schema { get; init; } = "diploma-diagnostic-v1";

    public MetadataDto Metadata { get; init; } = new();

    /// <summary><see cref="DiplomaSummaryDto"/> or <see cref="HistoricoSummaryDto"/>; null when the
    /// document type is not supported. Declared as object so the runtime type is serialized.</summary>
    public object? Document { get; init; }

    [JsonPropertyName("schema_validation")]
    public SchemaValidationDto SchemaValidation { get; init; } = new();

    public List<SignatureDto> Signatures { get; init; } = [];

    public DiagnosticsDto Diagnostics { get; init; } = new();
}

public sealed class MetadataDto
{
    [JsonPropertyName("file_path")]
    public string FilePath { get; init; } = string.Empty;

    [JsonPropertyName("file_size_bytes")]
    public long FileSizeBytes { get; init; }

    [JsonPropertyName("file_sha256")]
    public string FileSha256 { get; init; } = string.Empty;

    /// <summary>diploma | historico_parcial | historico_final | historico_segunda_via_nato_fisico | unsupported</summary>
    [JsonPropertyName("document_type")]
    public string DocumentType { get; init; } = string.Empty;

    [JsonPropertyName("root_element")]
    public string RootElement { get; init; } = string.Empty;

    /// <summary>Value of the <c>versao</c> attribute on the inf* node (e.g. "1.05").</summary>
    [JsonPropertyName("layout_version")]
    public string? LayoutVersion { get; init; }

    /// <summary>"Produção" or "Homologação" (Histórico only; absent attribute means Produção).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ambiente { get; init; }

    [JsonPropertyName("parsed_at_utc")]
    public string ParsedAtUtc { get; init; } = string.Empty;

    [JsonPropertyName("parser_version")]
    public string ParserVersion { get; init; } = string.Empty;

    [JsonPropertyName("pii_masked")]
    public bool PiiMasked { get; init; }
}

public sealed class SchemaValidationDto
{
    /// <summary>False when the layout version has no embedded XSD (only v1.05 is embedded).</summary>
    public bool Validated { get; set; }

    [JsonPropertyName("xsd_version")]
    public string? XsdVersion { get; set; }

    public bool? Valid { get; set; }

    [JsonPropertyName("error_count")]
    public int ErrorCount { get; set; }

    /// <summary>Capped at <see cref="Analyzers.SchemaAnalyzer.MaxReportedErrors"/>; see error_count for the total.</summary>
    public List<SchemaErrorDto> Errors { get; set; } = [];
}

public sealed class SchemaErrorDto
{
    public int Line { get; init; }
    public int Position { get; init; }
    public string Severity { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class SignatureDto
{
    /// <summary>1-based, in document order.</summary>
    public int Index { get; init; }

    /// <summary>Local name of the element the ds:Signature sits in (DadosDiploma, DadosRegistro, Diploma, DocumentoHistoricoEscolarFinal...).</summary>
    [JsonPropertyName("parent_element")]
    public string ParentElement { get; init; } = string.Empty;

    /// <summary>Local names of the elements the data references point to ("(document)" for URI="").</summary>
    [JsonPropertyName("signed_elements")]
    public List<string> SignedElements { get; init; } = [];

    [JsonPropertyName("reference_uris")]
    public List<string> ReferenceUris { get; init; } = [];

    public List<string> Transforms { get; init; } = [];

    public SignerDto? Signer { get; init; }

    [JsonPropertyName("signing_time")]
    public string? SigningTime { get; init; }

    public PolicyDto? Policy { get; init; }

    [JsonPropertyName("has_signed_properties")]
    public bool HasSignedProperties { get; init; }

    /// <summary>Result of XMLDSig core verification (digests + signature value against the embedded certificate).</summary>
    [JsonPropertyName("xmldsig_valid")]
    public bool XmldsigValid { get; init; }

    [JsonPropertyName("failure_reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FailureReason { get; init; }

    /// <summary>ICP-Brasil chain, revocation (LCR/OCSP) and timestamps are out of scope: always "unvalidated".</summary>
    [JsonPropertyName("icp_brasil_chain")]
    public string IcpBrasilChain { get; init; } = "unvalidated";
}

public sealed class SignerDto
{
    /// <summary>Holder name from the certificate CN (masked unless --no-mask).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>e-CPF | e-CNPJ | unknown — inferred from the CN suffix / OU.</summary>
    [JsonPropertyName("certificate_type")]
    public string CertificateType { get; init; } = "unknown";

    /// <summary>CPF (masked) or CNPJ taken from the CN suffix "NAME:DOCUMENT".</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Document { get; init; }

    public string Issuer { get; init; } = string.Empty;

    [JsonPropertyName("not_before")]
    public string NotBefore { get; init; } = string.Empty;

    [JsonPropertyName("not_after")]
    public string NotAfter { get; init; } = string.Empty;
}

public sealed class PolicyDto
{
    public string? Oid { get; init; }

    /// <summary>Policy name from the SPURI (e.g. PA_AD_RB_v2_4), when present.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Uri { get; init; }
}

public sealed class DiagnosticsDto
{
    public List<Finding> Errors { get; init; } = [];
    public List<Finding> Warnings { get; init; } = [];
    public List<Finding> Info { get; init; } = [];
}
