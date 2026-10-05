using System.Text.Encodings.Web;
using System.Text.Json;
using DiplomaDiagnosticCli.Models;

namespace DiplomaDiagnosticCli.Output;

public static class JsonEmitter
{
    public static string Serialize(DiagnosticResult result, bool pretty)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = pretty,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
            // Keep accented Portuguese readable for the LLM consumer instead of ç escapes.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        return JsonSerializer.Serialize(result, options);
    }

    public static void Write(DiagnosticResult result, string outputPath, bool pretty)
    {
        string json = Serialize(result, pretty);
        if (outputPath == "-")
        {
            // Raw UTF-8 bytes: Console.Out would encode with the console code page on Windows
            // (cp850/cp1252), mangling accents for whatever reads the pipe.
            using var stdout = Console.OpenStandardOutput();
            stdout.Write(System.Text.Encoding.UTF8.GetBytes(json + Environment.NewLine));
        }
        else
        {
            File.WriteAllText(outputPath, json);
        }
    }
}
