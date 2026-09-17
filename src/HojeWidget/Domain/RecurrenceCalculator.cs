using HojeWidget.Models;

namespace HojeWidget.Domain;

/// <summary>
/// Calcula em que dias uma rotina acontece. Função pura: só depende da regra e do
/// intervalo pedido, então é totalmente testável.
///
/// Nada aqui é "inteligente": a V0.4 só suporta recorrências previsíveis, com dias
/// e regras explícitos. Frequência flexível ("4x por semana, o app escolhe") está
/// fora do escopo de propósito.
/// </summary>
public static class RecurrenceCalculator
{
    /// <summary>Máscara de dias da semana. Bit 0 = segunda … bit 6 = domingo.</summary>
    public static int MaskOf(params DayOfWeek[] days)
        => days.Aggregate(0, (mask, day) => mask | (1 << BitIndex(day)));

    public static bool Includes(int mask, DayOfWeek day) => (mask & (1 << BitIndex(day))) != 0;

    private static int BitIndex(DayOfWeek day) => ((int)day + 6) % 7;

    /// <summary>
    /// Dias em que a rotina acontece dentro do intervalo fechado [from, to].
    /// Rotina inativa não gera nada. Nada é gerado antes de <see cref="Routine.StartDate"/>.
    /// </summary>
    public static IReadOnlyList<DateOnly> Occurrences(Routine routine, DateOnly from, DateOnly to)
    {
        var result = new List<DateOnly>();

        if (!routine.IsActive || to < from)
        {
            return result;
        }

        var start = from < routine.StartDate ? routine.StartDate : from;
        if (start > to)
        {
            return result;
        }

        switch (routine.RecurrenceType)
        {
            case RecurrenceType.Daily:
                for (var date = start; date <= to; date = date.AddDays(1))
                {
                    result.Add(date);
                }

                break;

            case RecurrenceType.DaysOfWeek:
                if (routine.DaysOfWeek == 0)
                {
                    // Regra exige ao menos um dia; sem nenhum, não há ocorrência.
                    break;
                }

                for (var date = start; date <= to; date = date.AddDays(1))
                {
                    if (Includes(routine.DaysOfWeek, date.DayOfWeek))
                    {
                        result.Add(date);
                    }
                }

                break;

            case RecurrenceType.EveryXDays:
                var interval = routine.IntervalDays.GetValueOrDefault();
                if (interval <= 0)
                {
                    break;
                }

                // Alinha na primeira ocorrência dentro da janela, sem varrer dia a dia
                // desde a data de início.
                var elapsed = start.DayNumber - routine.StartDate.DayNumber;
                var stepsToSkip = (elapsed + interval - 1) / interval;
                var first = routine.StartDate.AddDays(stepsToSkip * interval);

                for (var date = first; date <= to; date = date.AddDays(interval))
                {
                    result.Add(date);
                }

                break;

            case RecurrenceType.Monthly:
                var dayOfMonth = routine.DayOfMonth.GetValueOrDefault();
                if (dayOfMonth is < 1 or > 31)
                {
                    break;
                }

                var cursor = new DateOnly(start.Year, start.Month, 1);
                var lastMonth = new DateOnly(to.Year, to.Month, 1);

                while (cursor <= lastMonth)
                {
                    var date = MonthlyDate(cursor.Year, cursor.Month, dayOfMonth);
                    if (date >= start && date <= to && date >= routine.StartDate)
                    {
                        result.Add(date);
                    }

                    cursor = cursor.AddMonths(1);
                }

                break;
        }

        return result;
    }

    /// <summary>
    /// Data mensal segura: quando o dia pedido não existe no mês — dia 31 em abril,
    /// dia 30 em fevereiro —, usa o último dia daquele mês.
    /// </summary>
    public static DateOnly MonthlyDate(int year, int month, int dayOfMonth)
        => new(year, month, Math.Min(dayOfMonth, DateTime.DaysInMonth(year, month)));

    /// <summary>Descrição curta da regra, para listar a rotina na interface.</summary>
    public static string Describe(Routine routine) => routine.RecurrenceType switch
    {
        RecurrenceType.Daily => "todos os dias",
        RecurrenceType.DaysOfWeek => DescribeDays(routine.DaysOfWeek),
        RecurrenceType.EveryXDays => routine.IntervalDays is { } days and > 0
            ? days == 1 ? "todos os dias" : $"a cada {days} dias"
            : "sem intervalo definido",
        RecurrenceType.Monthly => routine.DayOfMonth is { } day
            ? $"todo dia {day} de cada mês"
            : "sem dia definido",
        _ => string.Empty,
    };

    private static string DescribeDays(int mask)
    {
        var order = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
        };

        var names = order.Where(d => Includes(mask, d)).Select(ShortName).ToArray();
        return names.Length == 0 ? "nenhum dia escolhido" : string.Join(" · ", names);
    }

    private static string ShortName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "SEG",
        DayOfWeek.Tuesday => "TER",
        DayOfWeek.Wednesday => "QUA",
        DayOfWeek.Thursday => "QUI",
        DayOfWeek.Friday => "SEX",
        DayOfWeek.Saturday => "SÁB",
        _ => "DOM",
    };
}
