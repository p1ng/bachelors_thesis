using System;
using System.Text;

public static class IbanValidator
{
    private const int MaxIbanLength = 34;

    private enum ValidationStatus
    {
        Valid,
        Empty,
        TooLong,
        InvalidFormat,
        UnsupportedCountry,
        InvalidLength,
        InvalidChecksum
    }

    private sealed class ValidationResult
    {
        public ValidationStatus Status { get; set; }
        public string Normalized { get; }
        public int Remainder { get; set; }

        public ValidationResult(ValidationStatus status, string normalized,
                                int remainder)
        {
            Status = status;
            Normalized = normalized;
            Remainder = remainder;
        }
    }

    private sealed class CountryRule
    {
        public string Code { get; }
        public int Length { get; }

        public CountryRule(string code, int length)
        {
            Code = code;
            Length = length;
        }
    }

    private static readonly CountryRule[] CountryRules =
    {
        new CountryRule("AT", 20), new CountryRule("BE", 16),
        new CountryRule("CH", 21), new CountryRule("DE", 22),
        new CountryRule("ES", 24), new CountryRule("FR", 27),
        new CountryRule("GB", 22), new CountryRule("IT", 27),
        new CountryRule("NL", 18), new CountryRule("PL", 28)
    };

    private static ValidationResult NormalizeIban(string input)
    {
        if (input == null)
        {
            return new ValidationResult(ValidationStatus.Empty, "", -1);
        }

        StringBuilder output = new StringBuilder();
        foreach (char inputCharacter in input)
        {
            char character = inputCharacter;

            if (char.IsWhiteSpace(character))
            {
                continue;
            }
            if (output.Length >= MaxIbanLength)
            {
                return new ValidationResult(ValidationStatus.TooLong, "", -1);
            }

            if (character >= 'a' && character <= 'z')
            {
                character = (char)(character - 'a' + 'A');
            }
            output.Append(character);
        }

        if (output.Length == 0)
        {
            return new ValidationResult(ValidationStatus.Empty, "", -1);
        }
        return new ValidationResult(ValidationStatus.Valid,
                                    output.ToString(), -1);
    }

    private static bool IsAsciiLetter(char character)
    {
        return character >= 'A' && character <= 'Z';
    }

    private static bool IsAsciiDigit(char character)
    {
        return character >= '0' && character <= '9';
    }

    private static bool HasValidFormat(string iban)
    {
        if (iban.Length < 5
            || !IsAsciiLetter(iban[0])
            || !IsAsciiLetter(iban[1])
            || !IsAsciiDigit(iban[2])
            || !IsAsciiDigit(iban[3]))
        {
            return false;
        }

        for (int i = 4; i < iban.Length; ++i)
        {
            char character = iban[i];
            if (!IsAsciiLetter(character) && !IsAsciiDigit(character))
            {
                return false;
            }
        }
        return true;
    }

    private static int ExpectedCountryLength(string iban)
    {
        string countryCode = iban.Substring(0, 2);

        foreach (CountryRule rule in CountryRules)
        {
            if (rule.Code == countryCode)
            {
                return rule.Length;
            }
        }
        return 0;
    }

    private static int AppendDigitToRemainder(int remainder, int digit)
    {
        return (remainder * 10 + digit) % 97;
    }

    private static int ProcessCharacter(int remainder, char character)
    {
        if (IsAsciiDigit(character))
        {
            return AppendDigitToRemainder(remainder, character - '0');
        }

        int value = character - 'A' + 10;
        remainder = AppendDigitToRemainder(remainder, value / 10);
        return AppendDigitToRemainder(remainder, value % 10);
    }

    private static int CalculateModulo97(string iban)
    {
        int remainder = 0;

        // ISO 13616 moves the first four characters to the end.
        for (int i = 4; i < iban.Length; ++i)
        {
            remainder = ProcessCharacter(remainder, iban[i]);
        }
        for (int i = 0; i < 4; ++i)
        {
            remainder = ProcessCharacter(remainder, iban[i]);
        }

        return remainder;
    }

    private static ValidationResult ValidateIban(string input)
    {
        ValidationResult result = NormalizeIban(input);
        if (result.Status != ValidationStatus.Valid)
        {
            return result;
        }

        if (!HasValidFormat(result.Normalized))
        {
            result.Status = ValidationStatus.InvalidFormat;
            return result;
        }

        int expectedLength = ExpectedCountryLength(result.Normalized);
        if (expectedLength == 0)
        {
            result.Status = ValidationStatus.UnsupportedCountry;
            return result;
        }
        if (result.Normalized.Length != expectedLength)
        {
            result.Status = ValidationStatus.InvalidLength;
            return result;
        }

        result.Remainder = CalculateModulo97(result.Normalized);
        if (result.Remainder != 1)
        {
            result.Status = ValidationStatus.InvalidChecksum;
        }
        return result;
    }

    private static string StatusMessage(ValidationStatus status)
    {
        switch (status)
        {
            case ValidationStatus.Valid:
                return "valid IBAN";
            case ValidationStatus.Empty:
                return "empty input";
            case ValidationStatus.TooLong:
                return "IBAN is too long";
            case ValidationStatus.InvalidFormat:
                return "invalid format";
            case ValidationStatus.UnsupportedCountry:
                return "unsupported country code";
            case ValidationStatus.InvalidLength:
                return "invalid country-specific length";
            case ValidationStatus.InvalidChecksum:
                return "invalid modulo-97 checksum";
            default:
                return "unknown validation result";
        }
    }

    private static void PrintResult(string input)
    {
        ValidationResult result = ValidateIban(input);

        Console.WriteLine("Input:      " + input);
        if (result.Normalized.Length != 0)
        {
            Console.WriteLine("Normalized: " + result.Normalized);
        }
        Console.Write("Result:     " + StatusMessage(result.Status));
        if (result.Remainder >= 0)
        {
            Console.Write(" (remainder: " + result.Remainder + ")");
        }
        Console.WriteLine("\n");
    }

    private static void RunTestCases()
    {
        string[] testIbans =
        {
            "AT61 1904 3002 3457 3201",
            "DE89 3704 0044 0532 0130 00",
            "GB82 WEST 1234 5698 7654 32",
            "AT61 1904 3002 3457 3202",
            "AT61-1904-3002-3457-3201"
        };

        foreach (string iban in testIbans)
        {
            PrintResult(iban);
        }
    }

    public static int Main(string[] args)
    {
        if (args.Length == 1)
        {
            PrintResult(args[0]);
            return 0;
        }

        if (args.Length > 1)
        {
            Console.Error.WriteLine("Usage: IbanValidator [IBAN]");
            return 1;
        }

        RunTestCases();
        return 0;
    }
}
