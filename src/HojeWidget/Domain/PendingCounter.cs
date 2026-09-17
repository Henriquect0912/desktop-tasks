using System.Globalization;

namespace HojeWidget.Domain;

/// <summary>Texto do rodape: conta somente pendentes, com singular/plural correto.</summary>
public static class PendingCounter
{
    public static string Format(int pendingCount)
    {
        var count = pendingCount < 0 ? 0 : pendingCount;
        var word = count == 1 ? "pendente" : "pendentes";
        return string.Create(CultureInfo.InvariantCulture, $"{count} {word}");
    }
}
