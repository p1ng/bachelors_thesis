public final class IbanValidator {
    private static final int MAX_IBAN_LENGTH = 34;

    private enum ValidationStatus {
        VALID,
        EMPTY,
        TOO_LONG,
        INVALID_FORMAT,
        UNSUPPORTED_COUNTRY,
        INVALID_LENGTH,
        INVALID_CHECKSUM
    }

    private static final class ValidationResult {
        private ValidationStatus status;
        private final String normalized;
        private int remainder;

        private ValidationResult(ValidationStatus status, String normalized,
                int remainder) {
            this.status = status;
            this.normalized = normalized;
            this.remainder = remainder;
        }
    }

    private static final class CountryRule {
        private final String code;
        private final int length;

        private CountryRule(String code, int length) {
            this.code = code;
            this.length = length;
        }
    }

    private static final CountryRule[] COUNTRY_RULES = {
            new CountryRule("AT", 20), new CountryRule("BE", 16),
            new CountryRule("CH", 21), new CountryRule("DE", 22),
            new CountryRule("ES", 24), new CountryRule("FR", 27),
            new CountryRule("GB", 22), new CountryRule("IT", 27),
            new CountryRule("NL", 18), new CountryRule("PL", 28)
    };

    private static ValidationResult normalizeIban(String input) {
        if (input == null) {
            return new ValidationResult(ValidationStatus.EMPTY, "", -1);
        }

        StringBuilder output = new StringBuilder();
        for (int i = 0; i < input.length(); ++i) {
            char character = input.charAt(i);

            if (Character.isWhitespace(character)) {
                continue;
            }
            if (output.length() >= MAX_IBAN_LENGTH) {
                return new ValidationResult(ValidationStatus.TOO_LONG, "", -1);
            }

            if (character >= 'a' && character <= 'z') {
                character = (char) (character - 'a' + 'A');
            }
            output.append(character);
        }

        if (output.length() == 0) {
            return new ValidationResult(ValidationStatus.EMPTY, "", -1);
        }
        return new ValidationResult(ValidationStatus.VALID,
                output.toString(), -1);
    }

    private static boolean isAsciiLetter(char character) {
        return character >= 'A' && character <= 'Z';
    }

    private static boolean isAsciiDigit(char character) {
        return character >= '0' && character <= '9';
    }

    private static boolean hasValidFormat(String iban) {
        if (iban.length() < 5
                || !isAsciiLetter(iban.charAt(0))
                || !isAsciiLetter(iban.charAt(1))
                || !isAsciiDigit(iban.charAt(2))
                || !isAsciiDigit(iban.charAt(3))) {
            return false;
        }

        for (int i = 4; i < iban.length(); ++i) {
            char character = iban.charAt(i);
            if (!isAsciiLetter(character) && !isAsciiDigit(character)) {
                return false;
            }
        }
        return true;
    }

    private static int expectedCountryLength(String iban) {
        String countryCode = iban.substring(0, 2);

        for (CountryRule rule : COUNTRY_RULES) {
            if (rule.code.equals(countryCode)) {
                return rule.length;
            }
        }
        return 0;
    }

    private static int appendDigitToRemainder(int remainder, int digit) {
        return (remainder * 10 + digit) % 97;
    }

    private static int processCharacter(int remainder, char character) {
        if (isAsciiDigit(character)) {
            return appendDigitToRemainder(remainder, character - '0');
        }

        int value = character - 'A' + 10;
        remainder = appendDigitToRemainder(remainder, value / 10);
        return appendDigitToRemainder(remainder, value % 10);
    }

    private static int calculateModulo97(String iban) {
        int remainder = 0;

        // ISO 13616 moves the first four characters to the end.
        for (int i = 4; i < iban.length(); ++i) {
            remainder = processCharacter(remainder, iban.charAt(i));
        }
        for (int i = 0; i < 4; ++i) {
            remainder = processCharacter(remainder, iban.charAt(i));
        }

        return remainder;
    }

    private static ValidationResult validateIban(String input) {
        ValidationResult result = normalizeIban(input);
        if (result.status != ValidationStatus.VALID) {
            return result;
        }

        if (!hasValidFormat(result.normalized)) {
            result.status = ValidationStatus.INVALID_FORMAT;
            return result;
        }

        int expectedLength = expectedCountryLength(result.normalized);
        if (expectedLength == 0) {
            result.status = ValidationStatus.UNSUPPORTED_COUNTRY;
            return result;
        }
        if (result.normalized.length() != expectedLength) {
            result.status = ValidationStatus.INVALID_LENGTH;
            return result;
        }

        result.remainder = calculateModulo97(result.normalized);
        if (result.remainder != 1) {
            result.status = ValidationStatus.INVALID_CHECKSUM;
        }
        return result;
    }

    private static String statusMessage(ValidationStatus status) {
        switch (status) {
            case VALID:
                return "valid IBAN";
            case EMPTY:
                return "empty input";
            case TOO_LONG:
                return "IBAN is too long";
            case INVALID_FORMAT:
                return "invalid format";
            case UNSUPPORTED_COUNTRY:
                return "unsupported country code";
            case INVALID_LENGTH:
                return "invalid country-specific length";
            case INVALID_CHECKSUM:
                return "invalid modulo-97 checksum";
            default:
                return "unknown validation result";
        }
    }

    private static void printResult(String input) {
        ValidationResult result = validateIban(input);

        System.out.println("Input:      " + input);
        if (!result.normalized.isEmpty()) {
            System.out.println("Normalized: " + result.normalized);
        }
        System.out.print("Result:     " + statusMessage(result.status));
        if (result.remainder >= 0) {
            System.out.print(" (remainder: " + result.remainder + ")");
        }
        System.out.println("\n");
    }

    private static void runTestCases() {
        String[] testIbans = {
                "AT61 1904 3002 3457 3201",
                "DE89 3704 0044 0532 0130 00",
                "GB82 WEST 1234 5698 7654 32",
                "AT61 1904 3002 3457 3202",
                "AT61-1904-3002-3457-3201"
        };

        for (String iban : testIbans) {
            printResult(iban);
        }
    }

    public static void main(String[] args) {
        if (args.length == 1) {
            printResult(args[0]);
            return;
        }

        if (args.length > 1) {
            System.err.println("Usage: java IbanValidator [IBAN]");
            System.exit(1);
        }

        runTestCases();
    }
}
