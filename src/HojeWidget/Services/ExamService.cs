using HojeWidget.Domain;
using HojeWidget.Domain.Study;
using HojeWidget.Models;
using HojeWidget.Services.Database;
using HojeWidget.Services.System;

namespace HojeWidget.Services;

/// <summary>Uma prova com a sua árvore de estudo e as sessões, já com o progresso objetivo.</summary>
public sealed record ExamBoard(Exam Exam, StudyGraph Graph, IReadOnlyList<StudySession> Sessions)
{
    /// <summary>Só as folhas: assunto agrupa, não se estuda.</summary>
    public IReadOnlyList<StudyNode> Plannable => Graph.Nodes.Where(n => n.IsPlannable).ToList();

    /// <summary>Conteúdos estudados: qualquer domínio acima de "não estudado".</summary>
    public Progress TopicProgress => Progress.Of(
        Plannable.Where(n => n.NodeType != StudyNodeType.ExerciseList),
        n => (n.MasteryLevel ?? Mastery.NotStudied) != Mastery.NotStudied);

    /// <summary>Listas concluídas: marcadas como feitas pelo usuário.</summary>
    public Progress ListProgress => Progress.Of(
        Plannable.Where(n => n.NodeType == StudyNodeType.ExerciseList),
        n => n.IsCompleted);

    public Progress SessionProgress => Progress.Of(Sessions, s => s.IsCompleted);

    /// <summary>Quanto de trabalho ainda falta, inclusive o que está travado.</summary>
    public int RemainingMinutes => Plannable.Where(n => !n.IsCompleted).Sum(n => n.EstimatedMinutes ?? 0);

    /// <summary>Itens que esperam a vez de outro. Eles são planejados assim mesmo.</summary>
    public int WaitingCount => Graph.WaitingNodes().Count;

    public int DaysUntil(DateOnly today) => Exam.ExamDate.DayNumber - today.DayNumber;
}

/// <summary>
/// V0.6 / V0.7.1 — provas e sessões de estudo.
///
/// A prova é uma estrutura própria: ela tem disciplina, data, limites humanos de
/// estudo e uma árvore do que cai nela. Ela não é tarefa e não se conclui; o que
/// entra em Hoje e na Semana são as sessões.
/// </summary>
public sealed class ExamService
{
    private readonly ExamRepository _repository;
    private readonly StudyGraphService _graph;
    private readonly IClock _clock;

    public ExamService(ExamRepository repository, StudyGraphService graph, IClock clock)
    {
        _repository = repository;
        _graph = graph;
        _clock = clock;
    }

    public StudyGraphService Graph => _graph;

    // ---------- Provas ----------

    public IReadOnlyList<Exam> GetAll() => _repository.GetAllExams();

    public Exam? GetById(string id) => _repository.GetExam(id);

    public ExamBoard? GetBoard(string examId)
    {
        var exam = _repository.GetExam(examId);
        return exam is null
            ? null
            : new ExamBoard(exam, _graph.GetGraph(examId), _repository.GetSessions(examId));
    }

    public IReadOnlyList<ExamBoard> GetBoards()
        => GetAll().Select(e => new ExamBoard(e, _graph.GetGraph(e.Id), _repository.GetSessions(e.Id))).ToList();

    /// <summary>Provas com data a partir de hoje, da mais próxima para a mais distante.</summary>
    public IReadOnlyList<Exam> GetUpcoming()
        => GetAll().Where(e => e.ExamDate >= _clock.Today).OrderBy(e => e.ExamDate).ToList();

