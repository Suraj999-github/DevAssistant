using System.Globalization;

public static class DateTimeHelper
{
    private const string DefaultDateFormat = "yyyy-MM-dd";
    private const string DefaultDateTimeFormat = "yyyy-MM-dd HH:mm:ss";
    private const string DisplayDateFormat = "dd-MMM-yyyy";
    private const string DisplayDateTimeFormat = "dd-MMM-yyyy hh:mm tt";

    public static string ToDateString(DateTime? date)
    {
        return date?.ToString(DefaultDateFormat) ?? string.Empty;
    }

    public static string ToDateTimeString(DateTime? dateTime)
    {
        return dateTime?.ToString(DefaultDateTimeFormat) ?? string.Empty;
    }

    public static string ToDisplayDate(DateTime? date)
    {
        return date?.ToString(DisplayDateFormat) ?? string.Empty;
    }

    public static string ToDisplayDateTime(DateTime? dateTime)
    {
        return dateTime?.ToString(DisplayDateTimeFormat) ?? string.Empty;
    }

    public static DateTime? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParse(value, out var result))
            return result;

        return null;
    }

    public static DateTime? ParseExactDate(string value, string format)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParseExact(
                value,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var result))
        {
            return result;
        }

        return null;
    }

    public static string ToIso8601(DateTime? dateTime)
    {
        return dateTime?.ToString("o") ?? string.Empty;
    }
}