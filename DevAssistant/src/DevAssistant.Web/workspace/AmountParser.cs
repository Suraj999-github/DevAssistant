using System.Globalization;
using System.Text.RegularExpressions;

public static class AmountParser
{
    public static decimal Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0m;

        // Remove currency symbols and text
        value = Regex.Replace(value, @"[^\d\.,\-]", "");

        // Remove thousand separators
        value = value.Replace(",", "");

        return decimal.TryParse(
            value,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var amount)
            ? amount
            : 0m;
    }

    public static decimal? ParseNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = Regex.Replace(value, @"[^\d\.,\-]", "");
        value = value.Replace(",", "");

        return decimal.TryParse(
            value,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var amount)
            ? amount
            : null;
    }

    public static string Format(decimal amount, int decimalPlaces = 2)
    {
        return amount.ToString($"N{decimalPlaces}");
    }

    public static string FormatCurrency(decimal amount, string currencySymbol = "Rs.")
    {
        return $"{currencySymbol} {amount:N2}";
    }
}