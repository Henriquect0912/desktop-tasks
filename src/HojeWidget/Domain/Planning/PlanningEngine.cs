namespace HojeWidget.Domain.Planning;

/// <summary>Tudo o que o motor precisa saber. Dados puros, sem banco e sem interface.</summary>
public sealed record PlanningInput(
    DateOnly Today,
    IReadOnlyList<PlannableItem> Items,
    ICapacityProvider Capacity)
{
    /// <summary>
    /// Horizonte curto de propósito: o motor não recalcula meses inteiros a cada
    /// execução. Duas semanas cobrem prazos e provas próximas sem virar adivinhação.
    /// </summary>
    public int HorizonDays { get; init; } = PlanningEngine.DefaultHorizonDays;
}

/// <summary>Carga e capacidade de um dia do horizonte.</summary>
public sealed record DayLoad(
    DateOnly Date,
    int CapacityMinutes,
    int PlannedMinutes,
    int ItemCount,
    int UnknownDurationCount)
{
    /// <summary>Um dia com capacidade 0 não é "sobrecarregado": ele é dia de descanso.</summary>
    public bool IsOverloaded => CapacityMinutes > 0 && PlannedMinutes > CapacityMinutes;

    public int OverloadMinutes => Math.Max(0, PlannedMinutes - CapacityMinutes);

    public int FreeMinutes => Math.Max(0, CapacityMinutes - PlannedMinutes);

    public bool IsRestDay => CapacityMinutes == 0;
}

/// <summary>Uma mudança de dia proposta. Nada é aplicado sem o usuário ver isto antes.</summary>
public sealed record ProposedMove(
    string ItemId,
    PlannableKind Kind,
    string Title,
    DateOnly? From,
    DateOnly To,
    string Reason);

/// <summary>O que o motor devolve.</summary>
public sealed record PlanningResult(
    IReadOnlyList<ScoredItem> TodayPlan,
    IReadOnlyList<DayLoad> Days,
    IReadOnlyList<DistributionConflict> Conflicts,
    int TodayPlannedMinutes,
    int TodayCapacityMinutes)
{
    public bool HasConflicts => Conflicts.Count > 0;

    public IReadOnlyList<DayLoad> OverloadedDays => Days.Where(d => d.IsOverloaded).ToList();

    public bool IsTodayOverloaded => Days.FirstOrDefault(d => d.Date == TodayDate)?.IsOverloaded ?? false;

    private DateOnly TodayDate => Days.Count > 0 ? Days[0].Date : default;
}

/// <summary>
/// V0.7 — o motor de planejamento.
///
/// Ele responde "dado tudo o que existe, o que faz mais sentido fazer hoje e nos
/// próximos dias?" — e faz isso sugerindo, explicando, distribuindo e apontando
/// sobrecarga. Nunca replaneja sozinho: toda proposta volta para o usuário
/// aceitar ou recusar.
/// </summary>
public static class PlanningEngine
{
    public const int DefaultHorizonDays = 14;

    /// <summary>Monta o plano do dia e a carga do horizonte.</summary>
    public static PlanningResult Plan(PlanningInput input)
    {
        var today = input.Today;
        var horizonEnd = today.AddDays(input.HorizonDays);

        var pending = input.Items.Where(i => !i.IsCompleted).ToList();

        // Atrasado conta como sendo de hoje: pendência não expira, ela chega.
        var todayItems = pending
            .Where(i => i.PlannedDate is { } planned && planned <= today)
            .ToList();

        var todayPlan = PriorityEngine.Rank(todayItems, today);

        var days = new List<DayLoad>();
        for (var date = today; date <= horizonEnd; date = date.AddDays(1))
        {
            var onDate = date == today
                ? todayItems
                : pending.Where(i => i.PlannedDate == date).ToList();

            days.Add(new DayLoad(
                date,
                input.Capacity.MinutesFor(date),
                onDate.Sum(i => i.EstimatedMinutes ?? 0),
                onDate.Count,
                onDate.Count(i => i.HasUnknownDuration)));
        }

        var conflicts = FindDeadlineConflicts(pending, today, horizonEnd, input.Capacity);

        return new PlanningResult(
            todayPlan,
            days,
            conflicts,
            days[0].PlannedMinutes,
            days[0].CapacityMinutes);
    }

