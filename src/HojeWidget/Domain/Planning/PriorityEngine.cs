using HojeWidget.Models;

namespace HojeWidget.Domain.Planning;

/// <summary>O que um item planejável é. Define o comportamento, não a aparência.</summary>
public enum PlannableKind
{
    Task = 0,
    Routine = 1,
    StudySession = 2,
}

/// <summary>
/// V0.7 — tudo o que o motor sabe sobre um item, em dados puros.
/// O motor nunca conversa com o banco nem com a interface.
/// </summary>
public sealed record PlannableItem(
    string Id,
    PlannableKind Kind,
    string Title,
    DateOnly? PlannedDate,
    DateOnly? DueDate,
    int? EstimatedMinutes,
    bool CanSplit,
    Priority Priority,
    bool ManualPlanning,
    bool IsCompleted,
    long SortOrder)
{
    /// <summary>Data da prova, quando o item é uma sessão de estudo.</summary>
    public DateOnly? ExamDate { get; init; }

    /// <summary>Domínio do conteúdo estudado, quando houver.</summary>
    public Mastery? TopicMastery { get; init; }

    /// <summary>Projeto, entrega ou prova a que o item pertence.</summary>
    public string? ParentTitle { get; init; }

    public bool HasUnknownDuration => EstimatedMinutes is null;
}

/// <summary>Cada motivo que empurrou um item para cima na lista.</summary>
public enum PlanningFactor
{
    Overdue,
    Deadline,
    Exam,
    Mastery,
    ManualPriority,
    PlanningDelay,
    ManualChoice,
    Routine,
    Unsplittable,
}

/// <summary>Um motivo com o peso que ele teve e o texto que o explica ao usuário.</summary>
public sealed record PlanningReason(PlanningFactor Factor, int Weight, string Text);

/// <summary>Item com a nota e os motivos que a produziram.</summary>
public sealed record ScoredItem(PlannableItem Item, int Score, IReadOnlyList<PlanningReason> Reasons)
{
    /// <summary>
    /// A explicação mostrada ao usuário. São os motivos de maior peso, em texto —
    /// nunca o número da nota, que é detalhe interno.
    /// </summary>
    public string Explanation => Reasons.Count == 0
        ? "sem prazo definido"
        : string.Join(" · ", Reasons.OrderByDescending(r => r.Weight).Take(2).Select(r => r.Text));
}

/// <summary>
/// V0.7 — o cálculo de prioridade.
///
/// Existe uma nota interna, mas ela é determinística, fica toda neste arquivo,
/// é testável e nunca aparece na interface como se fosse uma verdade. O que o
/// usuário vê são os motivos: "Prazo amanhã · prioridade alta".
///
/// Todos os pesos estão declarados aqui embaixo, num lugar só, justamente para
/// não haver número mágico espalhado pelo código.
/// </summary>
public static class PriorityEngine
{
    // --- Prazo -------------------------------------------------------------
    public const int OverdueWeight = 50;
    public const int DueTodayWeight = 40;
    public const int DueTomorrowWeight = 32;
    public const int DueWithinThreeDaysWeight = 24;
    public const int DueWithinWeekWeight = 14;
    public const int DueLaterWeight = 6;

    // --- Prova -------------------------------------------------------------
    public const int ExamImminentWeight = 36;
    public const int ExamWithinThreeDaysWeight = 28;
    public const int ExamWithinWeekWeight = 18;
    public const int ExamLaterWeight = 10;

    // --- Domínio do conteúdo ----------------------------------------------
    public const int NotStudiedWeight = 14;
    public const int WeakMasteryWeight = 10;
    public const int FairMasteryWeight = 4;

    // --- Prioridade manual -------------------------------------------------
    public const int HighPriorityWeight = 18;
    public const int LowPriorityWeight = -12;

    // --- Outros fatores ----------------------------------------------------
    public const int PlanningDelayPerDayWeight = 4;
    public const int PlanningDelayCap = 20;
    public const int ManualChoiceWeight = 8;
    public const int RoutineWeight = 6;
    public const int UnsplittableWeight = 3;

    /// <summary>Calcula a nota e monta a explicação a partir dos mesmos fatores.</summary>
    public static ScoredItem Score(PlannableItem item, DateOnly today)
    {
        var reasons = new List<PlanningReason>(4);

        AddDeadline(item, today, reasons);
        AddExam(item, today, reasons);
        AddMastery(item, reasons);
        AddPriority(item, reasons);
        AddPlanningDelay(item, today, reasons);
        AddContext(item, reasons);

        return new ScoredItem(item, reasons.Sum(r => r.Weight), reasons);
    }

