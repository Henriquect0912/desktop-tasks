using HojeWidget.Models;

namespace HojeWidget.Domain;

/// <summary>Uma entrada da lista "Próximos prazos".</summary>
public sealed record UpcomingDeadline(TaskItem Task, DateOnly DueDate, int DaysRemaining)
{
    public bool IsOverdue => DaysRemaining < 0;
}

/// <summary>Um dia do calendário mensal.</summary>
public sealed record CalendarDay(
    DateOnly Date,
    bool IsCurrentMonth,
    IReadOnlyList<DayEntry> Planned,
    IReadOnlyList<TaskItem> Due)
{
    /// <summary>V0.6 — provas marcadas para este dia. Não são tarefa: não se concluem.</summary>
    public IReadOnlyList<Exam> Exams { get; init; } = Array.Empty<Exam>();
}

/// <summary>
/// V0.3 — prazo é data limite real, diferente da data planejada.
/// Aqui só há leitura e ordenação; nada de alarme, punição ou pontuação.
/// </summary>
public static class Deadlines
{
    /// <summary>
    /// Próximos prazos ordenados por proximidade. Exclui tarefas concluídas e
    /// tarefas sem prazo. Vencidas aparecem primeiro, por serem as mais urgentes.
    /// </summary>
    public static IReadOnlyList<UpcomingDeadline> Upcoming(IEnumerable<TaskItem> all, DateOnly today)
        => all
            .Where(t => !t.IsCompleted && t.DueDate is not null)
            .Select(t => new UpcomingDeadline(t, t.DueDate!.Value, t.DueDate!.Value.DayNumber - today.DayNumber))
            .OrderBy(d => d.DueDate)
            .ThenBy(d => d.Task.SortOrder)
            .ToList();

    /// <summary>
    /// Grade do mês, sempre começando na segunda-feira e completando as semanas
    /// com os dias vizinhos, para o calendário ficar retangular.
    /// </summary>
    public static IReadOnlyList<CalendarDay> Month(
        IEnumerable<TaskItem> all,
        int year,
        int month,
        IReadOnlyList<RoutineOccurrenceView>? occurrences = null,
        IReadOnlyList<StudySessionView>? sessions = null,
        IReadOnlyList<Exam>? exams = null)
    {
        var items = all as IReadOnlyCollection<TaskItem> ?? all.ToList();
        var routineEntries = occurrences ?? Array.Empty<RoutineOccurrenceView>();
        var sessionEntries = sessions ?? Array.Empty<StudySessionView>();
        var examEntries = exams ?? Array.Empty<Exam>();

        var first = new DateOnly(year, month, 1);
        var start = LocalDate.StartOfWeek(first);
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var end = LocalDate.StartOfWeek(last).AddDays(6);

        var days = new List<CalendarDay>();
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var current = date;
            var planned = items.Where(t => t.PlannedDate == current).Select(t => DayEntry.FromTask(t))
                .Concat(routineEntries.Where(o => o.Occurrence.OccurrenceDate == current).Select(DayEntry.FromOccurrence))
                .Concat(sessionEntries.Where(s => s.Session.PlannedDate == current).Select(DayEntry.FromSession))
                .ToList();

            days.Add(new CalendarDay(
                date,
                date.Month == month && date.Year == year,
                planned,
                items.Where(t => t.DueDate == current && !t.IsCompleted).ToList())
            {
                Exams = examEntries.Where(e => e.ExamDate == current).ToList(),
            });
        }

        return days;
    }
}
