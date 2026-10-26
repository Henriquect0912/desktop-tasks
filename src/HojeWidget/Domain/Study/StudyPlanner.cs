using HojeWidget.Models;

namespace HojeWidget.Domain.Study;

/// <summary>Quanto de um dia já está tomado por outras coisas, e quanto ele tem no total.</summary>
public sealed record DayCapacity(DateOnly Date, int GeneralMinutes, int AlreadyUsedMinutes)
{
    /// <summary>O que sobra do dia para qualquer atividade.</summary>
    public int GeneralRemaining => Math.Max(0, GeneralMinutes - AlreadyUsedMinutes);

    /// <summary>Capacidade geral 0 é dia de descanso: o planejador não põe nada nele.</summary>
    public bool IsRestDay => GeneralMinutes == 0;
}

/// <summary>Uma sessão proposta pelo planejador de estudos.</summary>
public sealed record PlannedStudySession(
    string NodeId,
    string Title,
    DateOnly Date,
    int Minutes,
    int SessionIndex,
    int SessionCount,
    string Explanation)
{
    public bool IsSplit => SessionCount > 1;
}

/// <summary>Um item que não pôde ser planejado, com o motivo em português.</summary>
public sealed record StudyConflict(string NodeId, string Title, string Message);

/// <summary>Um nó que espera a vez de outro, com o que vem antes dele.</summary>
public sealed record WaitingNodeInfo(string NodeId, string Title, IReadOnlyList<string> After)
{
    public string Explanation => $"Vem depois de {string.Join(", ", After)}.";
}

/// <summary>
/// O resultado do planejamento de uma prova. Ele é sempre uma proposta: nada
/// disso vira sessão no banco antes de o usuário aceitar.
/// </summary>
public sealed record StudyPlanProposal(
    IReadOnlyList<PlannedStudySession> Sessions,
    IReadOnlyList<StudyConflict> Conflicts,
    IReadOnlyList<WaitingNodeInfo> Waiting,
    IReadOnlyList<string> Notes,
    DateOnly? MarginDate,
    DateOnly? RecommendedStart,
    int RequiredMinutes,
    int PossibleMinutes)
{
    public static readonly StudyPlanProposal Empty = new(
        Array.Empty<PlannedStudySession>(),
        Array.Empty<StudyConflict>(),
        Array.Empty<WaitingNodeInfo>(),
        Array.Empty<string>(),
        null, null, 0, 0);

    /// <summary>Quanto de trabalho não cabe até a prova, respeitando os limites.</summary>
    public int DeficitMinutes => Math.Max(0, RequiredMinutes - PossibleMinutes);

    /// <summary>Não existe plano viável com as restrições atuais.</summary>
    public bool IsImpossible => DeficitMinutes > 0;

    public bool HasConflicts => Conflicts.Count > 0;

    public bool IsEmpty => Sessions.Count == 0;
}

/// <summary>
/// V0.7.1 — o planejador de estudos de uma prova.
///
/// A refatoração central desta versão é a separação entre o que o planejador
/// **não pode** violar e o que ele **tenta** otimizar.
///
/// Restrições duras — nunca violadas automaticamente:
///
/// - a ordem das dependências: um item nunca é posto antes daquilo de que
///   depende (mas é posto — dependência ordena, não tranca);
/// - dia com capacidade geral 0;
/// - capacidade geral que sobrou no dia;
/// - máximo diário desta prova;
/// - máximo de uma sessão contínua;
/// - item que não pode ser dividido;
/// - a data da prova.
///
/// A janela de estudo não começa hoje só porque hoje existe. Ela é calculada de
/// trás para frente: o plano ocupa os últimos dias em que tudo cabe no ritmo
/// confortável — recuando um dia por vez até caber de verdade, não só na conta
/// dos minutos — e o primeiro deles é a **data-limite para começar**. Depois
/// disso o cronograma aperta. Ninguém começa hoje a estudar para uma prova
/// daqui a dois meses.
///
/// Preferências suaves — o planejador tenta, e abre mão quando precisa:
///
/// - ficar perto da meta confortável diária;
/// - deixar a véspera para revisão;
/// - atacar antes o que está fraco ou não estudado;
/// - preferir o que destrava outros itens.
///
/// A capacidade geral do dia e a capacidade desta matéria são coisas diferentes:
/// ter 24h livres não autoriza 24h da mesma prova.
/// </summary>
public static class StudyPlanner
{
    /// <summary>Abaixo disso uma sessão vira migalha de agenda e não vale a pena abrir.</summary>
    private const int MinimumSessionMinutes = 15;

