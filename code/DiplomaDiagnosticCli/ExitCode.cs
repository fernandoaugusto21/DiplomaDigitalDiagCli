namespace DiplomaDiagnosticCli;

public static class ExitCode
{
    public const int Success = 0;
    public const int InvalidFile = 1;
    public const int ParseError = 2;
    public const int AnalysisError = 3;
    public const int CliUsageError = 64;
}
