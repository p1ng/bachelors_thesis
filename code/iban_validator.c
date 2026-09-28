#include <ctype.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>

#define MAX_IBAN_LENGTH 34

typedef enum
{
    IBAN_VALID,
    IBAN_EMPTY,
    IBAN_TOO_LONG,
    IBAN_INVALID_FORMAT,
    IBAN_UNSUPPORTED_COUNTRY,
    IBAN_INVALID_LENGTH,
    IBAN_INVALID_CHECKSUM
} ValidationStatus;

typedef struct
{
    ValidationStatus status;
    char normalized[MAX_IBAN_LENGTH + 1];
    int remainder;
} ValidationResult;

typedef struct
{
    char code[3];
    size_t length;
} CountryRule;

static const CountryRule COUNTRY_RULES[] = {
    {"AT", 20}, {"BE", 16}, {"CH", 21}, {"DE", 22}, {"ES", 24}, {"FR", 27}, {"GB", 22}, {"IT", 27}, {"NL", 18}, {"PL", 28}};

static ValidationStatus normalize_iban(const char *input, char *output)
{
    size_t output_length = 0;

    if (input == NULL)
    {
        return IBAN_EMPTY;
    }

    for (size_t i = 0; input[i] != '\0'; ++i)
    {
        unsigned char character = (unsigned char)input[i];

        if (isspace(character))
        {
            continue;
        }
        if (output_length >= MAX_IBAN_LENGTH)
        {
            output[0] = '\0';
            return IBAN_TOO_LONG;
        }

        output[output_length++] = (char)toupper(character);
    }

    output[output_length] = '\0';
    return output_length == 0 ? IBAN_EMPTY : IBAN_VALID;
}

static bool has_valid_format(const char *iban)
{
    size_t length = strlen(iban);

    if (length < 5 || !isalpha((unsigned char)iban[0]) ||
        !isalpha((unsigned char)iban[1]) ||
        !isdigit((unsigned char)iban[2]) ||
        !isdigit((unsigned char)iban[3]))
    {
        return false;
    }

    for (size_t i = 4; i < length; ++i)
    {
        if (!isalnum((unsigned char)iban[i]))
        {
            return false;
        }
    }

    return true;
}

static size_t expected_country_length(const char *iban)
{
    size_t rule_count = sizeof(COUNTRY_RULES) / sizeof(COUNTRY_RULES[0]);

    for (size_t i = 0; i < rule_count; ++i)
    {
        if (iban[0] == COUNTRY_RULES[i].code[0] &&
            iban[1] == COUNTRY_RULES[i].code[1])
        {
            return COUNTRY_RULES[i].length;
        }
    }

    return 0;
}

static int append_digit_to_remainder(int remainder, int digit)
{
    return (remainder * 10 + digit) % 97;
}

static int process_character(int remainder, char character)
{
    if (isdigit((unsigned char)character))
    {
        return append_digit_to_remainder(remainder, character - '0');
    }

    int value = character - 'A' + 10;
    remainder = append_digit_to_remainder(remainder, value / 10);
    return append_digit_to_remainder(remainder, value % 10);
}

static int calculate_modulo_97(const char *iban)
{
    size_t length = strlen(iban);
    int remainder = 0;

    /* ISO 13616 moves the first four characters to the end. */
    for (size_t i = 4; i < length; ++i)
    {
        remainder = process_character(remainder, iban[i]);
    }
    for (size_t i = 0; i < 4; ++i)
    {
        remainder = process_character(remainder, iban[i]);
    }

    return remainder;
}

static ValidationResult validate_iban(const char *input)
{
    ValidationResult result = {IBAN_VALID, "", -1};
    size_t expected_length;

    result.status = normalize_iban(input, result.normalized);
    if (result.status != IBAN_VALID)
    {
        return result;
    }

    if (!has_valid_format(result.normalized))
    {
        result.status = IBAN_INVALID_FORMAT;
        return result;
    }

    expected_length = expected_country_length(result.normalized);
    if (expected_length == 0)
    {
        result.status = IBAN_UNSUPPORTED_COUNTRY;
        return result;
    }
    if (strlen(result.normalized) != expected_length)
    {
        result.status = IBAN_INVALID_LENGTH;
        return result;
    }

    result.remainder = calculate_modulo_97(result.normalized);
    if (result.remainder != 1)
    {
        result.status = IBAN_INVALID_CHECKSUM;
    }

    return result;
}

static const char *status_message(ValidationStatus status)
{
    switch (status)
    {
    case IBAN_VALID:
        return "valid IBAN";
    case IBAN_EMPTY:
        return "empty input";
    case IBAN_TOO_LONG:
        return "IBAN is too long";
    case IBAN_INVALID_FORMAT:
        return "invalid format";
    case IBAN_UNSUPPORTED_COUNTRY:
        return "unsupported country code";
    case IBAN_INVALID_LENGTH:
        return "invalid country-specific length";
    case IBAN_INVALID_CHECKSUM:
        return "invalid modulo-97 checksum";
    default:
        return "unknown validation result";
    }
}

static void print_result(const char *input)
{
    ValidationResult result = validate_iban(input);

    printf("Input:      %s\n", input);
    if (result.normalized[0] != '\0')
    {
        printf("Normalized: %s\n", result.normalized);
    }
    printf("Result:     %s", status_message(result.status));
    if (result.remainder >= 0)
    {
        printf(" (remainder: %d)", result.remainder);
    }
    printf("\n\n");
}

static void run_test_cases(void)
{
    static const char *TEST_IBANS[] = {
        "AT61 1904 3002 3457 3201",
        "DE89 3704 0044 0532 0130 00",
        "GB82 WEST 1234 5698 7654 32",
        "AT61 1904 3002 3457 3202",
        "AT61-1904-3002-3457-3201"};
    size_t test_count = sizeof(TEST_IBANS) / sizeof(TEST_IBANS[0]);

    for (size_t i = 0; i < test_count; ++i)
    {
        print_result(TEST_IBANS[i]);
    }
}

int main(int argc, char *argv[])
{
    if (argc == 2)
    {
        print_result(argv[1]);
        return 0;
    }

    if (argc > 2)
    {
        fprintf(stderr, "Usage: %s [IBAN]\n", argv[0]);
        return 1;
    }

    run_test_cases();
    return 0;
}
