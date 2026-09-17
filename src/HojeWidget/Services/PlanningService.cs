using HojeWidget.Domain;
using HojeWidget.Domain.Planning;
using HojeWidget.Models;
using HojeWidget.Services.Database;
using HojeWidget.Services.System;

namespace HojeWidget.Services;

/// <summary>
/// V0.7 — a ponte entre o banco e o motor de planejamento.
///
/// O motor em si (<see cref="PlanningEngine"/> e <see cref="PriorityEngine"/>) só
/// conhece dados puros. É aqui que os dados são reunidos, e é aqui que uma
/// proposta aceita vira gravação — nunca antes de o usuário confirmar.
/// </summary>
public sealed class PlanningService
{
    private readonly TaskRepository _tasks;
    private readonly RoutineService _routines;
    private readonly ExamService _exams;
    private readonly ContainerService _containers;
    private readonly CapacityService _capacity;
    private readonly DailyPlanRepository _plans;
    private readonly SettingsRepository _settings;
    private readonly IClock _clock;

    public PlanningService(
        TaskRepository tasks,
        RoutineService routines,
        ExamService exams,
        ContainerService containers,
        CapacityService capacity,
        DailyPlanRepository plans,
        SettingsRepository settings,
        IClock clock)
    {
        _tasks = tasks;
        _routines = routines;
        _exams = exams;
        _containers = containers;
        _capacity = capacity;
        _plans = plans;
        _settings = settings;
        _clock = clock;
    }

    public CapacityService Capacity => _capacity;

    /// <summary>Reúne tarefas, ocorrências de rotina e sessões de estudo do horizonte.</summary>
    public PlanningInput BuildInput(int horizonDays = PlanningEngine.DefaultHorizonDays)
    {
        var today = _clock.Today;
        var end = today.AddDays(horizonDays);
        var containers = _containers.GetProgressIndex();

        var items = new List<PlannableItem>();

        foreach (var task in _tasks.GetAll().Where(t => !t.IsCompleted))
        {
            if (task.PlannedDate is not { } planned || planned > end)
            {
                continue;
            }

            items.Add(new PlannableItem(
                task.Id,
                PlannableKind.Task,
                task.Title,
                task.PlannedDate,
                task.DueDate,
                task.EstimatedMinutes,
                task.CanSplit,
                task.Priority,
                task.ManualPlanning,
                task.IsCompleted,
                task.SortOrder)
            {
                ParentTitle = task.ParentId is { } parent && containers.TryGetValue(parent, out var found)
                    ? found.Container.Title
                    : null,
            });
        }

        foreach (var occurrence in _routines.GetBetween(today, end).Where(o => !o.Occurrence.IsCompleted))
        {
            items.Add(new PlannableItem(
                occurrence.Occurrence.Id,
                PlannableKind.Routine,
                occurrence.RoutineTitle,
                occurrence.Occurrence.OccurrenceDate,
                DueDate: null,
                EstimatedMinutes: null,
                CanSplit: false,
                Priority.Normal,
                // A rotina define o dia dela; para o motor isso é uma decisão firme.
                ManualPlanning: true,
                occurrence.Occurrence.IsCompleted,
                long.MaxValue));
        }

        foreach (var view in _exams.GetSessionsBetween(today, end).Where(s => !s.Session.IsCompleted))
        {
            items.Add(new PlannableItem(
                view.Session.Id,
                PlannableKind.StudySession,
                view.Session.Title,
                view.Session.PlannedDate,
                view.ExamDate,
                view.Session.EstimatedMinutes,
                view.Session.CanSplit,
                view.Session.Priority,
                view.Session.ManualPlanning,
                view.Session.IsCompleted,
                view.Session.SortOrder)
            {
                ExamDate = view.ExamDate,
                TopicMastery = view.TopicMastery,
                ParentTitle = view.ExamName,
            });
        }

        return new PlanningInput(today, items, _capacity) { HorizonDays = horizonDays };
    }

    public PlanningResult Plan(int horizonDays = PlanningEngine.DefaultHorizonDays)
        => PlanningEngine.Plan(BuildInput(horizonDays));

    /// <summary>
    /// Plano do dia com a ordem que o usuário já escolheu, quando houver.
    /// Uma ordem manual gravada vence a sugestão do motor: o motor sugere uma vez,
    /// a decisão do usuário fica.
    /// </summary>
    public IReadOnlyList<ScoredItem> TodayPlan()
    {
        var suggestion = Plan().TodayPlan;
        var saved = _plans.GetForDay(_clock.Today).Where(e => e.IsManual).ToList();

        if (saved.Count == 0)
        {
            return suggestion;
        }

        var positions = saved.ToDictionary(e => Key(e.Kind, e.ItemId), e => e.Position, StringComparer.Ordinal);

        return suggestion
            .OrderBy(s => positions.TryGetValue(Key((int)s.Item.Kind, s.Item.Id), out var position)
                ? position
                : int.MaxValue)
            .ToList();
    }

    /// <summary>Grava a ordem escolhida à mão no plano de hoje.</summary>
    public void SaveTodayOrder(IReadOnlyList<(PlannableKind Kind, string Id)> order)
    {
        var entries = order
            .Select((item, index) => new PlanEntryRecord((int)item.Kind, item.Id, index, IsManual: true))
            .ToList();

        _plans.Replace(_clock.Today, entries);
    }

