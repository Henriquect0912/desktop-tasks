namespace HojeWidget.Domain.Planning;

/// <summary>
/// Quanto tempo há disponível em cada dia.
///
/// Nasce na V0.5 com um valor fixo, porque a distribuição já precisa saber
/// respeitar um dia com capacidade 0, e passa a ser configurável na V0.7 sem que
/// o distribuidor mude uma linha.
/// </summary>
public interface ICapacityProvider
{
    /// <summary>Minutos disponíveis na data. Zero significa "não planejar automaticamente".</summary>
    int MinutesFor(DateOnly date);
}

/// <summary>Capacidade igual em todos os dias. Padrão da V0.5.</summary>
public sealed class FixedCapacityProvider : ICapacityProvider
{
    /// <summary>Três horas por dia — o mesmo valor com que a V0.7 semeia a tabela semanal.</summary>
    public const int DefaultDailyMinutes = 180;

    private readonly int _minutes;

    public FixedCapacityProvider(int minutes = DefaultDailyMinutes) => _minutes = Math.Max(0, minutes);

    public int MinutesFor(DateOnly date) => _minutes;
}

/// <summary>Um item que a distribuição precisa colocar em algum dia.</summary>
public sealed record DistributableItem(
    string Id,
    string Title,
    int? EstimatedMinutes,
    bool CanSplit,
    long SortOrder)
{
    public bool HasUnknownDuration => EstimatedMinutes is null;
}

/// <summary>
/// Uma linha do plano proposto: um item num dia. Um item divisível pode aparecer
/// em mais de uma linha, uma por sessão.
/// </summary>
public sealed record PlannedSlot(
    string ItemId,
    string Title,
    DateOnly Date,
    int? Minutes,
    int SessionIndex,
    int SessionCount)
{
    public bool IsSplit => SessionCount > 1;
}

/// <summary>Um item que não coube. Nunca se quebra em silêncio nem se esconde o problema.</summary>
public sealed record DistributionConflict(string ItemId, string Title, string Message);

/// <summary>Resultado da distribuição, pronto para ser mostrado antes de aplicar.</summary>
public sealed record DistributionPlan(
    IReadOnlyList<PlannedSlot> Slots,
    DateOnly? MarginDate,
    bool MarginPreserved,
    IReadOnlyList<DistributionConflict> Conflicts,
    int RequiredMinutes,
    int AvailableMinutes)
{
    public static readonly DistributionPlan Empty = new(
        Array.Empty<PlannedSlot>(), null, false, Array.Empty<DistributionConflict>(), 0, 0);

    public bool HasConflicts => Conflicts.Count > 0;

    public bool IsEmpty => Slots.Count == 0;

    /// <summary>
    /// A data-limite para começar: o primeiro dia que o plano usa.
    ///
    /// Como o plano é encostado no prazo, começar depois dela é começar a
    /// invadir a margem — é isso que faz dela um limite, e não uma sugestão.
    /// </summary>
    public DateOnly? RecommendedStart => Slots.Count == 0 ? null : Slots.Min(s => s.Date);
}

/// <summary>
/// V0.5 — distribuição automática simples.
///
/// Ela é determinística e explicável de propósito: as mesmas entradas produzem
/// sempre o mesmo plano, e o plano é mostrado antes de ser aplicado. O usuário
/// pode alterar tudo; nada é gravado sem confirmação.
///
/// As regras, na ordem em que valem:
///
/// 1. trabalha do dia atual até o prazo;
/// 2. pula os dias com capacidade 0;
/// 3. tenta reservar o último dia útil antes do prazo como margem de segurança;
/// 4. encosta o trabalho no prazo em vez de começar hoje: ocupa os últimos
///    dias possíveis, e o primeiro deles é a data-limite para começar;
/// 5. divide em sessões o que pode ser dividido;
/// 6. procura um dia inteiro para o que não pode ser dividido — e informa
///    conflito quando nenhum serve.
/// </summary>
public static class Distributor
{
    /// <summary>Abaixo disso não vale a pena abrir uma sessão: viraria migalha de agenda.</summary>
    private const int MinimumSessionMinutes = 15;

