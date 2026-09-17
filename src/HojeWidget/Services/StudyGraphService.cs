using HojeWidget.Domain;
using HojeWidget.Domain.Study;
using HojeWidget.Models;
using HojeWidget.Services.Database;
using HojeWidget.Services.System;

namespace HojeWidget.Services;

/// <summary>O que aconteceu ao tentar criar uma dependência.</summary>
public sealed record DependencyResult(bool Accepted, DependencyRejection Rejection, string Message)
{
    public static readonly DependencyResult Ok = new(true, DependencyRejection.None, string.Empty);
}

/// <summary>
/// V0.7.1 — a árvore de estudo e o grafo de dependências de uma prova.
///
/// Toda regra de domínio vive em <see cref="StudyGraph"/> e em
/// <see cref="DependencyValidation"/>, que são puros e testáveis sozinhos.
/// Aqui só se lê, se grava e se protege a integridade.
/// </summary>
public sealed class StudyGraphService
{
    private readonly StudyNodeRepository _repository;
    private readonly ExamRepository _exams;
    private readonly IClock _clock;

    public StudyGraphService(StudyNodeRepository repository, ExamRepository exams, IClock clock)
    {
        _repository = repository;
        _exams = exams;
        _clock = clock;
    }

    public StudyGraph GetGraph(string examId)
        => new(_repository.GetByExam(examId), _repository.GetDependencies(examId));

    public StudyNode? GetNode(string id) => _repository.GetNode(id);

    // ---------- Nós ----------

    /// <summary>Cria um assunto na raiz da árvore.</summary>
    public StudyNode? AddSubject(string examId, string? rawTitle)
        => AddNode(examId, rawTitle, StudyNodeType.Topic, parentId: null);

    /// <summary>
    /// Cria um nó. Um filho sempre nasce dentro de um assunto; se o pai informado
    /// não for um assunto desta prova, o nó vai para a raiz em vez de criar uma
    /// hierarquia sem sentido.
    /// </summary>
    public StudyNode? AddNode(
        string examId,
        string? rawTitle,
        StudyNodeType type,
        string? parentId = null,
        int? estimatedMinutes = null,
        bool canSplit = true,
        int? questionCount = null)
    {
        if (_exams.GetExam(examId) is null || !TaskTitle.TryNormalize(rawTitle, out var title))
        {
            return null;
        }

        var parent = parentId is null ? null : _repository.GetNode(parentId);
        var effectiveParent = parent is { NodeType: StudyNodeType.Topic } && parent.ExamId == examId
            ? parent.Id
            : null;

        var node = new StudyNode
        {
            Id = Guid.NewGuid().ToString("n"),
            ExamId = examId,
            ParentNodeId = effectiveParent,
            NodeType = type,
            Title = title,
            EstimatedMinutes = type == StudyNodeType.Topic ? null : estimatedMinutes,
            CanSplit = canSplit,
            MasteryLevel = type == StudyNodeType.Topic ? null : Mastery.NotStudied,
            QuestionCount = type == StudyNodeType.ExerciseList ? questionCount : null,
            SortOrder = _repository.GetNextSortOrder(examId),
            CreatedAtUtc = _clock.UtcNow,
        };

        _repository.Insert(node);
        return node;
    }

    /// <summary>
    /// Cria uma lista dentro de um assunto já oferecendo a dependência da teoria
    /// daquele assunto. É uma sugestão marcada por padrão, não uma imposição: o
    /// chamador decide passando <paramref name="dependOnTheory"/>.
    /// </summary>
    public StudyNode? AddExerciseList(
        string examId,
        string? rawTitle,
        string? parentId,
        int? estimatedMinutes = null,
        int? questionCount = null,
        bool dependOnTheory = true)
    {
        var node = AddNode(examId, rawTitle, StudyNodeType.ExerciseList, parentId, estimatedMinutes, true, questionCount);
        if (node is null || !dependOnTheory || parentId is null)
        {
            return node;
        }

        if (TheoryOf(examId, parentId) is { } theory)
        {
            AddDependency(examId, node.Id, theory.Id);
        }

        return node;
    }

