using System.Globalization;

namespace HojeWidget.Domain;

/// <summary>Data civil local no formato lógico YYYY-MM-DD.</summary>
public static class LocalDate
{
    public const string Format = "yyyy-MM-dd";

    public static string From(DateTime localDateTime)
        => localDateTime.ToString(Format, CultureInfo.InvariantCulture);

    public static string From(DateOnly date)
        => date.ToString(Format, CultureInfo.InvariantCulture);

    public static string? From(DateOnly? date)
        => date is null ? null : From(date.Value);

    public static DateOnly Of(DateTime localDateTime)
        => DateOnly.FromDateTime(localDateTime);

    /// <summary>Converte o texto guardado no banco. Valor inválido vira null.</summary>
    public static DateOnly? Parse(string? value)
        => DateOnly.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    /// <summary>Formato curto para a interface: 12/09.</summary>
    public static string ToShortText(DateOnly date)
        => date.ToString("dd/MM", CultureInfo.InvariantCulture);

    /// <summary>Segunda-feira da semana que contém a data.</summary>
    public static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7; // segunda = 0
        return date.AddDays(-offset);
    }

    /// <summary>Nome curto do dia da semana em português: seg, ter, qua…</summary>
    public static string DayOfWeekShortName(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Monday => "seg",
        DayOfWeek.Tuesday => "ter",
        DayOfWeek.Wednesday => "qua",
        DayOfWeek.Thursday => "qui",
        DayOfWeek.Friday => "sex",
        DayOfWeek.Saturday => "sáb",
        _ => "dom",
    };

    /// <summary>Nome do mês em português, com a inicial maiúscula.</summary>
    public static string MonthName(int month) => month switch
    {
        1 => "Janeiro",
        2 => "Fevereiro",
        3 => "Março",
        4 => "Abril",
        5 => "Maio",
        6 => "Junho",
        7 => "Julho",
        8 => "Agosto",
        9 => "Setembro",
        10 => "Outubro",
        11 => "Novembro",
        _ => "Dezembro",
    };
}