    public static StudyPlanProposal Plan(
        StudyGraph graph,
        ExamStudyPolicy policy,
        DateOnly today,
        DateOnly examDate,
        IReadOnlyList<DayCapacity> capacities)
    {
        var notes = new List<string>();
        var conflicts = new List<StudyConflict>();

        var waiting = graph.WaitingNodes()
            .Select(n => new WaitingNodeInfo(
                n.Id,
                n.Title,
                graph.PendingPrerequisites(n.Id).Select(d => d.Title).ToList()))
            .ToList();

        // Todo o trabalho que ainda falta: é ele que define se o plano é possível.
        var required = graph.Nodes
            .Where(n => n.IsPlannable && !n.IsCompleted)
            .Sum(n => n.EstimatedMinutes ?? 0);

        // Tudo o que falta entra na fila, inclusive o que espera a vez de outro.
        // A ordem topológica é o que garante que o pré-requisito venha primeiro.
        var candidates = graph.PlannableNodes().Where(n => n.EstimatedMinutes is > 0).ToList();

        var byDate = capacities.ToDictionary(c => c.Date);
        var window = Days(today, examDate.AddDays(-1)).Where(d => byDate.ContainsKey(d)).ToList();
        var usable = window.Where(d => !byDate[d].IsRestDay && byDate[d].GeneralRemaining > 0).ToList();

        // Quanto desta prova caberia até a véspera, respeitando os dois tetos.
        var possible = usable.Sum(d => Math.Min(byDate[d].GeneralRemaining, policy.MaxDailyMinutes));

        if (usable.Count == 0)
        {
            notes.Add(examDate <= today
                ? "A prova é hoje ou já passou: não há dia para planejar."
                : "Não há nenhum dia com capacidade livre antes da prova.");

            return new StudyPlanProposal(
                Array.Empty<PlannedStudySession>(), conflicts, waiting, notes, null, null, required, possible);
        }

        // A véspera fica de margem para revisão. Ela só recebe conteúdo novo se o
        // que sobra não couber nos dias anteriores.
        DateOnly? margin = usable.Count >= 2 ? usable[^1] : null;
        var workDays = margin is null ? usable : usable[..^1];

        // Um item indivisível maior que o máximo contínuo não cabe em janela
        // nenhuma. Ele vira conflito já, em vez de arrastar o plano inteiro para
        // trás à procura de um lugar que não existe.
        var unplaceable = candidates
            .Where(n => !n.CanSplit && n.EstimatedMinutes > policy.MaxSessionMinutes)
            .ToList();
        var fillable = candidates.Except(unplaceable).ToList();

        // De trás para frente: a janela mais curta, encostada na véspera, em que
        // tudo cabe no ritmo confortável. O primeiro dia dela é a data-limite
        // para começar — começando um dia depois, já não cabe nesse ritmo.
        var (studyWindow, result) = LatestFit(
            graph, policy, fillable, workDays, byDate, policy.ComfortableDailyMinutes);

        // Nem a janela inteira dá no ritmo confortável: sobe até o máximo diário.
        // É isto que "apertar o cronograma" quer dizer — e mesmo apertado, o
        // plano continua começando o mais tarde que ainda cabe.
        if (result.Pending.Count > 0)
        {
            (studyWindow, result) = LatestFit(
                graph, policy, fillable, workDays, byDate, policy.MaxDailyMinutes);

            notes.Add($"O conteúdo não cabe na meta confortável de {Estimate.Format(policy.ComfortableDailyMinutes)} " +
                      $"por dia; alguns dias vão até o máximo de {Estimate.Format(policy.MaxDailyMinutes)}.");
        }

        DateOnly? start = studyWindow.Count > 0 ? studyWindow[0] : null;

        // Ainda não coube: a véspera deixa de ser margem.
        if (result.Pending.Count > 0 && margin is not null)
        {
            notes.Add($"O conteúdo não coube até {LocalDate.ToShortText(workDays[^1])}; " +
                      $"a véspera ({LocalDate.ToShortText(margin.Value)}) precisou receber conteúdo novo.");

            result = Fill(graph, policy, fillable, usable, byDate, policy.MaxDailyMinutes);
            start = usable.Count > 0 ? usable[0] : null;
            margin = null;
        }

        foreach (var (node, missing) in result.Pending)
        {
            conflicts.Add(new StudyConflict(node.Id, node.Title, node.CanSplit
                ? $"Faltam {Estimate.Format(missing)} sem dia disponível antes da prova."
                : $"Nenhum dia antes da prova tem {Estimate.Format(missing)} livres, " +
                  "e este item não pode ser dividido."));
        }

        foreach (var node in unplaceable)
        {
            conflicts.Add(new StudyConflict(node.Id, node.Title,
                $"{Estimate.Format(node.EstimatedMinutes!.Value)} não cabem numa sessão só " +
                $"(o máximo contínuo é {Estimate.Format(policy.MaxSessionMinutes)}) e este item não pode ser dividido."));
        }

        // Nada é descartado em silêncio: um item sem estimativa também é dito.
        foreach (var node in graph.PlannableNodes().Where(n => n.EstimatedMinutes is null or <= 0))
        {
            notes.Add($"\"{node.Title}\" ficou fora do plano por não ter estimativa de duração.");
        }

        return new StudyPlanProposal(result.Sessions, conflicts, waiting, notes, margin, start, required, possible);
    }