    public static IReadOnlyList<ScoredItem> ScoreAll(IEnumerable<PlannableItem> items, DateOnly today)
        => items.Select(i => Score(i, today)).ToList();

    /// <summary>Ordem sugerida: nota primeiro, ordem manual como desempate estável.</summary>
    public static IReadOnlyList<ScoredItem> Rank(IEnumerable<PlannableItem> items, DateOnly today)
        => ScoreAll(items, today)
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Item.SortOrder)
            .ThenBy(s => s.Item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static void AddDeadline(PlannableItem item, DateOnly today, List<PlanningReason> reasons)
    {
        // Numa sessão de estudo quem faz o papel de prazo é a data da prova,
        // tratada logo abaixo. Contar as duas seria pesar o mesmo fato duas vezes.
        if (item.Kind == PlannableKind.StudySession || item.DueDate is not { } due)
        {
            return;
        }

        var days = due.DayNumber - today.DayNumber;
        var text = TaskLabels.Deadline(due, today) ?? string.Empty;

        var weight = days switch
        {
            < 0 => OverdueWeight,
            0 => DueTodayWeight,
            1 => DueTomorrowWeight,
            <= 3 => DueWithinThreeDaysWeight,
            <= 7 => DueWithinWeekWeight,
            _ => DueLaterWeight,
        };

        reasons.Add(new PlanningReason(days < 0 ? PlanningFactor.Overdue : PlanningFactor.Deadline, weight, text));
    }

    private static void AddExam(PlannableItem item, DateOnly today, List<PlanningReason> reasons)
    {
        if (item.ExamDate is not { } exam)
        {
            return;
        }

        var days = exam.DayNumber - today.DayNumber;

        var weight = days switch
        {
            <= 1 => ExamImminentWeight,
            <= 3 => ExamWithinThreeDaysWeight,
            <= 7 => ExamWithinWeekWeight,
            _ => ExamLaterWeight,
        };

        reasons.Add(new PlanningReason(PlanningFactor.Exam, weight, TaskLabels.ExamProximity(exam, today)));
    }

    private static void AddMastery(PlannableItem item, List<PlanningReason> reasons)
    {
        if (item.TopicMastery is not { } mastery)
        {
            return;
        }

        var (weight, text) = mastery switch
        {
            Mastery.NotStudied => (NotStudiedWeight, "conteúdo não estudado"),
            Mastery.Weak => (WeakMasteryWeight, "domínio fraco"),
            Mastery.Fair => (FairMasteryWeight, "domínio razoável"),
            _ => (0, string.Empty),
        };

        if (weight != 0)
        {
            reasons.Add(new PlanningReason(PlanningFactor.Mastery, weight, text));
        }
    }

    private static void AddPriority(PlannableItem item, List<PlanningReason> reasons)
    {
        switch (item.Priority)
        {
            case Priority.High:
                reasons.Add(new PlanningReason(PlanningFactor.ManualPriority, HighPriorityWeight, "prioridade alta"));
                break;
            case Priority.Low:
                reasons.Add(new PlanningReason(PlanningFactor.ManualPriority, LowPriorityWeight, "prioridade baixa"));
                break;
        }
    }

    private static void AddPlanningDelay(PlannableItem item, DateOnly today, List<PlanningReason> reasons)
    {
        if (item.PlannedDate is not { } planned || planned >= today)
        {
            return;
        }

        var late = today.DayNumber - planned.DayNumber;
        var weight = Math.Min(PlanningDelayCap, late * PlanningDelayPerDayWeight);

        reasons.Add(new PlanningReason(
            PlanningFactor.PlanningDelay,
            weight,
            late == 1 ? "ficou de ontem" : $"planejada há {late} dias"));
    }

    private static void AddContext(PlannableItem item, List<PlanningReason> reasons)
    {
        if (item.Kind == PlannableKind.Routine)
        {
            reasons.Add(new PlanningReason(PlanningFactor.Routine, RoutineWeight, "rotina do dia"));
        }

        if (item.ManualPlanning)
        {
            reasons.Add(new PlanningReason(PlanningFactor.ManualChoice, ManualChoiceWeight, "você escolheu este dia"));
        }

        if (!item.CanSplit && item.EstimatedMinutes is > 0)
        {
            reasons.Add(new PlanningReason(PlanningFactor.Unsplittable, UnsplittableWeight, "precisa de um bloco inteiro"));
        }
    }
}