    // ---------- Replanejamento ----------

    /// <summary>
    /// O que ficou para trás: pendências planejadas para antes de hoje.
    /// É o gatilho da pergunta "recalcular planejamento?" — que só pergunta.
    /// </summary>
    public IReadOnlyList<PlannableItem> Unfinished()
    {
        var today = _clock.Today;
        return BuildInput().Items
            .Where(i => !i.IsCompleted && i.Kind != PlannableKind.Routine
                        && i.PlannedDate is { } planned && planned < today)
            .OrderBy(i => i.PlannedDate)
            .ToList();
    }

    /// <summary>Proposta de redistribuição dos dias sobrecarregados. Não aplica nada.</summary>
    public IReadOnlyList<ProposedMove> BuildRedistribution()
        => PlanningEngine.Redistribute(BuildInput());

    /// <summary>Aplica uma proposta já aceita pelo usuário.</summary>
    public void ApplyMoves(IReadOnlyList<ProposedMove> moves)
    {
        var now = _clock.UtcNow;

        foreach (var move in moves)
        {
            switch (move.Kind)
            {
                case PlannableKind.Task:
                    var task = _tasks.GetById(move.ItemId);
                    if (task is null)
                    {
                        continue;
                    }

                    task.PlannedDate = move.To;
                    // O motor sugeriu este dia; a marca de escolha manual sai.
                    task.ManualPlanning = false;
                    task.UpdatedAtUtc = now;
                    _tasks.Update(task);
                    break;

                case PlannableKind.StudySession:
                    _exams.SetSessionPlannedDate(move.ItemId, move.To, manual: false);
                    break;
            }
        }
    }

    // ---------- Ações do plano do dia ----------

    /// <summary>Reagenda à mão. A marca manual protege a decisão do motor.</summary>
    public void Reschedule(PlannableKind kind, string id, DateOnly? date)
    {
        switch (kind)
        {
            case PlannableKind.Task:
                var task = _tasks.GetById(id);
                if (task is null)
                {
                    return;
                }

                task.PlannedDate = date;
                task.ManualPlanning = date is not null;
                task.UpdatedAtUtc = _clock.UtcNow;
                _tasks.Update(task);
                break;

            case PlannableKind.StudySession:
                _exams.SetSessionPlannedDate(id, date);
                break;
        }
    }

    /// <summary>Carga estimada do dia, em minutos.</summary>
    public int TodayLoadMinutes() => Plan().TodayPlannedMinutes;

    // ---------- O que ficou para trás ----------

    /// <summary>
    /// Traz para hoje o que estava planejado para antes e não foi concluído.
    ///
    /// Roda na abertura do aplicativo e a cada virada de dia. Pendência não
    /// expira: uma tarefa de ontem que não foi feita continua sendo para fazer, e
    /// deixá-la com a data velha só obrigava o usuário a arrastá-la à mão todo dia.
    ///
    /// Não mexe em ocorrência de rotina: a de ontem é de ontem: quem manda no dia
    /// dela é a regra da rotina, e trazê-la para hoje duplicaria a de hoje.
    ///
    /// Não apaga nem conclui nada — só muda o dia.
    /// </summary>
    /// <returns>Quantos itens foram trazidos.</returns>
    public int CarryForwardOverdue()
    {
        var today = _clock.Today;
        var now = _clock.UtcNow;
        var moved = 0;

        foreach (var task in _tasks.GetAll())
        {
            if (task.IsCompleted || task.PlannedDate is not { } planned || planned >= today)
            {
                continue;
            }

            task.PlannedDate = today;
            task.UpdatedAtUtc = now;
            _tasks.Update(task);
            moved++;
        }

        foreach (var session in _exams.GetAllSessions())
        {
            if (session.IsCompleted || session.PlannedDate is not { } planned || planned >= today)
            {
                continue;
            }

            // Mantém a marca de escolha manual como estava: o usuário continua
            // dono da decisão, ela só passou a valer para hoje.
            _exams.SetSessionPlannedDate(session.Id, today, session.ManualPlanning);
            moved++;
        }

        if (moved > 0)
        {
            _settings.SetRaw(SettingsRepository.CarryForwardKey, $"{LocalDate.From(today)}|{moved}");
        }

        return moved;
    }

    /// <summary>
    /// Quantos itens foram trazidos de dias anteriores para hoje. Zero quando o
    /// registro é de outro dia — a informação é do dia de hoje, não histórico.
    /// </summary>
    public int CarriedForwardToday()
    {
        var raw = _settings.GetRaw(SettingsRepository.CarryForwardKey);
        if (string.IsNullOrEmpty(raw))
        {
            return 0;
        }

        var parts = raw.Split('|');
        return parts.Length == 2
               && string.Equals(parts[0], LocalDate.From(_clock.Today), StringComparison.Ordinal)
               && int.TryParse(parts[1], out var count)
            ? count
            : 0;
    }

    /// <summary>Chave de um item no plano do dia: tipo e id, já que os ids vêm de tabelas diferentes.</summary>
    private static string Key(int kind, string id) => kind + ":" + id;
}