    /// <summary>O que uma tentativa de preenchimento colocou e o que ficou de fora.</summary>
    private readonly record struct FillResult(
        List<PlannedStudySession> Sessions,
        List<(StudyNode Node, int Missing)> Pending);

    private static FillResult Fill(
        StudyGraph graph,
        ExamStudyPolicy policy,
        IReadOnlyList<StudyNode> candidates,
        IReadOnlyList<DateOnly> days,
        IReadOnlyDictionary<DateOnly, DayCapacity> byDate,
        int ceilingMinutes)
    {
        var sessions = new List<PlannedStudySession>();
        var pending = new List<(StudyNode Node, int Missing)>();

        if (days.Count == 0 || candidates.Count == 0)
        {
            pending.AddRange(candidates.Select(n => (n, n.EstimatedMinutes ?? 0)));
            return new FillResult(sessions, pending);
        }

        var totalNeeded = candidates.Sum(n => n.EstimatedMinutes ?? 0);
        var perDay = (int)Math.Ceiling(totalNeeded / (double)days.Count);

        // O teto do dia vem de fora: na janela curta ele é a meta confortável, e
        // só quando o conteúdo não cabe nela é que sobe para o máximo diário.
        // Assim a promessa "comece aqui e vá no seu ritmo" não é quebrada em
        // seguida pelo próprio preenchimento.
        var budgets = days.ToDictionary(
            d => d,
            d =>
            {
                var hard = Math.Min(byDate[d].GeneralRemaining, ceilingMinutes);
                var target = Math.Clamp(Math.Max(policy.ComfortableDailyMinutes, perDay), 0, hard);
                return new DayBudget(d, hard, target);
            });

        // Onde cada nó terminou, para o dependente nunca começar antes.
        var lastDayOf = new Dictionary<string, DateOnly>(StringComparer.Ordinal);

        foreach (var node in Order(graph, candidates))
        {
            var remaining = node.EstimatedMinutes!.Value;
            var planned = new List<(DayBudget Day, int Minutes)>();

            // O item pode começar no mesmo dia em que o pré-requisito termina —
            // ele só não pode vir antes. Como a fila está em ordem topológica, o
            // que vem antes já foi colocado e aparece primeiro no dia.
            var earliest = days[0];
            foreach (var prerequisite in graph.PendingPrerequisites(node.Id))
            {
                if (lastDayOf.TryGetValue(prerequisite.Id, out var end) && end > earliest)
                {
                    earliest = end;
                }
            }

            var allowed = days.Where(d => d >= earliest).ToList();
            if (allowed.Count == 0)
            {
                pending.Add((node, remaining));
                continue;
            }

            if (!node.CanSplit)
            {
                // Indivisível: precisa de um bloco só, que ainda assim não pode
                // passar do máximo por sessão.
                if (remaining > policy.MaxSessionMinutes)
                {
                    pending.Add((node, remaining));
                    continue;
                }

                var day = FindDay(budgets, allowed, remaining);
                if (day is null)
                {
                    pending.Add((node, remaining));
                    continue;
                }

                day.Take(remaining);
                lastDayOf[node.Id] = day.Date;
                sessions.Add(Session(graph, node, day.Date, remaining, 1, 1, policy, single: true));
                continue;
            }

            // Divisível: fatia em blocos de no máximo o máximo por sessão, sempre
            // dentro do que sobra do dia e do teto diário da prova.
            // Primeira passada: só sessões do tamanho máximo permitido, dentro do
            // alvo suave do dia. É o que evita picar um item em migalhas só para
            // completar o dia — quatro horas viram duas sessões de duas, não cinco
            // pedaços. A segunda passada usa o teto duro e aceita a sobra.
            foreach (var useHardCeiling in new[] { false, true })
            {
                foreach (var date in allowed)
                {
                    var day = budgets[date];

                    while (remaining > 0)
                    {
                        var room = useHardCeiling ? day.HardRoom : day.TargetRoom;
                        var take = Math.Min(Math.Min(remaining, room), policy.MaxSessionMinutes);
                        var ideal = Math.Min(remaining, policy.MaxSessionMinutes);

                        if (take <= 0)
                        {
                            break;
                        }

                        if (!useHardCeiling && take < ideal)
                        {
                            // O dia não comporta uma sessão inteira: vai para o próximo.
                            break;
                        }

                        if (useHardCeiling && take < MinimumSessionMinutes && take < remaining)
                        {
                            break;
                        }

                        day.Take(take);
                        planned.Add((day, take));
                        remaining -= take;
                    }
                }

                if (remaining == 0)
                {
                    break;
                }
            }

            if (remaining > 0)
            {
                // O que couber fica planejado; o que sobrou vira conflito declarado.
                // Um item divisível parcialmente planejado é melhor do que nenhum —
                // desde que a falta apareça escrita.
                pending.Add((node, remaining));
            }

            if (planned.Count == 0)
            {
                // Nada coube: não há dia para registrar, e quem depende deste item
                // fica livre para ser posto em qualquer dia da janela.
                continue;
            }

            lastDayOf[node.Id] = planned[^1].Day.Date;

            for (var i = 0; i < planned.Count; i++)
            {
                sessions.Add(Session(
                    graph, node, planned[i].Day.Date, planned[i].Minutes,
                    i + 1, planned.Count, policy, single: planned.Count == 1));
            }
        }

        return new FillResult(sessions.OrderBy(s => s.Date).ThenBy(s => s.SessionIndex).ToList(), pending);
    }

