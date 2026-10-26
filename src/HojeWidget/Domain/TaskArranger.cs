using HojeWidget.Models;

namespace HojeWidget.Domain;

/// <summary>Um dia da visão semanal, com as tarefas planejadas para ele.</summary>
public sealed record DayPlan(DateOnly Date, IReadOnlyList<TaskItem> Pending, IReadOnlyList<TaskItem> Completed)
{
    public IReadOnlyList<TaskItem> All => Pending.Concat(Completed).ToList();
}

/// <summary>
/// Regras puras de agrupamento e ordenacao. Nao toca em banco nem em interface.
///
/// Regra central da V0.1: pendencia nao expira, conclusao expira visualmente no dia seguinte.
/// Regra acrescentada na V0.2: o que aparece em Hoje é o que foi planejado para hoje ou
/// para uma data anterior. Tarefa sem data planejada vive em "Algum dia".
/// </summary>
public static class TaskArranger
{
    /// <summary>
    /// Visão de Hoje, usada pelo widget e pela seção Hoje: pendentes planejadas para hoje
    /// ou para datas anteriores, mais as concluídas na data local de hoje.
    ///
    /// Concluída sem data planejada fica de fora: ela nunca esteve em Hoje, e sim
    /// nos lembretes ("Algum dia"), que é onde continua aparecendo riscada. Sem
    /// isso, riscar um lembrete fazia o widget dizer "Tudo concluído por hoje".
    /// </summary>
    public static TaskListSnapshot Arrange(IEnumerable<TaskItem> all, DateOnly today)
    {
        var items = all as IReadOnlyCollection<TaskItem> ?? all.ToList();
        var todayText = LocalDate.From(today);

        var pending = items
            .Where(t => !t.IsCompleted && t.PlannedDate is { } planned && planned <= today)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.CreatedAtUtc)
            .ToList();

        var completedToday = items
            .Where(t => t.IsCompleted && t.PlannedDate is not null
                        && string.Equals(t.CompletedLocalDate, todayText, StringComparison.Ordinal))
            .OrderBy(t => t.CompletedAtUtc ?? DateTime.MaxValue)
            .ThenBy(t => t.SortOrder)
            .ToList();

        return new TaskListSnapshot
        {
            Pending = pending,
            CompletedToday = completedToday,
            LocalDate = todayText,
            Today = today,
        };
    }

    /// <summary>
    /// Seção "Algum dia": tarefas sem data planejada. No widget, são os lembretes. Concluídas continuam listadas
    /// no dia da conclusão, pela mesma regra de virada de dia das outras seções.
    ///
    /// V0.5: subtarefas ficam de fora. Elas já têm um lugar — o projeto ou a
    /// entrega a que pertencem — e repeti-las aqui só encheria a lista de ruído.
    /// </summary>
    public static TaskListSnapshot Someday(IEnumerable<TaskItem> all, DateOnly today)
    {
        var items = all as IReadOnlyCollection<TaskItem> ?? all.ToList();
        var todayText = LocalDate.From(today);

        var pending = items
            .Where(t => !t.IsCompleted && t.PlannedDate is null && t.ParentId is null)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.CreatedAtUtc)
            .ToList();

        var completedToday = items
            .Where(t => t.IsCompleted && t.PlannedDate is null && t.ParentId is null
                        && string.Equals(t.CompletedLocalDate, todayText, StringComparison.Ordinal))
            .OrderBy(t => t.CompletedAtUtc ?? DateTime.MaxValue)
            .ToList();

        return new TaskListSnapshot
        {
            Pending = pending,
            CompletedToday = completedToday,
            LocalDate = todayText,
            Today = today,
        };
    }

    /// <summary>Sete dias a partir de <paramref name="weekStart"/>, com o que foi planejado para cada um.</summary>
    public static IReadOnlyList<DayPlan> Week(IEnumerable<TaskItem> all, DateOnly weekStart)
    {
        var items = all as IReadOnlyCollection<TaskItem> ?? all.ToList();
        var days = new List<DayPlan>(7);

        for (var offset = 0; offset < 7; offset++)
        {
            var date = weekStart.AddDays(offset);
            var onDate = items.Where(t => t.PlannedDate == date).ToList();

            days.Add(new DayPlan(
                date,
                onDate.Where(t => !t.IsCompleted).OrderBy(t => t.SortOrder).ThenBy(t => t.CreatedAtUtc).ToList(),
                onDate.Where(t => t.IsCompleted).OrderBy(t => t.CompletedAtUtc ?? DateTime.MaxValue).ToList()));
        }

        return days;
    }

    /// <summary>Tarefas planejadas para uma data específica, usada pelo calendário da V0.3.</summary>
    public static IReadOnlyList<TaskItem> PlannedOn(IEnumerable<TaskItem> all, DateOnly date)
        => all.Where(t => t.PlannedDate == date)
              .OrderBy(t => t.IsCompleted)
              .ThenBy(t => t.SortOrder)
              .ToList();

    /// <summary>
    /// Reordena as pendentes reaproveitando as mesmas posicoes (slots) que elas ja ocupavam.
    /// Assim as concluidas mantem seu sort_order e voltam ao lugar relativo ao serem desmarcadas.
    /// </summary>
    public static IReadOnlyList<(string Id, long SortOrder)> BuildReorder(
        IReadOnlyList<TaskItem> currentPending,
        IReadOnlyList<string> newOrderIds)
    {
        var byId = currentPending.ToDictionary(t => t.Id, StringComparer.Ordinal);
        var ordered = new List<TaskItem>(newOrderIds.Count);
        foreach (var id in newOrderIds)
        {
            if (byId.TryGetValue(id, out var item))
            {
                ordered.Add(item);
                byId.Remove(id);
            }
        }

        // Qualquer pendente nao citada mantem-se no fim, preservando a ordem atual.
        ordered.AddRange(currentPending.Where(t => byId.ContainsKey(t.Id)));

        var slots = currentPending.Select(t => t.SortOrder).OrderBy(v => v).ToList();
        var result = new List<(string, long)>(ordered.Count);
        for (var i = 0; i < ordered.Count && i < slots.Count; i++)
        {
            result.Add((ordered[i].Id, slots[i]));
        }

        return result;
    }
}
