using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using DiplomaDiagnosticCli.Documents;
using DiplomaDiagnosticCli.Models;

namespace DiplomaDiagnosticCli.Analyzers;

/// <summary>
/// Validates the document against the embedded MEC XSD of its declared layout version. Only v1.05 is
/// embedded; other versions are detected and reported, never validated against a different XSD
/// (that only produces noise — e.g. every v1.01 file fails v1.05 on the versao enum alone).
/// </summary>
public sealed class SchemaAnalyzer : IDocumentAnalyzer
{
    public const int MaxReportedErrors = 50;
    public const string SupportedVersion = "1.05";

    private static readonly Lazy<XmlSchemaSet> SchemaSet = new(LoadSchemaSet);

    public void Analyze(MecDocument document, AnalysisContext context)
    {
        var result = context.SchemaValidation;

        if (document.UsesHttpsNamespace)
        {
            context.Error("XSD_NAMESPACE_HTTPS_REWRITE",
                $"Root namespace is '{MecDocument.MecNamespaceHttps}'; the MEC namespace is '{MecDocument.MecNamespace}' (http). " +
                "This is the signature of the XSD files as served by the gov.br portal, which rewrites every http:// to https:// — including namespace URIs.",
                $"element::{document.RootName}",
                "Regenerate the XML with the http:// namespace. Do not use the XSDs downloaded straight from gov.br without reverting the rewrite.");
        }

        // Without a matching root declaration the validator finds "no schema information" (a warning,
        // not an error) and would report the file as valid without having checked anything.
        if (document.Kind == DocumentKind.Unsupported || document.UsesHttpsNamespace)
        {
            result.Validated = false;
            return;
        }

        if (document.LayoutVersion != SupportedVersion)
        {
            result.Validated = false;
            context.Warning("XSD_VERSION_UNSUPPORTED",
                $"Layout version '{document.LayoutVersion ?? "(missing)"}' has no embedded XSD; only {SupportedVersion} (current MEC version) is validated.",
                "attribute::versao",
                "Structural validation skipped. Business rules and signatures were still analyzed.");
            return;
        }

        result.Validated = true;
        result.XsdVersion = SupportedVersion;
        var errors = new List<SchemaErrorDto>();
        int count = 0;

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = SchemaSet.Value,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        // Only errors count. Warnings are not reported at all: ds:Object accepts any content with
        // processContents="lax", so every XAdES element would otherwise raise "no schema information".
        settings.ValidationEventHandler += (_, e) =>
        {
            if (e.Severity != XmlSeverityType.Error)
                return;
            count++;
            if (errors.Count < MaxReportedErrors)
            {
                errors.Add(new SchemaErrorDto
                {
                    Line = e.Exception.LineNumber,
                    Position = e.Exception.LinePosition,
                    Severity = e.Severity == XmlSeverityType.Error ? "ERROR" : "WARNING",
                    Message = context.Redactor.Scrub(e.Message)
                });
            }
        };

        // Validate the original bytes (line numbers must match the user's file), not the DOM.
        using (var reader = XmlReader.Create(new MemoryStream(document.RawBytes), settings))
            while (reader.Read()) { }

        result.ErrorCount = count;
        result.Errors = errors;
        result.Valid = count == 0;
        context.Trace("TRACE", $"XSD {SupportedVersion}: {count} validation event(s)");

        if (count > 0)
        {
            context.Error("XSD_VALIDATION_FAILED",
                $"{count} XSD {SupportedVersion} validation error(s); see schema_validation.errors.",
                $"element::{document.RootName}");
        }
    }

    private static XmlSchemaSet LoadSchemaSet()
    {
        var resolver = new EmbeddedSchemaResolver();
        var set = new XmlSchemaSet { XmlResolver = resolver };
        foreach (string entry in new[] { "DiplomaDigital_v1.05.xsd", "HistoricoEscolarDigital_v1.05.xsd" })
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = resolver };
            using var reader = XmlReader.Create(resolver.Open(entry), settings, EmbeddedSchemaResolver.BaseUri + entry);
            set.Add(null, reader);
        }
        set.Compile();
        return set;
    }

    /// <summary>
    /// Serves include/import targets from the embedded resources. Anything else (e.g. the W3C DTD
    /// referenced by xmldsig-core-schema) gets an empty stream: validation never touches the network.
    /// </summary>
    private sealed class EmbeddedSchemaResolver : XmlResolver
    {
        public const string BaseUri = "embedded:///Schemas/v1.05/";
        private static readonly Assembly Assembly = typeof(SchemaAnalyzer).Assembly;

        public Stream Open(string fileName) =>
            Assembly.GetManifestResourceStream("Schemas.v1.05." + fileName)
            ?? throw new FileNotFoundException($"Embedded XSD not found: {fileName}");

        public override Uri ResolveUri(Uri? baseUri, string? relativeUri) =>
            new(BaseUri + Path.GetFileName(relativeUri ?? string.Empty));

        public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            string name = Path.GetFileName(absoluteUri.AbsolutePath);
            return name.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase) ? Open(name) : new MemoryStream();
        }
    }
}