    /// <summary>
    /// Ordem em que os candidatos são atendidos.
    ///
    /// A profundidade no grafo manda: quem depende de alguém vem depois. Dentro
    /// do mesmo nível é tudo preferência suave — o que está mais cru primeiro,
    /// depois o que abre mais caminho, e a ordem manual desempata para o
    /// resultado ser sempre o mesmo.
    /// </summary>
    private static IEnumerable<StudyNode> Order(StudyGraph graph, IReadOnlyList<StudyNode> candidates)
        => candidates
            // A profundidade vem primeiro e é o que garante a ordem: um item só é
            // colocado depois de tudo de que ele depende. O resto é preferência.
            .OrderBy(graph.DependencyDepth)
            .ThenBy(n => n.MasteryLevel ?? Mastery.NotStudied)
            .ThenByDescending(n => graph.UnlockPotential(n.Id))
            .ThenBy(n => n.SortOrder)
            .ThenBy(n => n.Title, StringComparer.CurrentCultureIgnoreCase);

    private static DayBudget? FindDay(
        IReadOnlyDictionary<DateOnly, DayBudget> budgets,
        IReadOnlyList<DateOnly> days,
        int minutes)
    {
        foreach (var date in days)
        {
            if (budgets[date].TargetRoom >= minutes)
            {
                return budgets[date];
            }
        }

        foreach (var date in days)
        {
            if (budgets[date].HardRoom >= minutes)
            {
                return budgets[date];
            }
        }

        return null;
    }

    /// <summary>A explicação nasce dos mesmos fatores que produziram a decisão.</summary>
    private static PlannedStudySession Session(
        StudyGraph graph,
        StudyNode node,
        DateOnly date,
        int minutes,
        int index,
        int count,
        ExamStudyPolicy policy,
        bool single)
    {
        var reasons = new List<string>(3);

        var mastery = node.MasteryLevel ?? Mastery.NotStudied;
        if (mastery == Mastery.NotStudied)
        {
            reasons.Add("ainda não estudada");
        }
        else if (mastery == Mastery.Weak)
        {
            reasons.Add("domínio fraco");
        }

        var unlocks = graph.UnlockedBy(node.Id)
            .Select(graph.Find)
            .Where(n => n is { IsCompleted: false })
            .Select(n => n!.Title)
            .ToList();

        if (unlocks.Count > 0)
        {
            reasons.Add($"prepara {string.Join(", ", unlocks)}");
        }

        var after = graph.PendingPrerequisites(node.Id).Select(n => n.Title).ToList();
        if (after.Count > 0)
        {
            reasons.Add($"colocada depois de {string.Join(", ", after)}");
        }

        if (!single && minutes >= policy.MaxSessionMinutes)
        {
            reasons.Add($"sessão limitada a {Estimate.Format(policy.MaxSessionMinutes)} pelo máximo contínuo");
        }

        var explanation = reasons.Count == 0
            ? "Nenhum pré-requisito em aberto."
            : "Escolhida porque " + string.Join(" · ", reasons) + ".";

        return new PlannedStudySession(node.Id, node.Title, date, minutes, index, count, explanation);
    }