    public static DistributionPlan Distribute(
        IReadOnlyList<DistributableItem> items,
        DateOnly today,
        DateOnly dueDate,
        ICapacityProvider capacity)
    {
        var pending = items.Where(i => !string.IsNullOrWhiteSpace(i.Title)).OrderBy(i => i.SortOrder).ToList();
        var required = pending.Sum(i => i.EstimatedMinutes ?? 0);

        if (pending.Count == 0)
        {
            return DistributionPlan.Empty;
        }

        var window = DaysBetween(today, dueDate);
        var usable = window.Where(d => capacity.MinutesFor(d) > 0).ToList();
        var available = usable.Sum(capacity.MinutesFor);

        if (usable.Count == 0)
        {
            var reason = dueDate < today
                ? "O prazo já passou; não há dia disponível para distribuir."
                : "Todos os dias até o prazo estão com capacidade 0.";

            return new DistributionPlan(
                Array.Empty<PlannedSlot>(),
                null,
                MarginPreserved: false,
                pending.Select(i => new DistributionConflict(i.Id, i.Title, reason)).ToList(),
                required,
                available);
        }

        // Primeira tentativa: guardando o último dia útil como margem de segurança.
        DateOnly? margin = usable.Count >= 2 ? usable[^1] : null;
        var workDays = margin is null ? usable : usable[..^1];

        var attempt = Fill(pending, workDays, capacity);

        // Sem dois dias úteis não existe margem para preservar, e dizer que ela foi
        // preservada seria mentira.
        var marginPreserved = margin is not null;
        if (attempt.Conflicts.Count > 0 && margin is not null)
        {
            var second = Fill(pending, usable, capacity);
            if (second.Conflicts.Count < attempt.Conflicts.Count)
            {
                attempt = second;
                marginPreserved = false;
                margin = null;
            }
        }

        var byOrder = pending.Select((item, index) => (item.Id, index)).ToDictionary(p => p.Id, p => p.index);

        var slots = attempt.Slots
            .OrderBy(s => s.Date)
            .ThenBy(s => byOrder.TryGetValue(s.ItemId, out var index) ? index : int.MaxValue)
            .ThenBy(s => s.SessionIndex)
            .ToList();

        return new DistributionPlan(slots, margin, marginPreserved, attempt.Conflicts, required, available);
    }

    /// <summary>
    /// Coloca os itens nos dias.
    ///
    /// O espalhamento é contado em <em>itens por dia</em>, não em minutos: com três
    /// etapas e três dias úteis sai uma por dia, que é o que a especificação desenha.
    /// Dentro de um dia, porém, um item ocupa o quanto couber — assim uma tarefa de
    /// quatro horas vira duas sessões de duas, e não cinco pedacinhos espalhados.
    ///
    /// O preenchimento anda de trás para frente. O trabalho encosta no prazo em vez
    /// de começar hoje e deixar os últimos dias vazios; continua espalhado — uma
    /// etapa por dia enquanto houver dia —, só que ancorado no fim.
    /// </summary>
    private static (List<PlannedSlot> Slots, List<DistributionConflict> Conflicts) Fill(
        IReadOnlyList<DistributableItem> items,
        IReadOnlyList<DateOnly> days,
        ICapacityProvider capacity)
    {
        var slots = new List<PlannedSlot>();
        var conflicts = new List<DistributionConflict>();

        if (days.Count == 0)
        {
            foreach (var item in items)
            {
                conflicts.Add(new DistributionConflict(item.Id, item.Title,
                    "Não sobrou nenhum dia útil antes do prazo."));
            }

            return (slots, conflicts);
        }

        var itemTarget = Math.Max(1, (int)Math.Ceiling(items.Count / (double)days.Count));
        var state = days.Select(d => new DayBudget(d, capacity.MinutesFor(d), itemTarget)).ToList();

        // A escolha do dia começa pelo último. Os itens são percorridos na ordem
        // inversa junto com os dias, senão a primeira etapa cairia no último dia e
        // a ordem entre elas ficaria de cabeça para baixo.
        var preference = Enumerable.Reverse(state).ToList();
        var order = Enumerable.Reverse(items).ToList();

        // Os indivisíveis vão primeiro: são eles que exigem um dia inteiro livre.
        // Depois os divisíveis preenchem as folgas, e por último os de duração
        // desconhecida, que ocupam lugar sem consumir minutos.
        foreach (var item in order.Where(i => !i.HasUnknownDuration && !i.CanSplit))
        {
            PlaceWhole(item, preference, slots, conflicts);
        }

        foreach (var item in order.Where(i => !i.HasUnknownDuration && i.CanSplit))
        {
            PlaceSplittable(item, preference, slots, conflicts);
        }

        foreach (var item in order.Where(i => i.HasUnknownDuration))
        {
            PlaceUnknown(item, preference, slots);
        }

        return (slots, conflicts);
    }

