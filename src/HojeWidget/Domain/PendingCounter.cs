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

    /// <summary>
    /// Rodapé do widget com lembretes: "1 pendente · 2 lembretes". Sem lembrete
    /// pendente, fica exatamente como sempre foi.
    /// </summary>
    public static string WithReminders(int pendingCount, int reminderCount)
    {
        var text = Format(pendingCount);
        if (reminderCount <= 0)
        {
            return text;
        }

        var word = reminderCount == 1 ? "lembrete" : "lembretes";
        return string.Create(CultureInfo.InvariantCulture, $"{text} · {reminderCount} {word}");
    }
}
