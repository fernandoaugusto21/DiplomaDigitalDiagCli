using System.Security.Cryptography;
using System.Xml;
using DiplomaDiagnosticCli.Analyzers;
using DiplomaDiagnosticCli.Documents;
using DiplomaDiagnosticCli.Models;
using DiplomaDiagnosticCli.Output;
using DiplomaDiagnosticCli.Privacy;

namespace DiplomaDiagnosticCli.Commands;

public sealed class DiagnoseCommand
{
    public const string ParserVersion = "diploma-diagnostic-0.1.0";

    public int Run(string filePath, string? outputPath, bool verbose, string format, bool mask)
    {
        void Trace(string level, string message)
        {
            if (verbose) Console.Error.WriteLine($"[{level}] {message}");
        }

        Trace("TRACE", $"Opening file: {filePath}");

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"✗ Error: File {filePath} not found");
            return ExitCode.InvalidFile;
        }

        MecDocument document;
        try
        {
            document = MecDocument.Load(filePath);
        }
        catch (XmlException ex)
        {
            Console.Error.WriteLine($"✗ Error: {filePath} is not well-formed XML");
            Console.Error.WriteLine($"✗ Details: {ex.Message}");
            return ExitCode.ParseError;
        }

        if (!document.IsMecNamespace)
        {
            Console.Error.WriteLine($"✗ Error: {filePath} is not a MEC Diploma Digital document");
            Console.Error.WriteLine($"✗ Details: root element {{{document.RootNamespace}}}{document.RootName}; expected namespace {MecDocument.MecNamespace}");
            return ExitCode.InvalidFile;
        }
        Trace("TRACE", $"Root {document.RootName} → {document.KindLabel}, versao={document.LayoutVersion ?? "(none)"}");

        DiagnosticResult result;
        try
        {
            result = Analyze(document, filePath, mask, Trace);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("✗ Error: Analysis failed");
            Console.Error.WriteLine($"✗ Details: {ex.Message}");
            return ExitCode.AnalysisError;
        }

        string resolvedOutput = outputPath ?? $"{Path.GetFileNameWithoutExtension(filePath)}_diagnostic.json";
        bool pretty = !string.Equals(format, "json", StringComparison.OrdinalIgnoreCase);
        JsonEmitter.Write(result, resolvedOutput, pretty);

        // With "-" the JSON goes to stdout, so the human summary goes to stderr.
        TextWriter human = resolvedOutput == "-" ? Console.Error : Console.Out;
        var d = result.Diagnostics;
        int validSigs = result.Signatures.Count(s => s.XmldsigValid);
        human.WriteLine($"✓ Parsed {Path.GetFileName(filePath)} ({result.Metadata.DocumentType}, layout {result.Metadata.LayoutVersion ?? "?"})");
        human.WriteLine($"✓ Signatures: {validSigs}/{result.Signatures.Count} XMLDSig-valid; XSD: {(result.SchemaValidation.Validated ? (result.SchemaValidation.Valid == true ? "valid" : $"{result.SchemaValidation.ErrorCount} error(s)") : "not validated")}");
        human.WriteLine($"✓ Analysis complete: {d.Errors.Count} errors, {d.Warnings.Count} warnings, {d.Info.Count} info{(mask ? " (personal data masked)" : "")}");
        if (resolvedOutput != "-")
            human.WriteLine($"✓ Diagnostic JSON written to {resolvedOutput}");

        return ExitCode.Success;
    }

    /// <summary>Runs the whole pipeline on an already-loaded document. Public for tests.</summary>
    public static DiagnosticResult Analyze(MecDocument document, string filePath, bool mask,
        Action<string, string>? trace = null, DateOnly? today = null)
    {
        var context = new AnalysisContext(new PiiRedactor(mask))
        {
            Trace = trace ?? ((_, _) => { }),
            Today = today ?? DateOnly.FromDateTime(DateTime.UtcNow)
        };

        if (document.Kind == DocumentKind.Unsupported)
        {
            context.Error("DOCUMENT_TYPE_UNSUPPORTED",
                $"Root element '{document.RootName}' is a MEC document this tool does not analyze; supported: Diploma, DocumentoHistoricoEscolarParcial, DocumentoHistoricoEscolarFinal, DocumentoHistoricoEscolarSegundaViaNatoFisico.",
                $"element::{document.RootName}");
        }

        // PiiCollector must run first: later analyzers' messages are scrubbed against what it registers.
        IDocumentAnalyzer[] analyzers =
        [
            new PiiCollector(),
            new SchemaAnalyzer(),
            new DiplomaAnalyzer(),
            new HistoricoAnalyzer(),
            new SignatureAnalyzer()
        ];
        foreach (var analyzer in analyzers)
            analyzer.Analyze(document, context);

        context.Trace("INFO", $"Analysis: {context.Errors.Count} errors, {context.Warnings.Count} warnings, {context.Info.Count} info");

        return new DiagnosticResult
        {
            Metadata = new MetadataDto
            {
                FilePath = Path.GetFullPath(filePath),
                FileSizeBytes = document.RawBytes.LongLength,
                FileSha256 = Convert.ToHexStringLower(SHA256.HashData(document.RawBytes)),
                DocumentType = document.KindLabel,
                RootElement = document.RootName,
                LayoutVersion = document.LayoutVersion,
                Ambiente = document.IsHistorico ? NullIfEmpty(document.InfElement?.GetAttribute("ambiente")) ?? "Produção" : null,
                ParsedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ParserVersion = ParserVersion,
                PiiMasked = mask
            },
            Document = context.Document,
            SchemaValidation = context.SchemaValidation,
            Signatures = context.Signatures,
            Diagnostics = new DiagnosticsDto
            {
                Errors = context.Errors,
                Warnings = context.Warnings,
                Info = context.Info
            }
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