    /// <summary>
    /// Um item que não pode ser dividido precisa de um dia que o comporte inteiro.
    /// Procura primeiro entre os dias que ainda estão dentro do alvo de itens, para
    /// não amontoar; depois em qualquer dia com espaço. A lista já chega do dia
    /// mais tarde para o mais cedo.
    /// </summary>
    private static void PlaceWhole(
        DistributableItem item,
        List<DayBudget> state,
        List<PlannedSlot> slots,
        List<DistributionConflict> conflicts)
    {
        var minutes = item.EstimatedMinutes!.Value;
        var day = FindDayFor(state, minutes);

        if (day is null)
        {
            conflicts.Add(new DistributionConflict(item.Id, item.Title,
                $"{Estimate.Format(minutes)} não cabem em nenhum dia disponível: a tarefa não pode ser dividida."));
            return;
        }

        day.Take(minutes);
        slots.Add(new PlannedSlot(item.Id, item.Title, day.Date, minutes, 1, 1));
    }

    /// <summary>
    /// Um item divisível cabe inteiro sempre que possível. Só quando não cabe é que
    /// ele vira sessões — e em cada dia ele toma o máximo, para que sejam poucas.
    /// Nada é debitado dos dias enquanto o item não couber por completo.
    ///
    /// Os dias são escolhidos do fim para o começo, mas as sessões são numeradas na
    /// ordem do calendário: "sessão 1 de 2" tem de ser a que vem antes.
    /// </summary>
    private static void PlaceSplittable(
        DistributableItem item,
        List<DayBudget> state,
        List<PlannedSlot> slots,
        List<DistributionConflict> conflicts)
    {
        var minutes = item.EstimatedMinutes!.Value;

        if (FindDayFor(state, minutes) is { } single)
        {
            single.Take(minutes);
            slots.Add(new PlannedSlot(item.Id, item.Title, single.Date, minutes, 1, 1));
            return;
        }

        var remaining = minutes;
        var sessions = new List<(DayBudget Day, int Minutes)>();

        foreach (var day in state)
        {
            if (remaining == 0)
            {
                break;
            }

            var room = day.Room;
            if (room <= 0 || (room < MinimumSessionMinutes && room < remaining))
            {
                continue;
            }

            var take = Math.Min(remaining, room);
            sessions.Add((day, take));
            remaining -= take;
        }

        if (remaining > 0)
        {
            conflicts.Add(new DistributionConflict(item.Id, item.Title,
                $"Faltam {Estimate.Format(remaining)} sem dia disponível antes do prazo."));
            return;
        }

        sessions.Sort((a, b) => a.Day.Date.CompareTo(b.Day.Date));

        for (var i = 0; i < sessions.Count; i++)
        {
            var (day, take) = sessions[i];
            day.Take(take);
            slots.Add(new PlannedSlot(item.Id, item.Title, day.Date, take, i + 1, sessions.Count));
        }
    }

    /// <summary>
    /// Duração desconhecida ocupa um lugar no dia, não minutos. Fingir uma estimativa
    /// aqui só faria a carga do dia parecer precisa quando não é.
    /// </summary>
    private static void PlaceUnknown(DistributableItem item, List<DayBudget> state, List<PlannedSlot> slots)
    {
        var day = state.FirstOrDefault(d => d.HasItemRoom) ?? state.OrderBy(d => d.Items).First();

        day.AddItem();
        slots.Add(new PlannedSlot(item.Id, item.Title, day.Date, null, 1, 1));
    }

    /// <summary>
    /// Primeiro dia da lista de preferência que comporta os minutos pedidos — e a
    /// lista vem do fim para o começo. Dias ainda dentro do alvo de itens têm
    /// preferência: é isso que espalha as etapas em vez de empilhá-las todas na
    /// véspera.
    /// </summary>
    private static DayBudget? FindDayFor(List<DayBudget> state, int minutes)
        => state.FirstOrDefault(d => d.HasItemRoom && d.Room >= minutes)
           ?? state.FirstOrDefault(d => d.Room >= minutes);

    private static List<DateOnly> DaysBetween(DateOnly from, DateOnly to)
    {
        var days = new List<DateOnly>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            days.Add(date);
        }

        if (days.Count == 0)
        {
            days.Add(from);
        }

        return days;
    }

    /// <summary>Quanto ainda cabe num dia, em minutos e em número de itens.</summary>
    private sealed class DayBudget
    {
        private readonly int _capacity;

        public DayBudget(DateOnly date, int capacity, int itemTarget)
        {
            Date = date;
            _capacity = capacity;
            ItemTarget = itemTarget;
        }

        public DateOnly Date { get; }

        public int Used { get; private set; }

        public int Items { get; private set; }

        public int ItemTarget { get; }

        /// <summary>Minutos ainda livres no dia.</summary>
        public int Room => Math.Max(0, _capacity - Used);

        /// <summary>O dia ainda está dentro do número de itens que lhe cabe.</summary>
        public bool HasItemRoom => Items < ItemTarget;

        public void Take(int minutes)
        {
            Used += minutes;
            Items++;
        }

        public void AddItem() => Items++;
    }
}
