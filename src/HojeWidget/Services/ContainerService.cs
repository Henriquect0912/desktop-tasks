using HojeWidget.Domain;
using HojeWidget.Domain.Planning;
using HojeWidget.Models;
using HojeWidget.Services.Database;
using HojeWidget.Services.System;

namespace HojeWidget.Services;

/// <summary>Um projeto ou entrega junto com as suas subtarefas e o progresso.</summary>
public sealed record ContainerBoard(Container Container, IReadOnlyList<TaskItem> Subtasks)
{
    public Progress Progress => Domain.Progress.Of(Subtasks, t => t.IsCompleted);

    /// <summary>Prazo mais próximo entre as subtarefas, quando o todo não tem prazo próprio.</summary>
    public DateOnly? EffectiveDueDate
        => Container.DueDate ?? Subtasks.Where(t => !t.IsCompleted && t.DueDate is not null).Min(t => t.DueDate);
}

/// <summary>
/// V0.5 — projetos e entregas.
///
/// Os dois compartilham esta camada porque a mecânica é a mesma; o que os separa
/// é o <see cref="ContainerKind"/> e o lugar onde aparecem na interface.
///
/// Subtarefa é uma <see cref="TaskItem"/> com pai. Isso preserva tudo o que já
/// existia quando uma tarefa solta é movida para dentro de um projeto — texto,
/// conclusão, datas, duração, ordem — e é o que impede sub-subtarefa: o pai só
/// pode ser um container.
/// </summary>
public sealed class ContainerService
{
    private readonly ContainerRepository _containers;
    private readonly TaskRepository _tasks;
    private readonly IClock _clock;

    public ContainerService(ContainerRepository containers, TaskRepository tasks, IClock clock)
    {
        _containers = containers;
        _tasks = tasks;
        _clock = clock;
    }

    public IReadOnlyList<Container> GetAll() => _containers.GetAll();

    public IReadOnlyList<Container> GetByKind(ContainerKind kind)
        => _containers.GetAll().Where(c => c.Kind == kind).ToList();

    public Container? GetById(string id) => _containers.GetById(id);

    public IReadOnlyList<TaskItem> GetSubtasks(string containerId) => _tasks.GetByParent(containerId);

    public ContainerBoard? GetBoard(string containerId)
    {
        var container = _containers.GetById(containerId);
        return container is null ? null : new ContainerBoard(container, _tasks.GetByParent(containerId));
    }

    public IReadOnlyList<ContainerBoard> GetBoards(ContainerKind kind)
        => GetByKind(kind).Select(c => new ContainerBoard(c, _tasks.GetByParent(c.Id))).ToList();

    /// <summary>Progresso de todos os containers de uma vez, para as listas do dia.</summary>
    public IReadOnlyDictionary<string, (Container Container, Progress Progress)> GetProgressIndex()
    {
        var all = _tasks.GetAll();
        var index = new Dictionary<string, (Container, Progress)>(StringComparer.Ordinal);

        foreach (var container in _containers.GetAll())
        {
            var children = all.Where(t => string.Equals(t.ParentId, container.Id, StringComparison.Ordinal)).ToList();
            index[container.Id] = (container, Progress.Of(children, t => t.IsCompleted));
        }

        return index;
    }

    // ---------- Projetos e entregas ----------

