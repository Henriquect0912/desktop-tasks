using HojeWidget.Domain.Study;
using HojeWidget.Models;
using HojeWidget.Services.System;

namespace HojeWidget.Services;

/// <summary>
/// V0.7.1 — monta as entradas do planejador de estudos e aplica o plano aceito.
///
/// O papel dele é justamente separar as duas capacidades que estavam sendo
/// confundidas: a **capacidade geral do dia**, que já está parcialmente tomada
/// por tarefas, rotinas e outras provas, e o **limite desta matéria**, que é a
/// política da prova. Ter 8h livres não autoriza 8h da mesma prova.
/// </summary>
public sealed class ExamPlanningService
{
    private readonly ExamService _exams;
    private readonly CapacityService _capacity;
    private readonly PlanningService _planning;
    private readonly IClock _clock;

    public ExamPlanningService(
        ExamService exams,
        CapacityService capacity,
        PlanningService planning,
        IClock clock)
    {
        _exams = exams;
        _capacity = capacity;
        _planning = planning;
        _clock = clock;
    }

    /// <summary>
    /// Quanto de cada dia já está tomado, olhando tudo o que existe — tarefas,
    /// rotinas e sessões de outras provas. Só o que sobra pode ser oferecido a
    /// esta prova.
    /// </summary>
    public IReadOnlyList<DayCapacity> DayCapacities(string examId, DateOnly from, DateOnly to)
    {
        var horizon = Math.Max(1, to.DayNumber - _clock.Today.DayNumber + 1);
        var items = _planning.BuildInput(horizon).Items;

        var used = new Dictionary<DateOnly, int>();

        foreach (var item in items.Where(i => !i.IsCompleted && i.PlannedDate is not null))
        {
            // O que já está planejado para esta mesma prova não conta como
            // ocupado: o plano novo substitui o antigo.
            if (item.Kind == Domain.Planning.PlannableKind.StudySession && BelongsTo(examId, item.Id))
            {
                continue;
            }

            var date = item.PlannedDate!.Value;
            used[date] = used.GetValueOrDefault(date) + (item.EstimatedMinutes ?? 0);
        }

        var result = new List<DayCapacity>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            result.Add(new DayCapacity(date, _capacity.MinutesFor(date), used.GetValueOrDefault(date)));
        }

        return result;
    }

    /// <summary>Monta a proposta de plano de estudo. Não grava nada.</summary>
    public StudyPlanProposal BuildPlan(string examId)
    {
        var board = _exams.GetBoard(examId);
        if (board is null)
        {
            return StudyPlanProposal.Empty;
        }

        var today = _clock.Today;
        var last = board.Exam.ExamDate > today ? board.Exam.ExamDate.AddDays(-1) : today;

        return StudyPlanner.Plan(
            board.Graph,
            board.Exam.Policy,
            today,
            board.Exam.ExamDate,
            DayCapacities(examId, today, last));
    }

    /// <summary>Aplica um plano que o usuário já viu e aceitou.</summary>
    public void ApplyPlan(string examId, IReadOnlyList<PlannedStudySession> sessions)
        => _exams.ApplyStudyPlan(examId, sessions);

    private bool BelongsTo(string examId, string sessionId)
        => _exams.GetSessions(examId).Any(s => s.Id == sessionId);
}
