using HojeWidget.Domain;
using HojeWidget.Models;
using HojeWidget.Services.Database;
using HojeWidget.Services.System;

namespace HojeWidget.Services;

/// <summary>
/// Regras de tarefas aplicadas sobre o repositorio. Cada alteracao persiste imediatamente.
/// Nao depende de WPF, entao pode ser testada por inteiro.
/// </summary>
public sealed class TaskListService
{
    private readonly TaskRepository _repository;
    private readonly IClock _clock;

    public TaskListService(TaskRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <summary>Compacta as posicoes preservando a ordem atual. Chamado na inicializacao.</summary>
    public void Normalize() => _repository.NormalizeSortOrders();

    public IReadOnlyList<TaskItem> GetAll() => _repository.GetAll();

    /// <summary>Visão de Hoje: pendentes planejadas até hoje, mais as concluídas hoje.</summary>
    public TaskListSnapshot GetSnapshot()
        => TaskArranger.Arrange(_repository.GetAll(), _clock.Today);

    /// <summary>Seção "Algum dia": tarefas sem data planejada.</summary>
    public TaskListSnapshot GetSomeday()
        => TaskArranger.Someday(_repository.GetAll(), _clock.Today);

    /// <summary>Sete dias a partir da segunda-feira informada.</summary>
    public IReadOnlyList<DayPlan> GetWeek(DateOnly weekStart)
        => TaskArranger.Week(_repository.GetAll(), weekStart);

    /// <summary>V0.3 — próximos prazos, ordenados por proximidade.</summary>
    public IReadOnlyList<UpcomingDeadline> GetUpcomingDeadlines()
        => Deadlines.Upcoming(_repository.GetAll(), _clock.Today);

    /// <summary>V0.3 — grade do calendário mensal.</summary>
    public IReadOnlyList<CalendarDay> GetMonth(
        int year,
        int month,
        IReadOnlyList<RoutineOccurrenceView>? occurrences = null,
        IReadOnlyList<StudySessionView>? sessions = null,
        IReadOnlyList<Exam>? exams = null)
        => Deadlines.Month(_repository.GetAll(), year, month, occurrences, sessions, exams);

    /// <summary>
    /// Cria uma tarefa ao final das pendentes. Retorna null quando o texto e invalido.
    /// <paramref name="plannedDate"/> nulo coloca a tarefa em "Algum dia".
    /// </summary>
    public TaskItem? Add(
        string? rawTitle,
        DateOnly? plannedDate,
        DateOnly? dueDate = null,
        int? estimatedMinutes = null,
        bool canSplit = true,
        Priority priority = Priority.Normal,
        string? parentId = null)
    {
        if (!TaskTitle.TryNormalize(rawTitle, out var title))
        {
            return null;
        }

        var now = _clock.UtcNow;
        var item = new TaskItem
        {
            Id = Guid.NewGuid().ToString("n"),
            Title = title,
            IsCompleted = false,
            PlannedDate = plannedDate,
            DueDate = dueDate,
            ParentId = parentId,
            EstimatedMinutes = estimatedMinutes,
            CanSplit = canSplit,
            Priority = priority,
            // Escolher o dia ao criar é uma decisão manual; o motor a respeita.
            ManualPlanning = plannedDate is not null,
            SortOrder = _repository.GetNextSortOrder(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _repository.Insert(item);
        return item;
    }

    /// <summary>Atalho do widget: cria já planejada para hoje.</summary>
    public TaskItem? AddForToday(string? rawTitle) => Add(rawTitle, _clock.Today);

    /// <summary>Renomeia. Retorna false quando o texto e invalido ou a tarefa sumiu.</summary>
    public bool Rename(string id, string? rawTitle)
    {
        if (!TaskTitle.TryNormalize(rawTitle, out var title))
        {
            return false;
        }

        var item = _repository.GetById(id);
        if (item is null)
        {
            return false;
        }

        item.Title = title;
        item.UpdatedAtUtc = _clock.UtcNow;
        _repository.Update(item);
        return true;
    }

    /// <summary>Move a tarefa para outro dia, ou para "Algum dia" quando a data é nula.</summary>
    public bool SetPlannedDate(string id, DateOnly? plannedDate)
    {
        var item = _repository.GetById(id);
        if (item is null)
        {
            return false;
        }

        item.PlannedDate = plannedDate;
        item.ManualPlanning = plannedDate is not null;
        item.UpdatedAtUtc = _clock.UtcNow;
        _repository.Update(item);
        return true;
    }

    /// <summary>V0.3 — define ou remove o prazo. Independente da data planejada.</summary>
    public bool SetDueDate(string id, DateOnly? dueDate)
    {
        var item = _repository.GetById(id);
        if (item is null)
        {
            return false;
        }

        item.DueDate = dueDate;
        item.UpdatedAtUtc = _clock.UtcNow;
        _repository.Update(item);
        return true;
    }

    /// <summary>
    /// Edição completa vinda do aplicativo principal: texto, planejamento, prazo,
    /// duração estimada, divisibilidade e prioridade.
    /// </summary>
    public bool Update(
        string id,
        string? rawTitle,
        DateOnly? plannedDate,
        DateOnly? dueDate,
        int? estimatedMinutes = null,
        bool canSplit = true,
        Priority priority = Priority.Normal)
    {
        if (!TaskTitle.TryNormalize(rawTitle, out var title))
        {
            return false;
        }

        var item = _repository.GetById(id);
        if (item is null)
        {
            return false;
        }

        item.Title = title;
        item.PlannedDate = plannedDate;
        item.DueDate = dueDate;
        item.EstimatedMinutes = estimatedMinutes;
        item.CanSplit = canSplit;
        item.Priority = priority;
        item.ManualPlanning = plannedDate is not null;
        item.UpdatedAtUtc = _clock.UtcNow;
        _repository.Update(item);
        return true;
    }

    /// <summary>V0.7 — troca só a prioridade, sem abrir o formulário inteiro.</summary>
    public bool SetPriority(string id, Priority priority)
    {
        var item = _repository.GetById(id);
        if (item is null)
        {
            return false;
        }

        item.Priority = priority;
        item.UpdatedAtUtc = _clock.UtcNow;
        _repository.Update(item);
        return true;
    }

    public void Delete(string id) => _repository.Delete(id);

    /// <summary>
    /// Conclui ou reabre uma tarefa. O sort_order nunca muda aqui: e ele que devolve
    /// a tarefa a sua posicao manual anterior quando desmarcada.
    /// </summary>
    public bool SetCompleted(string id, bool completed)
    {
        var item = _repository.GetById(id);
        if (item is null)
        {
            return false;
        }

        var now = _clock.UtcNow;

        if (completed)
        {
            item.IsCompleted = true;
            item.CompletedAtUtc = now;
            item.CompletedLocalDate = _clock.LocalDate;
        }
        else
        {
            item.IsCompleted = false;
            item.CompletedAtUtc = null;
            item.CompletedLocalDate = null;
        }

        item.UpdatedAtUtc = now;
        _repository.Update(item);
        return true;
    }

    /// <summary>
    /// Aplica uma nova ordem manual as pendentes, de forma atomica.
    /// Ids desconhecidos ou concluidos sao ignorados.
    /// </summary>
    public void ReorderPending(IReadOnlyList<string> pendingIdsInNewOrder)
    {
        var snapshot = GetSnapshot();
        var assignments = TaskArranger.BuildReorder(snapshot.Pending, pendingIdsInNewOrder);
        _repository.ApplySortOrders(assignments, _clock.UtcNow);
    }
}