    /// <summary>
    /// Detecta o trabalho que não cabe antes dos prazos.
    ///
    /// A conta é deliberadamente simples e pessimista: para cada prazo dentro do
    /// horizonte, soma o que precisa ser feito até ele e compara com a capacidade
    /// disponível no período. Se não cabe, o plano não finge que cabe.
    /// </summary>
    private static IReadOnlyList<DistributionConflict> FindDeadlineConflicts(
        IReadOnlyList<PlannableItem> pending,
        DateOnly today,
        DateOnly horizonEnd,
        ICapacityProvider capacity)
    {
        var conflicts = new List<DistributionConflict>();

        var deadlines = pending
            .Select(i => i.DueDate ?? i.ExamDate)
            .Where(d => d is { } date && date <= horizonEnd)
            .Select(d => d!.Value)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        foreach (var deadline in deadlines)
        {
            var due = pending.Where(i => (i.DueDate ?? i.ExamDate) is { } d && d <= deadline).ToList();
            var required = due.Sum(i => i.EstimatedMinutes ?? 0);
            if (required == 0)
            {
                continue;
            }

            var available = 0;
            for (var date = today; date < deadline; date = date.AddDays(1))
            {
                available += capacity.MinutesFor(date);
            }

            if (required <= available)
            {
                continue;
            }

            conflicts.Add(new DistributionConflict(
                deadline.ToString("yyyy-MM-dd"),
                LocalDate.ToShortText(deadline),
                $"Até {LocalDate.ToShortText(deadline)} a carga necessária é de {Estimate.Format(required)} " +
                $"e a capacidade disponível é de {Estimate.Format(available)}."));
        }

        return conflicts;
    }

    /// <summary>
    /// Propõe tirar o excesso dos dias sobrecarregados.
    ///
    /// O que ela nunca faz: mexer no que o usuário planejou à mão, usar um dia de
    /// capacidade 0, passar do prazo do item, quebrar o que não pode ser quebrado
    /// ou aplicar qualquer coisa por conta própria — o resultado é uma proposta.
    /// </summary>
    public static IReadOnlyList<ProposedMove> Redistribute(PlanningInput input)
    {
        var plan = Plan(input);
        var moves = new List<ProposedMove>();

        var free = plan.Days.ToDictionary(d => d.Date, d => d.FreeMinutes);

        foreach (var day in plan.Days.Where(d => d.IsOverloaded))
        {
            var onDay = input.Items
                .Where(i => !i.IsCompleted && i.PlannedDate == day.Date)
                .ToList();

            // Sai primeiro o que menos urge, e nunca o que foi escolhido à mão.
            var candidates = PriorityEngine.Rank(onDay, input.Today)
                .Where(s => !s.Item.ManualPlanning && s.Item.Kind != PlannableKind.Routine)
                .Reverse()
                .ToList();

            var excess = day.OverloadMinutes;

            foreach (var candidate in candidates)
            {
                if (excess <= 0)
                {
                    break;
                }

                var minutes = candidate.Item.EstimatedMinutes ?? 0;
                if (minutes <= 0)
                {
                    continue;
                }

                var limit = candidate.Item.DueDate ?? candidate.Item.ExamDate;
                var target = FindTargetDay(plan.Days, free, day.Date, minutes, limit);

                if (target is not { } destination)
                {
                    continue;
                }

                free[destination] -= minutes;
                free[day.Date] += minutes;
                excess -= minutes;

                moves.Add(new ProposedMove(
                    candidate.Item.Id,
                    candidate.Item.Kind,
                    candidate.Item.Title,
                    day.Date,
                    destination,
                    $"{LocalDate.ToShortText(day.Date)} está com {Estimate.Format(day.PlannedMinutes)} " +
                    $"para {Estimate.Format(day.CapacityMinutes)} de capacidade"));
            }
        }

        return moves;
    }

    /// <summary>
    /// Primeiro dia posterior com espaço suficiente, respeitando capacidade,
    /// prazo e a margem de um dia antes do prazo.
    /// </summary>
    private static DateOnly? FindTargetDay(
        IReadOnlyList<DayLoad> days,
        IReadOnlyDictionary<DateOnly, int> free,
        DateOnly from,
        int minutes,
        DateOnly? limit)
    {
        // A véspera do prazo continua sendo margem: o item deve terminar antes dela.
        var lastAllowed = limit?.AddDays(-2);

        foreach (var day in days.Where(d => d.Date > from && !d.IsRestDay))
        {
            if (lastAllowed is { } last && day.Date > last)
            {
                break;
            }

            if (free.TryGetValue(day.Date, out var room) && room >= minutes)
            {
                return day.Date;
            }
        }

        return null;
    }
}