    /// <summary>
    /// A janela mais curta, encostada no último dia de trabalho, em que tudo cabe
    /// com o teto diário dado.
    ///
    /// Começa pela conta dos minutos, que é o mínimo possível, e recua um dia de
    /// cada vez até o preenchimento fechar. A conta sozinha não basta: uma sessão
    /// tem tamanho máximo, a sobra de meia hora de um dia não comporta uma sessão
    /// inteira, e um item só começa depois do que vem antes dele. Antes, quando a
    /// conta não fechava, o plano saltava direto para "começar hoje" — e uma prova
    /// daqui a três semanas terminava de ser estudada duas semanas antes.
    ///
    /// Quando nem a janela inteira dá, devolve a janela inteira e o que faltou.
    /// </summary>
    private static (List<DateOnly> Window, FillResult Result) LatestFit(
        StudyGraph graph,
        ExamStudyPolicy policy,
        IReadOnlyList<StudyNode> candidates,
        IReadOnlyList<DateOnly> workDays,
        IReadOnlyDictionary<DateOnly, DayCapacity> byDate,
        int ceilingMinutes)
    {
        var needed = candidates.Sum(n => n.EstimatedMinutes ?? 0);
        var size = MinimumDays(workDays, byDate, ceilingMinutes, needed);

        List<DateOnly> window = workDays.ToList();
        FillResult result = Fill(graph, policy, candidates, window, byDate, ceilingMinutes);

        for (; size < workDays.Count; size++)
        {
            var shorter = workDays.Skip(workDays.Count - size).ToList();
            var attempt = Fill(graph, policy, candidates, shorter, byDate, ceilingMinutes);

            if (attempt.Pending.Count == 0)
            {
                return (shorter, attempt);
            }
        }

        return (window, result);
    }

    /// <summary>
    /// Quantos dos últimos dias de trabalho bastam, só pela conta dos minutos,
    /// para o total caber com o teto dado. É o ponto de partida de
    /// <see cref="LatestFit"/>: menos que isso não cabe de jeito nenhum.
    /// </summary>
    private static int MinimumDays(
        IReadOnlyList<DateOnly> workDays,
        IReadOnlyDictionary<DateOnly, DayCapacity> byDate,
        int ceilingMinutes,
        int needed)
    {
        if (needed <= 0)
        {
            return Math.Min(1, workDays.Count);
        }

        var accumulated = 0;

        for (var i = workDays.Count - 1; i >= 0; i--)
        {
            accumulated += Math.Min(byDate[workDays[i]].GeneralRemaining, ceilingMinutes);

            if (accumulated >= needed)
            {
                return workDays.Count - i;
            }
        }

        return workDays.Count;
    }

    private static List<DateOnly> Days(DateOnly from, DateOnly to)
    {
        var days = new List<DateOnly>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            days.Add(date);
        }

        return days;
    }

    /// <summary>
    /// O orçamento de um dia para esta prova: um teto duro (o menor entre o que
    /// sobrou do dia e o máximo diário da matéria) e um alvo suave dentro dele.
    /// </summary>
    private sealed class DayBudget
    {
        private readonly int _hardCeiling;
        private readonly int _target;

        public DayBudget(DateOnly date, int hardCeiling, int target)
        {
            Date = date;
            _hardCeiling = hardCeiling;
            _target = Math.Min(target, hardCeiling);
        }

        public DateOnly Date { get; }

        public int Used { get; private set; }

        /// <summary>O alvo suave deste dia, já limitado pelo teto duro.</summary>
        public int Target => _target;

        public int TargetRoom => Math.Max(0, _target - Used);

        public int HardRoom => Math.Max(0, _hardCeiling - Used);

        public void Take(int minutes) => Used += minutes;

        public void Give(int minutes) => Used = Math.Max(0, Used - minutes);
    }
}
