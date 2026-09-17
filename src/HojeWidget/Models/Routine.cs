namespace HojeWidget.Models;

/// <summary>Tipos de recorrência previsível suportados na V0.4.</summary>
public enum RecurrenceType
{
    /// <summary>Todos os dias.</summary>
    Daily = 0,

    /// <summary>Dias específicos da semana.</summary>
    DaysOfWeek = 1,

    /// <summary>A cada X dias, contados a partir da data de início.</summary>
    EveryXDays = 2,

    /// <summary>Todo dia N de cada mês.</summary>
    Monthly = 3,
}

/// <summary>
/// Uma rotina é a regra; ela não é concluída nem termina.
/// Quem se conclui é a ocorrência (<see cref="RoutineOccurrence"/>) de um dia.
/// </summary>
public sealed class Routine
{
    public required string Id { get; init; }
    public required string Title { get; set; }

    public RecurrenceType RecurrenceType { get; set; }

    /// <summary>A recorrência não produz nada antes desta data.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Intervalo em dias, para <see cref="RecurrenceType.EveryXDays"/>. Sempre positivo.</summary>
    public int? IntervalDays { get; set; }

    /// <summary>
    /// Máscara de dias da semana, para <see cref="RecurrenceType.DaysOfWeek"/>.
    /// Bit 0 = segunda … bit 6 = domingo.
    /// </summary>
    public int DaysOfWeek { get; set; }

    /// <summary>Dia do mês, para <see cref="RecurrenceType.Monthly"/>. De 1 a 31.</summary>
    public int? DayOfMonth { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>
/// Uma ocorrência da rotina num dia. Cada uma é independente: concluir uma nunca
/// vira a próxima nem encerra a rotina.
/// </summary>
public sealed class RoutineOccurrence
{
    public required string Id { get; init; }
    public required string RoutineId { get; init; }
    public DateOnly OccurrenceDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

/// <summary>Ocorrência já acompanhada do título da rotina, para exibição.</summary>
public sealed record RoutineOccurrenceView(RoutineOccurrence Occurrence, string RoutineTitle);
