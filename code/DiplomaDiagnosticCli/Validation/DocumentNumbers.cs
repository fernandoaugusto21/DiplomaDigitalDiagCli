namespace DiplomaDiagnosticCli.Validation;

/// <summary>Check-digit validation for Brazilian CPF and CNPJ.</summary>
public static class DocumentNumbers
{
    public static bool IsValidCpf(string? value)
    {
        if (value is null || value.Length != 11 || !value.All(char.IsAsciiDigit))
            return false;
        if (value.All(c => c == value[0]))
            return false;

        int[] d = value.Select(c => c - '0').ToArray();
        return d[9] == CpfDigit(d, 9) && d[10] == CpfDigit(d, 10);
    }

    private static int CpfDigit(int[] d, int count)
    {
        int sum = 0;
        for (int i = 0; i < count; i++)
            sum += d[i] * (count + 1 - i);
        int rest = sum * 10 % 11;
        return rest == 10 ? 0 : rest;
    }

    /// <summary>Accepts both the numeric CNPJ and the alphanumeric format (12 chars [0-9A-Z] + 2 digits)
    /// introduced by the Receita Federal in 2026; letters are valued as (ASCII - 48).</summary>
    public static bool IsValidCnpj(string? value)
    {
        if (value is null || value.Length != 14)
            return false;
        value = value.ToUpperInvariant();
        if (!value[..12].All(c => char.IsAsciiDigit(c) || c is >= 'A' and <= 'Z') || !value[12..].All(char.IsAsciiDigit))
            return false;
        if (value.All(c => c == value[0]))
            return false;

        int[] v = value.Select(c => c - '0').ToArray();
        return v[12] == CnpjDigit(v, 12) && v[13] == CnpjDigit(v, 13);
    }

    private static int CnpjDigit(int[] v, int count)
    {
        int sum = 0;
        int weight = count - 7; // 5 for the 1st digit, 6 for the 2nd; cycles 9..2
        for (int i = 0; i < count; i++)
        {
            sum += v[i] * weight;
            weight = weight == 2 ? 9 : weight - 1;
        }
        int rest = sum % 11;
        return rest < 2 ? 0 : 11 - rest;
    }
}
