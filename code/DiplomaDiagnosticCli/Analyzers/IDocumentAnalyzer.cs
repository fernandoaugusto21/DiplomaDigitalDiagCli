using DiplomaDiagnosticCli.Documents;

namespace DiplomaDiagnosticCli.Analyzers;

public interface IDocumentAnalyzer
{
    void Analyze(MecDocument document, AnalysisContext context);
}
