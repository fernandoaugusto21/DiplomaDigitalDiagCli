using System.CommandLine;
using System.Text;
using DiplomaDiagnosticCli;
using DiplomaDiagnosticCli.Commands;

// Messages carry Portuguese accents and ✓/✗; don't let the Windows console code page mangle them.
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
Console.Error.Flush();

var fileOption = new Option<string>("--file")
{
    Description = "Path to the Diploma or Histórico Escolar XML file to analyze",
    Required = true
};

var outputOption = new Option<string?>("--output")
{
    Description = "Output JSON file path (default: <file_name>_diagnostic.json; use '-' for stdout)"
};

var verboseOption = new Option<bool>("--verbose")
{
    Description = "Emit debug/trace output on stderr"
};

var formatOption = new Option<string>("--format")
{
    Description = "Output format",
    DefaultValueFactory = _ => "json-pretty"
};
formatOption.AcceptOnlyFromAmong("json", "json-pretty");

var noMaskOption = new Option<bool>("--no-mask")
{
    Description = "Do NOT mask personal data (CPF, names, RG, birth date) in the output. Masking is on by default."
};

var rootCommand = new RootCommand("diploma-diagnostic: parses MEC Diploma Digital / Histórico Escolar Digital XML files and emits a structured JSON diagnostic")
{
    fileOption,
    outputOption,
    verboseOption,
    formatOption,
    noMaskOption
};

rootCommand.SetAction(parseResult =>
{
    string file = parseResult.GetValue(fileOption)!;
    string? output = parseResult.GetValue(outputOption);
    bool verbose = parseResult.GetValue(verboseOption);
    string format = parseResult.GetValue(formatOption)!;
    bool mask = !parseResult.GetValue(noMaskOption);

    return new DiagnoseCommand().Run(file, output, verbose, format, mask);
});

ParseResult parsed = rootCommand.Parse(args);
if (parsed.Errors.Count > 0)
{
    foreach (var error in parsed.Errors)
        Console.Error.WriteLine(error.Message);
    return ExitCode.CliUsageError;
}

return parsed.Invoke();