    /// <summary>
    /// Cria a prova com os limites de estudo. Retorna null quando a disciplina
    /// está vazia ou os limites são incoerentes — a mensagem vem de
    /// <see cref="ExamStudyPolicy.Validate"/>.
    /// </summary>
    public Exam? Create(string? subject, string? title, DateOnly examDate, ExamStudyPolicy? policy = null, string? notes = null)
    {
        var effective = policy ?? ExamStudyPolicy.Default;

        if (!TaskTitle.TryNormalize(subject, out var normalizedSubject) || !effective.IsValid)
        {
            return null;
        }

        TaskTitle.TryNormalize(title, out var normalizedTitle);

        var now = _clock.UtcNow;
        var exam = new Exam
        {
            Id = Guid.NewGuid().ToString("n"),
            Subject = normalizedSubject,
            Title = normalizedTitle ?? string.Empty,
            ExamDate = examDate,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Policy = effective,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _repository.InsertExam(exam);
        return exam;
    }

    public bool Update(string id, string? subject, string? title, DateOnly examDate, ExamStudyPolicy? policy = null, string? notes = null)
    {
        var exam = _repository.GetExam(id);
        if (exam is null || !TaskTitle.TryNormalize(subject, out var normalizedSubject))
        {
            return false;
        }

        var effective = policy ?? exam.Policy;
        if (!effective.IsValid)
        {
            return false;
        }

        TaskTitle.TryNormalize(title, out var normalizedTitle);

        exam.Subject = normalizedSubject;
        exam.Title = normalizedTitle ?? string.Empty;
        exam.ExamDate = examDate;
        exam.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        exam.Policy = effective;
        exam.UpdatedAtUtc = _clock.UtcNow;
        _repository.UpdateExam(exam);
        return true;
    }

    public void Delete(string id) => _repository.DeleteExam(id);

    /// <summary>
    /// Sessões futuras que passam dos limites atuais desta prova. Serve para
    /// avisar quando o usuário aperta um limite com um plano já feito — nada é
    /// corrigido em silêncio.
    /// </summary>
    public IReadOnlyList<StudySession> SessionsBeyondPolicy(string examId)
    {
        var exam = _repository.GetExam(examId);
        if (exam is null)
        {
            return Array.Empty<StudySession>();
        }

        var pending = _repository.GetSessions(examId)
            .Where(s => !s.IsCompleted && s.PlannedDate is { } date && date >= _clock.Today)
            .ToList();

        var overLimit = new List<StudySession>();

        foreach (var session in pending.Where(s => (s.EstimatedMinutes ?? 0) > exam.Policy.MaxSessionMinutes))
        {
            overLimit.Add(session);
        }

        foreach (var day in pending.GroupBy(s => s.PlannedDate!.Value))
        {
            if (day.Sum(s => s.EstimatedMinutes ?? 0) > exam.Policy.MaxDailyMinutes)
            {
                foreach (var session in day.Where(s => !overLimit.Contains(s)))
                {
                    overLimit.Add(session);
                }
            }
        }

        return overLimit;
    }

    /// <summary>Marca sessões como precisando de revisão, sem apagar nada.</summary>
    public void FlagForReview(IReadOnlyList<StudySession> sessions)
        => _repository.FlagSessionsForReview(sessions.Select(s => s.Id).ToList());

    /// <summary>
    /// Revalida as sessões futuras contra as regras atuais e marca as que
    /// deixaram de fazer sentido.
    ///
    /// Roda a cada abertura da prova de propósito: não importa se a
    /// incompatibilidade veio da migração, de um limite que o usuário apertou ou
    /// de uma dependência nova — o efeito é o mesmo e o tratamento também.
    /// Nada é apagado nem corrigido em silêncio; a sessão só fica marcada.
    /// </summary>
    /// <returns>Quantas sessões estão marcadas para revisão nesta prova.</returns>
    public int RevalidateSessions(string examId)
    {
        var board = GetBoard(examId);
        if (board is null)
        {
            return 0;
        }

        // Só os limites invalidam uma sessão. Um pré-requisito pendente não:
        // desde que a ordem esteja certa, a sessão continua boa.
        var beyond = SessionsBeyondPolicy(examId).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);

        var invalid = board.Sessions
            .Where(s => !s.IsCompleted && s.PlannedDate is { } date && date >= _clock.Today)
            .Where(s => beyond.Contains(s.Id))
            .ToList();

        var toFlag = invalid.Where(s => !s.NeedsReview).ToList();
        if (toFlag.Count > 0)
        {
            _repository.FlagSessionsForReview(toFlag.Select(s => s.Id).ToList());
        }

        return invalid.Count;
    }

    // ---------- Sessões de estudo ----------

    public IReadOnlyList<StudySessionView> GetSessionsBetween(DateOnly from, DateOnly to)
        => _repository.GetSessionsBetween(from, to);

    public IReadOnlyList<StudySessionView> GetSessionsForDay(DateOnly date)
        => _repository.GetSessionsBetween(date, date);

    public IReadOnlyList<StudySession> GetAllSessions() => _repository.GetAllSessions();

    public IReadOnlyList<StudySession> GetSessions(string examId) => _repository.GetSessions(examId);

    /// <summary>Sessões de uma prova, inclusive as que ainda não têm dia.</summary>
    public IReadOnlyList<StudySessionView> GetSessionViews(string examId) => _repository.GetSessionViews(examId);