    public Container? Create(ContainerKind kind, string? rawTitle, DateOnly? dueDate = null, Priority priority = Priority.Normal)
    {
        if (!TaskTitle.TryNormalize(rawTitle, out var title))
        {
            return null;
        }

        var now = _clock.UtcNow;
        var container = new Container
        {
            Id = Guid.NewGuid().ToString("n"),
            Kind = kind,
            Title = title,
            DueDate = dueDate,
            Priority = priority,
            SortOrder = _containers.GetNextSortOrder(kind),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _containers.Insert(container);
        return container;
    }

    public bool Update(string id, string? rawTitle, DateOnly? dueDate, Priority priority = Priority.Normal)
    {
        if (!TaskTitle.TryNormalize(rawTitle, out var title))
        {
            return false;
        }

        var container = _containers.GetById(id);
        if (container is null)
        {
            return false;
        }

        container.Title = title;
        container.DueDate = dueDate;
        container.Priority = priority;
        container.UpdatedAtUtc = _clock.UtcNow;
        _containers.Update(container);
        return true;
    }

    /// <summary>
    /// Exclui o projeto ou entrega. As subtarefas não são apagadas: elas voltam a
    /// ser tarefas soltas, com data, conclusão e ordem intactas. Apagar trabalho
    /// registrado nunca é um efeito colateral.
    /// </summary>
    public void Delete(string id)
    {
        _tasks.DetachChildren(id, _clock.UtcNow);
        _containers.Delete(id);
    }

    /// <summary>
    /// Conclui ou reabre o todo. Nunca é automático: mesmo com todas as subtarefas
    /// concluídas, quem decide que o projeto acabou é o usuário.
    /// </summary>
    public bool SetCompleted(string id, bool completed)
    {
        var container = _containers.GetById(id);
        if (container is null)
        {
            return false;
        }

        var now = _clock.UtcNow;
        container.IsCompleted = completed;
        container.CompletedAtUtc = completed ? now : null;
        container.UpdatedAtUtc = now;
        _containers.Update(container);
        return true;
    }

    /// <summary>Todas as subtarefas concluídas? É o gatilho da pergunta de conclusão.</summary>
    public bool IsFullyChecked(string containerId)
    {
        var children = _tasks.GetByParent(containerId);
        return children.Count > 0 && children.All(t => t.IsCompleted);
    }

    // ---------- Subtarefas ----------

    public TaskItem? AddSubtask(
        string containerId,
        string? rawTitle,
        DateOnly? plannedDate = null,
        DateOnly? dueDate = null,
        int? estimatedMinutes = null,
        bool canSplit = true,
        Priority priority = Priority.Normal)
    {
        if (_containers.GetById(containerId) is null || !TaskTitle.TryNormalize(rawTitle, out var title))
        {
            return null;
        }

        var now = _clock.UtcNow;
        var task = new TaskItem
        {
            Id = Guid.NewGuid().ToString("n"),
            Title = title,
            ParentId = containerId,
            PlannedDate = plannedDate,
            DueDate = dueDate,
            EstimatedMinutes = estimatedMinutes,
            CanSplit = canSplit,
            Priority = priority,
            ManualPlanning = plannedDate is not null,
            SortOrder = _tasks.GetNextSortOrder(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _tasks.Insert(task);
        return task;
    }

    /// <summary>
    /// Move uma tarefa existente para dentro de um projeto ou entrega.
    /// Texto, conclusão, datas, duração e ordem continuam exatamente como estavam:
    /// a única coisa que muda é o pai.
    /// </summary>
    public bool MoveTaskInto(string taskId, string? containerId)
    {
        var task = _tasks.GetById(taskId);
        if (task is null)
        {
            return false;
        }

        if (containerId is not null && _containers.GetById(containerId) is null)
        {
            return false;
        }

        task.ParentId = containerId;
        task.UpdatedAtUtc = _clock.UtcNow;
        _tasks.Update(task);
        return true;
    }

    // ---------- Distribuição automática ----------

    /// <summary>
    /// Monta a proposta de distribuição das subtarefas pendentes até o prazo.
    ///
    /// O último dia de trabalho é a véspera da véspera: o dia anterior ao prazo
    /// fica de margem e o dia do prazo não recebe trabalho novo. É por isso que a
    /// janela entregue ao distribuidor termina em <c>prazo - 1</c>.
    /// </summary>
    public DistributionPlan BuildDistribution(string containerId, ICapacityProvider capacity)
    {
        var board = GetBoard(containerId);
        if (board is null)
        {
            return DistributionPlan.Empty;
        }

        var due = board.EffectiveDueDate;
        if (due is null)
        {
            return DistributionPlan.Empty;
        }

        var items = board.Subtasks
            .Where(t => !t.IsCompleted)
            .Select(t => new DistributableItem(t.Id, t.Title, t.EstimatedMinutes, t.CanSplit, t.SortOrder))
            .ToList();

        return Distributor.Distribute(items, _clock.Today, due.Value.AddDays(-1), capacity);
    }

    /// <summary>
    /// Aplica um plano já revisado pelo usuário.
    ///
    /// Quando uma subtarefa foi dividida em sessões, a original fica com a primeira
    /// sessão e as demais viram subtarefas irmãs numeradas — sessões precisam ser
    /// concluídas uma a uma, então cada uma existe de verdade na lista.
    /// </summary>
    public void ApplyDistribution(IReadOnlyList<PlannedSlot> slots)
    {
        var now = _clock.UtcNow;

        foreach (var group in slots.GroupBy(s => s.ItemId))
        {
            var task = _tasks.GetById(group.Key);
            if (task is null)
            {
                continue;
            }

            var ordered = group.OrderBy(s => s.Date).ToList();
            var baseTitle = StripSessionSuffix(task.Title);

            task.Title = ordered.Count > 1 ? $"{baseTitle} ({ordered[0].SessionIndex}/{ordered.Count})" : baseTitle;
            task.PlannedDate = ordered[0].Date;
            task.EstimatedMinutes = ordered[0].Minutes ?? task.EstimatedMinutes;
            task.ManualPlanning = false;
            task.UpdatedAtUtc = now;
            _tasks.Update(task);

            for (var i = 1; i < ordered.Count; i++)
            {
                var session = ordered[i];
                _tasks.Insert(new TaskItem
                {
                    Id = Guid.NewGuid().ToString("n"),
                    Title = $"{baseTitle} ({session.SessionIndex}/{ordered.Count})",
                    ParentId = task.ParentId,
                    PlannedDate = session.Date,
                    DueDate = task.DueDate,
                    EstimatedMinutes = session.Minutes,
                    CanSplit = task.CanSplit,
                    Priority = task.Priority,
                    SortOrder = _tasks.GetNextSortOrder(),
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                });
            }
        }
    }

    /// <summary>Tira um "(1/3)" antigo para não empilhar sufixos a cada redistribuição.</summary>
    private static string StripSessionSuffix(string title)
    {
        var open = title.LastIndexOf(" (", StringComparison.Ordinal);
        if (open < 0 || !title.EndsWith(")", StringComparison.Ordinal))
        {
            return title;
        }

        var inner = title[(open + 2)..^1];
        var parts = inner.Split('/');

        return parts.Length == 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _)
            ? title[..open]
            : title;
    }
}
