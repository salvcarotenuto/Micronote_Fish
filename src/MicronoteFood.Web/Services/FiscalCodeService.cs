namespace MicronoteFood.Web.Services;

public static class FiscalCodeService
{
    private static readonly Dictionary<char, int> OddFiscalCodeValues = new()
    {
        ['0'] = 1, ['1'] = 0, ['2'] = 5, ['3'] = 7, ['4'] = 9,
        ['5'] = 13, ['6'] = 15, ['7'] = 17, ['8'] = 19, ['9'] = 21,
        ['A'] = 1, ['B'] = 0, ['C'] = 5, ['D'] = 7, ['E'] = 9,
        ['F'] = 13, ['G'] = 15, ['H'] = 17, ['I'] = 19, ['J'] = 21,
        ['K'] = 2, ['L'] = 4, ['M'] = 18, ['N'] = 20, ['O'] = 11,
        ['P'] = 3, ['Q'] = 6, ['R'] = 8, ['S'] = 12, ['T'] = 14,
        ['U'] = 16, ['V'] = 10, ['W'] = 22, ['X'] = 25, ['Y'] = 24,
        ['Z'] = 23
    };

    public static string NormalizeFiscalCode(string? value) =>
        new((value ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

    public static string NormalizeVatNumber(string? value) =>
        new((value ?? string.Empty)
            .Where(char.IsDigit)
            .Take(11)
            .ToArray());

    public static bool IsValidVatNumber(string? value)
    {
        var vatNumber = NormalizeFiscalCode(value);
        if (vatNumber.Length != 11 || vatNumber.Any(character => !char.IsDigit(character)))
        {
            return false;
        }

        var sum = 0;
        for (var index = 0; index < 11; index++)
        {
            var digit = vatNumber[index] - '0';
            if (index % 2 == 0)
            {
                sum += digit;
                continue;
            }

            var doubled = digit * 2;
            sum += doubled > 9 ? doubled - 9 : doubled;
        }

        return sum % 10 == 0;
    }

    public static bool IsValidFiscalCode(string? value)
    {
        var fiscalCode = NormalizeFiscalCode(value);
        return fiscalCode.Length switch
        {
            11 => IsValidVatNumber(fiscalCode),
            16 => IsValidPersonalFiscalCode(fiscalCode),
            _ => false
        };
    }

    private static bool IsValidPersonalFiscalCode(string fiscalCode)
    {
        if (fiscalCode.Length != 16 || fiscalCode.Any(character => !char.IsLetterOrDigit(character)))
        {
            return false;
        }

        var sum = 0;
        for (var index = 0; index < 15; index++)
        {
            var character = fiscalCode[index];
            var value = index % 2 == 0
                ? OddFiscalCodeValues.GetValueOrDefault(character, -1)
                : EvenFiscalCodeValue(character);
            if (value < 0)
            {
                return false;
            }

            sum += value;
        }

        var expectedControl = (char)('A' + (sum % 26));
        return fiscalCode[15] == expectedControl;
    }

    private static int EvenFiscalCodeValue(char character)
    {
        if (character is >= '0' and <= '9')
        {
            return character - '0';
        }

        if (character is >= 'A' and <= 'Z')
        {
            return character - 'A';
        }

        return -1;
    }
}

