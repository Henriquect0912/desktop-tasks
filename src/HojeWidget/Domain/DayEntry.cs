using HojeWidget.Models;

namespace HojeWidget.Domain;

/// <summary>O que uma linha da lista representa.</summary>
public enum EntryKind
{
    Task = 0,
    Routine = 1,

    /// <summary>V0.6 — sessão de estudo de uma prova.</summary>
    StudySession = 2,
}

/// <summary>
/// Linha unificada da lista: uma tarefa comum, uma subtarefa de projeto/entrega,
/// uma ocorrência de rotina ou uma sessão de estudo.
/// Visualmente as quatro se comportam igual; o que muda é para onde vai a conclusão.
/// </summary>
public sealed record DayEntry(
    string Id,
    EntryKind Kind,
    string Title,
    bool IsCompleted,
    DateOnly? PlannedDate,
    DateOnly? DueDate,
    long SortOrder,
    DateTime? CompletedAtUtc)
{
    /// <summary>V0.5 — nome do projeto ou entrega a que a subtarefa pertence.</summary>
    public string? ParentTitle { get; init; }

    /// <summary>V0.5 — progresso do todo maior, mostrado junto da subtarefa em Hoje e na Semana.</summary>
    public Progress ParentProgress { get; init; } = Progress.Empty;

    /// <summary>V0.5 — duração estimada em minutos, quando conhecida.</summary>
    public int? EstimatedMinutes { get; init; }

    /// <summary>V0.5 — se pode ser dividido em sessões.</summary>
    public bool CanSplit { get; init; } = true;

    /// <summary>V0.6 — prova a que a sessão de estudo pertence.</summary>
    public string? ExamName { get; init; }

    /// <summary>V0.6 — data da prova, para a linha "prova em 4 dias".</summary>
    public DateOnly? ExamDate { get; init; }

    /// <summary>V0.6 — domínio do conteúdo estudado na sessão.</summary>
    public Mastery? TopicMastery { get; init; }

    /// <summary>V0.7 — prioridade manual.</summary>
    public Priority Priority { get; init; } = Priority.Normal;

    /// <summary>V0.7 — o dia foi escolhido à mão pelo usuário.</summary>
    public bool ManualPlanning { get; init; }

    public bool IsSubtask => ParentTitle is not null;

    public static DayEntry FromTask(TaskItem task, Container? parent = null, Progress parentProgress = default) => new(
        task.Id,
        EntryKind.Task,
        task.Title,
        task.IsCompleted,
        task.PlannedDate,
        task.DueDate,
        task.SortOrder,
        task.CompletedAtUtc)
    {
        ParentTitle = parent?.Title,
        ParentProgress = parentProgress,
        EstimatedMinutes = task.EstimatedMinutes,
        CanSplit = task.CanSplit,
        Priority = task.Priority,
        ManualPlanning = task.ManualPlanning,
    };

    public static DayEntry FromOccurrence(RoutineOccurrenceView view) => new(
        view.Occurrence.Id,
        EntryKind.Routine,
        view.RoutineTitle,
        view.Occurrence.IsCompleted,
        view.Occurrence.OccurrenceDate,
        DueDate: null,
        // Ocorrências entram depois das tarefas comuns do dia, sem ordem manual própria.
        SortOrder: long.MaxValue,
        view.Occurrence.CompletedAtUtc);

    /// <summary>
    /// V0.6 — sessão de estudo. O prazo dela é a data da prova: é isso que faz a
    /// sessão aparecer nos mesmos lugares que uma tarefa com prazo.
    /// </summary>
    public static DayEntry FromSession(StudySessionView view) => new(
        view.Session.Id,
        EntryKind.StudySession,
        view.Session.Title,
        view.Session.IsCompleted,
        view.Session.PlannedDate,
        view.ExamDate,
        view.Session.SortOrder,
        view.Session.CompletedAtUtc)
    {
        ExamName = view.ExamName,
        ExamDate = view.ExamDate,
        TopicMastery = view.TopicMastery,
        EstimatedMinutes = view.Session.EstimatedMinutes,
        CanSplit = view.Session.CanSplit,
        Priority = view.Session.Priority,
        ManualPlanning = view.Session.ManualPlanning,
    };
}

/// <summary>Mistura tarefas, ocorrências de rotina e sessões de estudo num único quadro do dia.</summary>
public static class DayBoard
{
    public static (IReadOnlyList<DayEntry> Pending, IReadOnlyList<DayEntry> Completed) Merge(
        IReadOnlyList<DayEntry> pendingTasks,
        IReadOnlyList<DayEntry> completedTasks,
        IReadOnlyList<RoutineOccurrenceView> occurrences,
        IReadOnlyList<StudySessionView>? sessions = null)
    {
        var studySessions = sessions ?? Array.Empty<StudySessionView>();

        var pending = pendingTasks
            .Concat(studySessions.Where(s => !s.Session.IsCompleted)
                                 .OrderBy(s => s.Session.SortOrder)
                                 .Select(DayEntry.FromSession))
            .Concat(occurrences.Where(o => !o.Occurrence.IsCompleted)
                               .OrderBy(o => o.RoutineTitle, StringComparer.CurrentCultureIgnoreCase)
                               .Select(DayEntry.FromOccurrence))
            .ToList();

        var completed = completedTasks
            .Concat(studySessions.Where(s => s.Session.IsCompleted).Select(DayEntry.FromSession))
            .Concat(occurrences.Where(o => o.Occurrence.IsCompleted).Select(DayEntry.FromOccurrence))
            .OrderBy(e => e.CompletedAtUtc ?? DateTime.MaxValue)
            .ToList();

        return (pending, completed);
    }

    public static (IReadOnlyList<DayEntry> Pending, IReadOnlyList<DayEntry> Completed) Merge(
        IReadOnlyList<TaskItem> pendingTasks,
        IReadOnlyList<TaskItem> completedTasks,
        IReadOnlyList<RoutineOccurrenceView> occurrences)
        => Merge(
            pendingTasks.Select(t => DayEntry.FromTask(t)).ToList(),
            completedTasks.Select(t => DayEntry.FromTask(t)).ToList(),
            occurrences);
}