    /// <summary>A teoria de um assunto, quando existir. É o alvo da dependência automática.</summary>
    public StudyNode? TheoryOf(string examId, string subjectId)
        => _repository.GetByExam(examId)
            .Where(n => n.ParentNodeId == subjectId && n.NodeType == StudyNodeType.Theory)
            .OrderBy(n => n.SortOrder)
            .FirstOrDefault();

    public bool UpdateNode(
        string nodeId,
        string? rawTitle,
        StudyNodeType type,
        int? estimatedMinutes,
        bool canSplit,
        int? questionCount)
    {
        if (!TaskTitle.TryNormalize(rawTitle, out var title))
        {
            return false;
        }

        var node = _repository.GetNode(nodeId);
        if (node is null)
        {
            return false;
        }

        node.Title = title;
        node.NodeType = type;
        node.EstimatedMinutes = type == StudyNodeType.Topic ? null : estimatedMinutes;
        node.CanSplit = canSplit;
        node.QuestionCount = type == StudyNodeType.ExerciseList ? questionCount : null;

        if (type == StudyNodeType.Topic)
        {
            node.MasteryLevel = null;
        }
        else
        {
            node.MasteryLevel ??= Mastery.NotStudied;
        }

        _repository.Update(node);
        return true;
    }

    public bool SetMastery(string nodeId, Mastery mastery)
    {
        var node = _repository.GetNode(nodeId);
        if (node is null || node.IsSubject)
        {
            return false;
        }

        node.MasteryLevel = mastery;
        _repository.Update(node);
        return true;
    }

    /// <summary>
    /// Conclui ou reabre um nó. É a única coisa que destrava um dependente —
    /// e quando ela acontece, os nós que dependiam dele passam a ficar elegíveis
    /// sozinhos, sem entrar automaticamente no dia de hoje.
    /// </summary>
    public bool SetCompleted(string nodeId, bool completed)
    {
        var node = _repository.GetNode(nodeId);
        if (node is null)
        {
            return false;
        }

        node.IsCompleted = completed;
        node.CompletedAtUtc = completed ? _clock.UtcNow : null;
        _repository.Update(node);
        return true;
    }

    /// <summary>
    /// Muda o assunto de um nó. Posição na árvore e dependência são conceitos
    /// diferentes: mover não cria nem apaga nenhuma aresta do grafo.
    /// </summary>
    public bool MoveNode(string nodeId, string? newParentId)
    {
        var node = _repository.GetNode(nodeId);
        if (node is null || node.IsSubject)
        {
            return false;
        }

        if (newParentId is not null)
        {
            var parent = _repository.GetNode(newParentId);
            if (parent is not { NodeType: StudyNodeType.Topic } || parent.ExamId != node.ExamId)
            {
                return false;
            }
        }

        node.ParentNodeId = newParentId;
        _repository.Update(node);
        return true;
    }

    public void DeleteNode(string nodeId) => _repository.Delete(nodeId);

    // ---------- Dependências ----------

    /// <summary>
    /// Cria uma aresta, recusando o que quebraria o grafo: depender de si mesmo,
    /// repetir uma dependência ou fechar um ciclo. Um ciclo aceito deixaria todos
    /// os envolvidos travados para sempre, e em silêncio.
    /// </summary>
    public DependencyResult AddDependency(string examId, string nodeId, string dependsOnId)
    {
        var graph = GetGraph(examId);
        var rejection = DependencyValidation.Check(graph, nodeId, dependsOnId);

        if (rejection != DependencyRejection.None)
        {
            var message = DependencyValidation.Describe(
                rejection,
                graph.Find(nodeId)?.Title ?? "item",
                graph.Find(dependsOnId)?.Title ?? "item");

            return new DependencyResult(false, rejection, message);
        }

        _repository.InsertDependency(new StudyDependency
        {
            Id = Guid.NewGuid().ToString("n"),
            ExamId = examId,
            NodeId = nodeId,
            DependsOnNodeId = dependsOnId,
        });

        return DependencyResult.Ok;
    }

    public void RemoveDependency(string nodeId, string dependsOnId)
        => _repository.DeleteDependency(nodeId, dependsOnId);
}