    public StudySession? AddSession(
        string examId,
        string? rawTitle,
        string? nodeId = null,
        int? estimatedMinutes = null,
        DateOnly? plannedDate = null,
        bool canSplit = true,
        Priority priority = Priority.Normal)
    {
        if (_repository.GetExam(examId) is null || !TaskTitle.TryNormalize(rawTitle, out var title))
        {
            return null;
        }

        var now = _clock.UtcNow;
        var session = new StudySession
        {
            Id = Guid.NewGuid().ToString("n"),
            ExamId = examId,
            NodeId = nodeId,
            Title = title,
            EstimatedMinutes = estimatedMinutes,
            PlannedDate = plannedDate,
            CanSplit = canSplit,
            Priority = priority,
            ManualPlanning = plannedDate is not null,
            SortOrder = _repository.GetNextSessionSortOrder(examId),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _repository.InsertSession(session);
        return session;
    }

    /// <summary>
    /// Conclui ou reabre uma sessão.
    ///
    /// Concluir a primeira de duas sessões não conclui o nó: ele fica em estado
    /// parcial. O nó só se conclui quando todo o trabalho dele estiver feito —
    /// e é isso que destrava quem depende dele.
    /// </summary>
    public bool SetSessionCompleted(string sessionId, bool completed)
    {
        var session = _repository.GetSession(sessionId);
        if (session is null)
        {
            return false;
        }

        session.IsCompleted = completed;
        session.CompletedAtUtc = completed ? _clock.UtcNow : null;
        session.UpdatedAtUtc = _clock.UtcNow;
        _repository.UpdateSession(session);

        SyncNodeCompletion(session);
        return true;
    }

    /// <summary>
    /// Um nó fica concluído quando todas as sessões dele estão concluídas, e volta
    /// a ficar pendente se alguma for reaberta.
    /// </summary>
    private void SyncNodeCompletion(StudySession session)
    {
        if (session.NodeId is not { } nodeId || _graph.GetNode(nodeId) is not { } node)
        {
            return;
        }

        var sessions = _repository.GetSessions(session.ExamId)
            .Where(s => s.NodeId == nodeId)
            .ToList();

        if (sessions.Count == 0)
        {
            return;
        }

        var allDone = sessions.All(s => s.IsCompleted);
        if (allDone != node.IsCompleted)
        {
            _graph.SetCompleted(nodeId, allDone);
        }
    }

    /// <summary>Quantas sessões de um nó já foram concluídas — é o estado "parcial".</summary>
    public bool HasCompletedSessions(string examId, string nodeId)
        => _repository.GetSessions(examId).Any(s => s.NodeId == nodeId && s.IsCompleted);

    public bool SetSessionPlannedDate(string sessionId, DateOnly? date, bool manual = true)
    {
        var session = _repository.GetSession(sessionId);
        if (session is null)
        {
            return false;
        }

        session.PlannedDate = date;
        session.ManualPlanning = manual && date is not null;
        session.UpdatedAtUtc = _clock.UtcNow;
        _repository.UpdateSession(session);
        return true;
    }

    public void DeleteSession(string sessionId) => _repository.DeleteSession(sessionId);

    /// <summary>Sessões pendentes que dependem de um nó — mostradas antes de removê-lo.</summary>
    public int CountDependentSessions(string nodeId) => _repository.GetPendingSessionsForNode(nodeId).Count;

    /// <summary>
    /// Remove um nó e as sessões pendentes dele. Sessões já concluídas ficam:
    /// elas contam o que realmente aconteceu.
    /// </summary>
    public void DeleteNode(string nodeId)
    {
        foreach (var session in _repository.GetPendingSessionsForNode(nodeId))
        {
            _repository.DeleteSession(session.Id);
        }

        _graph.DeleteNode(nodeId);
    }

    /// <summary>
    /// Substitui as sessões pendentes desta prova pelas do plano aceito.
    /// As concluídas nunca são tocadas.
    /// </summary>
    public void ApplyStudyPlan(string examId, IReadOnlyList<PlannedStudySession> sessions)
    {
        foreach (var existing in _repository.GetSessions(examId).Where(s => !s.IsCompleted))
        {
            _repository.DeleteSession(existing.Id);
        }

        var now = _clock.UtcNow;
        var order = _repository.GetNextSessionSortOrder(examId);
        var exam = _repository.GetExam(examId);
        var prefix = exam is null ? string.Empty : exam.Subject + " — ";

        foreach (var slot in sessions.OrderBy(s => s.Date).ThenBy(s => s.SessionIndex))
        {
            var title = slot.IsSplit
                ? $"{prefix}{slot.Title} ({slot.SessionIndex}/{slot.SessionCount})"
                : prefix + slot.Title;

            _repository.InsertSession(new StudySession
            {
                Id = Guid.NewGuid().ToString("n"),
                ExamId = examId,
                NodeId = slot.NodeId,
                Title = title,
                EstimatedMinutes = slot.Minutes,
                PlannedDate = slot.Date,
                CanSplit = true,
                SortOrder = order++,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }
    }
}
