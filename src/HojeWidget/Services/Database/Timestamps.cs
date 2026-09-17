using System.Globalization;

namespace HojeWidget.Services.Database;

/// <summary>Formato unico de data/hora no banco: ISO-8601 UTC com precisao de milissegundos.</summary>
internal static class Timestamps
{
    private const string Format = "yyyy-MM-ddTHH:mm:ss.fffZ";

    public static string ToStorage(DateTime value)
        => value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture);

    public static DateTime FromStorage(string value)
        => DateTime.TryParseExact(
            value,
            Format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : DateTime.SpecifyKind(DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), DateTimeKind.Utc);
}
